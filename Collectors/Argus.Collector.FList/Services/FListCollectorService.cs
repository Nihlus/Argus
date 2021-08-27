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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.FList.API;
using Argus.Common;
using Argus.Common.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        /// <param name="options">The application options.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="flistAPI">The F-List API.</param>
        /// <param name="log">The logging instance.</param>
        public FListCollectorService
        (
            IOptions<CollectorOptions> options,
            IHttpClientFactory httpClientFactory,
            FListAPI flistAPI,
            ILogger<FListCollectorService> log
        )
            : base(options)
        {
            _log = log;
            _flistAPI = flistAPI;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var getResume = await GetResumePointAsync(stoppingToken);
            if (!getResume.IsSuccess)
            {
                _log.LogWarning("Failed to get the resume point: {Reason}", getResume.Error.Message);
                return;
            }

            var resumePoint = getResume.Entity;
            if (!int.TryParse(resumePoint, out var currentCharacterId))
            {
                currentCharacterId = 0;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var getCharacter = await _flistAPI.GetCharacterDataAsync(currentCharacterId, stoppingToken);
                if (!getCharacter.IsSuccess)
                {
                    ++currentCharacterId;
                    continue;
                }

                var character = getCharacter.Entity;
                if (character.Images.Count <= 0)
                {
                    ++currentCharacterId;
                    continue;
                }

                foreach (var image in character.Images)
                {
                    var client = _httpClientFactory.CreateClient();
                    var location = $"https://static.f-list.net/images/charimage/{image.ImageId}.{image.Extension}";
                    var bytes = await client.GetByteArrayAsync(location, stoppingToken);

                    var collectedImage = new CollectedImage
                    (
                        this.ServiceName,
                        new Uri($"https://www.f-list.net/c/{character.Name}"),
                        new Uri(location),
                        bytes
                    );

                    var statusReport = new StatusReport
                    (
                        DateTimeOffset.UtcNow,
                        this.ServiceName,
                        collectedImage.Source,
                        collectedImage.Image,
                        ImageStatus.Collected,
                        string.Empty
                    );

                    var push = PushCollectedImage(collectedImage);
                    if (!push.IsSuccess)
                    {
                        _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                    }

                    var collect = PushStatusReport(statusReport);
                    if (!collect.IsSuccess)
                    {
                        _log.LogWarning("Failed to push status report: {Reason}", collect.Error.Message);
                    }
                }

                ++currentCharacterId;
            }
        }
    }
}
