//
//  FingerprintController.cs
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
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Argus.Common.Portable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Puzzle;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Argus.API.Controllers;

/// <summary>
/// Controls fingerprinting requests.
/// </summary>
[Authorize]
[RequireHttps]
[Route("api/fingerprint")]
[ApiController]
[Produces("application/json")]
public class FingerprintController : ControllerBase
{
    private readonly SignatureGenerator _signatureGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FingerprintController"/> class.
    /// </summary>
    /// <param name="signatureGenerator">The signature generator.</param>
    public FingerprintController(SignatureGenerator signatureGenerator)
    {
        _signatureGenerator = signatureGenerator;
    }

    /// <summary>
    /// Calculates the fingerprint of the uploaded images.
    /// </summary>
    /// <param name="files">The files to fingerprint.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The result.</returns>
    [HttpPost]
    public async IAsyncEnumerable<PortableFingerprint> PostFingerprintRequestAsync
    (
        IReadOnlyList<IFormFile> files,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            using var image = await Image.LoadAsync<L8>(stream);

            stream.Seek(0, SeekOrigin.Begin);

            using var sha256 = SHA256.Create();

            var hashBytes = await sha256.ComputeHashAsync(stream, ct);
            var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

            var signature = await Task.Run(() => _signatureGenerator.GenerateSignature(image), ct);
            yield return new PortableFingerprint(file.FileName, hash, signature);
        }
    }
}
