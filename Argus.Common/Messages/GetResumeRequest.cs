//
//  GetResumeRequest.cs
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

using System.Diagnostics.CodeAnalysis;
using NetMQ;

namespace Argus.Common.Messages
{
    /// <summary>
    /// Represents a request to resume a collector.
    /// </summary>
    public record GetResumeRequest(string ServiceName)
    {
        /// <summary>
        /// Gets the name of the message type.
        /// </summary>
        public static string MessageType => nameof(GetResumeRequest);

        /// <summary>
        /// Gets the number of serialized frames the message will fit into.
        /// </summary>
        public static int FrameCount => 1;

        /// <summary>
        /// Attempts to parse a resume request from the given NetMQ message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="status">The parsed status report.</param>
        /// <returns>true if the entity was successfully parsed; otherwise, false.</returns>
        public static bool TryParse(NetMQMessage message, [NotNullWhen(true)] out GetResumeRequest? status)
        {
            status = null;

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

            status = new GetResumeRequest(serviceName);
            return true;
        }

        /// <summary>
        /// Serializes the request to a NetMQ message.
        /// </summary>
        /// <returns>The message.</returns>
        public NetMQMessage Serialize()
        {
            var message = new NetMQMessage();
            message.Append(MessageType);
            message.Append(this.ServiceName);

            return message;
        }
    }
}
