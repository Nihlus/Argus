//
//  FListOptions.cs
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

namespace Argus.Collector.FList.Configuration;

/// <summary>
/// Represents collector-specific configuration.
/// </summary>
/// <param name="Username">The username to log in with.</param>
/// <param name="Password">The password to log in with.</param>
/// <param name="RateLimit">
/// The rate limit to use for API requests, measured in requests per second.
/// </param>
public record FListOptions(string Username, string Password, int RateLimit = 1);
