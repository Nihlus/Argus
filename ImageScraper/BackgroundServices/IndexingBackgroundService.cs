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
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ImageScraper.Pipeline.Stages;
using ImageScraper.Services.Elasticsearch;
using ImageScraper.ServiceScrapers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Puzzle;

namespace ImageScraper.BackgroundServices
{
    /// <summary>
    /// Represents a background service that continuously indexes a single service.
    /// </summary>
    /// <typeparam name="TServiceScraper">The interface type to use for scraping the service.</typeparam>
    internal class IndexingBackgroundService<TServiceScraper> : BackgroundService
        where TServiceScraper : IServiceScraper
    {
        private readonly TServiceScraper _serviceIndexer;
        private readonly NESTService _nestService;
        private readonly SignatureGenerator _signatureGenerator;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<IndexingBackgroundService<TServiceScraper>> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingBackgroundService{TServiceIndexer}"/> class.
        /// </summary>
        /// <param name="serviceIndexer">The service scraper.</param>
        /// <param name="nestService">The NEST service.</param>
        /// <param name="signatureGenerator">The image signature generator.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="log">The logging instance.</param>
        public IndexingBackgroundService
        (
            TServiceScraper serviceIndexer,
            NESTService nestService,
            SignatureGenerator signatureGenerator,
            ILoggerFactory loggerFactory,
            ILogger<IndexingBackgroundService<TServiceScraper>> log
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
            var loadingStage = new LoadingStage<TServiceScraper>
            (
                _serviceIndexer,
                _loggerFactory.CreateLogger<LoadingStage<TServiceScraper>>(),
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

            loadingStage.Block.LinkTo(processingStage.Block);
            processingStage.Block.LinkTo(indexingStage.Block);

            await foreach (var uri in _serviceIndexer.GetTargetUrlsAsync(stoppingToken))
            {
                if (!await loadingStage.Block.SendAsync(uri, stoppingToken))
                {
                    continue;
                }

                _log.LogInformation("Enqueued {CurrentUri} for indexing...", uri);
            }
        }
    }
}
