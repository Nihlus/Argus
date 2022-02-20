//
//  CharacterImage.cs
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

namespace Argus.Collector.FList.API.Model;

/// <summary>
/// Represents an image associated with a character.
/// </summary>
public class CharacterImage
{
    /// <summary>
    /// Gets the ID of the image.
    /// </summary>
    public string ImageId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file extension of the image.
    /// </summary>
    public string Extension { get; init; } = string.Empty;
}
