//
//  FListCollectorService.cs
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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Services;
using Argus.Collector.FList.API;
using Argus.Collector.FList.API.Model;
using Argus.Common;
using Argus.Common.Messages.BulkData;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Results;

namespace Argus.Collector.FList.Services;

/// <summary>
/// Collects images from F-List.
/// </summary>
public class FListCollectorService : CollectorService
{
    private readonly FListAPI _flistAPI;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FListCollectorService> _log;
    private readonly IMessageDataRepository _repository;

    /// <inheritdoc />
    protected override string ServiceName => "f-list";

    /// <summary>
    /// Initializes a new instance of the <see cref="FListCollectorService"/> class.
    /// </summary>
    /// <param name="flistAPI">The F-List API.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="repository">The data repository.</param>
    /// <param name="bus">The message bus.</param>
    /// <param name="options">The application options.</param>
    /// <param name="log">The logging instance.</param>
    public FListCollectorService
    (
        FListAPI flistAPI,
        IHttpClientFactory httpClientFactory,
        IMessageDataRepository repository,
        IBus bus,
        IOptions<CollectorOptions> options,
        ILogger<FListCollectorService> log)
        : base(bus, options, log)
    {
        _log = log;
        _repository = repository;
        _flistAPI = flistAPI;
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
        if (!int.TryParse(resumePoint, out var currentCharacterId))
        {
            currentCharacterId = 0;
        }

        int? latestCharacterId = null;

        while (!ct.IsCancellationRequested)
        {
            if (currentCharacterId >= latestCharacterId || latestCharacterId is null)
            {
                var getLatestName = _flistAPI.GetMostRecentlyCreatedCharacter();
                if (!getLatestName.IsSuccess)
                {
                    return Result.FromError(getLatestName);
                }

                var getLatestCharacter = await _flistAPI.GetCharacterDataAsync(getLatestName.Entity, ct);
                if (!getLatestCharacter.IsSuccess)
                {
                    return Result.FromError(getLatestCharacter);
                }

                latestCharacterId = getLatestCharacter.Entity.Id;
                if (currentCharacterId >= latestCharacterId)
                {
                    _log.LogInformation("No new characters. Revisiting some old stuff while we wait...");

                    var then = DateTimeOffset.UtcNow;
                    while (DateTimeOffset.UtcNow - then < TimeSpan.FromHours(1))
                    {
                        var requestRevisits = await RequestRevisitsAsync(ct: ct);
                        if (!requestRevisits.IsSuccess)
                        {
                            return (Result)requestRevisits;
                        }

                        var revisits = requestRevisits.Entity.Sources;
                        if (revisits.Count <= 0)
                        {
                            await Task.Delay(TimeSpan.FromMinutes(15), ct);
                            continue;
                        }

                        foreach (var (source, reports) in requestRevisits.Entity.Sources)
                        {
                            var revisitExistingCharacter = await RevisitExistingCharacterAsync(source, reports, ct);
                            if (!revisitExistingCharacter.IsSuccess)
                            {
                                return revisitExistingCharacter;
                            }
                        }
                    }

                    continue;
                }
            }

            var collectNewCharacter = await CollectNewCharacterAsync(currentCharacterId, ct);
            if (!collectNewCharacter.IsSuccess)
            {
                return (Result)collectNewCharacter;
            }

            currentCharacterId = collectNewCharacter.Entity;
        }

        return Result.FromSuccess();
    }

    private async Task<Result<int>> CollectNewCharacterAsync(int characterId, CancellationToken ct = default)
    {
        var setResume = await SetResumePointAsync(characterId.ToString(), ct);
        if (!setResume.IsSuccess)
        {
            return Result<int>.FromError(setResume);
        }

        var collectImages = await CollectImagesAsync(characterId, ct);
        if (collectImages.Error is NotFoundError)
        {
            return characterId + 1;
        }

        if (!collectImages.IsSuccess)
        {
            return Result<int>.FromError(collectImages);
        }

        var (imageSource, collectedImages) = collectImages.Entity;

        var pushSource = await PushImageSourceAsync(imageSource, ct);
        if (!pushSource.IsSuccess)
        {
            return Result<int>.FromError(pushSource);
        }

        if (collectedImages.Count <= 0)
        {
            return characterId + 1;
        }

        foreach (var collectedImage in collectedImages)
        {
            var statusReport = new StatusReport
            (
                DateTimeOffset.UtcNow,
                this.ServiceName,
                collectedImage.Source,
                collectedImage.Link,
                ImageStatus.Collected,
                string.Empty
            );

            var report = await PushStatusReportAsync(statusReport, ct);
            if (!report.IsSuccess)
            {
                _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
                return Result<int>.FromError(report);
            }

            var push = await PushCollectedImageAsync(collectedImage, ct);
            if (!push.IsSuccess)
            {
                _log.LogWarning("Failed to push collected image: {Reason}", push.Error.Message);
                return Result<int>.FromError(push);
            }
        }

        return characterId + 1;
    }

    private async Task<Result> RevisitExistingCharacterAsync
    (
        ImageSource source,
        IReadOnlyList<StatusReport> images,
        CancellationToken ct = default
    )
    {
        if (!int.TryParse(source.SourceIdentifier, out var characterId))
        {
            throw new InvalidOperationException("Bad data in the database?");
        }

        ImageSource updatedSource;
        IReadOnlyList<StatusReport> updatedImages;
        IReadOnlyList<CollectedImage> collectedImages;

        var collectImages = await CollectImagesAsync(characterId, ct);
        if (collectImages.Error is NotFoundError)
        {
            updatedImages = images.Select(i => i with { Status = ImageStatus.Deleted }).ToList();
            updatedSource = source with
            {
                RevisitCount = source.RevisitCount + 1,
                LastRevisitedAt = DateTimeOffset.UtcNow
            };
            collectedImages = Array.Empty<CollectedImage>();
        }
        else
        {
            if (!collectImages.IsSuccess)
            {
                return (Result)collectImages;
            }

            (var imageSource, collectedImages) = collectImages.Entity;

            // Update the image records
            var newImages = collectedImages.Select(c => new StatusReport
            (
                DateTimeOffset.UtcNow,
                this.ServiceName,
                c.Source,
                c.Link,
                ImageStatus.Collected,
                string.Empty
            )).ToList();

            var finalImages = new List<StatusReport>();

            // Filter out deleted images
            foreach (var oldImage in images)
            {
                if (newImages.All(i => i.Link != oldImage.Link))
                {
                    finalImages.Add(oldImage with { Status = ImageStatus.Deleted });
                }
            }

            // Add in the existing or new images
            foreach (var newImage in newImages)
            {
                var existingImage = images.FirstOrDefault(i => i.Link == newImage.Link);
                if (existingImage is not null)
                {
                    switch (existingImage.Status)
                    {
                        case ImageStatus.Indexed or ImageStatus.Collected:
                        {
                            finalImages.Add(existingImage);
                            break;
                        }
                        case ImageStatus.Deleted: // it's come back?
                        case var _ when existingImage.Status != newImage.Status:
                        {
                            finalImages.Add(newImage);
                            break;
                        }
                        default:
                        {
                            throw new ArgumentOutOfRangeException(nameof(existingImage));
                        }
                    }
                }
                else
                {
                    finalImages.Add(newImage);
                }
            }

            updatedImages = finalImages;
            updatedSource = imageSource with
            {
                RevisitCount = imageSource.RevisitCount + 1,
                LastRevisitedAt = DateTimeOffset.UtcNow
            };
        }

        var pushSource = await PushImageSourceAsync(updatedSource, ct);
        if (!pushSource.IsSuccess)
        {
            return pushSource;
        }

        foreach (var updatedImage in updatedImages)
        {
            if (images.Contains(updatedImage))
            {
                // No need to send out identical reports
                continue;
            }

            var report = await PushStatusReportAsync(updatedImage, ct);
            if (report.IsSuccess)
            {
                continue;
            }

            _log.LogWarning("Failed to push status report: {Reason}", report.Error.Message);
            return report;
        }

        foreach (var collectedImage in collectedImages)
        {
            if (images.Any(i => i.Link == collectedImage.Link && i.Status is ImageStatus.Indexed or ImageStatus.Collected))
            {
                // No need to redownload existing images
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

        return Result.FromSuccess();
    }

    private async Task<Result<(ImageSource Source, IReadOnlyList<CollectedImage> Images)>> CollectImagesAsync
    (
        int characterId,
        CancellationToken ct = default
    )
    {
        async Task<Result<CollectedImage>> CollectImageAsync
        (
            HttpClient client,
            Uri characterLink,
            CharacterImage image
        )
        {
            try
            {
                var location = $"https://static.f-list.net/images/charimage/{image.ImageId}.{image.Extension}";
                var bytes = await client.GetByteArrayAsync(location, ct);

                return new CollectedImage
                (
                    this.ServiceName,
                    characterLink,
                    new Uri(location),
                    await _repository.PutBytes(bytes, TimeSpan.FromHours(8), ct)
                );
            }
            catch (Exception e)
            {
                return e;
            }
        }

        var getCharacter = await _flistAPI.GetCharacterDataAsync(characterId, ct);
        if (!getCharacter.IsSuccess)
        {
            if (!getCharacter.Error.Message.Contains("Character not found"))
            {
                _log.LogWarning
                (
                    "Failed to get data for character {ID}: {Reason}",
                    characterId,
                    getCharacter.Error.Message
                );
            }

            return new NotFoundError("Character not found.");
        }

        var character = getCharacter.Entity;
        var characterLink = new Uri($"https://www.f-list.net/c/{character.Name}");

        var imageSource = new ImageSource
        (
            this.ServiceName,
            characterLink,
            DateTimeOffset.UtcNow,
            characterId.ToString()
        );

        if (character.Images.Count <= 0)
        {
            return (imageSource, Array.Empty<CollectedImage>());
        }

        var client = _httpClientFactory.CreateClient("BulkDownload");

        var collections = character.Images.Select(i => CollectImageAsync(client, characterLink, i));
        var imageCollections = await Task.WhenAll(collections);

        var images = new List<CollectedImage>();
        foreach (var imageCollection in imageCollections)
        {
            if (!imageCollection.IsSuccess)
            {
                _log.LogWarning("Failed to collect image: {Reason}", imageCollection.Error.Message);
                continue;
            }

            images.Add(imageCollection.Entity);
        }

        return (imageSource, images);
    }
}
