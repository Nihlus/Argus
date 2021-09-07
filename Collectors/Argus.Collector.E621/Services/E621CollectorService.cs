//
//  E621CollectorService.cs
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.Driver.Minibooru;
using Argus.Collector.Driver.Minibooru.Model;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.E621.Services
{
    /// <summary>
    /// Collects images from E621.
    /// </summary>
    public class E621CollectorService : CollectorService
    {
        /// <inheritdoc />
        protected override string ServiceName => "e621";

        private readonly IBooruDriver _booruDriver;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<E621CollectorService> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="E621CollectorService"/> class.
        /// </summary>
        /// <param name="options">The application options.</param>
        /// <param name="booruDriver">The E621 client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="log">The logging instance.</param>
        public E621CollectorService
        (
            IOptions<CollectorOptions> options,
            IBooruDriver booruDriver,
            IHttpClientFactory httpClientFactory,
            ILogger<E621CollectorService> log
        )
            : base(options, log)
        {
            _booruDriver = booruDriver;
            _httpClientFactory = httpClientFactory;
            _log = log;
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
                    var getPage = await _booruDriver.GetPostsAsync(currentPostId, 320, ct);
                    if (!getPage.IsSuccess)
                    {
                        return Result.FromError(getPage);
                    }

                    var page = getPage.Entity;
                    if (page.Count <= 0)
                    {
                        _log.LogInformation("Waiting for new posts to come in...");

                        await Task.Delay(TimeSpan.FromHours(1), ct);
                        continue;
                    }

                    var client = _httpClientFactory.CreateClient("BulkDownload");
                    var collections = await Task.WhenAll(page.Select(p => CollectImageAsync(client, p, ct)));

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

                    currentPostId = page.Select(p => p.ID).Max();

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
            try
            {
                var (_, file, source) = post;

                var statusReport = new StatusReport
                (
                    DateTime.UtcNow,
                    this.ServiceName,
                    source,
                    new Uri("about:blank"),
                    ImageStatus.Collected,
                    string.Empty
                );

                if (file is null)
                {
                    var rejectionReport = statusReport with
                    {
                        Status = ImageStatus.Rejected,
                        Message = "No file (login required)"
                    };

                    return (rejectionReport, null);
                }

                var fileExtension = Path.GetExtension(file);
                if (fileExtension is "swf" or "gif")
                {
                    var rejectionReport = statusReport with
                    {
                        Status = ImageStatus.Rejected,
                        Image = new Uri(file),
                        Message = "Animation"
                    };

                    return (rejectionReport, null);
                }

                statusReport = statusReport with
                {
                    Image = new Uri(file),
                };

                var bytes = await client.GetByteArrayAsync(file, ct);

                var collectedImage = new CollectedImage
                (
                    this.ServiceName,
                    statusReport.Source,
                    statusReport.Image,
                    bytes
                );

                return (statusReport, collectedImage);
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
