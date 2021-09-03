//
//  HypnohubCollectorService.cs
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
using Argus.Collector.Hypnohub.Implementations;
using Argus.Common;
using Argus.Common.Messages;
using BooruDex.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoreLinq.Extensions;
using Remora.Results;

namespace Argus.Collector.Hypnohub.Services
{
    /// <summary>
    /// Collects images from Hypnohub.
    /// </summary>
    public class HypnohubCollectorService : CollectorService
    {
        /// <inheritdoc />
        protected override string ServiceName => "hypnohub";

        private readonly HypnohubAPI _hypnohubAPI;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HypnohubCollectorService> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="HypnohubCollectorService"/> class.
        /// </summary>
        /// <param name="options">The application options.</param>
        /// <param name="hypnohubAPI">The Hypnohub API.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="log">The logging instance.</param>
        public HypnohubCollectorService
        (
            IOptions<CollectorOptions> options,
            HypnohubAPI hypnohubAPI,
            IHttpClientFactory httpClientFactory,
            ILogger<HypnohubCollectorService> log
        )
            : base(options, log)
        {
            _hypnohubAPI = hypnohubAPI;
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
            if (!uint.TryParse(resumePoint, out var currentPostId))
            {
                currentPostId = 0;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _hypnohubAPI.HttpClient = _httpClientFactory.CreateClient(nameof(HypnohubAPI));
                    var getPosts = await _hypnohubAPI.GetPostsAsync(after: currentPostId);
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

                    var client = _httpClientFactory.CreateClient();
                    var collections = await Task.WhenAll(posts.Select(p => CollectImageAsync(client, p, ct)));

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

                    var mostRecentPost = posts.OrderByDescending(p => p.ID).First();
                    currentPostId = mostRecentPost.ID;

                    // TODO: This hangs
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

        private async Task<Result<(StatusReport Report, CollectedImage? Image)>> CollectImageAsync(HttpClient client, Post post, CancellationToken ct = default)
        {
            var statusReport = new StatusReport
            (
                DateTimeOffset.UtcNow,
                this.ServiceName,
                new Uri($"{_hypnohubAPI.BaseUrl}post/show/{post.PostUrl}"),
                new Uri("about:blank"),
                ImageStatus.Collected,
                string.Empty
            );

            if (string.IsNullOrWhiteSpace(post.FileUrl))
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "No file"
                };

                return (rejectionReport, null);
            }

            var fileExtension = Path.GetExtension(post.FileUrl);
            if (fileExtension is ".swf" or ".gif")
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Image = new Uri(post.FileUrl),
                    Message = "Animation"
                };

                return (rejectionReport, null);
            }

            statusReport = statusReport with
            {
                Image = new Uri(post.FileUrl)
            };

            var bytes = await client.GetByteArrayAsync(post.FileUrl, ct);

            var collectedImage = new CollectedImage
            (
                this.ServiceName,
                statusReport.Source,
                statusReport.Image,
                bytes
            );

            return (statusReport, collectedImage);
        }
    }
}
