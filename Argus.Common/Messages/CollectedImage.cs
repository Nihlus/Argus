//
//  CollectedImage.cs
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

namespace Argus.Common.Messages
{
    /// <summary>
    /// Represents an image that has been retrieved by a service collector.
    /// </summary>
    /// <param name="ServiceName">The name of the service the collector retrieved the image from.</param>
    /// <param name="Source">The source URL where the image was retrieved.</param>
    /// <param name="Image">A direct link to the image.</param>
    /// <param name="Data">The image data.</param>
    public record CollectedImage(string ServiceName, Uri Source, Uri Image, byte[] Data)
    {
        /// <summary>
        /// Gets the name of the message type.
        /// </summary>
        public static string MessageType => nameof(CollectedImage);

        /// <summary>
        /// Gets the number of serialized frames the message will fit into.
        /// </summary>
        public static int FrameCount => 4;

        /// <summary>
        /// Attempts to parse a retrieved image from the given NetMQ message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="image">The parsed image.</param>
        /// <returns>true if an image was successfully parsed; otherwise, false.</returns>
        public static bool TryParse(NetMQMessage message, [NotNullWhen(true)] out CollectedImage? image)
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

            var imageData = message.Pop().ToByteArray();

            image = new CollectedImage(serviceName, source, imageLink, imageData);
            return true;
        }

        /// <summary>
        /// Serializes the retrieved image to a NetMQ message.
        /// </summary>
        /// <returns>The message.</returns>
        public NetMQMessage Serialize()
        {
            var message = new NetMQMessage();
            message.Append(MessageType);
            message.Append(this.ServiceName);
            message.Append(this.Source.ToString());
            message.Append(this.Image.ToString());
            message.Append(this.Data.ToArray());

            return message;
        }
    }
}
