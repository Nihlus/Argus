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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Argus.Common.Extensions;
using Argus.Common.Services.Elasticsearch;
using Argus.Coordinator.Configuration;
using Argus.Coordinator.MassTransit.Consumers;
using Argus.Coordinator.Model;
using GreenPipes;
using MassTransit;
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
            var exists = await elasticClient.Indices.ExistsAsync("argus");
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
                var ensureCreated = await elasticClient.Indices.CreateAsync
                (
                    "argus",
                    i => i.Map<IndexedImage>
                    (
                        x => x.AutoMap()
                    )
                );

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

            await host.RunAsync();
            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()
            .UseSerilog((hostContext, logging) =>
            {
                logging
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                    .WriteTo.Console()
                    .ReadFrom.Configuration(hostContext.Configuration);
            })
        #if DEBUG
            .UseEnvironment("Development")
        #else
            .UseEnvironment("Production")
        #endif
            .UseMassTransit((busConfig, _) =>
            {
                busConfig.AddConsumer<FingerprintedImageConsumer>(consumer =>
                {
                    consumer.Options<BatchOptions>
                    (
                        options => options
                            .SetMessageLimit(100)
                            .SetTimeLimit(TimeSpan.FromSeconds(10))
                    );
                });

                busConfig.AddConsumer<StatusReportConsumer>(consumer =>
                {
                    consumer.UseMessageRetry
                    (
                        c => c.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1))
                    );

                    consumer.Options<BatchOptions>
                    (
                        options => options
                                .SetMessageLimit(100)
                                .SetTimeLimit(TimeSpan.FromSeconds(10))
                    );
                });

                busConfig.AddConsumer<ResumeRequestConsumer>();
                busConfig.AddConsumer<RetryRequestConsumer>();
                busConfig.AddConsumer<FingerprintedImageFaultConsumer>();
            })
            .ConfigureAppConfiguration((_, configuration) =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Add a set of config files for the /etc directory
                    var systemConfigFolder = "/etc";

                    var systemConfigFile = Path.Combine(systemConfigFolder, "argus", "coordinator.json");
                    configuration.AddJsonFile(systemConfigFile, true);
                }

                // equivalent to /home/someone/.config
                var localConfigFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var localConfigFile = Path.Combine(localConfigFolder, "argus", "coordinator.json");
                configuration.AddJsonFile(localConfigFile, true);
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

                            settings.DefaultIndex("argus");

                            return settings;
                        }
                    )
                    .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                    .AddTransient<NESTService>();
            });
    }
}
