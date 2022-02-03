//
//  BooruCollectorService.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Booru.Configuration;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.Driver.Minibooru;
using Argus.Collector.Driver.Minibooru.Model;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using MassTransit.MessageData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Booru.Services
{
    /// <summary>
    /// Collects images from a Booru.
    /// </summary>
    public class BooruCollectorService : CollectorService
    {
        /// <inheritdoc />
        protected override string ServiceName => _options.ServiceName;

        private readonly BooruOptions _options;
        private readonly IBooruDriver _booruDriver;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BooruCollectorService> _log;
        private readonly IMessageDataRepository _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="BooruCollectorService"/> class.
        /// </summary>
        /// <param name="booruOptions">The collector-specific options.</param>
        /// <param name="booruDriver">The Booru driver.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="bus">The message bus.</param>
        /// <param name="repository">The message repository.</param>
        /// <param name="options">The application options.</param>
        /// <param name="log">The logging instance.</param>
        public BooruCollectorService
        (
            IOptions<BooruOptions> booruOptions,
            IBooruDriver booruDriver,
            IHttpClientFactory httpClientFactory,
            IBus bus,
            IMessageDataRepository repository,
            IOptions<CollectorOptions> options,
            ILogger<BooruCollectorService> log)
            : base(bus, options, log)
        {
            _options = booruOptions.Value;
            _booruDriver = booruDriver;
            _httpClientFactory = httpClientFactory;
            _log = log;
            _repository = repository;
        }

        /// <inheritdoc />
        protected override async Task<Result> CollectAsync(CancellationToken ct = default)
        {
            var getResume = await GetResumePointAsync(ct);
            if (!getResume.IsSuccess)
            {
                _log.LogWarning("Failed to get the resume point: {Reason}", getResume.Error.Message);
                return Result.FromError(getResume);
            }

            var resumePoint = getResume.Entity;
            if (!ulong.TryParse(resumePoint, out var currentPostId))
            {
                currentPostId = 0;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var getPosts = await _booruDriver.GetPostsAsync(currentPostId, ct: ct);
                    if (!getPosts.IsSuccess)
                    {
                        return Result.FromError(getPosts);
                    }

                    var posts = getPosts.Entity;
                    if (posts.Count <= 0)
                    {
                        _log.LogInformation("Waiting for new posts to come in...");

                        await Task.Delay(TimeSpan.FromHours(1), ct);
                        continue;
                    }

                    var client = _httpClientFactory.CreateClient("BulkDownload");
                    var collections = await Task.WhenAll(posts.Select(p => CollectImageAsync(client, p, ct)));

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

                    var mostRecentPost = posts.OrderByDescending(p => p.ID).First();
                    currentPostId = mostRecentPost.ID;

                    var setResume = await SetResumePointAsync(currentPostId.ToString(), ct);
                    if (setResume.IsSuccess)
                    {
                        continue;
                    }

                    _log.LogWarning("Failed to set resume point: {Reason}", setResume.Error.Message);
                    return setResume;
                }
                catch (Exception e)
                {
                    return e;
                }
            }

            return Result.FromSuccess();
        }

        private async Task<Result<(StatusReport Report, CollectedImage? Image)>> CollectImageAsync
        (
            HttpClient client,
            BooruPost post,
            CancellationToken ct = default
        )
        {
            var (_, file, source) = post;

            var statusReport = new StatusReport
            (
                DateTimeOffset.UtcNow,
                this.ServiceName,
                source,
                new Uri("about:blank"),
                ImageStatus.Collected,
                string.Empty
            );

            try
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    var rejectionReport = statusReport with
                    {
                        Status = ImageStatus.Rejected,
                        Message = "No file (deleted or login required)"
                    };

                    return (rejectionReport, null);
                }

                var fileExtension = Path.GetExtension(file);
                if (fileExtension is ".swf" or ".gif")
                {
                    var rejectionReport = statusReport with
                    {
                        Status = ImageStatus.Rejected,
                        Link = new Uri(file),
                        Message = "Animation"
                    };

                    return (rejectionReport, null);
                }

                statusReport = statusReport with
                {
                    Link = new Uri(file)
                };

                var bytes = await client.GetByteArrayAsync(file, ct);

                var collectedImage = new CollectedImage
                (
                    this.ServiceName,
                    statusReport.Source,
                    statusReport.Link,
                    await _repository.PutBytes(bytes, TimeSpan.FromHours(8), ct)
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
    }
}
