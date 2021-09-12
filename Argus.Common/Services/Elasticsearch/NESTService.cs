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
            return await _client.SearchAsync<IndexedImage>
            (
                q =>
                {
                    var query = q.Index("argus").Query
                    (
                        q1 => q1.Bool
                        (
                            b => b
                                .Must(m => m.Term(ma => ma.Field(im => im.Source.Suffix("keyword")).Value(source)))
                                .Must(m => m.Term(ma => ma.Field(im => im.Link.Suffix("keyword")).Value(link)))
                        )
                    );

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
        /// Indexes the given image in Elasticsearch.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>true if the image was indexed; otherwise, false.</returns>
        public async Task<Result> IndexImageAsync(IndexedImage image, CancellationToken ct = default)
        {
            var existingImage = await SearchByOriginAsync(image.Source, image.Link, true, ct);
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

            var response = await _client.IndexAsync(image, idx => idx.Index("argus"), ct);
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
                    if (foundResults >= limit)
                    {
                        yield break;
                    }
                }

                after += pageSize;
            }
        }

        private ISearchRequest BuildQuery(SearchDescriptor<IndexedImage> q, SignatureWords searchWords)
        {
            q = q.Index("argus").Query
            (
                qd => qd.Bool
                (
                    b => b
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word1).Value(searchWords.Word1)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word2).Value(searchWords.Word2)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word3).Value(searchWords.Word3)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word4).Value(searchWords.Word4)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word5).Value(searchWords.Word5)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word6).Value(searchWords.Word6)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word7).Value(searchWords.Word7)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word8).Value(searchWords.Word8)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word9).Value(searchWords.Word9)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word10).Value(searchWords.Word10)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word11).Value(searchWords.Word11)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word12).Value(searchWords.Word12)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word13).Value(searchWords.Word13)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word14).Value(searchWords.Word14)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word15).Value(searchWords.Word15)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word16).Value(searchWords.Word16)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word17).Value(searchWords.Word17)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word18).Value(searchWords.Word18)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word19).Value(searchWords.Word19)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word20).Value(searchWords.Word20)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word21).Value(searchWords.Word21)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word22).Value(searchWords.Word22)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word23).Value(searchWords.Word23)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word24).Value(searchWords.Word24)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word25).Value(searchWords.Word25)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word26).Value(searchWords.Word26)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word27).Value(searchWords.Word27)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word28).Value(searchWords.Word28)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word29).Value(searchWords.Word29)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word30).Value(searchWords.Word30)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word31).Value(searchWords.Word31)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word32).Value(searchWords.Word32)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word33).Value(searchWords.Word33)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word34).Value(searchWords.Word34)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word35).Value(searchWords.Word35)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word36).Value(searchWords.Word36)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word37).Value(searchWords.Word37)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word38).Value(searchWords.Word38)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word39).Value(searchWords.Word39)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word40).Value(searchWords.Word40)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word41).Value(searchWords.Word41)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word42).Value(searchWords.Word42)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word43).Value(searchWords.Word43)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word44).Value(searchWords.Word44)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word45).Value(searchWords.Word45)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word46).Value(searchWords.Word46)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word47).Value(searchWords.Word47)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word48).Value(searchWords.Word48)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word49).Value(searchWords.Word49)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word50).Value(searchWords.Word50)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word51).Value(searchWords.Word51)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word52).Value(searchWords.Word52)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word53).Value(searchWords.Word53)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word54).Value(searchWords.Word54)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word55).Value(searchWords.Word55)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word56).Value(searchWords.Word56)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word57).Value(searchWords.Word57)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word58).Value(searchWords.Word58)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word59).Value(searchWords.Word59)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word60).Value(searchWords.Word60)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word61).Value(searchWords.Word61)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word62).Value(searchWords.Word62)))
                        .Should(s => s.Term(t => t.Field(i => i.Words.Word63).Value(searchWords.Word63)))
                )
            );

            q = q.Source(so => so.Excludes(fs => fs.Field(i => i.Words)));

            return q;
        }
    }
}
