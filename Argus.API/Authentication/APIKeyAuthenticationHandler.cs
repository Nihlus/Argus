//
//  APIKeyAuthenticationHandler.cs
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
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Argus.API.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Argus.API.Authentication
{
    /// <summary>
    /// Handles API key authentication.
    /// </summary>
    public class APIKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly ArgusAPIContext _db;

        /// <summary>
        /// Initializes a new instance of the <see cref="APIKeyAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="options">The authentication scheme options.</param>
        /// <param name="logger">The logging instance.</param>
        /// <param name="encoder">The URL encoder.</param>
        /// <param name="clock">The system clock.</param>
        public APIKeyAuthenticationHandler
        (
            ArgusAPIContext db,
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base
            (
                options,
                logger,
                encoder,
                clock
            )
        {
            _db = db;
        }

        /// <inheritdoc />
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var endpoint = this.Context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                return AuthenticateResult.NoResult();
            }

            if (!this.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                return AuthenticateResult.Fail("No authorization header provided.");
            }

            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization))
            {
                return AuthenticateResult.Fail("Invalid authorization header provided.");
            }

            if (authorization.Scheme is not "Bearer")
            {
                return AuthenticateResult.Fail("Invalid authorization scheme.");
            }

            if (authorization.Parameter is null)
            {
                return AuthenticateResult.Fail("Missing authorization key.");
            }

            var parameter = authorization.Parameter;
            if (!Guid.TryParse(parameter, out var apiKey))
            {
                return AuthenticateResult.Fail("Invalid authorization key.");
            }

            var knownKey = await _db.APIKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Key == apiKey);
            if (knownKey is null)
            {
                return AuthenticateResult.Fail("Unknown authorization key.");
            }

            var now = DateTime.UtcNow;
            if (knownKey.ExpiresAt <= now)
            {
                return AuthenticateResult.Fail("Authorization key expired.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, knownKey.ID.ToString())
            };

            var identity = new ClaimsIdentity(claims, this.Scheme.Name);
            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(principal, this.Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
