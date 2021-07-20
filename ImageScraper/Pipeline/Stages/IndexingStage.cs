//
//  IndexingStage.cs
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
using ImageScraper.Pipeline.WorkUnits;
using ImageScraper.Services.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace ImageScraper.Pipeline.Stages
{
    /// <summary>
    /// Indexes <see cref="ProcessedImage"/> instances into Elasticsearch.
    /// </summary>
    public sealed class IndexingStage
    {
        private readonly ILogger<IndexingStage> _log;
        private readonly NESTService _nestService;

        /// <summary>
        /// Gets the <see cref="ActionBlock{TInput}"/> that the stage represents.
        /// </summary>
        public ActionBlock<ProcessedImage> Block { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingStage"/> class.
        /// </summary>
        /// <param name="nestService">The NEST service.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        public IndexingStage
        (
            NESTService nestService,
            ILogger<IndexingStage> log,
            CancellationToken ct = default
        )
        {
            _nestService = nestService;
            _log = log;

            this.Block = new ActionBlock<ProcessedImage>
            (
                IndexImageAsync,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = ct,
                    EnsureOrdered = false,
                    SingleProducerConstrained = true
                }
            );
        }

        private async Task IndexImageAsync(ProcessedImage image)
        {
            try
            {
                _log.LogInformation("Indexing image...");

                var indexedImage = new IndexedImage
                (
                    image.Service,
                    DateTimeOffset.UtcNow,
                    image.Link.ToString(),
                    image.Source.ToString(),
                    image.Signature.Signature,
                    image.Signature.Words
                );

                if (!await _nestService.IndexImageAsync(indexedImage))
                {
                    return;
                }

                _log.LogInformation
                (
                    "Indexed image at {Link}, retrieved from {Source}",
                    image.Link,
                    image.Source
                );
            }
            catch (Exception e)
            {
                _log.LogWarning(e, "Failed to index {Link}", image.Link);
            }
        }
    }
}
