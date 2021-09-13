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
using MassTransit;
using MassTransit.MessageData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly IMessageDataRepository _repository;

        /// <inheritdoc />
        protected override string ServiceName => "retry";

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryCollectorService"/> class.
        /// </summary>
        /// <param name="retryOptions">The retryOptions.</param>
        /// <param name="httpClientFactory">The http client factory.</param>
        /// <param name="bus">The message bus.</param>
        /// <param name="repository">The message data repository.</param>
        /// <param name="options">The collector retryOptions.</param>
        /// <param name="log">The logging instance.</param>
        public RetryCollectorService
        (
            IOptions<RetryOptions> retryOptions,
            IHttpClientFactory httpClientFactory,
            IBus bus,
            IMessageDataRepository repository,
            IOptions<CollectorOptions> options,
            ILogger<RetryCollectorService> log)
            : base(bus, options, log)
        {
            _options = retryOptions.Value;
            _httpClientFactory = httpClientFactory;
            _log = log;
            _repository = repository;
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

                    var report = await PushStatusReportAsync(statusReport, ct);
                    if (!report.IsSuccess)
                    {
                        _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
                        return report;
                    }

                    if (collectedImage is null)
                    {
                        continue;
                    }

                    var push = await PushCollectedImageAsync(collectedImage, ct);
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
                failedImage.Link,
                ImageStatus.Collected,
                string.Empty
            );

            try
            {
                var bytes = await client.GetByteArrayAsync(failedImage.Link, ct);

                var collectedImage = new CollectedImage
                (
                    statusReport.ServiceName,
                    statusReport.Source,
                    statusReport.Link,
                    await _repository.PutBytes(bytes, ct)
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
            var message = new GetImagesToRetry(_options.PageSize);
            var response = await this.Bus.Request<GetImagesToRetry, ImagesToRetry>(message, ct);
            return Result<IReadOnlyCollection<StatusReport>>.FromSuccess(response.Message.Value);
        }
    }
}
