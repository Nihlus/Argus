//
//  CoordinatorOptions.cs
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

namespace Argus.Coordinator.Configuration
{
    /// <summary>
    /// Represents the application configuration.
    /// </summary>
    /// <param name="CoordinatorEndpoint">The request-reply endpoint of the cluster coordinator.</param>
    /// <param name="ElasticsearchServer">The endpoint of the elasticsearch server.</param>
    /// <param name="ElasticsearchUsername">The username of the elasticsearch credentials.</param>
    /// <param name="ElasticsearchPassword">The password of the elasticsearch credentials.</param>
    public record CoordinatorOptions
    (
        Uri CoordinatorEndpoint,
        Uri ElasticsearchServer,
        string ElasticsearchUsername,
        string ElasticsearchPassword
    );
}
