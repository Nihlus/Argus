//
//  StatusReport.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NetMQ;

namespace Argus.Common.Messages
{
    /// <summary>
    /// Represents a report regarding the processing status of an image.
    /// </summary>
    /// <param name="Timestamp">The time at which the report was created.</param>
    /// <param name="ServiceName">The name of the service the collector retrieved the image from.</param>
    /// <param name="Source">The source URL where the image was retrieved.</param>
    /// <param name="Image">A direct link to the image.</param>
    /// <param name="Status">The status of the image.</param>
    /// <param name="Message">The status message.</param>
    public record StatusReport
    (
        DateTimeOffset Timestamp,
        string ServiceName,
        Uri Source,
        Uri Image,
        ImageStatus Status,
        string Message
    )
    {
        /// <summary>
        /// Gets the name of the message type.
        /// </summary>
        public static string MessageType => nameof(StatusReport);

        /// <summary>
        /// Gets the number of serialized frames the message will fit into.
        /// </summary>
        public static int FrameCount => 4;

        /// <summary>
        /// Attempts to parse a status report from the given NetMQ message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="status">The parsed status report.</param>
        /// <returns>true if a status report was successfully parsed; otherwise, false.</returns>
        public static bool TryParse(NetMQMessage message, [NotNullWhen(true)] out StatusReport? status)
        {
            status = null;

            if (message.FrameCount < 5)
            {
                return false;
            }

            var messageType = message.Pop().ConvertToString();
            if (messageType != MessageType)
            {
                return false;
            }

            var rawTime = message.Pop().ConvertToString();
            var formatInfo = DateTimeFormatInfo.InvariantInfo;
            if (!DateTimeOffset.TryParse(rawTime, formatInfo, DateTimeStyles.None, out var timestamp))
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

            var imageStatus = (ImageStatus)message.Pop().ConvertToInt64();
            var statusMessage = message.Pop().ConvertToString();

            status = new StatusReport(timestamp, serviceName, source, imageLink, imageStatus, statusMessage);
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
            message.Append(this.Timestamp.ToString(DateTimeFormatInfo.InvariantInfo));
            message.Append(this.ServiceName);
            message.Append(this.Source.ToString());
            message.Append(this.Image.ToString());
            message.Append((long)this.Status);
            message.Append(this.Message);

            return message;
        }
    }
}
