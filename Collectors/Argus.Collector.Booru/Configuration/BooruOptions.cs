//
//  BooruOptions.cs
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

namespace Argus.Collector.Booru.Configuration;

/// <summary>
/// Represents collector-specific configuration.
/// </summary>
/// <param name="ServiceName">The name of the Booru that's being collected from.</param>
/// <param name="DriverName">The type name of the driver that should be used.</param>
/// <param name="BaseUrl">The base URL of the Booru.</param>
/// <param name="RateLimit">
/// The rate limit to use for API requests, measured in requests per second.
/// </param>
public record BooruOptions
(
    string ServiceName,
    string DriverName,
    Uri BaseUrl,
    int RateLimit = 1
);
