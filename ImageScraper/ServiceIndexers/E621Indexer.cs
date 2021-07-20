//
//  E621Indexer.cs
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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ImageScraper.Model;
using ImageScraper.Pipeline.WorkUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Noppes.E621;

namespace ImageScraper.ServiceIndexers
{
    /// <summary>
    /// Indexes https://e621.net.
    /// </summary>
    public class E621Indexer : IServiceIndexer<int>
    {
        private readonly IDbContextFactory<IndexingContext> _contextFactory;
        private readonly IE621Client _e621Client;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<E621Indexer> _log;

        /// <inheritdoc />
        public string Service => "e621";

        /// <summary>
        /// Initializes a new instance of the <see cref="E621Indexer"/> class.
        /// </summary>
        /// <param name="e621Client">The e621 client.</param>
        /// <param name="memoryCache">The memory cache.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="contextFactory">The database context factory.</param>
        public E621Indexer
        (
            IE621Client e621Client,
            IMemoryCache memoryCache,
            IHttpClientFactory httpClientFactory,
            ILogger<E621Indexer> log,
            IDbContextFactory<IndexingContext> contextFactory
        )
        {
            _e621Client = e621Client;
            _memoryCache = memoryCache;
            _httpClientFactory = httpClientFactory;
            _log = log;
            _contextFactory = contextFactory;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<int> GetSourceIdentifiersAsync
        (
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            int currentPostId;
            await using (var db = _contextFactory.CreateDbContext())
            {
                var state = db.ServiceStates.FirstOrDefault(s => s.Name == this.Service);
                if (state is null)
                {
                    state = db.CreateProxy<ServiceState>();
                    state.Name = this.Service;

                    db.ServiceStates.Update(state);
                    await db.SaveChangesAsync(ct);
                }

                if (!int.TryParse(state.ResumePoint, out currentPostId))
                {
                    currentPostId = 0;
                    state.ResumePoint = currentPostId.ToString();

                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Be pessimistic - assume the whole chain has failed
                    var potentiallyFailed = Environment.ProcessorCount * 4 * 3;
                    currentPostId -= potentiallyFailed;
                    currentPostId = Math.Clamp(currentPostId, 0, currentPostId);
                }
            }

            while (!ct.IsCancellationRequested)
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

                foreach (var post in page)
                {
                    if (post.File is null)
                    {
                        _log.LogDebug("Skipping post {ID} (no file)", post.Id);
                        continue;
                    }

                    if (post.File?.FileExtension is "swf" or "gif")
                    {
                        _log.LogDebug("Skipping post {ID} (animation)", post.Id);
                        continue;
                    }

                    var key = $"e621.{post.Id}";
                    _memoryCache.Set(key, post);

                    yield return post.Id;
                }

                var mostRecentPost = page.OrderByDescending(p => p.Id).First();
                currentPostId = mostRecentPost.Id;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AssociatedImage> GetImagesAsync
        (
            int sourceIdentifier,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            var key = $"e621.{sourceIdentifier}";
            if (!_memoryCache.TryGetValue<Post>(key, out var post))
            {
                post = await _e621Client.GetPostAsync(sourceIdentifier);
            }
            else
            {
                _memoryCache.Remove(key);
            }

            if (post?.File is null)
            {
                yield break;
            }

            var client = _httpClientFactory.CreateClient();
            await using var stream = await client.GetStreamAsync(post.File.Location, ct);

            // Copy to avoid dealing with HttpClient for longer than necessary
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct);

            // Rewind the stream for the upcoming consumer
            memoryStream.Seek(0, SeekOrigin.Begin);

            yield return new AssociatedImage
            (
                "e621",
                new Uri($"{_e621Client.BaseUrl}/posts/{sourceIdentifier}"),
                post.File.Location,
                memoryStream
            );
        }
    }
}
