//
//  CoordinatorService.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2021 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Affero General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Affero General Public License for more details.
//
//  You should have received a copy of the GNU Affero General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using Argus.Common.Services.Elasticsearch;
using Argus.Coordinator.Configuration;
using Argus.Coordinator.Model;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Remora.Results;

namespace Argus.Coordinator.Services
{
    /// <summary>
    /// Continuously accepts and fingerprints images.
    /// </summary>
    public class CoordinatorService : BackgroundService
    {
        private readonly NESTService _nestService;
        private readonly IDbContextFactory<CoordinatorContext> _contextFactory;
        private readonly ILogger<CoordinatorService> _log;

        private readonly CoordinatorOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoordinatorService"/> class.
        /// </summary>
        /// <param name="options">The coordinator options.</param>
        /// <param name="nestService">The NEST service.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="contextFactory">The context factory.</param>
        public CoordinatorService
        (
            IOptions<CoordinatorOptions> options,
            NESTService nestService,
            ILogger<CoordinatorService> log,
            IDbContextFactory<CoordinatorContext> contextFactory
        )
        {
            _options = options.Value;
            _nestService = nestService;
            _log = log;
            _contextFactory = contextFactory;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Started coordinator");
            await Task.WhenAll
            (
                Task.Run(() => RunRequestReplyHandlerAsync(stoppingToken), stoppingToken),
                Task.Run(() => RunPullsAsync(stoppingToken), stoppingToken)
            );
        }

        private async Task RunRequestReplyHandlerAsync(CancellationToken ct = default)
        {
            var responseSocket = new ResponseSocket();
            responseSocket.Bind(_options.CoordinatorEndpoint.ToString().TrimEnd('/'));

            while (!ct.IsCancellationRequested)
            {
                if (responseSocket.TryReceiveFrameBytes(out var bytes))
                {
                    var request = MessagePackSerializer.Deserialize<ICoordinatorRequest>(bytes, cancellationToken: ct);

                    ICoordinatorReply reply;
                    switch (request)
                    {
                        case GetResumeRequest getResumeRequest:
                        {
                            await using var db = _contextFactory.CreateDbContext();
                            var serviceStatus = await db.ServiceStates.AsNoTracking().FirstOrDefaultAsync
                            (
                                s => s.Name == getResumeRequest.ServiceName,
                                ct
                            );

                            var resumePoint = serviceStatus?.ResumePoint;
                            reply = new ResumeReply(resumePoint ?? string.Empty);

                            _log.LogInformation
                            (
                                "Told collector for service \"{Service}\" its resume point",
                                getResumeRequest.ServiceName
                            );
                            break;
                        }
                        case SetResumeRequest setResumeRequest:
                        {
                            await using var db = _contextFactory.CreateDbContext();
                            var serviceStatus = await db.ServiceStates.FirstOrDefaultAsync
                            (
                                s => s.Name == setResumeRequest.ServiceName,
                                ct
                            );

                            if (serviceStatus is null)
                            {
                                serviceStatus = new ServiceState(setResumeRequest.ServiceName);
                                db.Update(serviceStatus);
                            }

                            serviceStatus.ResumePoint = setResumeRequest.ResumePoint;
                            await db.SaveChangesAsync(ct);

                            var resumePoint = serviceStatus.ResumePoint;
                            reply = new ResumeReply(resumePoint ?? string.Empty);
                            break;
                        }
                        case GetImagesToRetryRequest getImagesRequest:
                        {
                            var now = DateTime.UtcNow;
                            var then = now - TimeSpan.FromHours(1);

                            await using var db = _contextFactory.CreateDbContext();
                            var reports = await db.ServiceStatusReports.AsNoTracking()
                                .Select(r => r.Report)
                                .OrderBy(r => r.Timestamp)
                                .Where(r => r.Timestamp < then)
                                .Where
                                (
                                    r =>
                                        r.Status != ImageStatus.Faulted &&
                                        r.Status != ImageStatus.Rejected &&
                                        r.Status != ImageStatus.Indexed
                                )
                                .Take(getImagesRequest.MaxCount)
                                .ToListAsync(ct);

                            reply = new ImagesToRetryReply(reports);
                            break;
                        }
                        default:
                        {
                            reply = new ErrorReply("Unknown request.");
                            break;
                        }
                    }

                    var response = MessagePackSerializer.Serialize(reply, cancellationToken: ct);
                    responseSocket.SendFrame(response);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                }
            }
        }

        private async Task RunPullsAsync(CancellationToken ct = default)
        {
            var incomingSocket = new PullSocket();
            var outgoingSocket = new PushSocket();

            incomingSocket.Bind(_options.CoordinatorInputEndpoint.ToString().TrimEnd('/'));
            outgoingSocket.Bind(_options.CoordinatorOutputEndpoint.ToString().TrimEnd('/'));

            var blockOptions = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = Environment.ProcessorCount,
                CancellationToken = ct,
                EnsureOrdered = false,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                SingleProducerConstrained = true
            };

            var indexBlock = new ActionBlock<FingerprintedImage>
            (
                f => HandleFingerprintedImageAsync(f, ct),
                blockOptions
            );

            var reportBlock = new ActionBlock<StatusReport>
            (
                r => HandleStatusReportAsync(r, ct),
                blockOptions
            );

            while (!ct.IsCancellationRequested)
            {
                if (incomingSocket.TryReceiveFrameBytes(out var bytes))
                {
                    var inputMessage = MessagePackSerializer.Deserialize<ICoordinatorInputMessage>
                    (
                        bytes, cancellationToken: ct
                    );

                    switch (inputMessage)
                    {
                        case CollectedImage collectedImage:
                        {
                            outgoingSocket.SendFrame(bytes);

                            var statusReport = new StatusReport
                            (
                                DateTime.UtcNow,
                                collectedImage.ServiceName,
                                collectedImage.Source,
                                collectedImage.Image,
                                ImageStatus.Processing,
                                string.Empty
                            );

                            var processing = await HandleStatusReportAsync(statusReport, ct);
                            if (!processing.IsSuccess)
                            {
                                _log.LogWarning("Failed to create status report: {Reason}", processing.Error.Message);
                            }

                            _log.LogInformation
                            (
                                "Dispatched collected image from service \"{Service}\" to worker",
                                collectedImage.ServiceName
                            );
                            break;
                        }
                        case FingerprintedImage fingerprintedImage:
                        {
                            while (!await indexBlock.SendAsync(fingerprintedImage, ct))
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                            }

                            break;
                        }
                        case StatusReport statusReport:
                        {
                            while (!await reportBlock.SendAsync(statusReport, ct))
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                            }

                            break;
                        }
                        default:
                        {
                            _log.LogWarning("Failed to parse incoming message");
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                }
            }
        }

        private async Task<Result> HandleFingerprintedImageAsync
        (
            FingerprintedImage fingerprintedImage,
            CancellationToken ct
        )
        {
            // Save to database
            var signature = new ImageSignature(fingerprintedImage.Fingerprint);

            var indexedImage = new IndexedImage
            (
                fingerprintedImage.ServiceName,
                DateTimeOffset.UtcNow,
                fingerprintedImage.Image.ToString(),
                fingerprintedImage.Source.ToString(),
                signature.Signature,
                signature.Words
            );

            var indexImage = await _nestService.IndexImageAsync(indexedImage, ct);
            if (!indexImage.IsSuccess)
            {
                return indexImage;
            }

            var statusReport = new StatusReport
            (
                DateTime.UtcNow,
                fingerprintedImage.ServiceName,
                fingerprintedImage.Source,
                fingerprintedImage.Image,
                ImageStatus.Indexed,
                string.Empty
            );

            var reportIndexed = await HandleStatusReportAsync(statusReport, ct);
            if (!reportIndexed.IsSuccess)
            {
                _log.LogWarning("Failed to create status report: {Reason}", reportIndexed.Error.Message);
            }

            _log.LogInformation
            (
                "Indexed fingerprinted image from service \"{Service}\"",
                fingerprintedImage.ServiceName
            );

            return Result.FromSuccess();
        }

        private async Task<Result> HandleStatusReportAsync
        (
            StatusReport statusReport,
            CancellationToken ct
        )
        {
            try
            {
                await using var db = _contextFactory.CreateDbContext();
                var existingReport = await db.ServiceStatusReports.FirstOrDefaultAsync
                (
                    r =>
                        r.Report.ServiceName == statusReport.ServiceName &&
                        r.Report.Source == statusReport.Source &&
                        r.Report.Image == statusReport.Image,
                    ct
                );

                if (existingReport is null)
                {
                    existingReport = new ServiceStatusReport(statusReport);
                }
                else
                {
                    existingReport.Report = statusReport;
                }

                db.ServiceStatusReports.Update(existingReport);
                await db.SaveChangesAsync(ct);

                _log.LogInformation
                (
                    "Logged status report regarding image {Image} from {Source}",
                    statusReport.Image,
                    statusReport.Source
                );

                return Result.FromSuccess();
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
