//
//  AbstractBooruDriver.cs
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
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru
{
    /// <summary>
    /// Serves as an abstract base for Booru drivers.
    /// </summary>
    /// <typeparam name="TInternalPost">The internal post format.</typeparam>
    public abstract class AbstractBooruDriver<TInternalPost> : IBooruDriver
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Gets the driver options.
        /// </summary>
        protected BooruDriverOptions DriverOptions { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractBooruDriver{TInternalPost}"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        /// <param name="jsonOptions">The JSON serializer options.</param>
        /// <param name="driverOptions">The driver options.</param>
        protected AbstractBooruDriver
        (
            HttpClient httpClient,
            IOptionsMonitor<JsonSerializerOptions> jsonOptions,
            IOptionsMonitor<BooruDriverOptions> driverOptions
        )
        {
            _httpClient = httpClient;
            _jsonOptions = jsonOptions.Get(GetType().Name);

            this.DriverOptions = driverOptions.Get(GetType().Name);
        }

        /// <summary>
        /// Maps the Booru's internal post representation to the public API surface.
        /// </summary>
        /// <param name="internalPost">The internal post.</param>
        /// <returns>The public post.</returns>
        protected abstract Result<BooruPost> MapInternalPost(TInternalPost internalPost);

        /// <summary>
        /// Gets the search URL for the given parameters.
        /// </summary>
        /// <param name="after">The ID after which posts should be returned.</param>
        /// <param name="limit">The maximum number of posts to return.</param>
        /// <returns>The search URL.</returns>
        protected abstract Uri GetSearchUrl(ulong after, uint limit);

        /// <inheritdoc/>
        public async Task<Result<IReadOnlyList<BooruPost>>> GetPostsAsync
        (
            ulong after = 0,
            uint limit = 100,
            CancellationToken ct = default
        )
        {
            try
            {
                var searchUrl = GetSearchUrl(after, limit);
                var response = await _httpClient.GetAsync(searchUrl, ct);

                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                var posts = await JsonSerializer.DeserializeAsync<IReadOnlyList<TInternalPost>>
                (
                    contentStream,
                    _jsonOptions,
                    ct
                );

                if (posts is null)
                {
                    throw new InvalidOperationException();
                }

                var mappedPosts = new List<BooruPost>();
                foreach (var internalPost in posts)
                {
                    var mapPost = MapInternalPost(internalPost);
                    if (!mapPost.IsSuccess)
                    {
                        return Result<IReadOnlyList<BooruPost>>.FromError(mapPost);
                    }

                    mappedPosts.Add(mapPost.Entity);
                }

                return mappedPosts;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
