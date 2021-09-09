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
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Common.Services
{
    /// <summary>
    /// Represents the abstract base class of all collector services.
    /// </summary>
    public abstract class CollectorService : BackgroundService
    {
        /// <summary>
        /// Holds the logging instance.
        /// </summary>
        private readonly ILogger<CollectorService> _log;

        /// <summary>
        /// Gets the name of the service.
        /// </summary>
        protected abstract string ServiceName { get; }

        /// <summary>
        /// Gets the message bus.
        /// </summary>
        protected IBus Bus { get; }

        /// <summary>
        /// Gets the application options.
        /// </summary>
        protected CollectorOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectorService"/> class.
        /// </summary>
        /// <param name="bus">The message bus.</param>
        /// <param name="options">The application options.</param>
        /// <param name="log">The logging instance.</param>
        protected CollectorService(IBus bus, IOptions<CollectorOptions> options, ILogger<CollectorService> log)
        {
            this.Bus = bus;
            this.Options = options.Value;
            _log = log;
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
            var message = new GetResumePoint(this.ServiceName);
            var response = await this.Bus.Request<GetResumePoint, ResumePoint>(message, ct);

            return response.Message.Value;
        }

        /// <summary>
        /// Sets the resume point of the current collector.
        /// </summary>
        /// <param name="resumePoint">The resume point.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected async Task<Result> SetResumePointAsync(string resumePoint, CancellationToken ct = default)
        {
            var message = new SetResumePoint(this.ServiceName, resumePoint);
            var response = await this.Bus.Request<SetResumePoint, ResumePoint>(message, ct);

            return response.Message.Value != resumePoint
                ? new InvalidOperationError("The new resume point did not match the requested value.")
                : Result.FromSuccess();
        }

        /// <summary>
        /// Pushes a collected image out to the coordinator.
        /// </summary>
        /// <param name="collectedImage">The collected image.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected async Task<Result> PushCollectedImageAsync
        (
            CollectedImage collectedImage,
            CancellationToken ct = default
        )
        {
            await this.Bus.Publish(collectedImage, ct);
            return Result.FromSuccess();
        }

        /// <summary>
        /// Pushes a status report out to the coordinator.
        /// </summary>
        /// <param name="statusReport">The status report.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>A result which may or may not have succeeded.</returns>
        protected async Task<Result> PushStatusReportAsync(StatusReport statusReport, CancellationToken ct = default)
        {
            await this.Bus.Publish(statusReport, ct);
            return Result.FromSuccess();
        }
    }
}
