//
//  LoadingStage.cs
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
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace ImageScraper.Pipeline.Stages
{
    /// <summary>
    /// Processes <see cref="Uri"/> instances into <see cref="LoadedImage"/> instances, downloading them into memory.
    /// </summary>
    public sealed class LoadingStage
    {
        private readonly ILogger<LoadingStage> _log;

        /// <summary>
        /// Gets the <see cref="TransformManyBlock{TInput,TOutput}"/> that the stage represents.
        /// </summary>
        public TransformBlock<AssociatedImage, LoadedImage> Block { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoadingStage"/> class.
        /// </summary>
        /// <param name="log">The logging instance.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        public LoadingStage
        (
            ILogger<LoadingStage> log,
            CancellationToken ct = default
        )
        {
            _log = log;
            this.Block = new TransformBlock<AssociatedImage, LoadedImage>
            (
                LoadImageAsync,
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = ct,
                    EnsureOrdered = false,
                    SingleProducerConstrained = true
                }
            );
        }

        private async Task<LoadedImage> LoadImageAsync(AssociatedImage associatedImage)
        {
            try
            {
                _log.LogInformation("Downloading image from {Link}...", associatedImage.Link);
                using (associatedImage)
                {
                    var image = await Image.LoadAsync(associatedImage.ImageStream);
                    return new LoadedImage(associatedImage.Source, associatedImage.Link, image);
                }
            }
            catch (Exception e)
            {
                _log.LogWarning(e, "Failed to download {Link}", associatedImage.Link);
                throw;
            }
        }
    }
}
