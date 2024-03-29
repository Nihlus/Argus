//
//  HostBuilderExtensions.cs
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
using System.IO;
using System.Runtime.InteropServices;
using Argus.Collector.Common.Configuration;
using Argus.Collector.Common.Polly;
using Argus.Collector.Common.Services;
using Argus.Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Remora.Extensions.Options.Immutable;
using Serilog;

namespace Argus.Collector.Common.Extensions;

/// <summary>
/// Defines extension methods for the <see cref="IHostBuilder"/> interface.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Adds and configures the given collector service.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="optionsFactory">The options factory.</param>
    /// <typeparam name="TCollector">The collector service.</typeparam>
    /// <typeparam name="TCollectorOptions">The options type.</typeparam>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder UseCollector<TCollector, TCollectorOptions>
    (
        this IHostBuilder hostBuilder,
        string serviceName,
        Func<TCollectorOptions> optionsFactory
    )
        where TCollector : CollectorService
        where TCollectorOptions : class
    {
        hostBuilder.UseCollector<TCollector>();

        hostBuilder.ConfigureAppConfiguration((_, configuration) =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Add a set of config files for the /etc directory
                var systemConfigFolder = "/etc";

                var systemServiceConfigFile = Path.Combine
                (
                    systemConfigFolder,
                    "argus",
                    $"collector.{serviceName}.json"
                );

                configuration.AddJsonFile(systemServiceConfigFile, true);
            }

            // equivalent to /home/someone/.config
            var localConfigFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var localServiceConfigFile = Path.Combine(localConfigFolder, "argus", $"collector.{serviceName}.json");
            configuration.AddJsonFile(localServiceConfigFile, true);
        });

        return hostBuilder.ConfigureServices((hostContext, services) =>
        {
            // Configure app-specific options
            services.Configure(() =>
            {
                var options = optionsFactory();

                hostContext.Configuration.Bind(typeof(TCollectorOptions).Name, options);
                return options;
            });
        });
    }

    /// <summary>
    /// Adds and configures the given collector service.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <typeparam name="TCollector">The collector service.</typeparam>
    /// <returns>The configured host builder.</returns>
    public static IHostBuilder UseCollector<TCollector>
    (
        this IHostBuilder hostBuilder
    )
        where TCollector : CollectorService
    {
        hostBuilder
            .UseSerilog((hostContext, logging) =>
            {
                logging.ReadFrom.Configuration(hostContext.Configuration);
            })
            #if DEBUG
            .UseEnvironment("Development")
            #else
                .UseEnvironment("Production")
            #endif
            .UseConsoleLifetime()
            .UseMassTransit()
            .ConfigureAppConfiguration((_, configuration) =>
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Add a set of config files for the /etc directory
                    var systemConfigFolder = "/etc";

                    var systemConfigFile = Path.Combine(systemConfigFolder, "argus", "collector.json");
                    configuration.AddJsonFile(systemConfigFile, true);
                }

                // equivalent to /home/someone/.config
                var localConfigFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var localConfigFile = Path.Combine(localConfigFolder, "argus", "collector.json");
                configuration.AddJsonFile(localConfigFile, true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var options = new CollectorOptions();

                hostContext.Configuration.Bind(nameof(CollectorOptions), options);
                services.Configure(() => options);

                // Various
                services
                    .AddHttpClient()
                    .AddMemoryCache();

                var rateLimit = options.BulkDownloadRateLimit;
                if (rateLimit == 0)
                {
                    rateLimit = 25;
                }

                var retryDelay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);
                services
                    .AddHttpClient("BulkDownload")
                    .AddTransientHttpErrorPolicy
                    (
                        b => b
                            .WaitAndRetryAsync(retryDelay)
                            .WrapAsync(new ThrottlingPolicy(rateLimit, TimeSpan.FromSeconds(1)))
                    );

                services.AddHostedService<TCollector>();
            });

        return hostBuilder;
    }
}
