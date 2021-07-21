//
//  IndexingBackgroundService.cs
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
using System.Threading;
using System.Threading.Tasks;
using ImageScraper.Model;
using ImageScraper.ServiceIndexers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImageScraper.BackgroundServices
{
    /// <summary>
    /// Represents a background service that continuously indexes a single service.
    /// </summary>
    /// <typeparam name="TServiceIndexer">The interface type to use for scraping the service.</typeparam>
    /// <typeparam name="TIdentifier">The identifier type used by the service.</typeparam>
    public class IndexingBackgroundService<TServiceIndexer, TIdentifier> : BackgroundService
        where TServiceIndexer : IServiceIndexer<TIdentifier>
    {
        private readonly TServiceIndexer _serviceIndexer;
        private readonly ImageProcessingService _processing;
        private readonly ILogger<IndexingBackgroundService<TServiceIndexer, TIdentifier>> _log;
        private readonly IDbContextFactory<IndexingContext> _contextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingBackgroundService{TServiceScraper, TIdentifier}"/> class.
        /// </summary>
        /// <param name="serviceIndexer">The service scraper.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="processing">The image processing chain.</param>
        public IndexingBackgroundService
        (
            TServiceIndexer serviceIndexer,
            ILogger<IndexingBackgroundService<TServiceIndexer, TIdentifier>> log,
            IDbContextFactory<IndexingContext> contextFactory,
            ImageProcessingService processing
        )
        {
            _serviceIndexer = serviceIndexer;
            _log = log;
            _contextFactory = contextFactory;
            _processing = processing;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await foreach (var identifier in _serviceIndexer.GetSourceIdentifiersAsync(ct))
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                _log.LogDebug("Indexing {Identifier} from {Service}...", identifier, _serviceIndexer.Service);
                await foreach (var image in _serviceIndexer.GetImagesAsync(identifier, ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    while (!await _processing.SendAsync(image, ct))
                    {
                        _log.LogWarning
                        (
                            "Failed to send {Link} (from {Source}) into the processing chain",
                            image.Link,
                            image.Source
                        );

                        _log.LogWarning("Waiting a small amount of time to let the chain catch up...");
                        await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    }
                }

                await using var db = _contextFactory.CreateDbContext();
                var state = db.ServiceStates.FirstOrDefault(s => s.Name == _serviceIndexer.Service);
                if (state is null)
                {
                    state = new ServiceState(_serviceIndexer.Service);
                    db.ServiceStates.Update(state);
                }

                state.ResumePoint = identifier?.ToString();
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
