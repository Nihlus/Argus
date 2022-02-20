//
//  FingerprintedImageFaultConsumer.cs
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
using Argus.Common.Messages.BulkData;
using Argus.Coordinator.Model;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Argus.Coordinator.MassTransit.Consumers;

/// <summary>
/// Handles faults produced by fingerprinted images.
/// </summary>
public class FingerprintedImageFaultConsumer : IConsumer<Fault<FingerprintedImage>>
{
    private readonly CoordinatorContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprintedImageFaultConsumer"/> class.
    /// </summary>
    /// <param name="db">The database.</param>
    public FingerprintedImageFaultConsumer(CoordinatorContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Fault<FingerprintedImage>> context)
    {
        var fault = context.Message;
        var message = fault.Message;
        var reportMessage = string.Join(',', fault.Exceptions.Select(e => e.Message));

        var statusReport = new StatusReport
        (
            DateTimeOffset.UtcNow,
            message.ServiceName,
            message.Source,
            message.Link,
            ImageStatus.Faulted,
            reportMessage
        );

        await _db.ServiceStatusReports.Upsert(statusReport).RunAsync(context.CancellationToken);
    }
}
