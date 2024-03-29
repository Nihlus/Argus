//
//  PortableFingerprint.cs
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

using Puzzle;

namespace Argus.Common.Portable;

/// <summary>
/// Represents a portable image fingerprint.
/// </summary>
/// <param name="Filename">The filename of the source file.</param>
/// <param name="Hash">The SHA256 hash of the image.</param>
/// <param name="Fingerprint">The perceptual hash of the image.</param>
public record PortableFingerprint
(
    string Filename,
    string Hash,
    LuminosityLevel[] Fingerprint
);
