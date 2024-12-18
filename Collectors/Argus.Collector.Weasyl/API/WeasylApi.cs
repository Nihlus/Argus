//
//  WeasylApi.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) Jarl Gullberg
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
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Weasyl.API.Model;
using Argus.Collector.Weasyl.Configuration;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Weasyl.API;

/// <summary>
/// Interfaces with the Weasyl API.
/// </summary>
public class WeasylApi
{
    private readonly WeasylOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeasylApi"/> class.
    /// </summary>
    /// <param name="options">The Weasyl options.</param>
    /// <param name="jsonOptions">The JSON options.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public WeasylApi
    (
        IOptions<WeasylOptions> options,
        IOptions<JsonSerializerOptions> jsonOptions,
        IHttpClientFactory httpClientFactory
    )
    {
        _options = options.Value;
        _jsonOptions = jsonOptions.Value;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Gets a submission by its ID.
    /// </summary>
    /// <param name="submissionID">The ID of the submission.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The submission.</returns>
    public async Task<Result<WeasylSubmission>> GetSubmissionAsync(int submissionID, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(WeasylApi));
            using var request = new HttpRequestMessage
            (
                HttpMethod.Get,
                $"https://www.weasyl.com/api/submissions/{submissionID}/view?anyway=true"
            );

            request.Headers.Add("X-Weasyl-API-Key", _options.ApiKey);

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return await JsonSerializer.DeserializeAsync<WeasylSubmission>
                (
                    await response.Content.ReadAsStreamAsync(ct),
                    _jsonOptions,
                    ct
                );
            }

            var document = await JsonDocument.ParseAsync
            (
                await response.Content.ReadAsStreamAsync(ct),
                cancellationToken: ct
            );

            if (!document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return new NotFoundError();
                }

                return new InvalidOperationError($"HTTP operation failed: {response.StatusCode}");
            }

            var errorJson = errorProperty.ToString();
            if (string.IsNullOrWhiteSpace(errorJson))
            {
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return new NotFoundError();
                }

                return new InvalidOperationError($"HTTP operation failed: {response.StatusCode}");
            }

            var error = JsonSerializer.Deserialize<WeasylError>(errorJson, _jsonOptions)
                        ?? throw new InvalidOperationException();

            return error with { StatusCode = response.StatusCode };
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>
    /// Gets the submissions on the front page.
    /// </summary>
    /// <param name="count">The number of submissions to get from the front page.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The submissions.</returns>
    public async Task<Result<IReadOnlyList<WeasylSubmission>>> GetFrontpageAsync
    (
        int count = 1,
        CancellationToken ct = default
    )
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(WeasylApi));
            using var request = new HttpRequestMessage
            (
                HttpMethod.Get,
                $"https://www.weasyl.com/api/submissions/frontpage?count={count}"
            );

            request.Headers.Add("X-Weasyl-API-Key", _options.ApiKey);

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return await JsonSerializer.DeserializeAsync<List<WeasylSubmission>>
                (
                    await response.Content.ReadAsStreamAsync(ct),
                    _jsonOptions,
                    ct
                );
            }

            var document = await JsonDocument.ParseAsync
            (
                await response.Content.ReadAsStreamAsync(ct),
                cancellationToken: ct
            );

            if (!document.RootElement.TryGetProperty("error", out var errorProperty))
            {
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return new NotFoundError();
                }

                return new InvalidOperationError($"HTTP operation failed: {response.StatusCode}");
            }

            var errorJson = errorProperty.ToString();
            if (string.IsNullOrWhiteSpace(errorJson))
            {
                if (response.StatusCode is HttpStatusCode.NotFound)
                {
                    return new NotFoundError();
                }

                return new InvalidOperationError($"HTTP operation failed: {response.StatusCode}");
            }

            var error = JsonSerializer.Deserialize<WeasylError>(errorJson, _jsonOptions)
                        ?? throw new InvalidOperationException();

            return error with { StatusCode = response.StatusCode };
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
