//
//  FListAPI.cs
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
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.FList.API.Model;
using Argus.Collector.FList.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Remora.Results;

namespace Argus.Collector.FList.API
{
    /// <summary>
    /// Wraps the F-List API.
    /// </summary>
    public class FListAPI
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly FListOptions _options;

        private string _account;
        private string _ticket;

        /// <summary>
        /// Initializes a new instance of the <see cref="FListAPI"/> class.
        /// </summary>
        /// <param name="clientFactory">The HTTP client factory.</param>
        /// <param name="jsonOptions">The JSON serializer options.</param>
        /// <param name="options">The F-List options.</param>
        public FListAPI
        (
            IHttpClientFactory clientFactory,
            IOptions<JsonSerializerOptions> jsonOptions,
            IOptions<FListOptions> options
        )
        {
            _clientFactory = clientFactory;
            _jsonOptions = jsonOptions.Value;
            _options = options.Value;

            _account = string.Empty;
            _ticket = string.Empty;
        }

        /// <summary>
        /// Gets an API ticket from F-List. The ticket will be valid for 30 minutes.
        /// </summary>
        /// <param name="account">The account to get a ticket for.</param>
        /// <param name="password">The account's password.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The API ticket.</returns>
        public async Task<Result> RefreshAPITicketAsync
        (
            string account,
            string password,
            CancellationToken ct = default
        )
        {
            if (string.IsNullOrWhiteSpace(account))
            {
                return new ArgumentOutOfRangeError(nameof(account), "The account name must be defined.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new ArgumentOutOfRangeError(nameof(password), "The account password must be defined.");
            }

            try
            {
                var client = _clientFactory.CreateClient();

                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.f-list.net/json/getApiTicket.php");
                var executionContext = new Context
                {
                    ["account"] = _account,
                    ["ticket"] = _ticket
                };

                request.SetPolicyExecutionContext(executionContext);

                var parameters = new Dictionary<string, string>
                {
                    { nameof(account), account },
                    { nameof(password), password },
                    { "no_characters", true.ToString() },
                    { "no_friends", true.ToString() },
                    { "no_bookmarks", true.ToString() }
                };

                var content = new FormUrlEncodedContent(parameters.AsEnumerable()!);
                request.Content = content;

                var getTicket = await DeserializePayload<APITicket>(await client.SendAsync(request, ct), ct);
                if (!getTicket.IsSuccess)
                {
                    return Result.FromError(getTicket);
                }

                var ticket = getTicket.Entity;
                if (ticket is null || ticket.Ticket == string.Empty)
                {
                    throw new InvalidOperationException();
                }

                _account = account;
                _ticket = ticket.Ticket;
            }
            catch (Exception e)
            {
                return e;
            }

            return Result.FromSuccess();
        }

        /// <summary>
        /// Gets information about a character from F-List.
        /// </summary>
        /// <param name="id">The ID of the character.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The character data.</returns>
        public async Task<Result<CharacterData>> GetCharacterDataAsync(int id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_account) || string.IsNullOrWhiteSpace(_ticket))
            {
                var refresh = await RefreshAPITicketAsync(_options.Username, _options.Password, ct);
                if (!refresh.IsSuccess)
                {
                    return Result<CharacterData>.FromError(refresh);
                }
            }

            try
            {
                var client = _clientFactory.CreateClient(nameof(FListAPI));

                var request = new HttpRequestMessage(HttpMethod.Post, "json/api/character-data.php");
                var executionContext = new Context
                {
                    ["account"] = _account,
                    ["ticket"] = _ticket
                };

                request.SetPolicyExecutionContext(executionContext);

                var parameters = new Dictionary<string, string>
                {
                    { "account", _account },
                    { "ticket", _ticket },
                    { "id", id.ToString() }
                };

                var content = new FormUrlEncodedContent(parameters.AsEnumerable()!);
                request.Content = content;

                return await DeserializePayload<CharacterData>(await client.SendAsync(request, ct), ct);
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private async Task<Result<T>> DeserializePayload<T>
        (
            HttpResponseMessage responseMessage,
            CancellationToken ct = default
        )
        {
            var content = await responseMessage.Content.ReadAsStreamAsync(ct);
            var json = await JsonDocument.ParseAsync(content, cancellationToken: ct);

            if (json.RootElement.TryGetProperty("error", out var errorProperty) && !string.IsNullOrWhiteSpace(errorProperty.GetString()) )
            {
                var error = JsonSerializer.Deserialize<FListError>
                (
                    json.RootElement.ToString() ?? throw new InvalidOperationException(),
                    _jsonOptions
                );

                if (error is null)
                {
                    return new InvalidOperationError();
                }

                return error;
            }

            var entity = JsonSerializer.Deserialize<T>
            (
                json.RootElement.ToString() ?? throw new InvalidOperationException(),
                _jsonOptions
            );

            if (entity is null)
            {
                return new InvalidOperationError();
            }

            return entity;
        }
    }
}
