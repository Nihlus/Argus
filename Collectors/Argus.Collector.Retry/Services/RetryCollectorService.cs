//
//  RetryCollectorService.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.Retry.Configuration;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using Remora.Results;

namespace Argus.Collector.Retry.Services
{
    /// <summary>
    /// Retries failed images.
    /// </summary>
    public class RetryCollectorService : CollectorService
    {
        private readonly RetryOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RetryCollectorService> _log;

        /// <inheritdoc />
        protected override string ServiceName => "retry";

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryCollectorService"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="httpClientFactory">The http client factory.</param>
        /// <param name="collectorOptions">The collector options.</param>
        /// <param name="log">The logging instance.</param>
        public RetryCollectorService
        (
            IOptions<RetryOptions> options,
            IHttpClientFactory httpClientFactory,
            IOptions<CollectorOptions> collectorOptions,
            ILogger<RetryCollectorService> log)
            : base(collectorOptions, log)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _log = log;
        }

        /// <inheritdoc />
        protected override async Task<Result> CollectAsync(CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested)
            {
                var getPage = await GetImagesToRetryAsync(ct);
                if (!getPage.IsSuccess)
                {
                    return Result.FromError(getPage);
                }

                var page = getPage.Entity;
                if (page.Count == 0)
                {
                    _log.LogInformation("Waiting for new images to retry...");
                    await Task.Delay(TimeSpan.FromHours(1), ct);
                    continue;
                }

                var client = _httpClientFactory.CreateClient("BulkDownload");
                var collections = await Task.WhenAll(page.Select(report => CollectImageAsync(client, report, ct)));

                foreach (var collection in collections)
                {
                    if (!collection.IsSuccess)
                    {
                        _log.LogWarning("Failed to collect image: {Reason}", collection.Error.Message);
                        continue;
                    }

                    var (statusReport, collectedImage) = collection.Entity;

                    var report = PushStatusReport(statusReport);
                    if (!report.IsSuccess)
                    {
                        _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
                        return report;
                    }

                    if (collectedImage is null)
                    {
                        continue;
                    }

                    var push = PushCollectedImage(collectedImage);
                    if (push.IsSuccess)
                    {
                        continue;
                    }

                    _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                    return push;
                }
            }

            return Result.FromSuccess();
        }

        private async Task<Result<(StatusReport Report, CollectedImage? Image)>> CollectImageAsync
        (
            HttpClient client,
            StatusReport failedImage,
            CancellationToken ct = default
        )
        {
            var statusReport = new StatusReport
            (
                DateTime.UtcNow,
                failedImage.ServiceName,
                failedImage.Source,
                failedImage.Image,
                ImageStatus.Collected,
                string.Empty
            );

            try
            {
                var bytes = await client.GetByteArrayAsync(failedImage.Image, ct);

                var collectedImage = new CollectedImage
                (
                    statusReport.ServiceName,
                    statusReport.Source,
                    statusReport.Image,
                    bytes
                );

                return (statusReport, collectedImage);
            }
            catch (HttpRequestException hex) when (hex.StatusCode is HttpStatusCode.NotFound)
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "File not found (deleted?)"
                };

                return (rejectionReport, null);
            }
            catch (Exception e)
            {
                return e;
            }
        }

        /// <summary>
        /// Gets a page of images to retry.
        /// </summary>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The resume point.</returns>
        private async Task<Result<IReadOnlyCollection<StatusReport>>> GetImagesToRetryAsync(CancellationToken ct = default)
        {
            var message = new GetImagesToRetryRequest(_options.PageSize);
            var serialized = MessagePackSerializer.Serialize<ICoordinatorRequest>(message, cancellationToken: ct);
            this.RequestSocket.SendFrame(serialized);

            var (frame, _) = await this.RequestSocket.ReceiveFrameBytesAsync(ct);
            var response = MessagePackSerializer.Deserialize<ICoordinatorReply>(frame, cancellationToken: ct);
            return response switch
            {
                ImagesToRetryReply imagesToRetryReply
                    => Result<IReadOnlyCollection<StatusReport>>.FromSuccess(imagesToRetryReply.ImagesToRetry),
                _
                    => new InvalidOperationError("Unknown response.")
            };
        }
    }
}
