//
//  ImageSignature.cs
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
using System.Linq;
using MoreLinq;
using Puzzle;

namespace ImageScraper.Services.Elasticsearch
{
    /// <summary>
    /// Represents the signature of an indexed image in Elasticsearch.
    /// </summary>
    public class ImageSignature
    {
        /// <summary>
        /// Gets the raw signature of the image.
        /// </summary>
        public IReadOnlyCollection<LuminosityLevel> Signature { get; }

        /// <summary>
        /// Gets a composite signature, used for rapid full-text search.
        /// </summary>
        public IReadOnlyCollection<int> Words { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSignature"/> class.
        /// </summary>
        /// <param name="signature">The raw signature to wrap.</param>
        public ImageSignature(IReadOnlyCollection<LuminosityLevel> signature)
        {
            this.Signature = signature;

            this.Words = this.Signature
                .Batch(3)
                .Select
                (
                    wa =>
                    {
                        var word = 0;

                        var i = 0;
                        foreach (var syllable in wa)
                        {
                            word |= (sbyte)syllable << (i * 8);
                            ++i;
                        }

                        return word;
                    }
                )
                .ToList();
        }
    }
}
