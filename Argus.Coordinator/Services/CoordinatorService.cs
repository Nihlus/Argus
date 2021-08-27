//
//  CoordinatorService.cs
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
using Argus.Common.Messages;
using Argus.Coordinator.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Remora.Results;

namespace Argus.Coordinator.Services
{
    /// <summary>
    /// Continuously accepts and fingerprints images.
    /// </summary>
    public class CoordinatorService : BackgroundService
    {
        private readonly CoordinatorOptions _options;
        private readonly ILogger<CoordinatorService> _log;

        private readonly PullSocket _incomingSocket;
        private readonly PushSocket _outgoingSocket;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoordinatorService"/> class.
        /// </summary>
        /// <param name="options">The coordinator options.</param>
        /// <param name="log">The logging instance.</param>
        public CoordinatorService(IOptions<CoordinatorOptions> options, ILogger<CoordinatorService> log)
        {
            _log = log;
            _options = options.Value;

            _incomingSocket = new PullSocket();
            _outgoingSocket = new PushSocket();

            _incomingSocket.Bind(_options.CoordinatorInputEndpoint.ToString().TrimEnd('/'));
            _outgoingSocket.Bind(_options.CoordinatorOutputEndpoint.ToString().TrimEnd('/'));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var incomingMessage = await _incomingSocket.ReceiveMultipartMessageAsync
                (
                    cancellationToken: stoppingToken
                );

                var messageType = incomingMessage.First.ConvertToString();
                switch (messageType)
                {
                    case var _ when messageType == CollectedImage.MessageType:
                    {
                        if (!CollectedImage.TryParse(incomingMessage, out var collectedImage))
                        {
                            _log.LogWarning
                            (
                                "Failed to parse incoming message as a {Type}",
                                CollectedImage.MessageType
                            );

                            continue;
                        }

                        _outgoingSocket.SendMultipartMessage(collectedImage.Serialize());
                        break;
                    }
                    case var _ when messageType == FingerprintedImage.MessageType:
                    {
                        if (!FingerprintedImage.TryParse(incomingMessage, out var fingerprintedImage))
                        {
                            _log.LogWarning
                            (
                                "Failed to parse incoming message as a {Type}",
                                FingerprintedImage.MessageType
                            );

                            continue;
                        }

                        var result = await HandleFingerprintedImageAsync(fingerprintedImage, stoppingToken);
                        if (!result.IsSuccess)
                        {
                            _log.LogWarning("Failed to handle fingerprinted image: {Message}", result.Error.Message);
                        }

                        break;
                    }
                    case var _ when messageType == StatusReport.MessageType:
                    {
                        if (!StatusReport.TryParse(incomingMessage, out var statusReport))
                        {
                            _log.LogWarning
                            (
                                "Failed to parse incoming message as a {Type}",
                                FingerprintedImage.MessageType
                            );

                            continue;
                        }

                        var result = await HandleStatusReportAsync(statusReport, stoppingToken);
                        if (!result.IsSuccess)
                        {
                            _log.LogWarning("Failed to handle status report: {Message}", result.Error.Message);
                        }

                        break;
                    }
                }
            }
        }

        private async Task<Result> HandleFingerprintedImageAsync
        (
            FingerprintedImage fingerprintedImage,
            CancellationToken ct
        )
        {
            // Save to database
            return new NotImplementedException();
        }

        private async Task<Result> HandleStatusReportAsync
        (
            StatusReport statusReport,
            CancellationToken ct
        )
        {
            // Save to database
            return new NotImplementedException();
        }

        /// <inheritdoc />
        public sealed override void Dispose()
        {
            base.Dispose();

            _incomingSocket.Dispose();
            _outgoingSocket.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
