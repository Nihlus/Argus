//
//  SearchController.cs
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

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading;
using Argus.Common.Portable;
using Argus.Common.Services.Elasticsearch;
using Argus.Common.Services.Elasticsearch.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Argus.API.Controllers
{
    /// <summary>
    /// Controls search requests.
    /// </summary>
    [Authorize]
    [RequireHttps]
    [Route("api/search")]
    [ApiController]
    [Produces("application/json")]
    public class SearchController : ControllerBase
    {
        private readonly NESTService _nestService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchController"/> class.
        /// </summary>
        /// <param name="nestService">The NEST service.</param>
        public SearchController(NESTService nestService)
        {
            _nestService = nestService;
        }

        /// <summary>
        /// Searches the database for matching images.
        /// </summary>
        /// <param name="fingerprint">The fingerprint to search for.</param>
        /// <param name="after">The hit index after which to start returning results.</param>
        /// <param name="limit">The limit on the number of hits to return per fingerprint.</param>
        /// <param name="ct">The cancellation token for this operation.</param>
        /// <returns>The results.</returns>
        [HttpPost]
        public async IAsyncEnumerable<SearchResult> GetSearchAsync
        (
            [Required] PortableFingerprint fingerprint,
            uint after = 0,
            [Range(1, 100)] uint limit = 100,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            var signature = new ImageSignature(fingerprint.Fingerprint);
            await foreach (var hit in _nestService.SearchAsync(signature, after, limit, ct))
            {
                yield return hit;
            }
        }
    }
}
