//
//  PhilomenaImage.cs
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
using System.Text.Json.Serialization;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Represents a Philomena image.
/// </summary>
/// <param name="ID">The ID of the image.</param>
/// <param name="Representations">
/// A mapping of representation names to their respective URLs. Contains the keys "full", "large", "medium", "small",
/// "tall", "thumb", "thumb_small", and "thumb_tiny".
/// </param>
public record PhilomenaImage
(
    [property: JsonPropertyName("id")] ulong ID,
    [property: JsonPropertyName("representations")] IReadOnlyDictionary<string, Uri> Representations
);
