//
//  PhilomenaDriver.cs
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
using System.Web;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Implements Philomena-specific driver functionality.
/// </summary>
public class PhilomenaDriver : AbstractBooruDriver<PhilomenaPage>
{
    /// <summary>
    /// Gets the name of the driver.
    /// </summary>
    public static string Name => "philomena";

    /// <inheritdoc />
    protected override IReadOnlyList<ProductInfoHeaderValue> UserAgent => new[]
    {
        new ProductInfoHeaderValue("Argus", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0"),
        new ProductInfoHeaderValue("(by Jax#7487 on Discord)")
    };

    private readonly PhilomenaDriverOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhilomenaDriver"/> class.
    /// </summary>
    /// <param name="options">The Philomena options.</param>
    /// <param name="clientFactory">The HTTP client to use.</param>
    /// <param name="jsonOptions">The JSON serializer options.</param>
    /// <param name="driverOptions">The driver options.</param>
    public PhilomenaDriver
    (
        IOptions<PhilomenaDriverOptions> options,
        IHttpClientFactory clientFactory,
        IOptionsMonitor<JsonSerializerOptions> jsonOptions,
        IOptionsMonitor<BooruDriverOptions> driverOptions
    )
        : base(clientFactory, jsonOptions, driverOptions)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override Result<IReadOnlyList<BooruPost>> MapInternalPage(PhilomenaPage internalPage)
    {
        return internalPage.Images.Select
        (
            philomenaImage =>
            {
                var id = philomenaImage.ID;
                if (!philomenaImage.Representations.TryGetValue("full", out var fileUrl))
                {
                    fileUrl = philomenaImage.Representations.Values.FirstOrDefault();
                }

                var postUrl = new Uri(this.DriverOptions.BaseUrl, $"images/{id}");
                return new BooruPost(id, fileUrl?.ToString(), postUrl);
            }
        ).ToList();
    }

    /// <inheritdoc />
    protected override Uri GetSearchUrl(ulong after, uint limit)
    {
        if (limit > 50)
        {
            limit = 50;
        }

        var nameValueCollection = HttpUtility.ParseQueryString(string.Empty);
        if (_options.EverythingFilterID is not null)
        {
            nameValueCollection.Add("filter_id", _options.EverythingFilterID.ToString());
        }

        nameValueCollection.Add("sd", "asc");
        nameValueCollection.Add("sf", "id");
        nameValueCollection.Add("per_page", limit.ToString());

        // philomena starts indexing at 0, not 1 like most imageboards
        nameValueCollection.Add("q", $"id.gt:{(after is 0 ? "-1" : after.ToString())}");

        return new Uri(this.DriverOptions.BaseUrl, $"api/v1/json/search/images?{nameValueCollection}");
    }
}
