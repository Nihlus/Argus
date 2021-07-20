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
using System.Threading.Tasks.Dataflow;
using ImageScraper.Model;
using ImageScraper.Pipeline.Stages;
using ImageScraper.ServiceIndexers;
using ImageScraper.Services.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Puzzle;

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
        private readonly NESTService _nestService;
        private readonly SignatureGenerator _signatureGenerator;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<IndexingBackgroundService<TServiceIndexer, TIdentifier>> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingBackgroundService{TServiceScraper, TIdentifier}"/> class.
        /// </summary>
        /// <param name="serviceIndexer">The service scraper.</param>
        /// <param name="nestService">The NEST service.</param>
        /// <param name="signatureGenerator">The image signature generator.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="log">The logging instance.</param>
        public IndexingBackgroundService
        (
            TServiceIndexer serviceIndexer,
            NESTService nestService,
            SignatureGenerator signatureGenerator,
            ILoggerFactory loggerFactory,
            ILogger<IndexingBackgroundService<TServiceIndexer, TIdentifier>> log
        )
        {
            _serviceIndexer = serviceIndexer;
            _nestService = nestService;
            _signatureGenerator = signatureGenerator;
            _loggerFactory = loggerFactory;
            _log = log;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var loadingStage = new LoadingStage
            (
                _loggerFactory.CreateLogger<LoadingStage>(),
                stoppingToken
            );

            var processingStage = new ProcessingStage
            (
                _signatureGenerator,
                _loggerFactory.CreateLogger<ProcessingStage>(),
                stoppingToken
            );

            var indexingStage = new IndexingStage
            (
                _nestService,
                _loggerFactory.CreateLogger<IndexingStage>(),
                stoppingToken
            );

            var linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            loadingStage.Block.LinkTo(processingStage.Block, linkOptions);
            processingStage.Block.LinkTo(indexingStage.Block, linkOptions);

            var producer = ProduceImagesAsync(loadingStage, stoppingToken);

            var task = await Task.WhenAny
            (
                producer,
                loadingStage.Block.Completion,
                processingStage.Block.Completion,
                indexingStage.Block.Completion
            );

            if (!task.IsCanceled)
            {
                var taskName = task == producer
                    ? nameof(producer)
                    : task == loadingStage.Block.Completion
                        ? nameof(loadingStage)
                        : task == processingStage.Block.Completion
                            ? nameof(processingStage)
                            : task == indexingStage.Block.Completion
                                ? nameof(indexingStage)
                                : "unknown";

                _log.LogWarning("Unexpected termination by {Task}", taskName);
            }

            loadingStage.Block.Complete();
            processingStage.Block.Complete();
            indexingStage.Block.Complete();

            await Task.WhenAll
            (
                producer,
                loadingStage.Block.Completion,
                processingStage.Block.Completion,
                indexingStage.Block.Completion
            );
        }

        private async Task ProduceImagesAsync(LoadingStage loadingStage, CancellationToken ct = default)
        {
            try
            {
                await foreach (var identifier in _serviceIndexer.GetSourceIdentifiersAsync(ct))
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    _log.LogDebug("Indexing {Identifier}...", identifier);
                    await foreach (var image in _serviceIndexer.GetImagesAsync(identifier, ct))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        while (!await loadingStage.Block.SendAsync(image, ct))
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

                    await using var db = new IndexingContext();
                    var state = db.ServiceStates.FirstOrDefault(s => s.Name == _serviceIndexer.Service);
                    if (state is null)
                    {
                        state = db.CreateProxy<ServiceState>();
                        state.Name = _serviceIndexer.Service;

                        db.ServiceStates.Update(state);
                    }

                    state.ResumePoint = identifier?.ToString();
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception e)
            {
                _log.LogError(e, "Image producer faulted");
                throw;
            }
        }
    }
}
