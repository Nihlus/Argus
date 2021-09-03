//
//  HostBuilderExtensions.cs
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
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Polly;
using Argus.Collector.Common.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Remora.Extensions.Options.Immutable;
using Serilog;

namespace Argus.Collector.Common.Extensions
{
    /// <summary>
    /// Defines extension methods for the <see cref="IHostBuilder"/> interface.
    /// </summary>
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// Adds and configures the given collector service.
        /// </summary>
        /// <param name="hostBuilder">The host builder.</param>
        /// <typeparam name="TCollector">The collector service.</typeparam>
        /// <returns>The configured host builder.</returns>
        public static IHostBuilder UseCollector<TCollector>(this IHostBuilder hostBuilder)
            where TCollector : CollectorService
        {
            hostBuilder
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
                .UseConsoleLifetime()
                .ConfigureAppConfiguration((_, configuration) =>
                {
                    var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var systemConfigFile = Path.Combine(configFolder, "argus", "collector.json");
                    configuration.AddJsonFile(systemConfigFile, true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure(() =>
                    {
                        var options = new CollectorOptions
                        (
                            new Uri("about:blank"),
                            new Uri("about:blank")
                        );

                        hostContext.Configuration.Bind(nameof(CollectorOptions), options);
                        return options;
                    });

                    services
                        .AddHttpClient()
                        .AddMemoryCache();

                    var retryDelay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);
                    services
                        .AddHttpClient("BulkDownload")
                        .AddTransientHttpErrorPolicy
                        (
                            b => b
                                .WaitAndRetryAsync(retryDelay)
                                .WrapAsync(new ThrottlingPolicy(25, TimeSpan.FromSeconds(1)))
                        );

                    services.AddHostedService<TCollector>();
                });

            return hostBuilder;
        }
    }
}
