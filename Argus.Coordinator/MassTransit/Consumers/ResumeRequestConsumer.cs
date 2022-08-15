//
//  ResumeRequestConsumer.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) Jarl Gullberg
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

using System.Threading.Tasks;
using Argus.Common.Messages.Replies;
using Argus.Common.Messages.Requests;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Argus.Coordinator.MassTransit.Consumers;

/// <summary>
/// Consumes various resume requests.
/// </summary>
public class ResumeRequestConsumer : IConsumer<GetResumePoint>, IConsumer<SetResumePoint>
{
    private readonly CoordinatorContext _db;
    private readonly ILogger<ResumeRequestConsumer> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeRequestConsumer"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="log">The logging instance.</param>
    public ResumeRequestConsumer(CoordinatorContext db, ILogger<ResumeRequestConsumer> log)
    {
        _db = db;
        _log = log;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<GetResumePoint> context)
    {
        var serviceStatus = await _db.ServiceStates.AsNoTracking().FirstOrDefaultAsync
        (
            s => s.Name == context.Message.ServiceName,
            context.CancellationToken
        );

        var resumePoint = serviceStatus?.ResumePoint;
        await context.RespondAsync(new ResumePoint(resumePoint ?? string.Empty));

        _log.LogInformation
        (
            "Told collector for service \"{Service}\" its resume point",
            context.Message.ServiceName
        );
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<SetResumePoint> context)
    {
        var serviceStatus = await _db.ServiceStates.FirstOrDefaultAsync
        (
            s => s.Name == context.Message.ServiceName,
            context.CancellationToken
        ) ?? new ServiceState(context.Message.ServiceName);

        serviceStatus.ResumePoint = context.Message.ResumePoint;

        _db.Update(serviceStatus);
        await _db.SaveChangesAsync(context.CancellationToken);

        var resumePoint = serviceStatus.ResumePoint;
        await context.RespondAsync(new ResumePoint(resumePoint ?? string.Empty));
    }
}
