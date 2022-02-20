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
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Driver.Minibooru.Model;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Serves as an abstract base for Booru drivers.
/// </summary>
/// <typeparam name="TInternalPage">The internal page format.</typeparam>
public abstract class AbstractBooruDriver<TInternalPage> : IBooruDriver
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Gets the driver options.
    /// </summary>
    protected BooruDriverOptions DriverOptions { get; }

    /// <summary>
    /// Gets the user agent to use.
    /// </summary>
    protected virtual IReadOnlyCollection<ProductInfoHeaderValue>? UserAgent => null;

    /// <summary>
    /// Gets a value indicating whether an empty response should be allowed, in which case an empty page is assumed.
    /// </summary>
    protected virtual bool AllowEmptyResponse => false;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractBooruDriver{TInternalPost}"/> class.
    /// </summary>
    /// <param name="clientFactory">The HTTP client factory.</param>
    /// <param name="jsonOptions">The JSON serializer options.</param>
    /// <param name="driverOptions">The driver options.</param>
    protected AbstractBooruDriver
    (
        IHttpClientFactory clientFactory,
        IOptionsMonitor<JsonSerializerOptions> jsonOptions,
        IOptionsMonitor<BooruDriverOptions> driverOptions
    )
    {
        _clientFactory = clientFactory;
        _jsonOptions = jsonOptions.Get(GetType().Name);

        this.DriverOptions = driverOptions.Get(GetType().Name);
    }

    /// <summary>
    /// Maps the Booru's internal post representation to the public API surface.
    /// </summary>
    /// <param name="internalPage">The internal page.</param>
    /// <returns>The public post.</returns>
    protected abstract Result<IReadOnlyList<BooruPost>> MapInternalPage(TInternalPage internalPage);

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
            var client = _clientFactory.CreateClient(GetType().Name);
            var searchUrl = GetSearchUrl(after, limit);

            using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            if (this.UserAgent is not null)
            {
                foreach (var headerValue in this.UserAgent)
                {
                    request.Headers.UserAgent.Add(headerValue);
                }
            }

            using var response = await client.SendAsync(request, ct);

            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);

            // Buffer the response so we can check the actual size of the response
            await using var bufferedContentStream = new MemoryStream();
            await contentStream.CopyToAsync(bufferedContentStream, ct);

            if (bufferedContentStream.Length is 0 && this.AllowEmptyResponse)
            {
                return Array.Empty<BooruPost>();
            }

            bufferedContentStream.Seek(0, SeekOrigin.Begin);
            var internalPage = await JsonSerializer.DeserializeAsync<TInternalPage>
            (
                bufferedContentStream,
                _jsonOptions,
                ct
            );

            if (internalPage is null)
            {
                throw new InvalidOperationException();
            }

            return MapInternalPage(internalPage);
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
