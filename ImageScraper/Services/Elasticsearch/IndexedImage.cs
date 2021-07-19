//
//  IndexedImage.cs
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

using System.Collections.Generic;
using Puzzle;

namespace ImageScraper.Services.Elasticsearch
{
    /// <summary>
    /// Represents an indexed image.
    /// </summary>
    public class IndexedImage
    {
        /// <summary>
        /// Gets the source page where the image was scraped.
        /// </summary>
        public string Source { get; init; }

        /// <summary>
        /// Gets the direct link to the image.
        /// </summary>
        public string Link { get; init; }

        /// <summary>
        /// Gets the image's calculated signature.
        /// </summary>
        public IReadOnlyCollection<LuminosityLevel> Signature { get; init; }

        /// <summary>
        /// Gets the words that compose the signature. This field is used for search performance.
        /// </summary>
        public IReadOnlyCollection<int> Words { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexedImage"/> class.
        /// </summary>
        /// <param name="source">The source page.</param>
        /// <param name="link">The direct link.</param>
        /// <param name="signature">The image signature.</param>
        /// <param name="words">The composed signature.</param>
        public IndexedImage
        (
            string source,
            string link,
            IReadOnlyCollection<LuminosityLevel> signature,
            IReadOnlyCollection<int> words
        )
        {
            this.Source = source;
            this.Link = link;
            this.Signature = signature;
            this.Words = words;
        }
    }
}
