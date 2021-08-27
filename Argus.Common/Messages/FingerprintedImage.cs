//
//  FingerprintedImage.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetMQ;
using Puzzle;

namespace Argus.Common.Messages
{
    /// <summary>
    /// Represents an image that has been fingerprinted by a worker.
    /// </summary>
    /// <param name="ServiceName">The name of the service the original collector retrieved the image from.</param>
    /// <param name="Source">The source URL where the image was retrieved.</param>
    /// <param name="Image">A direct link to the image.</param>
    /// <param name="Fingerprint">The image data.</param>
    /// <param name="Hash">A SHA256 hash of the image data.</param>
    public record FingerprintedImage
    (
        string ServiceName,
        Uri Source,
        Uri Image,
        IReadOnlyCollection<LuminosityLevel> Fingerprint,
        string Hash
    )
    {
        /// <summary>
        /// Gets the name of the message type.
        /// </summary>
        public static string MessageType => nameof(FingerprintedImage);

        /// <summary>
        /// Gets the number of serialized frames the message will fit into.
        /// </summary>
        public static int FrameCount => 5;

        /// <summary>
        /// Attempts to parse a fingerprinted image from the given NetMQ message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="image">The parsed image.</param>
        /// <returns>true if an image was successfully parsed; otherwise, false.</returns>
        public static bool TryParse(NetMQMessage message, [NotNullWhen(true)] out FingerprintedImage? image)
        {
            image = null;

            if (message.FrameCount < FrameCount)
            {
                return false;
            }

            var messageType = message.Pop().ConvertToString();
            if (messageType != MessageType)
            {
                return false;
            }

            var serviceName = message.Pop().ConvertToString();

            var rawSource = message.Pop().ConvertToString();
            if (!Uri.TryCreate(rawSource, UriKind.RelativeOrAbsolute, out var source))
            {
                return false;
            }

            var rawImageLink = message.Pop().ConvertToString();
            if (!Uri.TryCreate(rawImageLink, UriKind.RelativeOrAbsolute, out var imageLink))
            {
                return false;
            }

            var fingerprint = message.Pop().ToByteArray().Select(b => (LuminosityLevel)b).ToList();
            var hash = message.Pop().ConvertToString();

            image = new FingerprintedImage(serviceName, source, imageLink, fingerprint, hash);
            return true;
        }

        /// <summary>
        /// Serializes the image to a NetMQ message.
        /// </summary>
        /// <returns>The message.</returns>
        public NetMQMessage Serialize()
        {
            var message = new NetMQMessage();
            message.Append(MessageType);
            message.Append(this.ServiceName);
            message.Append(this.Source.ToString());
            message.Append(this.Image.ToString());
            message.Append(this.Fingerprint.Select(l => (byte)l).ToArray());
            message.Append(this.Hash);

            return message;
        }
    }
}
