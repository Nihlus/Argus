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
using System.Threading;
using System.Threading.Tasks;
using Argus.Worker.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetMQ.Sockets;

namespace Argus.Worker.Services
{
    /// <summary>
    /// Continuously accepts and fingerprints images.
    /// </summary>
    public class ImageFingerprintingService : BackgroundService
    {
        private readonly WorkerOptions _options;
        private readonly RequestSocket _socket;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFingerprintingService"/> class.
        /// </summary>
        /// <param name="options">The worker options.</param>
        public ImageFingerprintingService(IOptions<WorkerOptions> options)
        {
            _options = options.Value;
            _socket = new RequestSocket(_options.CoordinatorEndpoint.ToString());
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
