//
//  WeasylSubmission.cs
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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Argus.Collector.Weasyl.API.Model;

/// <summary>
/// Represents a Weasyl submission.
/// </summary>
public class WeasylSubmission
{
    /// <summary>
    /// Gets the submission ID.
    /// </summary>
    [JsonPropertyName("submitid")]
    public int SubmitID { get; init; }

    /// <summary>
    /// Gets the submission subtype.
    /// </summary>
    public string Subtype { get; init; } = string.Empty;

    /// <summary>
    /// Gets the link to the submission.
    /// </summary>
    public string Link { get; init; } = string.Empty;

    /// <summary>
    /// Gets the media associated with the submission.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<WeasylMedia>> Media { get; init; }
        = new Dictionary<string, IReadOnlyList<WeasylMedia>>();
}
