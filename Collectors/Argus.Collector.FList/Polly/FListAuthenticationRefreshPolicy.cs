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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.FList.API;
using Argus.Collector.FList.Configuration;
using Microsoft.Extensions.Options;
using Polly;

namespace Argus.Collector.FList.Polly
{
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
            var result = await _fListAPI.RefreshAPITicketAsync(_options.Username, _options.Password, cancellationToken);
            if (!result.IsSuccess)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return await action(context, cancellationToken);
        }
    }
}