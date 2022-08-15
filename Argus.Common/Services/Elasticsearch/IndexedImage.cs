//
//  IndexedImage.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) Jarl Gullberg
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
using Puzzle;

namespace Argus.Common.Services.Elasticsearch;

/// <summary>
/// Represents an indexed image.
/// </summary>
public class IndexedImage
{
    /// <summary>
    /// Gets the name of the service that the image was indexed from.
    /// </summary>
    public string Service { get; }

    /// <summary>
    /// Gets the time at which the image was indexed.
    /// </summary>
    public DateTimeOffset IndexedAt { get; init; }

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
    public LuminosityLevel[] Signature { get; init; }

    /// <summary>
    /// Gets the words that compose the signature. This field is used for search performance.
    /// </summary>
    public SignatureWords Words { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexedImage"/> class.
    /// </summary>
    /// <param name="service">The name of the service the image was indexed from.</param>
    /// <param name="indexedAt">The time at which the image was indexed.</param>
    /// <param name="source">The source page.</param>
    /// <param name="link">The direct link.</param>
    /// <param name="signature">The image signature.</param>
    /// <param name="words">The composed signature.</param>
    public IndexedImage
    (
        string service,
        DateTimeOffset indexedAt,
        string source,
        string link,
        LuminosityLevel[] signature,
        SignatureWords words
    )
    {
        this.Service = service;
        this.IndexedAt = indexedAt;
        this.Source = source;
        this.Link = link;
        this.Signature = signature;
        this.Words = words;
    }
}
