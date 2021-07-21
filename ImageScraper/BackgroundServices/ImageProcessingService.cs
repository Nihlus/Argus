//
//  ImageProcessingService.cs
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

using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ImageScraper.Pipeline.Stages;
using ImageScraper.Pipeline.WorkUnits;
using ImageScraper.Services.Elasticsearch;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Puzzle;

namespace ImageScraper.BackgroundServices
{
    /// <summary>
    /// Wraps the image processing pipeline.
    /// </summary>
    public class ImageProcessingService : BackgroundService
    {
        private readonly NESTService _nestService;
        private readonly SignatureGenerator _signatureGenerator;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ImageProcessingService> _log;

        private LoadingStage? _loadingStage;
        private ProcessingStage? _processingStage;
        private IndexingStage? _indexingStage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingService"/> class.
        /// </summary>
        /// <param name="nestService">The NEST service.</param>
        /// <param name="signatureGenerator">The image signature generator.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="log">The logging instance.</param>
        public ImageProcessingService
        (
            NESTService nestService,
            SignatureGenerator signatureGenerator,
            ILoggerFactory loggerFactory,
            ILogger<ImageProcessingService> log
        )
        {
            _nestService = nestService;
            _signatureGenerator = signatureGenerator;
            _loggerFactory = loggerFactory;
            _log = log;
        }

        /// <summary>
        /// Sends an image into the processing chain.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>true if the image was accepted; otherwise, false.</returns>
        public Task<bool> SendAsync(AssociatedImage image, CancellationToken ct = default)
        {
            return _loadingStage is null
                ? Task.FromResult(false)
                : _loadingStage.Block.SendAsync(image, ct);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _loadingStage = new LoadingStage
            (
                _loggerFactory.CreateLogger<LoadingStage>(),
                stoppingToken
            );

            _processingStage = new ProcessingStage
            (
                _signatureGenerator,
                _loggerFactory.CreateLogger<ProcessingStage>(),
                stoppingToken
            );

            _indexingStage = new IndexingStage
            (
                _nestService,
                _loggerFactory.CreateLogger<IndexingStage>(),
                stoppingToken
            );

            var linkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            _loadingStage.Block.LinkTo(_processingStage.Block, linkOptions);
            _processingStage.Block.LinkTo(_indexingStage.Block, linkOptions);

            var task = await Task.WhenAny
            (
                _loadingStage.Block.Completion,
                _processingStage.Block.Completion,
                _indexingStage.Block.Completion
            );

            if (!task.IsCanceled)
            {
                var taskName = task == _loadingStage.Block.Completion
                    ? nameof(_loadingStage)
                    : task == _processingStage.Block.Completion
                        ? nameof(_processingStage)
                        : task == _indexingStage.Block.Completion
                            ? nameof(_indexingStage)
                            : "unknown";

                _log.LogWarning("Unexpected termination by {Task}", taskName);
            }

            _loadingStage.Block.Complete();
            _processingStage.Block.Complete();
            _indexingStage.Block.Complete();

            await Task.WhenAll
            (
                _loadingStage.Block.Completion,
                _processingStage.Block.Completion,
                _indexingStage.Block.Completion
            );
        }
    }
}
