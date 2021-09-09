//
//  CollectedImage.cs
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

namespace Argus.Common.Messages.BulkData
{
    /// <summary>
    /// Represents an image that has been retrieved by a service collector.
    /// </summary>
    /// <param name="ServiceName">The name of the service the collector retrieved the image from.</param>
    /// <param name="Source">The source URL where the image was retrieved.</param>
    /// <param name="Image">A direct link to the image.</param>
    /// <param name="Data">The image data.</param>
    public record CollectedImage
    (
        string ServiceName,
        Uri Source,
        Uri Image,
        byte[] Data
    );
}
