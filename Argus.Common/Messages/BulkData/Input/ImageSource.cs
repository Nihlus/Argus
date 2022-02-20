//
//  ImageSource.cs
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
using JetBrains.Annotations;

namespace Argus.Common.Messages.BulkData;

/// <summary>
/// Represents a unique source of one or more images from a service.
/// </summary>
/// <param name="ServiceName">The name of the service the source belongs to.</param>
/// <param name="FirstVisitedAt">The time at which the source was first visited.</param>
/// <param name="Source">The URL to the source itself.</param>
/// <param name="SourceIdentifier">The unique identifier of the source, as understood by the service.</param>
/// <param name="RevisitCount">The number of times the source has been revisited.</param>
/// <param name="LastRevisitedAt">The time at which the source was last revisited.</param>
[PublicAPI]
public record ImageSource
(
    string ServiceName,
    Uri Source,
    DateTimeOffset FirstVisitedAt,
    string? SourceIdentifier = default,
    int RevisitCount = 0,
    DateTimeOffset? LastRevisitedAt = default
);
