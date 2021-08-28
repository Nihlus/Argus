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
using System.Threading.Tasks;
using Argus.Coordinator.Configuration;
using Argus.Coordinator.Model;
using Argus.Coordinator.Services;
using Argus.Coordinator.Services.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using NetMQ;
using Remora.Extensions.Options.Immutable;
using Serilog;

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
            var contextFactory = host.Services.GetRequiredService<IDbContextFactory<CoordinatorContext>>();
            await using (var db = contextFactory.CreateDbContext())
            {
                await db.Database.MigrateAsync();
            }

            using var runtime = new NetMQRuntime();
            runtime.Run(host.RunAsync());
            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseConsoleLifetime()
            .UseSerilog((_, logging) =>
            {
                logging
                    .MinimumLevel.Information()
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
                services.Configure(() =>
                {
                    var options = new CoordinatorOptions
                    (
                        new Uri("tcp://localhost:6666"),
                        new Uri("tcp://localhost:6667"),
                        new Uri("tcp://localhost:6668"),
                        new Uri("http://192.168.0.11:9200"),
                        string.Empty,
                        string.Empty
                    );

                    hostContext.Configuration.Bind(nameof(CoordinatorOptions), options);
                    return options;
                });

                // Database
                services
                    .AddDbContextFactory<CoordinatorContext>()
                    .AddSingleton<SqliteConnectionPool>();

                // Elasticsearch services
                services
                    .AddTransient
                    (
                        transientServices =>
                        {
                            var configuration = transientServices
                                .GetRequiredService<IOptions<CoordinatorOptions>>().Value;

                            var node = configuration.ElasticsearchServer;
                            var settings = new ConnectionSettings(node);

                            var username = configuration.ElasticsearchUsername;
                            var password = configuration.ElasticsearchPassword;
                            settings.BasicAuthentication(username, password);

                            settings.DefaultIndex("images");

                            return settings;
                        }
                    )
                    .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                    .AddTransient<NESTService>();

                services.AddHostedService<CoordinatorService>();
            });
    }
}
