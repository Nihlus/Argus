//
//  FingerprintedImageConsumer.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using Argus.Common.Services.Elasticsearch;
using MassTransit;
using Microsoft.Extensions.Logging;
using Nest;

namespace Argus.Coordinator.MassTransit.Consumers;

/// <summary>
/// Consumers fingerprinted images, indexing them.
/// </summary>
public class FingerprintedImageConsumer : IConsumer<Batch<FingerprintedImage>>
{
    private readonly IBus _bus;
    private readonly NestService _nestService;
    private readonly ILogger<FingerprintedImageConsumer> _log;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprintedImageConsumer"/> class.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="nestService">The elasticsearch service.</param>
    /// <param name="log">The logging instance.</param>
    public FingerprintedImageConsumer(IBus bus, NestService nestService, ILogger<FingerprintedImageConsumer> log)
    {
        _bus = bus;
        _nestService = nestService;
        _log = log;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Batch<FingerprintedImage>> context)
    {
        var batch = context.Message;

        var indexChecks = await Task.WhenAll
        (
            context.Message
                .Select(m => m.Message)
                .Select
                (
                    async im => (Image: im, Result: await _nestService.IsIndexedAsync
                    (
                        im.Source.ToString(),
                        im.Link.ToString(),
                        im.Signature.Signature,
                        context.CancellationToken
                    ))
                )
        );

        var failed = indexChecks.Where(c => !c.Result.IsSuccess);
        foreach (var check in failed)
        {
            var (image, result) = check;

            _log.LogWarning
            (
                "Failed to index image from {Source} at {Link}: {Reason}",
                image.Source,
                image.Link,
                result.Error!.Message
            );
        }

        // Skip images that have already been indexed for whatever reason
        var alreadyIndexed = indexChecks.Where(c => c.Result.IsSuccess && c.Result.Entity);

        var alreadyIndexedReports = new List<StatusReport>();
        foreach (var check in alreadyIndexed)
        {
            var (image, _) = check;

            _log.LogWarning
            (
                "Skipping image from {Source} at {Link} (already indexed)",
                image.Source,
                image.Link
            );

            alreadyIndexedReports.Add(new StatusReport
            (
                DateTimeOffset.UtcNow,
                image.ServiceName,
                image.Source,
                image.Link,
                ImageStatus.Indexed,
                string.Empty
            ));
        }

        if (alreadyIndexedReports.Count > 0)
        {
            await _bus.PublishBatch(alreadyIndexedReports, context.CancellationToken);
        }

        // Index the new images
        var toIndex = indexChecks
            .Where(c => c.Result.IsSuccess && !c.Result.Entity)
            .Select(check => check.Image)
            .Select(image => new IndexedImage
            (
                image.ServiceName,
                DateTimeOffset.UtcNow,
                image.Source.ToString(),
                image.Link.ToString(),
                image.Signature.Signature,
                image.Signature.Words
            ))
            .ToList();

        if (toIndex.Count == 0)
        {
            return;
        }

        // Save to database
        var response = await _nestService.Client.IndexManyAsync(toIndex, "argus", context.CancellationToken);
        if (!response.IsValid)
        {
            var errorMessage = response.ServerError is not null
                ? response.ServerError.ToString() ?? "Unknown error"
                : response.OriginalException.Message;

            _log.LogWarning
            (
                "Failed to index an image batch: {Reason} ",
                errorMessage
            );

            return;
        }

        var statusReports = toIndex.Select(indexedImage => new StatusReport
        (
            DateTimeOffset.UtcNow,
            indexedImage.Service,
            new Uri(indexedImage.Source),
            new Uri(indexedImage.Link),
            ImageStatus.Indexed,
            string.Empty
        ));

        await _bus.PublishBatch(statusReports, context.CancellationToken);

        _log.LogInformation
        (
            "Indexed a batch of images ({Count})",
            batch.Length
        );
    }
}
