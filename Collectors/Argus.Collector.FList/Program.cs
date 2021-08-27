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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Argus.Collector.Common.Extensions;
using Argus.Collector.FList.API;
using Argus.Collector.FList.Configuration;
using Argus.Collector.FList.Json;
using Argus.Collector.FList.Polly;
using Argus.Collector.FList.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Remora.Extensions.Options.Immutable;

namespace Argus.Collector.FList
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
            .UseCollector<FListCollectorService>()
            .ConfigureAppConfiguration((_, configuration) =>
            {
                var configFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var systemConfigFile = Path.Combine(configFolder, "argus", "collector.flist.json");
                configuration.AddJsonFile(systemConfigFile, true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure(() =>
                {
                    var options = new FListOptions(string.Empty, string.Empty);

                    hostContext.Configuration.Bind(nameof(FListOptions), options);
                    return options;
                });

                var retryDelay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);

                services
                    .Configure<JsonSerializerOptions>
                    (
                        o =>
                        {
                            o.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                            o.PropertyNameCaseInsensitive = true;
                        }
                    );

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
            });
    }
}