//
//  SignatureWords.cs
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

namespace Argus.Common.Services.Elasticsearch
{
    /// <summary>
    /// Represents a statically-sized array of individual words, used for search acceleration.
    /// </summary>
    public record SignatureWords
    (
        int Word1,
        int Word2,
        int Word3,
        int Word4,
        int Word5,
        int Word6,
        int Word7,
        int Word8,
        int Word9,
        int Word10,
        int Word11,
        int Word12,
        int Word13,
        int Word14,
        int Word15,
        int Word16,
        int Word17,
        int Word18,
        int Word19,
        int Word20,
        int Word21,
        int Word22,
        int Word23,
        int Word24,
        int Word25,
        int Word26,
        int Word27,
        int Word28,
        int Word29,
        int Word30,
        int Word31,
        int Word32,
        int Word33,
        int Word34,
        int Word35,
        int Word36,
        int Word37,
        int Word38,
        int Word39,
        int Word40,
        int Word41,
        int Word42,
        int Word43,
        int Word44,
        int Word45,
        int Word46,
        int Word47,
        int Word48,
        int Word49,
        int Word50,
        int Word51,
        int Word52,
        int Word53,
        int Word54,
        int Word55,
        int Word56,
        int Word57,
        int Word58,
        int Word59,
        int Word60,
        int Word61,
        int Word62,
        int Word63
    )
    {
        /// <summary>
        /// Creates a <see cref="SignatureWords"/> instance from an array of words.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <returns>The words.</returns>
        public static SignatureWords FromArray(int[] array)
        {
            return new SignatureWords
            (
                array[0],
                array[1],
                array[2],
                array[3],
                array[4],
                array[5],
                array[6],
                array[7],
                array[8],
                array[9],
                array[10],
                array[11],
                array[12],
                array[13],
                array[14],
                array[15],
                array[16],
                array[17],
                array[18],
                array[19],
                array[20],
                array[21],
                array[22],
                array[23],
                array[24],
                array[25],
                array[26],
                array[27],
                array[28],
                array[29],
                array[30],
                array[31],
                array[32],
                array[33],
                array[34],
                array[35],
                array[36],
                array[37],
                array[38],
                array[39],
                array[40],
                array[41],
                array[42],
                array[43],
                array[44],
                array[45],
                array[46],
                array[47],
                array[48],
                array[49],
                array[50],
                array[51],
                array[52],
                array[53],
                array[54],
                array[55],
                array[56],
                array[57],
                array[58],
                array[59],
                array[60],
                array[61],
                array[62]
            );
        }
    }
}
