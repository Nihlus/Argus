//
//  CollectorService.cs
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

using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Common.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Remora.Results;

namespace Argus.Collector.Common.Services
{
    /// <summary>
    /// Represents the abstract base class of all collector services.
    /// </summary>
    public abstract class CollectorService : BackgroundService
    {
        /// <summary>
        /// Holds the request socket used for communication with the coordinator.
        /// </summary>
        private readonly RequestSocket _requestSocket;

        /// <summary>
        /// Holds the push socket used for data output.
        /// </summary>
        private readonly PushSocket _pushSocket;

        /// <summary>
        /// Gets the name of the service.
        /// </summary>
        protected abstract string ServiceName { get; }

        /// <summary>
        /// Gets the application options.
        /// </summary>
        protected CollectorOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectorService"/> class.
        /// </summary>
        /// <param name="options">The application options.</param>
        protected CollectorService(IOptions<CollectorOptions> options)
        {
            this.Options = options.Value;
            _requestSocket = new RequestSocket();
            _pushSocket = new PushSocket();

            _requestSocket.Connect(this.Options.CoordinatorEndpoint.ToString().TrimEnd('/'));
            _pushSocket.Connect(this.Options.CoordinatorInputEndpoint.ToString().TrimEnd('/'));
        }

        /// <summary>
        /// Gets the resume point of the current collector.
        /// </summary>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The resume point.</returns>
        protected async Task<Result<string>> GetResumePointAsync(CancellationToken ct = default)
        {
            var message = new GetResumeRequest(this.ServiceName);
            _requestSocket.SendMultipartMessage(message.Serialize());

            var response = await _requestSocket.ReceiveMultipartMessageAsync(cancellationToken: ct);
            if (!ResumeReply.TryParse(response, out var reply))
            {
                return new InvalidOperationError("Failed to parse the reply as a resume reply.");
            }

            return reply.ResumePoint;
        }

        /// <summary>
        /// Sets the resume point of the current collector.
        /// </summary>
        /// <param name="resumePoint">The resume point.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected async Task<Result> SetResumePointAsync(string resumePoint, CancellationToken ct = default)
        {
            var message = new SetResumeRequest(this.ServiceName, resumePoint);
            _requestSocket.SendMultipartMessage(message.Serialize());

            var response = await _requestSocket.ReceiveMultipartMessageAsync(cancellationToken: ct);
            if (!ResumeReply.TryParse(response, out var reply))
            {
                return new InvalidOperationError("Failed to parse the reply as a resume reply.");
            }

            return reply.ResumePoint == resumePoint
                ? Result.FromSuccess()
                : new InvalidOperationError("The new resume point did not match the requested value.");
        }

        /// <summary>
        /// Pushes a collected image out to the coordinator.
        /// </summary>
        /// <param name="collectedImage">The collected image.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected Result PushCollectedImage(CollectedImage collectedImage)
        {
            _pushSocket.SendMultipartMessage(collectedImage.Serialize());
            return Result.FromSuccess();
        }

        /// <summary>
        /// Pushes a status report out to the coordinator.
        /// </summary>
        /// <param name="statusReport">The status report.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected Result PushStatusReport(StatusReport statusReport)
        {
            _pushSocket.SendMultipartMessage(statusReport.Serialize());
            return Result.FromSuccess();
        }
    }
}
