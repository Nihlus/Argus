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
using System.Threading.Tasks;
using Nest;
using Puzzle;

namespace ImageScraper.Services.Elasticsearch
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

        private ISearchRequest BuildQuery(SearchDescriptor<IndexedImage> q, IEnumerable<int> searchWords)
        {
            q = q.Index("images")
                .Query(qu =>
                {
                    return qu.Bool(bu =>
                    {
                        bu.Should(sh =>
                        {
                            var composite = sh.Term(t => t.Field(i => i.Words).Value(searchWords.First()));
                            foreach (var searchWord in searchWords.Skip(1))
                            {
                                composite = composite || sh.Term(t => t.Field(i => i.Words).Value(searchWord));
                            }

                            return composite;
                        });

                        return bu;
                    });
                });

            q = q.Source(so => so.Excludes(fs => fs.Field(i => i.Words)));

            return q;
        }

        /// <summary>
        /// Indexes the given image in Elasticsearch.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns>true if the image was indexed; otherwise, false.</returns>
        public async Task<bool> IndexImageAsync(IndexedImage image)
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
                )
            );

            if (existingImage.ServerError is not null)
            {
                return false;
            }

            if (existingImage.Hits.Any())
            {
                if (existingImage.Hits.Any(hit => hit.Source.Signature.SequenceEqual(image.Signature)))
                {
                    // It's already indexed, so it's fine
                    return true;
                }
            }

            var response = await _client.IndexAsync(image, idx => idx.Index("images"));
            return response.IsValid;
        }

        /// <summary>
        /// Searches the database for matching images.
        /// </summary>
        /// <param name="signature">The signature to search for.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<IReadOnlyCollection<(SignatureSimilarity Similarity, IndexedImage Image)>> SearchAsync
        (
            ImageSignature signature
        )
        {
            var hits = new List<(SignatureSimilarity, IndexedImage)>();

            var offset = 0;
            var hasRelevantResult = true;
            while (hasRelevantResult)
            {
                var offsetCopy = offset;
                var searchResponse = await _client.SearchAsync<IndexedImage>
                (
                    s => BuildQuery(s.From(offsetCopy).Size(8), signature.Words)
                ).ConfigureAwait(false);

                var signatureArray = signature.Signature.ToArray();
                var dists = searchResponse.Documents
                    .Select
                    (
                        d =>
                        {
                            var left = signatureArray.AsSpan();
                            var right = d.Signature.ToArray().AsSpan();

                            return (Similarity: SignatureComparison.CompareTo(left, right), Image: d);
                        }
                    )
                    .OrderByDescending(x => x)
                    .ToList();

                if (!dists.Any(x => x.Similarity is SignatureSimilarity.Similar or SignatureSimilarity.Identical))
                {
                    hasRelevantResult = false;
                }

                hits.AddRange(dists);
                offset += 8;
            }

            return hits;
        }
    }
}
