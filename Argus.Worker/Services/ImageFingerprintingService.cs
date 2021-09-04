//
//  ImageFingerprintingService.cs
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using Argus.Worker.Configuration;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Puzzle;
using Remora.Results;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Argus.Worker.Services
{
    /// <summary>
    /// Continuously accepts and fingerprints images.
    /// </summary>
    public class ImageFingerprintingService : BackgroundService
    {
        private readonly WorkerOptions _options;
        private readonly SignatureGenerator _signatureGenerator;
        private readonly ILogger<ImageFingerprintingService> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFingerprintingService"/> class.
        /// </summary>
        /// <param name="options">The worker options.</param>
        /// <param name="signatureGenerator">The signature generator.</param>
        /// <param name="log">The logging instance.</param>
        public ImageFingerprintingService
        (
            IOptions<WorkerOptions> options,
            SignatureGenerator signatureGenerator,
            ILogger<ImageFingerprintingService> log
        )
        {
            _options = options.Value;
            _signatureGenerator = signatureGenerator;
            _log = log;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Started fingerprinting worker");

            var incomingSocket = new PullSocket();
            var outgoingSocket = new PushSocket();

            incomingSocket.Connect(_options.CoordinatorOutputEndpoint.ToString().TrimEnd('/'));
            outgoingSocket.Connect(_options.CoordinatorInputEndpoint.ToString().TrimEnd('/'));

            var transformOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * _options.ParallelismMultiplier,
                CancellationToken = stoppingToken,
                SingleProducerConstrained = true,
                BoundedCapacity = Environment.ProcessorCount * _options.ParallelismMultiplier,
                EnsureOrdered = false
            };

            var transform = new TransformBlock<CollectedImage, (CollectedImage, Result<FingerprintedImage>)>
            (
                c => (c, FingerprintImage(c, stoppingToken)),
                transformOptions
            );

            var sendOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                CancellationToken = stoppingToken,
                SingleProducerConstrained = true,
                BoundedCapacity = Environment.ProcessorCount * _options.ParallelismMultiplier,
                EnsureOrdered = false
            };

            var send = new ActionBlock<(CollectedImage, Result<FingerprintedImage>)>
            (
                tuple =>
                {
                    var (request, result) = tuple;
                    if (result.IsSuccess)
                    {
                        _log.LogInformation("Fingerprinted {Image} from {Source}", request.Image, request.Source);

                        var serialized = MessagePackSerializer.Serialize<ICoordinatorInputMessage>(result.Entity);
                        outgoingSocket.SendFrame(serialized);
                    }
                    else
                    {
                        _log.LogInformation
                        (
                            "Failed to fingerprint {Image} from {Source}: {Reason}",
                            request.Image,
                            request.Source,
                            result.Error.Message
                        );
                    }

                    // send status message
                    var message = new StatusReport
                    (
                        DateTime.UtcNow,
                        request.ServiceName,
                        request.Source,
                        request.Image,
                        result.IsSuccess ? ImageStatus.Processed : ImageStatus.Faulted,
                        result.IsSuccess ? string.Empty : result.Error.Message
                    );

                    var serializedStatusReport = MessagePackSerializer.Serialize<ICoordinatorInputMessage>(message);
                    outgoingSocket.SendFrame(serializedStatusReport);
                },
                sendOptions
            );

            transform.LinkTo(send);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (incomingSocket.TryReceiveFrameBytes(out var bytes))
                    {
                        var message = MessagePackSerializer.Deserialize<ICoordinatorOutputMessage>
                        (
                            bytes,
                            cancellationToken: stoppingToken
                        );

                        switch (message)
                        {
                            case CollectedImage collectedImage:
                            {
                                while (!await transform.SendAsync(collectedImage, stoppingToken))
                                {
                                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                                }

                                break;
                            }
                            default:
                            {
                                _log.LogWarning("Failed to parse incoming message from the coordinator");
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _log.LogInformation("Shutting down...");
        }

        private Result<FingerprintedImage> FingerprintImage
        (
            CollectedImage collectedImage,
            CancellationToken ct = default
        )
        {
            try
            {
                // CPU-intensive step 1
                using var image = Image.Load<L8>(collectedImage.Data);
                if (ct.IsCancellationRequested)
                {
                    return new TaskCanceledException();
                }

                // CPU-intensive step 2
                using var sha256 = new SHA256Managed();
                var hash = sha256.ComputeHash(collectedImage.Data);
                if (ct.IsCancellationRequested)
                {
                    return new TaskCanceledException();
                }

                var hashString = BitConverter.ToString(hash).ToLowerInvariant();

                // CPU-intensive step 3
                var signature = _signatureGenerator.GenerateSignature(image);
                return new FingerprintedImage
                (
                    collectedImage.ServiceName,
                    collectedImage.Source,
                    collectedImage.Image,
                    signature,
                    hashString
                );
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
