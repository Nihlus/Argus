//
//  FListAuthenticationRefreshPolicy.cs
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.FList.API;
using Argus.Collector.FList.Configuration;
using Microsoft.Extensions.Options;
using Polly;

namespace Argus.Collector.FList.Polly;

/// <summary>
/// Automatically appends authentication information to F-List requests.
/// </summary>
public class FListAuthenticationRefreshPolicy : AsyncPolicy<HttpResponseMessage>
{
    private readonly FListAPI _fListAPI;
    private readonly FListOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="FListAuthenticationRefreshPolicy"/> class.
    /// </summary>
    /// <param name="fListAPI">The F-List API.</param>
    /// <param name="options">The application options.</param>
    /// <param name="policyBuilder">The policy builder.</param>
    public FListAuthenticationRefreshPolicy
    (
        FListAPI fListAPI,
        IOptions<FListOptions> options,
        PolicyBuilder<HttpResponseMessage>? policyBuilder = null
    )
        : base(policyBuilder)
    {
        _fListAPI = fListAPI;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> ImplementationAsync
    (
        Func<Context, CancellationToken, Task<HttpResponseMessage>> action,
        Context context,
        CancellationToken cancellationToken,
        bool continueOnCapturedContext
    )
    {
        // stupid bloody api
        var ticketRefreshIndicators = new[]
        {
            "{\"error\":\"Ticket or account missing",
            "{\"error\":\"Invalid ticket",
            "{\"error\":\"Your login ticket has expired",
        };

        if (!string.IsNullOrWhiteSpace((string)context["ticket"]))
        {
            // We might have a good ticket
            var result = await action(context, cancellationToken);

            await result.Content.LoadIntoBufferAsync();
            var contentStream = await result.Content.ReadAsStreamAsync(cancellationToken);
            var reader = new StreamReader(contentStream);

            var content = await reader.ReadToEndAsync();
            contentStream.Seek(0, SeekOrigin.Begin);

            // Retry once if we get an account issue
            if (content.StartsWith("{\"error\":\"This account may not"))
            {
                result = await action(context, cancellationToken);
            }

            if (!this.ResultPredicates.AnyMatch(result) && !ticketRefreshIndicators.Any(content.StartsWith))
            {
                return result;
            }
        }

        var refreshResult = await _fListAPI.RefreshAPITicketAsync
        (
            _options.Username,
            _options.Password,
            cancellationToken
        );

        if (!refreshResult.IsSuccess)
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        // Do it again
        return await action(context, cancellationToken);
    }
}
