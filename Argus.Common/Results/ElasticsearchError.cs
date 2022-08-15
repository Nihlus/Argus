//
//  ElasticsearchError.cs
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

using Elasticsearch.Net;
using Remora.Results;

namespace Argus.Common.Results;

/// <summary>
/// Represents an error resulting from Elasticsearch.
/// </summary>
/// <param name="Error">The error.</param>
/// <param name="Message">The error message.</param>
public record ElasticsearchError
(
    ServerError Error,
    string Message = "An error occurred in Elasticsearch."
) : ResultError(Message);
