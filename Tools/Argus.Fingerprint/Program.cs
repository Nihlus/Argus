//
//  Program.cs
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
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Argus.Common.Json;
using Argus.Common.Portable;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Puzzle;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Argus.Fingerprint;

/// <summary>
/// The main class of the program.
/// </summary>
internal class Program
{
    private static Task<int> Main(string[] args) => Parser.Default.ParseArguments
    (
        () => new FingerprintOptions
        (
            Array.Empty<string>(),
            Directory.GetCurrentDirectory()
        ),
        args
    ).MapResult
    (
        RunAsync,
        _ => Task.FromResult(1)
    );

    private static async Task<int> RunAsync(FingerprintOptions options)
    {
        var imageConfiguration = Configuration.Default.Clone();
        imageConfiguration.PreferContiguousImageBuffers = true;

        var services = new ServiceCollection()
            .AddLogging(logging =>
            {
                var loggingConfiguration = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .CreateLogger();

                logging
                    .ClearProviders()
                    .AddSerilog(loggingConfiguration, true);
            })
            .AddSingleton(imageConfiguration)
            .BuildServiceProvider();

        var log = services.GetRequiredService<ILogger<Program>>();

        try
        {
            // Setup
            var absoluteFilePaths = new List<string>();
            foreach (var file in options.Files)
            {
                var absolutePath = Path.GetFullPath(file);
                if (!File.Exists(absolutePath))
                {
                    log.LogWarning("File not found: {File}", absolutePath);
                    return 1;
                }

                absoluteFilePaths.Add(absolutePath);
            }

            var absoluteOutputPath = Path.GetFullPath(options.OutputDirectory);
            if (!Directory.Exists(absoluteOutputPath))
            {
                log.LogInformation("Creating output directories...");
                Directory.CreateDirectory(absoluteOutputPath);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
                Converters = { new Base64FingerprintConverter() },
                WriteIndented = true
            };

            // Processing
            var portableFingerprints = await CreateFingerprints
            (
                imageConfiguration,
                absoluteFilePaths,
                log
            );

            // Output
            if (options.ShouldPack)
            {
                await CreatePackedOutput
                (
                    options,
                    absoluteOutputPath,
                    absoluteFilePaths,
                    portableFingerprints,
                    jsonOptions
                );
            }
            else
            {
                await CreateOutput
                (
                    options,
                    absoluteFilePaths,
                    absoluteOutputPath,
                    portableFingerprints,
                    jsonOptions
                );
            }

            log.LogInformation("Fingerprinting completed");

            return 0;
        }
        catch (Exception e)
        {
            log.LogError(e, "Unexpected error during execution");
            return 1;
        }
    }

    private static async Task<List<PortableFingerprint>> CreateFingerprints
    (
        Configuration imageConfiguration,
        List<string> absoluteFilePaths,
        ILogger<Program> log
    )
    {
        var signatureGenerator = new SignatureGenerator();

        var portableFingerprints = new List<PortableFingerprint>();
        foreach (var absoluteFilePath in absoluteFilePaths)
        {
            var filename = Path.GetFileName(absoluteFilePath);
            log.LogInformation("Fingerprinting {File}", filename);

            await using var file = File.OpenRead(absoluteFilePath);
            using var sha256 = SHA256.Create();

            log.LogInformation("Computing hash...");
            var hashBytes = await sha256.ComputeHashAsync(file);
            var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

            // Rewind file stream
            file.Seek(0, SeekOrigin.Begin);

            log.LogInformation("Computing perceptual fingerprint...");
            using var image = await Image.LoadAsync<L8>(imageConfiguration, file);
            var fingerprint = signatureGenerator.GenerateSignature(image);

            portableFingerprints.Add(new PortableFingerprint(filename, hash, fingerprint));
        }

        return portableFingerprints;
    }

    private static async Task CreateOutput
    (
        FingerprintOptions options,
        List<string> absoluteFilePaths,
        string absoluteOutputPath,
        List<PortableFingerprint> portableFingerprints,
        JsonSerializerOptions jsonOptions
    )
    {
        if (options.IncludeSourceImages)
        {
            foreach (var absoluteFilePath in absoluteFilePaths)
            {
                var filename = Path.GetFileName(absoluteFilePath);
                var output = Path.Combine(absoluteOutputPath, "source", filename);
                File.Copy(absoluteFilePath, output);
            }
        }

        foreach (var portableFingerprint in portableFingerprints)
        {
            var filename = $"{Path.GetFileNameWithoutExtension(portableFingerprint.Filename)}.json";
            var output = Path.Combine(absoluteOutputPath, filename);

            await using var outputStream = File.OpenWrite(output);
            await JsonSerializer.SerializeAsync(outputStream, portableFingerprint, jsonOptions);
        }
    }

    private static async Task CreatePackedOutput
    (
        FingerprintOptions options,
        string absoluteOutputPath,
        List<string> absoluteFilePaths,
        List<PortableFingerprint> portableFingerprints,
        JsonSerializerOptions jsonOptions
    )
    {
        var now = DateTimeOffset.UtcNow.ToString("s").Replace(":", ".");
        await using var output = File.Open(Path.Combine(absoluteOutputPath, $"{now}.fpkg"), FileMode.Create);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create);

        if (options.IncludeSourceImages)
        {
            foreach (var absoluteFilePath in absoluteFilePaths)
            {
                var entryName = Path.Combine("source", Path.GetFileName(absoluteFilePath));
                archive.CreateEntryFromFile(absoluteFilePath, entryName);
            }
        }

        foreach (var portableFingerprint in portableFingerprints)
        {
            var filename = $"{Path.GetFileNameWithoutExtension(portableFingerprint.Filename)}.json";

            var entry = archive.CreateEntry(filename);
            await using var entryStream = entry.Open();

            await JsonSerializer.SerializeAsync(entryStream, portableFingerprint, jsonOptions);
        }
    }
}
