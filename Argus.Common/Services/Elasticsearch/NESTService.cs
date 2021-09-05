//
//  NESTService.cs
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Argus.Common.Results;
using Argus.Common.Services.Elasticsearch.Search;
using Nest;
using Puzzle;
using Result = Remora.Results.Result;

namespace Argus.Common.Services.Elasticsearch
{
    /// <summary>
    /// Represents an interface with Elasticsearch.
    /// </summary>
    public class NESTService
    {
        private readonly ElasticClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="NESTService"/> class.
        /// </summary>
        /// <param name="client">The Elasticsearch client to use.</param>
        public NESTService(ElasticClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Indexes the given image in Elasticsearch.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>true if the image was indexed; otherwise, false.</returns>
        public async Task<Result> IndexImageAsync(IndexedImage image, CancellationToken ct = default)
        {
            var existingImage = await _client.SearchAsync<IndexedImage>
            (
                q => q.Index("images").Query
                (
                    q1 => q1.Bool
                    (
                        b => b
                            .Must(m => m.Match(ma => ma.Field(im => im.Source).Query(image.Source)))
                            .Must(m => m.Match(ma => ma.Field(im => im.Link).Query(image.Link)))
                    )
                ),
                ct
            );

            if (!existingImage.IsValid)
            {
                return existingImage.ServerError is not null
                    ? new ElasticsearchError(existingImage.ServerError)
                    : existingImage.OriginalException;
            }

            if (existingImage.Hits.Any())
            {
                if (existingImage.Hits.Any(hit => hit.Source.Signature.SequenceEqual(image.Signature)))
                {
                    // It's already indexed, so it's fine
                    return Result.FromSuccess();
                }
            }

            var response = await _client.IndexAsync(image, idx => idx.Index("images"), ct);
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
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    yield break;
                }

                var offsetCopy = after;
                var searchResponse = await _client.SearchAsync<IndexedImage>
                (
                    s => BuildQuery(s.From((int)offsetCopy).Size(pageSize), signature.Words), ct
                ).ConfigureAwait(false);

                var stillFindingResults = false;
                foreach (var hit in searchResponse.Documents)
                {
                    if (ct.IsCancellationRequested)
                    {
                        yield break;
                    }

                    var similarity = signature.Signature.CompareTo(hit.Signature);

                    if (similarity is not (SignatureSimilarity.Similar or SignatureSimilarity.Identical))
                    {
                        continue;
                    }

                    var imageInformation = new ImageInformation
                    (
                        hit.IndexedAt,
                        hit.Service,
                        new Uri(hit.Source),
                        new Uri(hit.Link)
                    );

                    stillFindingResults = true;
                    yield return new SearchResult(similarity, imageInformation);

                    ++foundResults;
                    if (foundResults >= limit)
                    {
                        yield break;
                    }
                }

                if (!stillFindingResults)
                {
                    yield break;
                }

                after += pageSize;
            }
        }

        private ISearchRequest BuildQuery(SearchDescriptor<IndexedImage> q, int[] searchWords)
        {
            q = q.Index("images")
                .Query(query =>
                {
                    return query.Bool(boolQuery =>
                    {
                        boolQuery.Should(shouldQuery =>
                        {
                            return searchWords[1..].Aggregate
                            (
                                shouldQuery.Term(t => t.Field(i => i.Words).Value(searchWords[0])),
                                (current, word) => current || shouldQuery.Term(t => t.Field(i => i.Words).Value(word))
                            );
                        });

                        return boolQuery;
                    });
                });

            q = q.Source(so => so.Excludes(fs => fs.Field(i => i.Words)));

            return q;
        }
    }
}
