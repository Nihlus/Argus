//
//  APIKey.cs
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

namespace Argus.API.Database.Model;

/// <summary>
/// Represents an API key.
/// </summary>
public class APIKey
{
    /// <summary>
    /// Gets the ID of the key.
    /// </summary>
    public long ID { get; init; }

    /// <summary>
    /// Gets the time at which the API key was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the time at which the API key expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the API key itself.
    /// </summary>
    public Guid Key { get; init; }

    /// <summary>
    /// Creates a new API key.
    /// </summary>
    /// <param name="expiresAt">The time at which the key expires.</param>
    /// <returns>The key.</returns>
    public static APIKey Create(DateTimeOffset? expiresAt = null)
    {
        return new APIKey
        {
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            Key = Guid.NewGuid()
        };
    }
}
