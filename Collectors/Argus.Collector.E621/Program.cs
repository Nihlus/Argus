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
using System.Reflection;
using Argus.Collector.Common.Extensions;
using Argus.Collector.E621.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using Noppes.E621;

namespace Argus.Collector.E621
{
    /// <summary>
    /// The main class of the program.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            using var runtime = new NetMQRuntime();
            using var host = CreateHostBuilder(args).Build();

            var log = host.Services.GetRequiredService<ILogger<Program>>();

            runtime.Run(host.RunAsync());
            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseCollector<E621CollectorService>()
            .ConfigureAppConfiguration((_, configuration) =>
            {
                var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var systemConfigFile = Path.Combine(configFolder, "argus", "collector.e621.json");
                configuration.AddJsonFile(systemConfigFile, true);
            })
            .ConfigureServices((_, services) =>
            {
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
                    );
            });
    }
}
