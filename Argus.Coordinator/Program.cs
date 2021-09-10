//
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Argus.Common;
using Argus.Common.Configuration;
using Argus.Common.Services.Elasticsearch;
using Argus.Coordinator.Configuration;
using Argus.Coordinator.MassTransit.Consumers;
using Argus.Coordinator.Model;
using MassTransit;
using MassTransit.Initializers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using Remora.Extensions.Options.Immutable;
using Serilog;
using Serilog.Events;

namespace Argus.Coordinator
{
    /// <summary>
    /// The main class of the program.
    /// </summary>
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();

            var log = host.Services.GetRequiredService<ILogger<Program>>();

            // Ensure the index is created
            var elasticClient = host.Services.GetRequiredService<ElasticClient>();
            var exists = await elasticClient.Indices.ExistsAsync("images");
            if (exists.ServerError is not null)
            {
                log.LogError
                (
                    "Failed to check whether the Elasticsearch index exists: {Message}",
                    exists.DebugInformation
                );
                return;
            }

            if (!exists.Exists)
            {
                var ensureCreated = await elasticClient.Indices
                    .CreateAsync("images", i => i.Map<IndexedImage>(x => x.AutoMap()));

                if (ensureCreated.ServerError is not null and not { Error: { Type: "resource_already_exists_exception" } })
                {
                    log.LogError
                    (
                        "Failed to ensure the Elasticsearch index was created: {Message}",
                        ensureCreated.DebugInformation
                    );
                    return;
                }
            }

            // Ensure the database is created
            using var scope = host.Services.CreateScope();
            await using var db = scope.ServiceProvider.GetRequiredService<CoordinatorContext>();
            await db.Database.MigrateAsync();

            // Figure out what needs to be retried
            var nestService = scope.ServiceProvider.GetRequiredService<NESTService>();
            var result = await nestService.SearchByOriginAsync
            (
                "https://e926.net/posts/82428",
                "https://static1.e926.net/data/6a/8e/6a8edaaa333296025df21545d6bd2ce0.jpg",
                true
            );

            var offset = 0;

            await using var file = new StreamWriter(File.OpenWrite("/home/jarl/output.csv"));
            await file.WriteLineAsync("id,report_source,report_image,report_status");
            while (true)
            {
                var batch = await db.ServiceStatusReports
                    .OrderBy(r => r.Report.Timestamp)
                    .Where(r => r.Report.Status == ImageStatus.Indexed)
                    .Skip(offset)
                    .Take(100)
                    .ToListAsync();

                if (batch.Count == 0)
                {
                    break;
                }

                var missing = (await Task.WhenAll(batch.Select
                (
                    async r => (r, await nestService.SearchByOriginAsync(r.Report.Source.ToString(), r.Report.Link.ToString()))
                ))).Where(r => r.Item2.Hits.Count == 0).Select(r => r.r).ToList();

                log.LogInformation("Found {Count} missing images", missing.Count);
                foreach (var missingImage in missing)
                {
                    await file.WriteLineAsync($"{missingImage.Id},{missingImage.Report.Source},{missingImage.Report.Link},0");
                }

                offset += missing.Count;
            }

            await host.RunAsync();
            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()
            .UseSerilog((_, logging) =>
            {
                logging
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .WriteTo.Console();
            })
        #if DEBUG
            .UseEnvironment("Development")
        #else
            .UseEnvironment("Production")
        #endif
            .ConfigureAppConfiguration((_, configuration) =>
            {
                var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var systemConfigFile = Path.Combine(configFolder, "argus", "coordinator.json");
                configuration.AddJsonFile(systemConfigFile, true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var options = new CoordinatorOptions
                (
                    new Uri("about:blank"),
                    string.Empty,
                    string.Empty
                );

                hostContext.Configuration.Bind(nameof(CoordinatorOptions), options);
                services.Configure(() => options);

                var brokerOptions = new BrokerOptions
                (
                    new Uri("about:blank"),
                    string.Empty,
                    string.Empty
                );

                hostContext.Configuration.Bind(nameof(BrokerOptions), brokerOptions);
                services.Configure(() => brokerOptions);

                // MassTransit
                services.AddMassTransit(busConfig =>
                {
                    busConfig.SetKebabCaseEndpointNameFormatter();
                    busConfig.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(brokerOptions.Host, "/argus", h =>
                        {
                            h.Username(brokerOptions.Username);
                            h.Password(brokerOptions.Password);
                        });

                        cfg.ConfigureEndpoints(context);
                    });

                    busConfig.AddConsumer<ResumeRequestConsumer>();
                    busConfig.AddConsumer<RetryRequestConsumer>();
                    busConfig.AddConsumer<FingerprintedImageConsumer>();
                    busConfig.AddConsumer<StatusReportConsumer>();
                });

                services.AddMassTransitHostedService();

                // Database
                services.AddDbContext<CoordinatorContext>(dbOptions =>
                {
                    dbOptions.UseNpgsql
                    (
                        hostContext.Configuration.GetConnectionString("Coordinator")
                    )
                    .UseSnakeCaseNamingConvention();
                });

                // Elasticsearch services
                services
                    .AddTransient
                    (
                        _ =>
                        {
                            var node = options.ElasticsearchServer;
                            var settings = new ConnectionSettings(node);

                            var username = options.ElasticsearchUsername;
                            var password = options.ElasticsearchPassword;
                            settings.BasicAuthentication(username, password);

                            settings.DefaultIndex("images");

                            return settings;
                        }
                    )
                    .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                    .AddTransient<NESTService>();
            });
    }
}
