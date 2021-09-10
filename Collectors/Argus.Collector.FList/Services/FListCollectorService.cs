//
//  FListCollectorService.cs
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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.FList.API;
using Argus.Collector.FList.API.Model;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.FList.Services
{
    /// <summary>
    /// Collects images from F-List.
    /// </summary>
    public class FListCollectorService : CollectorService
    {
        private readonly FListAPI _flistAPI;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FListCollectorService> _log;

        /// <inheritdoc />
        protected override string ServiceName => "f-list";

        /// <summary>
        /// Initializes a new instance of the <see cref="FListCollectorService"/> class.
        /// </summary>
        /// <param name="flistAPI">The F-List API.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="bus">The message bus.</param>
        /// <param name="options">The application options.</param>
        /// <param name="log">The logging instance.</param>
        public FListCollectorService
        (
            FListAPI flistAPI,
            IHttpClientFactory httpClientFactory,
            IBus bus,
            IOptions<CollectorOptions> options,
            ILogger<FListCollectorService> log
        )
            : base(bus, options, log)
        {
            _log = log;
            _flistAPI = flistAPI;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        protected override async Task<Result> CollectAsync(CancellationToken ct = default)
        {
            var getResume = await GetResumePointAsync(ct);
            if (!getResume.IsSuccess)
            {
                _log.LogWarning("Failed to get the resume point: {Reason}", getResume.Error.Message);
                return Result.FromError(getResume);
            }

            var resumePoint = getResume.Entity;
            if (!int.TryParse(resumePoint, out var currentCharacterId))
            {
                currentCharacterId = 0;
            }

            int? latestCharacterId = null;

            while (!ct.IsCancellationRequested)
            {
                if (currentCharacterId >= latestCharacterId || latestCharacterId is null)
                {
                    var getLatestName = _flistAPI.GetMostRecentlyCreatedCharacterAsync();
                    if (!getLatestName.IsSuccess)
                    {
                        return Result.FromError(getLatestName);
                    }

                    var getLatestCharacter = await _flistAPI.GetCharacterDataAsync(getLatestName.Entity, ct);
                    if (!getLatestCharacter.IsSuccess)
                    {
                        return Result.FromError(getLatestCharacter);
                    }

                    latestCharacterId = getLatestCharacter.Entity.Id;
                    if (currentCharacterId >= latestCharacterId)
                    {
                        _log.LogInformation("Waiting for new characters to come in...");
                        await Task.Delay(TimeSpan.FromHours(1), ct);
                        continue;
                    }
                }

                var setResume = await SetResumePointAsync(currentCharacterId.ToString(), ct);
                if (!setResume.IsSuccess)
                {
                    return setResume;
                }

                var getCharacter = await _flistAPI.GetCharacterDataAsync(currentCharacterId, ct);
                if (!getCharacter.IsSuccess)
                {
                    if (!getCharacter.Error.Message.Contains("Character not found"))
                    {
                        _log.LogWarning
                        (
                            "Failed to get data for character {ID}: {Reason}",
                            currentCharacterId,
                            getCharacter.Error.Message
                        );
                    }

                    ++currentCharacterId;
                    continue;
                }

                var character = getCharacter.Entity;
                if (character.Images.Count <= 0)
                {
                    ++currentCharacterId;
                    continue;
                }

                var client = _httpClientFactory.CreateClient("BulkDownload");

                var collections = character.Images.Select(i => CollectImageAsync(character.Name, client, i, ct));
                var collectedImages = await Task.WhenAll(collections);

                foreach (var imageCollection in collectedImages)
                {
                    if (!imageCollection.IsSuccess)
                    {
                        _log.LogWarning("Failed to collect image: {Reason}", imageCollection.Error.Message);
                        continue;
                    }

                    var collectedImage = imageCollection.Entity;

                    var statusReport = new StatusReport
                    (
                        DateTime.UtcNow,
                        this.ServiceName,
                        collectedImage.Source,
                        collectedImage.Link,
                        ImageStatus.Collected,
                        string.Empty
                    );

                    var report = await PushStatusReportAsync(statusReport, ct);
                    if (!report.IsSuccess)
                    {
                        _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
                        return report;
                    }

                    var push = await PushCollectedImageAsync(collectedImage, ct);
                    if (push.IsSuccess)
                    {
                        continue;
                    }

                    _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                    return push;
                }

                ++currentCharacterId;
            }

            return Result.FromSuccess();
        }

        private async Task<Result<CollectedImage>> CollectImageAsync
        (
            string characterName,
            HttpClient client,
            CharacterImage image,
            CancellationToken ct = default
        )
        {
            try
            {
                var location = $"https://static.f-list.net/images/charimage/{image.ImageId}.{image.Extension}";
                var bytes = await client.GetByteArrayAsync(location, ct);

                return new CollectedImage
                (
                    this.ServiceName,
                    new Uri($"https://www.f-list.net/c/{characterName}"),
                    new Uri(location),
                    bytes
                );
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
