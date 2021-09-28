//
//  GelbooruDriver.cs
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
using System.Net.Http;
using System.Text.Json;
using System.Web;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru
{
    /// <summary>
    /// Implements Gelbooru-specific driver functionality.
    /// </summary>
    public class GelbooruDriver : AbstractBooruDriver<IReadOnlyList<GelbooruPost>>
    {
        /// <summary>
        /// Gets the name of the driver.
        /// </summary>
        public static string Name => "gelbooru";

        /// <inheritdoc />
        protected override bool AllowEmptyResponse => true;

        private readonly GelbooruDriverOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GelbooruDriver"/> class.
        /// </summary>
        /// <param name="options">The Gelbooru options.</param>
        /// <param name="clientFactory">The HTTP client to use.</param>
        /// <param name="jsonOptions">The JSON serializer options.</param>
        /// <param name="driverOptions">The driver options.</param>
        public GelbooruDriver
        (
            IOptions<GelbooruDriverOptions> options,
            IHttpClientFactory clientFactory,
            IOptionsMonitor<JsonSerializerOptions> jsonOptions,
            IOptionsMonitor<BooruDriverOptions> driverOptions
        )
            : base
            (
                clientFactory,
                jsonOptions,
                driverOptions
            )
        {
            _options = options.Value;
        }

        /// <inheritdoc />
        protected override Result<IReadOnlyList<BooruPost>> MapInternalPage(IReadOnlyList<GelbooruPost> internalPage)
        {
            return internalPage.Select
            (
                post =>
                {
                    var (id, fileUrl, directory, image) = post;

                    // Prefer the suggested file URL whenever possible
                    var realFileUrl = fileUrl is not null
                        ? new Uri(fileUrl)
                        : new Uri($"{_options.BaseCDNUrl.ToString().TrimEnd('/')}/images/{directory}/{image}");

                    var postUrl = new Uri(this.DriverOptions.BaseUrl, $"index.php?page=post&s=view&id={id}");
                    return new BooruPost(id, realFileUrl.ToString(), postUrl);
                }
            ).ToList();
        }

        /// <inheritdoc />
        protected override Uri GetSearchUrl(ulong after, uint limit)
        {
            if (limit > 1000)
            {
                limit = 1000;
            }

            var tags = HttpUtility.UrlEncode($"sort:id:asc id:>{after}");
            return new Uri
            (
                this.DriverOptions.BaseUrl,
                $"index.php?page=dapi&s=post&q=index&json=1&limit={limit}&tags={tags}"
            );
        }
    }
}
