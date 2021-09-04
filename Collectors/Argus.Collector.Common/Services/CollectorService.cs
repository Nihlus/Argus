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

using System;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Common.Messages.BulkData;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        /// Holds the push socket used for data output.
        /// </summary>
        private readonly PushSocket _pushSocket;

        /// <summary>
        /// Holds the logging instance.
        /// </summary>
        private readonly ILogger<CollectorService> _log;

        /// <summary>
        /// Gets the request socket used for communication with the coordinator.
        /// </summary>
        protected RequestSocket RequestSocket { get; }

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
        /// <param name="log">The logging instance.</param>
        protected CollectorService(IOptions<CollectorOptions> options, ILogger<CollectorService> log)
        {
            this.Options = options.Value;
            _log = log;

            this.RequestSocket = new RequestSocket();
            _pushSocket = new PushSocket();

            this.RequestSocket.Connect(this.Options.CoordinatorEndpoint.ToString().TrimEnd('/'));
            _pushSocket.Connect(this.Options.CoordinatorInputEndpoint.ToString().TrimEnd('/'));
        }

        /// <inheritdoc/>
        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var collection = await CollectAsync(stoppingToken);
                if (!collection.IsSuccess && collection.Error is not ExceptionError { Exception: OperationCanceledException })
                {
                    _log.LogWarning("Error in collector: {Message}", collection.Error.Message);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Runs the main collection task.
        /// </summary>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected abstract Task<Result> CollectAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets the resume point of the current collector.
        /// </summary>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The resume point.</returns>
        protected async Task<Result<string>> GetResumePointAsync(CancellationToken ct = default)
        {
            var message = new GetResumeRequest(this.ServiceName);
            var serialized = MessagePackSerializer.Serialize<ICoordinatorRequest>(message, cancellationToken: ct);
            this.RequestSocket.SendFrame(serialized);

            var (frame, _) = await this.RequestSocket.ReceiveFrameBytesAsync(ct);
            var response = MessagePackSerializer.Deserialize<ICoordinatorReply>(frame, cancellationToken: ct);
            return response switch
            {
                ResumeReply resumeReply => resumeReply.ResumePoint,
                _ => new InvalidOperationError("Unknown response.")
            };
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
            var serialized = MessagePackSerializer.Serialize<ICoordinatorRequest>(message, cancellationToken: ct);
            this.RequestSocket.SendFrame(serialized);

            var (frame, _) = await this.RequestSocket.ReceiveFrameBytesAsync(ct);
            var response = MessagePackSerializer.Deserialize<ICoordinatorReply>(frame, cancellationToken: ct);
            return response switch
            {
                ResumeReply resumeReply => resumeReply.ResumePoint == resumePoint
                    ? Result.FromSuccess()
                    : new InvalidOperationError("The new resume point did not match the requested value."),
                _ => new InvalidOperationError("Unknown response.")
            };
        }

        /// <summary>
        /// Pushes a collected image out to the coordinator.
        /// </summary>
        /// <param name="collectedImage">The collected image.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected Result PushCollectedImage(CollectedImage collectedImage)
        {
            var serialized = MessagePackSerializer.Serialize<ICoordinatorInputMessage>(collectedImage);
            _pushSocket.SendFrame(serialized);

            return Result.FromSuccess();
        }

        /// <summary>
        /// Pushes a status report out to the coordinator.
        /// </summary>
        /// <param name="statusReport">The status report.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected Result PushStatusReport(StatusReport statusReport)
        {
            var serialized = MessagePackSerializer.Serialize<ICoordinatorInputMessage>(statusReport);
            _pushSocket.SendFrame(serialized);

            return Result.FromSuccess();
        }
    }
}
