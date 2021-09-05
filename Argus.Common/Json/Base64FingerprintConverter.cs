//
//  Base64FingerprintConverter.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Puzzle;

namespace Argus.Common.Json
{
    /// <summary>
    /// Converts a fingerprint to and from a base64 encoded string.
    /// </summary>
    public class Base64FingerprintConverter : JsonConverter<IReadOnlyList<LuminosityLevel>>
    {
        /// <inheritdoc />
        public override IReadOnlyList<LuminosityLevel> Read
        (
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            var bytes = reader.GetBytesFromBase64();
            return MemoryMarshal.Cast<byte, LuminosityLevel>(bytes).ToArray();
        }

        /// <inheritdoc />
        public override void Write
        (
            Utf8JsonWriter writer,
            IReadOnlyList<LuminosityLevel> value,
            JsonSerializerOptions options
        )
        {
            writer.WriteBase64StringValue(value.Select(l => (byte)l).ToArray());
        }
    }
}
