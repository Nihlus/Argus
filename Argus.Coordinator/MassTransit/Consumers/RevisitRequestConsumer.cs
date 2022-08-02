//
//  RevisitRequestConsumer.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Argus.Common.Messages.BulkData;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Argus.Coordinator.MassTransit.Consumers;

/// <summary>
/// Consumes various revisit-related requests.
/// </summary>
public class RevisitRequestConsumer : IConsumer<GetSourcesToRevisit>
{
    private readonly CoordinatorContext _db;
    private readonly ILogger<RevisitRequestConsumer> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevisitRequestConsumer"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="log">The logging instance.</param>
    public RevisitRequestConsumer(CoordinatorContext db, ILogger<RevisitRequestConsumer> log)
    {
        _db = db;
        _log = log;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<GetSourcesToRevisit> context)
    {
        // look up sources that have
        //  1. low revisit counts
        //  2. a long time since they were revisited
        var sources = await _db.ServiceImageSources.AsNoTracking()
            .Where(s => s.ServiceName == context.Message.ServiceName)
            .OrderBy(s => s.RevisitCount)
            .ThenByDescending(s => s.LastRevisitedAt == null)
            .ThenByDescending(s => DateTimeOffset.UtcNow - s.LastRevisitedAt)
            .Take(context.Message.MaxCount)
            .Join
            (
                _db.ServiceStatusReports,
                s => s.Source,
                r => r.Source,
                (source, report) => new
                {
                    Source = source,
                    Report = report
                }
            )
            .ToListAsync();

        var asDictionary = sources
            .GroupBy(o => o.Source)
            .Select(g => new KeyValuePair<ImageSource, IReadOnlyList<StatusReport>>(g.Key, g.Select(o => o.Report).ToList()))
            .ToList();

        await context.RespondAsync(new SourcesToRevisit(asDictionary));
        _log.LogInformation("Sent {Count} sources for revisiting", asDictionary.Count);
    }
}
