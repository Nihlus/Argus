//
//  WeasylCollectorService.cs
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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.Weasyl.API;
using Argus.Collector.Weasyl.API.Model;
using Argus.Collector.Weasyl.Configuration;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using MassTransit.MessageData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.Weasyl.Services;

/// <summary>
/// Collects images from Weasyl.
/// </summary>
public class WeasylCollectorService : CollectorService
{
    private readonly WeasylOptions _options;
    private readonly WeasylAPI _weasylAPI;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeasylCollectorService> _log;
    private readonly IMessageDataRepository _repository;

    /// <inheritdoc />
    protected override string ServiceName => "weasyl";

    /// <summary>
    /// Initializes a new instance of the <see cref="WeasylCollectorService"/> class.
    /// </summary>
    /// <param name="weasylOptions">The Weasyl options.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="repository">The message data repository.</param>
    /// <param name="options">The application options.</param>
    /// <param name="weasylAPI">The weasyl weasylAPI.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="log">The logging instance.</param>
    public WeasylCollectorService
    (
        IOptions<WeasylOptions> weasylOptions,
        WeasylAPI weasylAPI,
        IHttpClientFactory httpClientFactory,
        IBus bus,
        IMessageDataRepository repository,
        IOptions<CollectorOptions> options,
        ILogger<WeasylCollectorService> log)
        : base(bus, options, log)
    {
        _options = weasylOptions.Value;
        _log = log;
        _repository = repository;
        _weasylAPI = weasylAPI;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    protected override async Task<Result> CollectAsync(CancellationToken ct = default)
    {
        var getResume = await GetResumePointAsync(ct);
        if (!getResume.IsSuccess)
        {
            _log.LogWarning("Failed to get the resume point: {Reason}", getResume.Error.Message);
            return Result.FromError(getResume);
        }

        var resumePoint = getResume.Entity;
        if (!int.TryParse(resumePoint, out var currentSubmissionID))
        {
            currentSubmissionID = 0;
        }

        int? latestSubmissionID = null;

        while (!ct.IsCancellationRequested)
        {
            if (currentSubmissionID >= latestSubmissionID || latestSubmissionID is null)
            {
                var getFrontpage = await _weasylAPI.GetFrontpageAsync(ct: ct);
                if (!getFrontpage.IsSuccess)
                {
                    return Result.FromError(getFrontpage);
                }

                var frontpage = getFrontpage.Entity;
                if (frontpage.Count > 0)
                {
                    latestSubmissionID = frontpage[0].SubmitID;
                }

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

            var ids = Enumerable.Range(currentSubmissionID, _options.PageSize);
            var getSubmissions = await Task.WhenAll(ids.Select(i => _weasylAPI.GetSubmissionAsync(i, ct)));

            var submissions = new List<WeasylSubmission>();
            foreach (var getSubmission in getSubmissions)
            {
                if (getSubmission.IsSuccess)
                {
                    submissions.Add(getSubmission.Entity);
                    continue;
                }

                if (getSubmission.Error is not NotFoundError or WeasylError { StatusCode: HttpStatusCode.NotFound })
                {
                    _log.LogWarning
                    (
                        "Failed to get data for submission {ID}: {Reason}",
                        currentSubmissionID,
                        getSubmission.Error.Message
                    );
                }
            }

            var client = _httpClientFactory.CreateClient("BulkDownload");

            var collections = await Task.WhenAll(submissions.Select(s => CollectImageAsync(client, s, ct)));
            foreach (var collection in collections)
            {
                if (!collection.IsSuccess)
                {
                    _log.LogWarning("Failed to collect image: {Reason}", collection.Error.Message);
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

            currentSubmissionID += _options.PageSize;
        }

        return Result.FromSuccess();
    }

    private async Task<Result<(StatusReport Report, CollectedImage? Image)>> CollectImageAsync
    (
        HttpClient client,
        WeasylSubmission submission,
        CancellationToken ct = default
    )
    {
        try
        {
            var imageSource = new ImageSource
            (
                this.ServiceName,
                new Uri(submission.Link),
                DateTimeOffset.UtcNow,
                submission.SubmitID.ToString()
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
                new Uri(submission.Link),
                new Uri("about:blank"),
                ImageStatus.Collected,
                string.Empty
            );

            if (submission.Subtype is not "visual")
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "Unsupported media type"
                };

                return (rejectionReport, null);
            }

            if (!submission.Media.TryGetValue("submission", out var media) || media.Count == 0)
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "No file"
                };

                return (rejectionReport, null);
            }

            var supportedMedia = media.FirstOrDefault
            (
                m =>
                    m.URL.EndsWith("jpeg") ||
                    m.URL.EndsWith("jpg") ||
                    m.URL.EndsWith("png") ||
                    m.URL.EndsWith("bmp") ||
                    m.URL.EndsWith("tga")
            );

            if (supportedMedia is null)
            {
                var rejectionReport = statusReport with
                {
                    Status = ImageStatus.Rejected,
                    Message = "Unsupported media type"
                };

                return (rejectionReport, null);
            }

            var location = supportedMedia.URL;
            var bytes = await client.GetByteArrayAsync(location, ct);

            statusReport = statusReport with
            {
                Link = new Uri(supportedMedia.URL)
            };

            var collectedImage = new CollectedImage
            (
                this.ServiceName,
                new Uri(submission.Link),
                new Uri(location),
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
