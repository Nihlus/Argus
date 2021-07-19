//
//  IServiceScraper.cs
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
using System.Runtime.CompilerServices;
using System.Threading;
using ImageScraper.Pipeline.WorkUnits;

namespace ImageScraper.ServiceScrapers
{
    /// <summary>
    /// Represents the public API of a service indexer.
    /// </summary>
    internal interface IServiceScraper
    {
        /// <summary>
        /// Gets an asynchronous sequence of target URLs to scrape. This sequence may be unbounded.
        /// </summary>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A sequence of target URLs to scrape.</returns>
        IAsyncEnumerable<Uri> GetTargetUrlsAsync([EnumeratorCancellation] CancellationToken ct = default);

        /// <summary>
        /// Gets images to index from the given URL.
        /// </summary>
        /// <param name="url">The URL to scrape.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The images to index.</returns>
        IAsyncEnumerable<AssociatedImage> GetImagesAsync(Uri url, [EnumeratorCancellation] CancellationToken ct = default);
    }
}
