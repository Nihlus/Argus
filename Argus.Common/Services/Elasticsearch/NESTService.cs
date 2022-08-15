//
//  NESTService.cs
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Argus.Common.Results;
using Argus.Common.Services.Elasticsearch.Search;
using Nest;
using Puzzle;
using Remora.Results;
using Result = Remora.Results.Result;

namespace Argus.Common.Services.Elasticsearch;

/// <summary>
/// Represents an interface with Elasticsearch.
/// </summary>
public class NESTService
{
    /// <summary>
    /// Gets the Elasticsearch client associated with the service.
    /// </summary>
    public ElasticClient Client { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NESTService"/> class.
    /// </summary>
    /// <param name="client">The Elasticsearch client to use.</param>
    public NESTService(ElasticClient client)
    {
        this.Client = client;
    }

    /// <summary>
    /// Searches for an indexed image by its origin.
    /// </summary>
    /// <param name="source">The source URL of the origin.</param>
    /// <param name="link">The direct link to the image.</param>
    /// <param name="includeSource">Whether the source of the document should be included.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The search response.</returns>
    public async Task<ISearchResponse<IndexedImage>> SearchByOriginAsync
    (
        string source,
        string link,
        bool includeSource = false,
        CancellationToken ct = default
    )
    {
        return await this.Client.SearchAsync<IndexedImage>
        (
            q =>
            {
                var query = q.Index("argus").Query(q1 => q1.Bool(b => b.Must
                (
                    m => m.Term(ma => ma.Field(im => im.Source.Suffix("keyword")).Value(source)),
                    m => m.Term(ma => ma.Field(im => im.Link.Suffix("keyword")).Value(link))
                )));

                if (!includeSource)
                {
                    query.Source(m => m.ExcludeAll());
                }

                // never include words
                query.Source(m => m.Excludes(ed => ed.Field(im => im.Words)));

                return query;
            },
            ct
        );
    }

    /// <summary>
    /// Determines whether a fingerprinted image at the given origin has been indexed.
    /// </summary>
    /// <param name="source">The source link.</param>
    /// <param name="link">The image link.</param>
    /// <param name="signature">The signature.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>true if the image has been indexed; otherwise, false.</returns>
    public async Task<Result<bool>> IsIndexedAsync
    (
        string source,
        string link,
        LuminosityLevel[] signature,
        CancellationToken ct = default
    )
    {
        var existingImage = await SearchByOriginAsync(source, link, true, ct);
        if (!existingImage.IsValid)
        {
            return existingImage.ServerError is not null
                ? new ElasticsearchError(existingImage.ServerError)
                : existingImage.OriginalException;
        }

        return existingImage.Hits.Any(hit => hit.Source.Signature.SequenceEqual(signature));
    }

    /// <summary>
    /// Indexes the given image in Elasticsearch.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>true if the image was indexed; otherwise, false.</returns>
    public async Task<Result> IndexImageAsync(IndexedImage image, CancellationToken ct = default)
    {
        var checkIsIndexed = await IsIndexedAsync(image.Source, image.Link, image.Signature, ct);
        if (!checkIsIndexed.IsSuccess)
        {
            return Result.FromError(checkIsIndexed);
        }

        if (checkIsIndexed.Entity)
        {
            return Result.FromSuccess();
        }

        var response = await this.Client.IndexAsync(image, idx => idx.Index("argus"), ct);
        if (!response.IsValid)
        {
            return response.ServerError is not null
                ? new ElasticsearchError(response.ServerError)
                : response.OriginalException;
        }

        return Result.FromSuccess();
    }

    /// <summary>
    /// Searches the database for matching images.
    /// </summary>
    /// <param name="signature">The signature to search for.</param>
    /// <param name="after">The index after which the search should start returning results.</param>
    /// <param name="limit">The maximum number of records to return. Maximum 100.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async IAsyncEnumerable<SearchResult> SearchAsync
    (
        ImageSignature signature,
        uint after = 0,
        uint limit = 100,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        if (limit > 100)
        {
            limit = 100;
        }

        const int pageSize = 10;

        var foundResults = 0;
        while (foundResults < limit)
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            var offsetCopy = after;
            var searchResponse = await this.Client.SearchAsync<IndexedImage>
            (
                s => BuildQuery(s.From((int)offsetCopy).Size(pageSize), signature.Words), ct
            ).ConfigureAwait(false);

            if (!searchResponse.IsValid)
            {
                throw new InvalidOperationException(searchResponse.DebugInformation);
            }

            if (searchResponse.Documents.Count == 0)
            {
                yield break;
            }

            foreach (var hit in searchResponse.Documents)
            {
                if (ct.IsCancellationRequested)
                {
                    yield break;
                }

                var similarity = signature.Signature.CompareTo(hit.Signature);

                if (similarity is not (SignatureSimilarity.Same or SignatureSimilarity.Identical))
                {
                    yield break;
                }

                var imageInformation = new ImageInformation
                (
                    hit.IndexedAt,
                    hit.Service,
                    new Uri(hit.Source),
                    new Uri(hit.Link)
                );

                yield return new SearchResult(similarity, imageInformation);

                ++foundResults;
            }

            after += pageSize;
        }
    }

    private ISearchRequest BuildQuery(SearchDescriptor<IndexedImage> q, SignatureWords searchWords)
    {
        q = q.Index("argus").Query(qd => qd.Bool(b => b.Should
        (
            s => s.Term(t => t.Field(i => i.Words.Word1).Value(searchWords.Word1)),
            s => s.Term(t => t.Field(i => i.Words.Word2).Value(searchWords.Word2)),
            s => s.Term(t => t.Field(i => i.Words.Word3).Value(searchWords.Word3)),
            s => s.Term(t => t.Field(i => i.Words.Word4).Value(searchWords.Word4)),
            s => s.Term(t => t.Field(i => i.Words.Word5).Value(searchWords.Word5)),
            s => s.Term(t => t.Field(i => i.Words.Word6).Value(searchWords.Word6)),
            s => s.Term(t => t.Field(i => i.Words.Word7).Value(searchWords.Word7)),
            s => s.Term(t => t.Field(i => i.Words.Word8).Value(searchWords.Word8)),
            s => s.Term(t => t.Field(i => i.Words.Word9).Value(searchWords.Word9)),
            s => s.Term(t => t.Field(i => i.Words.Word10).Value(searchWords.Word10)),
            s => s.Term(t => t.Field(i => i.Words.Word11).Value(searchWords.Word11)),
            s => s.Term(t => t.Field(i => i.Words.Word12).Value(searchWords.Word12)),
            s => s.Term(t => t.Field(i => i.Words.Word13).Value(searchWords.Word13)),
            s => s.Term(t => t.Field(i => i.Words.Word14).Value(searchWords.Word14)),
            s => s.Term(t => t.Field(i => i.Words.Word15).Value(searchWords.Word15)),
            s => s.Term(t => t.Field(i => i.Words.Word16).Value(searchWords.Word16)),
            s => s.Term(t => t.Field(i => i.Words.Word17).Value(searchWords.Word17)),
            s => s.Term(t => t.Field(i => i.Words.Word18).Value(searchWords.Word18)),
            s => s.Term(t => t.Field(i => i.Words.Word19).Value(searchWords.Word19)),
            s => s.Term(t => t.Field(i => i.Words.Word20).Value(searchWords.Word20)),
            s => s.Term(t => t.Field(i => i.Words.Word21).Value(searchWords.Word21)),
            s => s.Term(t => t.Field(i => i.Words.Word22).Value(searchWords.Word22)),
            s => s.Term(t => t.Field(i => i.Words.Word23).Value(searchWords.Word23)),
            s => s.Term(t => t.Field(i => i.Words.Word24).Value(searchWords.Word24)),
            s => s.Term(t => t.Field(i => i.Words.Word25).Value(searchWords.Word25)),
            s => s.Term(t => t.Field(i => i.Words.Word26).Value(searchWords.Word26)),
            s => s.Term(t => t.Field(i => i.Words.Word27).Value(searchWords.Word27)),
            s => s.Term(t => t.Field(i => i.Words.Word28).Value(searchWords.Word28)),
            s => s.Term(t => t.Field(i => i.Words.Word29).Value(searchWords.Word29)),
            s => s.Term(t => t.Field(i => i.Words.Word30).Value(searchWords.Word30)),
            s => s.Term(t => t.Field(i => i.Words.Word31).Value(searchWords.Word31)),
            s => s.Term(t => t.Field(i => i.Words.Word32).Value(searchWords.Word32)),
            s => s.Term(t => t.Field(i => i.Words.Word33).Value(searchWords.Word33)),
            s => s.Term(t => t.Field(i => i.Words.Word34).Value(searchWords.Word34)),
            s => s.Term(t => t.Field(i => i.Words.Word35).Value(searchWords.Word35)),
            s => s.Term(t => t.Field(i => i.Words.Word36).Value(searchWords.Word36)),
            s => s.Term(t => t.Field(i => i.Words.Word37).Value(searchWords.Word37)),
            s => s.Term(t => t.Field(i => i.Words.Word38).Value(searchWords.Word38)),
            s => s.Term(t => t.Field(i => i.Words.Word39).Value(searchWords.Word39)),
            s => s.Term(t => t.Field(i => i.Words.Word40).Value(searchWords.Word40)),
            s => s.Term(t => t.Field(i => i.Words.Word41).Value(searchWords.Word41)),
            s => s.Term(t => t.Field(i => i.Words.Word42).Value(searchWords.Word42)),
            s => s.Term(t => t.Field(i => i.Words.Word43).Value(searchWords.Word43)),
            s => s.Term(t => t.Field(i => i.Words.Word44).Value(searchWords.Word44)),
            s => s.Term(t => t.Field(i => i.Words.Word45).Value(searchWords.Word45)),
            s => s.Term(t => t.Field(i => i.Words.Word46).Value(searchWords.Word46)),
            s => s.Term(t => t.Field(i => i.Words.Word47).Value(searchWords.Word47)),
            s => s.Term(t => t.Field(i => i.Words.Word48).Value(searchWords.Word48)),
            s => s.Term(t => t.Field(i => i.Words.Word49).Value(searchWords.Word49)),
            s => s.Term(t => t.Field(i => i.Words.Word50).Value(searchWords.Word50)),
            s => s.Term(t => t.Field(i => i.Words.Word51).Value(searchWords.Word51)),
            s => s.Term(t => t.Field(i => i.Words.Word52).Value(searchWords.Word52)),
            s => s.Term(t => t.Field(i => i.Words.Word53).Value(searchWords.Word53)),
            s => s.Term(t => t.Field(i => i.Words.Word54).Value(searchWords.Word54)),
            s => s.Term(t => t.Field(i => i.Words.Word55).Value(searchWords.Word55)),
            s => s.Term(t => t.Field(i => i.Words.Word56).Value(searchWords.Word56)),
            s => s.Term(t => t.Field(i => i.Words.Word57).Value(searchWords.Word57)),
            s => s.Term(t => t.Field(i => i.Words.Word58).Value(searchWords.Word58)),
            s => s.Term(t => t.Field(i => i.Words.Word59).Value(searchWords.Word59)),
            s => s.Term(t => t.Field(i => i.Words.Word60).Value(searchWords.Word60)),
            s => s.Term(t => t.Field(i => i.Words.Word61).Value(searchWords.Word61)),
            s => s.Term(t => t.Field(i => i.Words.Word62).Value(searchWords.Word62)),
            s => s.Term(t => t.Field(i => i.Words.Word63).Value(searchWords.Word63))
        )));

        q = q.Source(so => so.Excludes(fs => fs.Field(i => i.Words)));

        return q;
    }
}
