﻿//
//  Program.cs
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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using ImageScraper.API.FList;
using ImageScraper.BackgroundServices;
using ImageScraper.Json;
using ImageScraper.Model;
using ImageScraper.Polly;
using ImageScraper.ServiceIndexers;
using ImageScraper.Services.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using Noppes.E621;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Puzzle;
using Policy = Polly.Policy;

namespace ImageScraper
{
    /// <summary>
    /// Defines the main class of the program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main entrypoint of the program.
        /// </summary>
        /// <param name="args">The arguments passed to the program.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var services = host.Services;

            var log = services.GetRequiredService<ILogger<Program>>();

            // Ensure the index is created
            var elasticClient = services.GetRequiredService<ElasticClient>();
            var ensureCreated = await elasticClient.Indices
                .CreateAsync("images", i => i.Map<IndexedImage>(x => x.AutoMap()));

            if (ensureCreated.ServerError is not null and not { Error: { Type: "resource_already_exists_exception" } })
            {
                log.LogError("Failed to initialize connection to Elasticsearch");
                return;
            }

            // Ensure the database is created
            var contextFactory = services.GetRequiredService<IDbContextFactory<IndexingContext>>();
            await using (var db = contextFactory.CreateDbContext())
            {
                await db.Database.MigrateAsync();
            }

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureServices
            (
                services =>
                {
                    var retryDelay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);

                    // JSON
                    services
                        .Configure<JsonSerializerOptions>
                        (
                            o =>
                            {
                                o.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                                o.PropertyNameCaseInsensitive = true;
                            }
                        );

                    // Database
                    services
                        .AddDbContextFactory<IndexingContext>()
                        .AddSingleton<SqliteConnectionPool>();

                    // Signature generation services
                    services
                        .AddTransient<SignatureGenerator>()
                        .AddSingleton<ImageProcessingService>()
                        .AddHostedService(s => s.GetRequiredService<ImageProcessingService>());

                    // Elasticsearch services
                    services
                        .AddTransient
                        (
                            _ =>
                            {
                                var node = new Uri("http://192.168.0.11:9200");
                                var settings = new ConnectionSettings(node);

                                var username = Environment.GetEnvironmentVariable("IMAGE_SCRAPER_ELASTIC_USERNAME");
                                var password = Environment.GetEnvironmentVariable("IMAGE_SCRAPER_ELASTIC_PASSWORD");
                                settings.BasicAuthentication(username, password);

                                settings.DefaultIndex("images");

                                return settings;
                            }
                        )
                        .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                        .AddTransient<NESTService>();

                    // e621 services
                    services
                        .AddSingleton
                        (
                            _ =>
                            {
                                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
                                var builder = new E621ClientBuilder()
                                    .WithUserAgent("ImageIndexer", version, "Jax#7487", "Discord")
                                    .WithMaximumConnections(E621Constants.MaximumConnectionsLimit)
                                    .WithRequestInterval(E621Constants.MinimumRequestInterval);

                                return builder.Build();
                            }
                        )
                        .AddSingleton<E621Indexer>();
                        //.AddHostedService<IndexingBackgroundService<E621Indexer, int>>();

                    // f-list services
                    services
                        .AddSingleton<FListAPI>()
                        .AddSingleton<FListAuthenticationRefreshPolicy>();

                    services
                        .AddHttpClient
                        (
                            nameof(FListAPI),
                            (_, client) =>
                            {
                                var assemblyName = Assembly.GetExecutingAssembly().GetName();
                                var name = assemblyName.Name ?? "Indexer";
                                var version = assemblyName.Version ?? new Version(1, 0, 0);

                                client.BaseAddress = new Uri("https://www.f-list.net");
                                client.DefaultRequestHeaders.UserAgent.Add
                                (
                                    new ProductInfoHeaderValue(name, version.ToString())
                                );
                            }
                        )
                        .AddTransientHttpErrorPolicy
                        (
                            b => b
                                .WaitAndRetryAsync(retryDelay)
                        )
                        .AddPolicyHandler
                        (
                            (s, _) => Policy
                                .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.Unauthorized)
                                .RetryAsync(1)
                                .WrapAsync(s.GetRequiredService<FListAuthenticationRefreshPolicy>())
                        );

                    services
                        .AddSingleton<FListIndexer>()
                        .AddHostedService<IndexingBackgroundService<FListIndexer, int>>();

                    // Other services
                    services
                        .AddHttpClient()
                        .AddMemoryCache()
                        .AddLogging
                        (
                            c => c
                                .AddConsole()
                        );
                }
            );
    }
}
