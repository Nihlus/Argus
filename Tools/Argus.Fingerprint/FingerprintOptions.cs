//
//  FingerprintOptions.cs
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

using System.Collections.Generic;
using CommandLine;

namespace Argus.Fingerprint;

/// <summary>
/// Represents the program options.
/// </summary>
/// <param name="Files">The input files.</param>
/// <param name="OutputDirectory">The output directory.</param>
/// <param name="ShouldPack">Whether the results should be packed into a single file.</param>
/// <param name="IncludeSourceImages">Whether the source images should be included in the output.</param>
public record FingerprintOptions
(
    [property: Option('f', "files", HelpText = "The input files.", Required = true)] IReadOnlyList<string> Files,
    [property: Option('o', "output", HelpText = "The output directory.")] string OutputDirectory,
    [property: Option('p', "pack", HelpText = "Whether the results should be packed into a single file.")] bool ShouldPack = true,
    [property: Option('s', "include-source", HelpText = "Whether the source images should be included in the output.")] bool IncludeSourceImages = false
);
