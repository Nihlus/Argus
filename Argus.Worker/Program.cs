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
using Argus.Worker.Configuration;
using Argus.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using Puzzle;
using Remora.Extensions.Options.Immutable;
using Serilog;

namespace Argus.Worker
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

            runtime.Run(host.RunAsync());
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
                var systemConfigFile = Path.Combine(configFolder, "argus", "worker.json");
                configuration.AddJsonFile(systemConfigFile, true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure(() =>
                {
                    var options = new WorkerOptions
                    (
                        new Uri("tcp://localhost:6666"),
                        new Uri("tcp://localhost:6667"),
                        new Uri("tcp://localhost:6668")
                    );

                    hostContext.Configuration.Bind(nameof(WorkerOptions), options);

                    return options;
                });

                services.AddSingleton<SignatureGenerator>();
                services.AddHostedService<ImageFingerprintingService>();
            });
    }
}
