//
//  DanbooruPost.cs
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

using System.Text.Json.Serialization;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Represents the internal post representation of the Danbooru driver.
/// </summary>
/// <param name="ID">The ID of the post.</param>
/// <param name="FileUrl">The full URL to the image file.</param>
public record DanbooruPost
(
    [property: JsonPropertyName("id")] ulong ID,
    [property: JsonPropertyName("file_url")] string? FileUrl
);
