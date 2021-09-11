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

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Puzzle;

namespace Argus.Common.Services.Elasticsearch
{
    /// <summary>
    /// Represents the signature of an indexed image in Elasticsearch.
    /// </summary>
    public class ImageSignature
    {
        /// <summary>
        /// Gets the raw signature of the image.
        /// </summary>
        public LuminosityLevel[] Signature { get; }

        /// <summary>
        /// Gets a composite signature, used for rapid full-text search.
        /// </summary>
        public SignatureWords Words { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSignature"/> class.
        /// </summary>
        /// <param name="signature">The raw signature to wrap.</param>
        public ImageSignature(LuminosityLevel[] signature)
        {
            const int wordSize = 16;
            const int wordCount = 63;

            this.Signature = signature;

            var words = ArrayPool<int>.Shared.Rent(63);
            Span<sbyte> word = stackalloc sbyte[wordSize];

            for (var i = 0; i < wordCount; i++)
            {
                word.Fill(0);

                var slice = signature.AsSpan().Slice(i, wordSize);
                var asByte = MemoryMarshal.Cast<LuminosityLevel, sbyte>(slice);
                asByte.CopyTo(word);

                // See https://github.com/ProvenanceLabs/image-match/blob/master/image_match/signature_database_base.py#L124
                var sum = 0;
                for (var index = 0; index < word.Length; index++)
                {
                    var syllable = word[index];

                    // Squish the contrast range
                    var squishedSyllable = syllable > 0
                        ? 1
                        : syllable < 0
                            ? -1
                            : 0;

                    // Convert the syllable into a positive number
                    var a = squishedSyllable + 1;

                    // Compute the rolling coding vector value
                    var b = (byte)Math.Pow(3, index);

                    // Then push the value into the rolling dot product
                    sum += a * b;
                }

                words[i] = sum;
            }

            this.Words = SignatureWords.FromArray(words);
            ArrayPool<int>.Shared.Return(words);
        }
    }
}
