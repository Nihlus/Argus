//
//  OuroborosDriver.cs
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
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Implements Ouroboros-specific driver functionality.
/// </summary>
public class OuroborosDriver : AbstractBooruDriver<OuroborosPage>
{
    /// <summary>
    /// Gets the name of the driver.
    /// </summary>
    public static string Name => "ouroboros";

    /// <inheritdoc />
    protected override IReadOnlyList<ProductInfoHeaderValue> UserAgent => new[]
    {
        new ProductInfoHeaderValue("Argus", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0"),
        new ProductInfoHeaderValue("(by Jax#7487 on Discord)")
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosDriver"/> class.
    /// </summary>
    /// <param name="clientFactory">The HTTP client to use.</param>
    /// <param name="jsonOptions">The JSON serializer options.</param>
    /// <param name="driverOptions">The driver options.</param>
    public OuroborosDriver
    (
        IHttpClientFactory clientFactory,
        IOptionsMonitor<JsonSerializerOptions> jsonOptions,
        IOptionsMonitor<BooruDriverOptions> driverOptions
    )
        : base(clientFactory, jsonOptions, driverOptions)
    {
    }

    /// <inheritdoc />
    protected override Result<IReadOnlyList<BooruPost>> MapInternalPage(OuroborosPage internalPage)
    {
        return internalPage.Posts.Select
        (
            post =>
            {
                var (id, file) = post;

                var postUrl = new Uri(this.DriverOptions.BaseUrl, $"posts/{id}");
                return new BooruPost(id, file.Url?.ToString(), postUrl);
            }
        ).ToList();
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
