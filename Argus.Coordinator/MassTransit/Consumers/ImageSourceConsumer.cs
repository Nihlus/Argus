//
//  ImageSourceConsumer.cs
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

using System.Linq;
using System.Threading.Tasks;
using Argus.Common.Messages.BulkData;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Argus.Coordinator.MassTransit.Consumers;

/// <summary>
/// Consumes image sources, persisting them to the database.
/// </summary>
public class ImageSourceConsumer : IConsumer<Batch<ImageSource>>
{
    private readonly CoordinatorContext _db;
    private readonly ILogger<ImageSourceConsumer> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageSourceConsumer"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="log">The logging instance.</param>
    public ImageSourceConsumer(CoordinatorContext db, ILogger<ImageSourceConsumer> log)
    {
        _db = db;
        _log = log;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Batch<ImageSource>> context)
    {
        var imageSources = context.Message;

        var sources = imageSources.AsEnumerable().Select(t => t.Message)
            .OrderByDescending(s => s.LastRevisitedAt.HasValue)
            .ThenBy(s => s.LastRevisitedAt)
            .DistinctBy(s => (s.ServiceName, s.Source))
            .ToList();

        await _db.ServiceImageSources.UpsertRange(sources)
            .RunAsync(context.CancellationToken);

        foreach (var imageSource in sources)
        {
            _log.LogInformation
            (
                "Logged image source from {Service} at {Source}",
                imageSource.ServiceName,
                imageSource.Source
            );
        }
    }
}
