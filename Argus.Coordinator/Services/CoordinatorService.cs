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
using System.Threading;
using System.Threading.Tasks;
using Argus.Common;
using Argus.Common.Messages;
using Argus.Coordinator.Configuration;
using Argus.Coordinator.Model;
using Argus.Coordinator.Services.Elasticsearch;
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
            await Task.WhenAll(RunResponsesAsync(stoppingToken), RunPullsAsync(stoppingToken));
        }

        private async Task RunResponsesAsync(CancellationToken ct = default)
        {
            var responseSocket = new ResponseSocket();
            responseSocket.Bind(_options.CoordinatorEndpoint.ToString().TrimEnd('/'));

            while (!ct.IsCancellationRequested)
            {
                NetMQMessage? responseMessage = null;
                if (responseSocket.TryReceiveMultipartMessage(ref responseMessage))
                {
                    await HandleResponseMessageAsync(responseMessage, responseSocket, ct);
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

            while (!ct.IsCancellationRequested)
            {
                NetMQMessage? pullMessage = null;
                if (incomingSocket.TryReceiveMultipartMessage(ref pullMessage))
                {
                    await HandlePullMessageAsync(pullMessage, outgoingSocket, ct);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                }
            }
        }

        private async Task HandleResponseMessageAsync(NetMQMessage incomingMessage, ResponseSocket responseSocket, CancellationToken ct = default)
        {
            var messageType = incomingMessage.First.ConvertToString();
            switch (messageType)
            {
                case var _ when messageType == GetResumeRequest.MessageType:
                {
                    if (!GetResumeRequest.TryParse(incomingMessage, out var getResumeRequest))
                    {
                        _log.LogWarning
                        (
                            "Failed to parse incoming message as a {Type}",
                            CollectedImage.MessageType
                        );

                        return;
                    }

                    await using var db = _contextFactory.CreateDbContext();
                    var serviceStatus = await db.ServiceStates.AsNoTracking().FirstOrDefaultAsync
                    (
                        s => s.Name == getResumeRequest.ServiceName,
                        ct
                    );

                    var resumePoint = serviceStatus?.ResumePoint;
                    var resumeResponse = new ResumeReply(resumePoint ?? string.Empty);
                    responseSocket.SendMultipartMessage(resumeResponse.Serialize());

                    _log.LogInformation
                    (
                        "Told collector for service \"{Service}\" its resume point",
                        getResumeRequest.ServiceName
                    );
                    break;
                }
                case var _ when messageType == SetResumeRequest.MessageType:
                {
                    if (!SetResumeRequest.TryParse(incomingMessage, out var setResumeRequest))
                    {
                        _log.LogWarning
                        (
                            "Failed to parse incoming message as a {Type}",
                            CollectedImage.MessageType
                        );

                        return;
                    }

                    await using var db = _contextFactory.CreateDbContext();
                    var serviceStatus = await db.ServiceStates.FirstOrDefaultAsync
                    (
                        s => s.Name == setResumeRequest.ServiceName,
                        ct
                    );

                    serviceStatus.ResumePoint = setResumeRequest.ResumePoint;
                    await db.SaveChangesAsync(ct);

                    var resumePoint = serviceStatus.ResumePoint;
                    var resumeResponse = new ResumeReply(resumePoint ?? string.Empty);
                    responseSocket.SendMultipartMessage(resumeResponse.Serialize());

                    _log.LogInformation
                    (
                        "Set the resume point of the collector for service \"{Service}\" to \"{ResumePoint}\"",
                        setResumeRequest.ServiceName,
                        setResumeRequest.ResumePoint
                    );

                    break;
                }
            }
        }

        private async Task HandlePullMessageAsync(NetMQMessage incomingMessage, PushSocket outgoingSocket, CancellationToken ct = default)
        {
            var messageType = incomingMessage.First.ConvertToString();
            switch (messageType)
            {
                case var _ when messageType == CollectedImage.MessageType:
                {
                    if (!CollectedImage.TryParse(incomingMessage, out var collectedImage))
                    {
                        _log.LogWarning
                        (
                            "Failed to parse incoming message as a {Type}",
                            CollectedImage.MessageType
                        );

                        return;
                    }

                    outgoingSocket.SendMultipartMessage(collectedImage.Serialize());

                    var statusReport = new StatusReport
                    (
                        DateTimeOffset.UtcNow,
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
                case var _ when messageType == FingerprintedImage.MessageType:
                {
                    if (!FingerprintedImage.TryParse(incomingMessage, out var fingerprintedImage))
                    {
                        _log.LogWarning
                        (
                            "Failed to parse incoming message as a {Type}",
                            FingerprintedImage.MessageType
                        );

                        return;
                    }

                    var result = await HandleFingerprintedImageAsync(fingerprintedImage, ct);
                    if (!result.IsSuccess)
                    {
                        _log.LogWarning("Failed to handle fingerprinted image: {Message}", result.Error.Message);
                    }

                    _log.LogInformation
                    (
                        "Indexed fingerprinted image from service \"{Service}\"",
                        fingerprintedImage.ServiceName
                    );
                    break;
                }
                case var _ when messageType == StatusReport.MessageType:
                {
                    if (!StatusReport.TryParse(incomingMessage, out var statusReport))
                    {
                        _log.LogWarning
                        (
                            "Failed to parse incoming message as a {Type}",
                            FingerprintedImage.MessageType
                        );

                        return;
                    }

                    var result = await HandleStatusReportAsync(statusReport, ct);
                    if (!result.IsSuccess)
                    {
                        _log.LogWarning("Failed to handle status report: {Message}", result.Error.Message);
                    }

                    _log.LogInformation
                    (
                        "Logged status report regarding image {Image} from {Source}",
                        statusReport.Image,
                        statusReport.Source
                    );
                    break;
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

            return await _nestService.IndexImageAsync(indexedImage, ct);
        }

        private async Task<Result> HandleStatusReportAsync
        (
            StatusReport statusReport,
            CancellationToken ct
        )
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

            return Result.FromSuccess();
        }
    }
}
