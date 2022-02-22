//
//  FurAffinityCollectorService.cs
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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.FurAffinity.API;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using MassTransit.MessageData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.FurAffinity.Services;

/// <summary>
/// Collects images from FurAffinity.
/// </summary>
public class FurAffinityCollectorService : CollectorService
{
    /// <inheritdoc />
    protected override string ServiceName => "furaffinity";

    private readonly FurAffinityAPI _furAffinityAPI;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FurAffinityCollectorService> _log;
    private readonly IMessageDataRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="FurAffinityCollectorService"/> class.
    /// </summary>
    /// <param name="furAffinityAPI">The FurAffinity API.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="repository">The data repository.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="options">The application options.</param>
    /// <param name="log">The logging instance.</param>
    public FurAffinityCollectorService
    (
        FurAffinityAPI furAffinityAPI,
        IHttpClientFactory httpClientFactory,
        IMessageDataRepository repository,
        IBus bus,
        IOptions<CollectorOptions> options,
        ILogger<FurAffinityCollectorService> log
    )
        : base
        (
            bus,
            options,
            log
        )
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _log = log;
        _furAffinityAPI = furAffinityAPI;
    }

    /// <inheritdoc/>
    protected override async Task<Result> CollectAsync(CancellationToken ct = default)
    {
        var getResume = await GetResumePointAsync(ct);
        if (!getResume.IsSuccess)
        {
            _log.LogWarning("Failed to get the resume point: {Reason}", getResume.Error.Message);
            return Result.FromError(getResume);
        }

        var resumePoint = getResume.Entity;
        if (!ulong.TryParse(resumePoint, out var currentSubmissionID))
        {
            currentSubmissionID = 0;
        }

        ulong? latestSubmissionID = null;

        while (!ct.IsCancellationRequested)
        {
            if (currentSubmissionID >= latestSubmissionID || latestSubmissionID is null)
            {
                var getLatestID = await _furAffinityAPI.GetMostRecentSubmissionIDAsync(ct);
                if (!getLatestID.IsSuccess)
                {
                    return Result.FromError(getLatestID);
                }

                latestSubmissionID = getLatestID.Entity;
                if (currentSubmissionID >= latestSubmissionID)
                {
                    _log.LogInformation("Waiting for new submissions to come in...");
                    await Task.Delay(TimeSpan.FromHours(1), ct);
                    continue;
                }
            }

            var setResume = await SetResumePointAsync(currentSubmissionID.ToString(), ct);
            if (!setResume.IsSuccess)
            {
                return setResume;
            }

            var client = _httpClientFactory.CreateClient("BulkDownload");

            var collections = new List<Task<Result<(StatusReport Report, CollectedImage? Image)>>>();
            for
            (
                var submissionID = currentSubmissionID;
                submissionID < currentSubmissionID + 25 && submissionID < latestSubmissionID;
                ++submissionID
            )
            {
                collections.Add(CollectImageAsync(client, submissionID, ct));
            }

            var collectedImages = await Task.WhenAll(collections);

            foreach (var collection in collectedImages)
            {
                if (!collection.IsSuccess)
                {
                    _log.LogWarning("Failed to collect image: {Reason}", collection.Error.Message);

                    if (collection.Error is InvalidOperationError)
                    {
                        return Result.FromError(collection);
                    }

                    continue;
                }

                var (statusReport, collectedImage) = collection.Entity;

                var report = await PushStatusReportAsync(statusReport, ct);
                if (!report.IsSuccess)
                {
                    _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
                    return report;
                }

                if (collectedImage is null)
                {
                    continue;
                }

                var push = await PushCollectedImageAsync(collectedImage, ct);
                if (push.IsSuccess)
                {
                    continue;
                }

                _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                return push;
            }

            currentSubmissionID += 25;
        }

        return Result.FromSuccess();
    }

    private async Task<Result<(StatusReport Report, CollectedImage? Image)>> CollectImageAsync
    (
        HttpClient client,
        ulong submissionID,
        CancellationToken ct = default
    )
    {
        try
        {
            var source = new Uri($"https://www.furaffinity.net/view/{submissionID}");
            var imageSource = new ImageSource
            (
                this.ServiceName,
                source,
                DateTimeOffset.UtcNow,
                submissionID.ToString()
            );

            var pushSource = await PushImageSourceAsync(imageSource, ct);
            if (!pushSource.IsSuccess)
            {
                return Result<(StatusReport Report, CollectedImage? Image)>.FromError(pushSource);
            }

            var statusReport = new StatusReport
            (
                DateTimeOffset.UtcNow,
                this.ServiceName,
                source,
                new Uri("about:blank"),
                ImageStatus.Collected,
                string.Empty
            );

            var getLink = await _furAffinityAPI.GetSubmissionDownloadLinkAsync(submissionID, ct);
            if (!getLink.IsSuccess)
            {
                if (getLink.Error is not NotFoundError)
                {
                    return Result<(StatusReport Report, CollectedImage? Image)>.FromError(getLink);
                }

                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "No file"
                };

                return (rejectionReport, null);
            }

            var location = getLink.Entity;
            var file = location.ToString();
            if (string.IsNullOrWhiteSpace(file))
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "No file"
                };

                return (rejectionReport, null);
            }

            var fileExtension = Path.GetExtension(file);
            if (fileExtension is not (".jpg" or ".jpeg" or ".png"))
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Link = new Uri(file),
                    Message = "Unsupported media"
                };

                return (rejectionReport, null);
            }

            statusReport = statusReport with
            {
                Link = new Uri(file)
            };

            var bytes = await client.GetByteArrayAsync(file, ct);

            var collectedImage = new CollectedImage
            (
                this.ServiceName,
                statusReport.Source,
                statusReport.Link,
                await _repository.PutBytes(bytes, TimeSpan.FromHours(8), ct)
            );

            return (statusReport, collectedImage);
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
