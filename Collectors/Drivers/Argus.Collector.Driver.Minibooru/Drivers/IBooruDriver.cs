//
//  IBooruDriver.cs
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
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Driver.Minibooru.Model;
using Remora.Results;

namespace Argus.Collector.Driver.Minibooru;

/// <summary>
/// Represents the public interface of a Booru driver.
/// </summary>
public interface IBooruDriver
{
    /// <summary>
    /// Gets a set of posts from the Booru.
    /// </summary>
    /// <param name="after">The ID after which posts should be retrieved.</param>
    /// <param name="limit">The maximum number of posts to retrieve.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A set of posts.</returns>
    Task<Result<IReadOnlyList<BooruPost>>> GetPostsAsync
    (
        ulong after = 0,
        uint limit = 100,
        CancellationToken ct = default
    );
}
