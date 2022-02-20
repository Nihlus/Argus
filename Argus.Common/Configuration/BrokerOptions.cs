//
//  BrokerOptions.cs
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

namespace Argus.Common.Configuration;

/// <summary>
/// Holds options related to the message broker.
/// </summary>
/// <param name="Host">The host at which the broker is available.</param>
/// <param name="Username">The username to use for authentication with the broker.</param>
/// <param name="Password">The password to use for authentication with the broker.</param>
/// <param name="DataRepository">The path to the data repository.</param>
public record BrokerOptions
(
    Uri Host,
    string Username,
    string Password,
    string DataRepository
);
