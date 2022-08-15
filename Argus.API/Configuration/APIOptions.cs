//
//  APIOptions.cs
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

namespace Argus.API.Configuration;

/// <summary>
/// Holds various API options.
/// </summary>
/// <param name="ElasticsearchServer">The Elasticsearch server to connect to.</param>
/// <param name="ElasticsearchUsername">The Elasticsearch username to use.</param>
/// <param name="ElasticsearchPassword">The Elasticsearch password to use.</param>
/// <param name="ElasticsearchCertificateFingerprint">The fingerprint of the Elasticsearch server's certificate.</param>
public record APIOptions
(
    Uri ElasticsearchServer,
    string ElasticsearchUsername,
    string ElasticsearchPassword,
    string? ElasticsearchCertificateFingerprint
);
