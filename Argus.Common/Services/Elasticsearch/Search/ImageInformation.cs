//
//  ImageInformation.cs
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

namespace Argus.Common.Services.Elasticsearch.Search;

/// <summary>
/// Represents minimal information about an indexed image.
/// </summary>
/// <param name="IndexedAt">The time at which the image was indexed.</param>
/// <param name="Service">The name of the service the image was indexed from.</param>
/// <param name="Source">The source link from which the image was indexed.</param>
/// <param name="Link">The direct link to the image.</param>
public record ImageInformation(DateTimeOffset IndexedAt, string Service, Uri Source, Uri Link);
