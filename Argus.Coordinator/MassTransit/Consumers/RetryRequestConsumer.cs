//
//  RetryRequestConsumer.cs
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
using System.Linq;
using System.Threading.Tasks;
using Argus.Common;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Argus.Coordinator.MassTransit.Consumers
{
    /// <summary>
    /// Consumes various retry-related requests.
    /// </summary>
    public class RetryRequestConsumer : IConsumer<GetImagesToRetry>
    {
        private readonly CoordinatorContext _db;
        private readonly ILogger<RetryRequestConsumer> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryRequestConsumer"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="log">The logging instance.</param>
        public RetryRequestConsumer(CoordinatorContext db, ILogger<RetryRequestConsumer> log)
        {
            _db = db;
            _log = log;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<GetImagesToRetry> context)
        {
            var now = DateTime.UtcNow;
            var then = now - TimeSpan.FromHours(1);

            var reports = await _db.ServiceStatusReports.AsNoTracking()
                .OrderBy(r => r.Timestamp)
                .Where(r => r.Timestamp < then)
                .Where
                (
                    r =>
                        r.Status != ImageStatus.Faulted &&
                        r.Status != ImageStatus.Rejected &&
                        r.Status != ImageStatus.Indexed
                )
                .Take(context.Message.MaxCount)
                .ToListAsync(context.CancellationToken);

            await context.RespondAsync(new ImagesToRetry(reports));
            _log.LogInformation("Sent {Count} images for retrying", reports.Count);
        }
    }
}
