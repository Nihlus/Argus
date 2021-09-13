//
//  CollectedImageConsumer.cs
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
using System.Threading.Tasks;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using Microsoft.Extensions.Logging;
using Puzzle;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Argus.Worker.MassTransit.Consumers
{
    /// <summary>
    /// Consumes images for fingerprinting.
    /// </summary>
    public class CollectedImageConsumer : IConsumer<CollectedImage>
    {
        private readonly IBus _bus;
        private readonly SignatureGenerator _signatureGenerator;
        private readonly ILogger<CollectedImageConsumer> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectedImageConsumer"/> class.
        /// </summary>
        /// <param name="bus">The message bus.</param>
        /// <param name="signatureGenerator">The signature generator.</param>
        /// <param name="log">The logging instance.</param>
        public CollectedImageConsumer
        (
            IBus bus,
            SignatureGenerator signatureGenerator,
            ILogger<CollectedImageConsumer> log
        )
        {
            _bus = bus;
            _signatureGenerator = signatureGenerator;
            _log = log;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<CollectedImage> context)
        {
            var collectedImage = context.Message;
            try
            {
                // CPU-intensive step 1
                using var image = Image.Load<L8>(collectedImage.Data);
                context.CancellationToken.ThrowIfCancellationRequested();

                // CPU-intensive step 2
                var signature = _signatureGenerator.GenerateSignature(image);
                var fingerprint = new ImageSignature(signature);

                await _bus.Publish
                (
                    new FingerprintedImage
                    (
                        collectedImage.ServiceName,
                        collectedImage.Source,
                        collectedImage.Link,
                        fingerprint
                    )
                );

                _log.LogInformation
                (
                    "Fingerprinted image {Link} from {Source}",
                    collectedImage.Link,
                    collectedImage.Source
                );
            }
            catch (Exception e)
            {
                var message = new StatusReport
                (
                    DateTime.UtcNow,
                    collectedImage.ServiceName,
                    collectedImage.Source,
                    collectedImage.Link,
                    ImageStatus.Faulted,
                    e.Message
                );

                await _bus.Publish(message);
                throw;
            }
        }
    }
}
