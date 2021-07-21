//
//  FListIndexer.cs
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
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using ImageScraper.API.FList;
using ImageScraper.Model;
using ImageScraper.Pipeline.WorkUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ImageScraper.ServiceIndexers
{
    /// <summary>
    /// Indexes https://f-list.net characters.
    /// </summary>
    public class FListIndexer : IServiceIndexer<int>
    {
        private readonly IDbContextFactory<IndexingContext> _contextFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FListIndexer> _log;
        private readonly FListAPI _fListAPI;

        /// <inheritdoc />
        public string Service => "f-list";

        /// <summary>
        /// Initializes a new instance of the <see cref="FListIndexer"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="memoryCache">The memory cache.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="fListAPI">The F-List API.</param>
        public FListIndexer
        (
            IDbContextFactory<IndexingContext> contextFactory,
            IMemoryCache memoryCache,
            IHttpClientFactory httpClientFactory,
            ILogger<FListIndexer> log,
            FListAPI fListAPI
        )
        {
            _contextFactory = contextFactory;
            _memoryCache = memoryCache;
            _httpClientFactory = httpClientFactory;
            _log = log;
            _fListAPI = fListAPI;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<int> GetSourceIdentifiersAsync
        (
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            int currentCharacterId;
            await using (var db = _contextFactory.CreateDbContext())
            {
                var state = db.ServiceStates.FirstOrDefault(s => s.Name == this.Service);
                if (state is null)
                {
                    state = new ServiceState(this.Service);

                    db.ServiceStates.Update(state);
                    await db.SaveChangesAsync(ct);
                }

                if (!int.TryParse(state.ResumePoint, out currentCharacterId))
                {
                    currentCharacterId = 0;
                    state.ResumePoint = currentCharacterId.ToString();

                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Be pessimistic - assume the whole chain has failed
                    var potentiallyFailed = Environment.ProcessorCount * 4 * 3;
                    currentCharacterId -= potentiallyFailed;
                    currentCharacterId = Math.Clamp(currentCharacterId, 0, int.MaxValue);
                }
            }

            while (!ct.IsCancellationRequested)
            {
                var getCharacter = await _fListAPI.GetCharacterDataAsync(currentCharacterId, ct);
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

                _memoryCache.Set($"flist.{character.Id}", character);
                yield return currentCharacterId;

                ++currentCharacterId;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AssociatedImage> GetImagesAsync
        (
            int sourceIdentifier,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            var key = $"flist.{sourceIdentifier}";
            if (!_memoryCache.TryGetValue<CharacterData>(key, out var character))
            {
                var getCharacter = await _fListAPI.GetCharacterDataAsync(sourceIdentifier, ct);
                if (!getCharacter.IsSuccess)
                {
                    yield break;
                }

                character = getCharacter.Entity;
            }
            else
            {
                _memoryCache.Remove(key);
            }

            foreach (var image in character.Images)
            {
                var client = _httpClientFactory.CreateClient();
                var location = $"https://static.f-list.net/images/charimage/{image.ImageId}.{image.Extension}";
                await using var stream = await client.GetStreamAsync(location, ct);

                // Copy to avoid dealing with HttpClient for longer than necessary
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, ct);

                // Rewind the stream for the upcoming consumer
                memoryStream.Seek(0, SeekOrigin.Begin);

                yield return new AssociatedImage
                (
                    "e621",
                    new Uri($"https://www.f-list.net/c/{character.Name}"),
                    new Uri(location),
                    memoryStream
                );
            }
        }
    }
}
