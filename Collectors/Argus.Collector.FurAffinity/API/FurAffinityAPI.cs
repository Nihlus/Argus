//
//  FurAffinityAPI.cs
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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Remora.Results;

namespace Argus.Collector.FurAffinity.API;

/// <summary>
/// Wraps the FurAffinity API.
/// </summary>
public class FurAffinityApi
{
    private readonly IHttpClientFactory _clientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FurAffinityApi"/> class.
    /// </summary>
    /// <param name="clientFactory">The HTTP client factory.</param>
    public FurAffinityApi(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Gets the download link for the submission of the given ID.
    /// </summary>
    /// <param name="submissionID">The ID of the submission.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The download link.</returns>
    public async Task<Result<Uri>> GetSubmissionDownloadLinkAsync(ulong submissionID, CancellationToken ct = default)
    {
        try
        {
            var client = _clientFactory.CreateClient(nameof(FurAffinityApi));
            using var request = new HttpRequestMessage
            (
                HttpMethod.Get,
                $"https://www.furaffinity.net/full/{submissionID}"
            );

            using var get = await client.SendAsync(request, ct);
            get.EnsureSuccessStatusCode();

            var content = await get.Content.ReadAsStringAsync(ct);

            // Heuristic validity check
            if (content.Contains("Log In</strong>"))
            {
                return new InvalidOperationError
                (
                    "The credentials are no longer valid. Collection cannot continue."
                );
            }

            var context = new BrowsingContext();
            var document = await context.OpenAsync(req => req.Content(content), ct);

            var downloadLink = document.Images.FirstOrDefault(im => im.Id is "submissionImg")?.Source;
            if (downloadLink is not null)
            {
                return new Uri(downloadLink);
            }

            // try looking for the download link
            var link = document.Links.FirstOrDefault(ln => ln.InnerHtml is "Download");
            if (link is IHtmlAnchorElement anchor)
            {
                downloadLink = anchor.Href;
            }

            return downloadLink is null
                ? new NotFoundError("No valid download link could be found.")
                : new Uri(downloadLink);
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>
    /// Gets the ID of the most recently posted submission.
    /// </summary>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The submission ID.</returns>
    public async Task<Result<ulong>> GetMostRecentSubmissionIDAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _clientFactory.CreateClient(nameof(FurAffinityApi));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.furaffinity.net/browse");

            using var get = await client.SendAsync(request, ct);
            var content = await get.Content.ReadAsStringAsync(ct);

            var context = new BrowsingContext();
            var document = await context.OpenAsync(req => req.Content(content), ct);

            var firstLink = document.Links.FirstOrDefault
            (
                ln => ln is IHtmlAnchorElement anchor && anchor.Href.Contains("/view/")
            );

            if (firstLink is null)
            {
                return new InvalidOperationError
                (
                    "Failed to find a valid submission element. The scraping logic is no longer valid."
                );
            }

            var rawID = ((IHtmlAnchorElement)firstLink).Href.Split
            (
                '/',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ).LastOrDefault();

            if (rawID is null)
            {
                return new InvalidOperationError
                (
                    "Failed to parse a valid submission ID. The scraping logic is no longer valid."
                );
            }

            if (!ulong.TryParse(rawID, out var submissionID))
            {
                return new InvalidOperationError
                (
                    "Failed to parse a valid submission ID. The scraping logic is no longer valid."
                );
            }

            return submissionID;
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
