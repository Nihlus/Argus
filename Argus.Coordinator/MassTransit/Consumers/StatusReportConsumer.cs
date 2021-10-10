//
//  StatusReportConsumer.cs
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

using System.Linq;
using System.Threading.Tasks;
using Argus.Common.Messages.BulkData;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoreLinq;

namespace Argus.Coordinator.MassTransit.Consumers
{
    /// <summary>
    /// Consumes status reports, logging them for later use.
    /// </summary>
    public class StatusReportConsumer : IConsumer<Batch<StatusReport>>
    {
        private readonly CoordinatorContext _db;
        private readonly ILogger<StatusReportConsumer> _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusReportConsumer"/> class.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="log">The logging instance.</param>
        public StatusReportConsumer(CoordinatorContext db, ILogger<StatusReportConsumer> log)
        {
            _db = db;
            _log = log;
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<Batch<StatusReport>> context)
        {
            var statusReports = context.Message;

            var reports = statusReports.AsEnumerable().Select(t => t.Message)
                .DistinctBy(m => (m.Source, m.Link))
                .ToList();

            await _db.ServiceStatusReports.UpsertRange(reports)
                .RunAsync(context.CancellationToken);

            foreach (var statusReport in reports)
            {
                _log.LogInformation
                (
                    "Logged status report regarding image {Link} from {Source}",
                    statusReport.Link,
                    statusReport.Source
                );
            }
        }
    }
}
