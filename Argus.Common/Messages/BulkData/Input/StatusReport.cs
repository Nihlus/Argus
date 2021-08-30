//
//  StatusReport.cs
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
using MessagePack;

namespace Argus.Common.Messages.BulkData
{
    /// <summary>
    /// Represents a report regarding the processing status of an image.
    /// </summary>
    /// <param name="Timestamp">The time at which the report was created.</param>
    /// <param name="ServiceName">The name of the service the collector retrieved the image from.</param>
    /// <param name="Source">The source URL where the image was retrieved.</param>
    /// <param name="Image">A direct link to the image.</param>
    /// <param name="Status">The status of the image.</param>
    /// <param name="Message">The status message.</param>
    [MessagePackObject]
    public record StatusReport
    (
        [property: Key(0)] DateTimeOffset Timestamp,
        [property: Key(1)] string ServiceName,
        [property: Key(2)] Uri Source,
        [property: Key(3)] Uri Image,
        [property: Key(4)] ImageStatus Status,
        [property: Key(5)] string Message
    ) : ICoordinatorInputMessage;
}
