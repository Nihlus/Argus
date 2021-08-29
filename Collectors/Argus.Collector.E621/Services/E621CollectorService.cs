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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Common;
using Argus.Common.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Noppes.E621;
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

        private readonly IE621Client _e621Client;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<E621CollectorService> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="E621CollectorService"/> class.
        /// </summary>
        /// <param name="options">The application options.</param>
        /// <param name="e621Client">The E621 client.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="log">The logging instance.</param>
        public E621CollectorService
        (
            IOptions<CollectorOptions> options,
            IE621Client e621Client,
            IHttpClientFactory httpClientFactory,
            ILogger<E621CollectorService> log
        )
            : base(options, log)
        {
            _e621Client = e621Client;
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
            if (!int.TryParse(resumePoint, out var currentPostId))
            {
                currentPostId = 0;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var page = await _e621Client.GetPostsAsync
                    (
                        currentPostId,
                        Position.After,
                        E621Constants.PostsMaximumLimit
                    );

                    if (page.Count <= 0)
                    {
                        _log.LogInformation("Waiting for new posts to come in...");

                        await Task.Delay(TimeSpan.FromHours(1), ct);
                        continue;
                    }

                    var client = _httpClientFactory.CreateClient();
                    foreach (var post in page)
                    {
                        var statusReport = new StatusReport
                        (
                            DateTimeOffset.UtcNow,
                            this.ServiceName,
                            new Uri($"{_e621Client.BaseUrl}/posts/{post.Id}"),
                            new Uri("about:blank"),
                            ImageStatus.Collected,
                            string.Empty
                        );

                        if (post.File is null)
                        {
                            var rejectionReport = statusReport with
                            {
                                Status = ImageStatus.Rejected,
                                Message = "No file"
                            };

                            var reject = PushStatusReport(rejectionReport);
                            if (!reject.IsSuccess)
                            {
                                _log.LogWarning("Failed to push status report: {Reason}", reject.Error.Message);
                                return reject;
                            }

                            return Result.FromSuccess();
                        }

                        if (post.File.FileExtension is "swf" or "gif")
                        {
                            var rejectionReport = statusReport with
                            {
                                Status = ImageStatus.Rejected,
                                Image = post.File.Location,
                                Message = "Animation"
                            };

                            var reject = PushStatusReport(rejectionReport);
                            if (!reject.IsSuccess)
                            {
                                _log.LogWarning("Failed to push status report: {Reason}", reject.Error.Message);
                                return reject;
                            }

                            return Result.FromSuccess();
                        }

                        statusReport = statusReport with
                        {
                            Image = post.File.Location
                        };

                        var bytes = await client.GetByteArrayAsync(post.File.Location, ct);

                        var collectedImage = new CollectedImage
                        (
                            this.ServiceName,
                            statusReport.Source,
                            statusReport.Image,
                            bytes
                        );

                        var push = PushCollectedImage(collectedImage);
                        if (!push.IsSuccess)
                        {
                            _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                            return push;
                        }

                        var collect = PushStatusReport(statusReport);
                        if (!collect.IsSuccess)
                        {
                            _log.LogWarning("Failed to push status report: {Reason}", collect.Error.Message);
                            return collect;
                        }
                    }

                    var mostRecentPost = page.OrderByDescending(p => p.Id).First();
                    currentPostId = mostRecentPost.Id;

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
    }
}
