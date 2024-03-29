//
//  WeasylError.cs
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

using System.Net;
using Remora.Results;

namespace Argus.Collector.Weasyl.API.Model;

/// <summary>
/// Represents an error returned by Weasyl.
/// </summary>
/// <param name="StatusCode">The Http status code.</param>
/// <param name="Code">The error code.</param>
/// <param name="Name">The error name.</param>
public record WeasylError(HttpStatusCode StatusCode, int? Code, string? Name) : ResultError
(
    $"A Weasyl error occurred: {Name ?? string.Empty}{(Code is null ? string.Empty : $" (code {Code})")}"
);
