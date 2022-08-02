//
//  SourcesToRevisit.cs
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

using System.Collections.Generic;
using Argus.Common.Messages.BulkData;

namespace Argus.Common.Messages.Replies;

/// <summary>
/// Represents a set of sources that the service should revisit.
/// </summary>
/// <param name="Sources">The sources, as a mapping of image sources to the images that are associated with it.</param>
public record SourcesToRevisit(IReadOnlyList<KeyValuePair<ImageSource, IReadOnlyList<StatusReport>>> Sources);
