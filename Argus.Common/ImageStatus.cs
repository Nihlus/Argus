//
//  ImageStatus.cs
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

namespace Argus.Common
{
    /// <summary>
    /// Enumerates the various states an image can be in.
    /// </summary>
    public enum ImageStatus
    {
        /// <summary>
        /// The image has been collected, and has been submitted for processing.
        /// </summary>
        Collected,

        /// <summary>
        /// The image has been rejected by the collector.
        /// </summary>
        Rejected,

        /// <summary>
        /// The image has been sent for processing by a worker.
        /// </summary>
        Processing,

        /// <summary>
        /// The image has been processed, and has been sent back to the coordinator.
        /// </summary>
        Processed,

        /// <summary>
        /// Processing of the image faulted in some way.
        /// </summary>
        Faulted
    }
}
