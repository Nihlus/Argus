//
//  AssociationStage.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ImageScraper.Pipeline.WorkUnits;
using ImageScraper.ServiceScrapers;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace ImageScraper.Pipeline.Stages
{
    /// <summary>
    /// Processes <see cref="Uri"/> instances into <see cref="LoadedImage"/> instances, downloading them into memory.
    /// </summary>
    /// <typeparam name="TServiceScraper">The service scraper type.</typeparam>
    internal sealed class AssociationStage<TServiceScraper> where TServiceScraper : IServiceScraper
    {
        private readonly ILogger<AssociationStage<TServiceScraper>> _log;
        private readonly TServiceScraper _serviceScraper;

        /// <summary>
        /// Gets the <see cref="TransformManyBlock{TInput,TOutput}"/> that the stage represents.
        /// </summary>
        public TransformManyBlock<Uri, LoadedImage> Block { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssociationStage{TServiceScraper}"/> class.
        /// </summary>
        /// <param name="serviceScraper">The scraping service.</param>
        /// <param name="log">The logging instance.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        public AssociationStage
        (
            TServiceScraper serviceScraper,
            ILogger<AssociationStage<TServiceScraper>> log,
            CancellationToken ct = default
        )
        {
            _serviceScraper = serviceScraper;
            _log = log;
            this.Block = new TransformManyBlock<Uri, LoadedImage>
            (
                ScrapeImagesAsync,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = ct,
                    BoundedCapacity = Environment.ProcessorCount,
                    EnsureOrdered = false,
                    SingleProducerConstrained = true
                }
            );
        }

        private async Task<IEnumerable<LoadedImage>> ScrapeImagesAsync(Uri uri)
        {
            var loadedImages = new List<LoadedImage>();

            try
            {
                await foreach (var scrapedImage in _serviceScraper.GetImagesAsync(uri))
                {
                    _log.LogInformation("Downloading image from {Link}...", scrapedImage.Link);
                    using (scrapedImage)
                    {
                        var image = await Image.LoadAsync(scrapedImage.ImageStream);
                        loadedImages.Add(new LoadedImage(scrapedImage.Source, scrapedImage.Link, image));
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogWarning(e, "Failed to index {Link}", uri);
            }

            return loadedImages;
        }
    }
}
