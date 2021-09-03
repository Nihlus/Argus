//
//  HypnohubAPI.cs
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
using Argus.Collector.Common.Json;
using Argus.Collector.Hypnohub.Json;
using BooruDex.Booru.Template;
using BooruDex.Exceptions;
using BooruDex.Models;
using Remora.Results;

namespace Argus.Collector.Hypnohub.Implementations
{
    /// <summary>
    /// Acts as an interface class for Hypnohub.
    /// </summary>
    public class HypnohubAPI : Moebooru
    {
        /// <summary>
        /// Gets the base URL of the API.
        /// </summary>
        public string BaseUrl => _BaseUrl.ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="HypnohubAPI"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        public HypnohubAPI(HttpClient? httpClient = null)
            : base("hypnohub.net", true, httpClient)
        {
        }

        /// <summary>
        /// Gets the posts on the given page.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="after">The ID the search should start after.</param>
        /// <returns>The posts.</returns>
        public async Task<Result<IReadOnlyCollection<Post>>> GetPostsAsync(uint page = 1, uint? after = 1)
        {
            var tags = new[]
            {
                "order:id",
                $"{(after.HasValue ? $"id:>={after}" : string.Empty)}"
            };

            try
            {
                return await PostListAsync(100, tags, page);
            }
            catch (SearchNotFoundException)
            {
                return Array.Empty<Post>();
            }
            catch (Exception e)
            {
                return e;
            }
        }

        /// <inheritdoc />
        protected override Post ReadPost(JsonElement json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
            };

            options.Converters.Add(new RatingConverter());

            var post = JsonSerializer.Deserialize<Post>
            (
                json.ToString() ?? throw new InvalidOperationException(),
                options
            );

            return post ?? throw new InvalidOperationException();
        }
    }
}
