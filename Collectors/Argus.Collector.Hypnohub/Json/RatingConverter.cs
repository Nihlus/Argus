//
//  RatingConverter.cs
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
using System.Text.Json;
using System.Text.Json.Serialization;
using BooruDex.Models;

namespace Argus.Collector.Hypnohub.Json
{
    /// <summary>
    /// Converts ratings.
    /// </summary>
    public class RatingConverter : JsonConverter<Rating>
    {
        /// <inheritdoc />
        public override Rating Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString() ?? throw new JsonException();
            switch (value[0])
            {
                case 'E':
                case 'e':
                    return Rating.Explicit;
                case 'Q':
                case 'q':
                    return Rating.Questionable;
                case 'S':
                case 's':
                    return Rating.Safe;
                default:
                    return Rating.Questionable;
            }
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Rating value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
