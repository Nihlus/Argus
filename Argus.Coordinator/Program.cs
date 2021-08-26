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
using Argus.Coordinator.Configuration;
using Argus.Coordinator.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using Remora.Extensions.Options.Immutable;

namespace Argus.Coordinator
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
            .UseConsoleLifetime()
        #if DEBUG
            .UseEnvironment("Development")
        #else
            .UseEnvironment("Production")
        #endif
            .ConfigureAppConfiguration((hostContext, configuration) =>
            {
                var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var systemConfigFile = Path.Combine(configFolder, "argus", "coordinator.json");
                configuration.AddJsonFile(systemConfigFile, true);
            })
            .ConfigureLogging((hostContext, logging) =>
            {
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure(() =>
                {
                    var options = new CoordinatorOptions(new Uri("tcp://localhost"));
                    hostContext.Configuration.Bind(nameof(CoordinatorOptions), options);

                    return options;
                });

                services.AddHostedService<CoordinatorService>();
            });
    }
}
