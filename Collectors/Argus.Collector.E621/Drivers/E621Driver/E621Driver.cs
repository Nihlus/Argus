//
//  E621Driver.cs
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
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Web;
using Argus.Collector.Driver.Minibooru;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.E621.Drivers
{
    /// <summary>
    /// Implements E621-specific driver functionality.
    /// </summary>
    public class E621Driver : AbstractBooruDriver<E621Post>
    {
        /// <inheritdoc />
        protected override IReadOnlyList<ProductInfoHeaderValue> UserAgent => new[]
        {
            new ProductInfoHeaderValue("Argus", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0"),
            new ProductInfoHeaderValue("(by Jax#7487 on Discord)")
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="E621Driver"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use.</param>
        /// <param name="jsonOptions">The JSON serializer options.</param>
        /// <param name="driverOptions">The driver options.</param>
        public E621Driver
        (
            HttpClient httpClient,
            IOptionsMonitor<JsonSerializerOptions> jsonOptions,
            IOptionsMonitor<BooruDriverOptions> driverOptions
        )
            : base(httpClient, jsonOptions, driverOptions)
        {
        }

        /// <inheritdoc />
        protected override Result<BooruPost> MapInternalPost(E621Post internalPost)
        {
            var (id, file) = internalPost;

            var postUrl = new Uri(this.DriverOptions.BaseUrl, $"posts/{id}");
            return new BooruPost(id, file.Url.ToString(), postUrl);
        }

        /// <inheritdoc />
        protected override Uri GetSearchUrl(ulong after, uint limit)
        {
            if (limit > 320)
            {
                limit = 320;
            }

            return new Uri(this.DriverOptions.BaseUrl, $"posts.json?limit={limit}&page=a{after}");
        }
    }
}
