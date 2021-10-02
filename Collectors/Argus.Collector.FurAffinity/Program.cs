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
using System.Text.Json;
using System.Threading.Tasks;
using Argus.Collector.Common.Extensions;
using Argus.Collector.Common.Polly;
using Argus.Collector.FurAffinity.API;
using Argus.Collector.FurAffinity.Configuration;
using Argus.Collector.FurAffinity.Services;
using Argus.Common.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Contrib.WaitAndRetry;

namespace Argus.Collector.FurAffinity
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

            await host.RunAsync();
            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseCollector<FurAffinityCollectorService, FurAffinityOptions>
            (
                "furaffinity",
                () => new FurAffinityOptions(string.Empty, string.Empty)
            )
            .ConfigureAppConfiguration((hostContext, configuration) =>
            {
                if (hostContext.HostingEnvironment.IsDevelopment())
                {
                    configuration.AddUserSecrets<Program>();
                }
            })
            .ConfigureServices((hostContext, services) =>
            {
                var retryDelay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);

                services.Configure<JsonSerializerOptions>(o =>
                {
                    o.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                    o.PropertyNameCaseInsensitive = true;
                });

                services.AddSingleton<FurAffinityAPI>();

                var rateLimit = hostContext.Configuration
                    .GetSection(nameof(FurAffinityOptions))
                    .GetValue<int>(nameof(FurAffinityOptions.RateLimit));

                if (rateLimit == 0)
                {
                    rateLimit = 10;
                }

                services.AddHttpClient(nameof(FurAffinityAPI), (s, client) =>
                {
                    var options = s.GetRequiredService<IOptions<FurAffinityOptions>>();

                    var (a, b, _) = options.Value;
                    client.DefaultRequestHeaders.Add("Cookie", $"a={a}; b={b}");
                })
                .AddTransientHttpErrorPolicy
                (
                    b => b
                        .WaitAndRetryAsync(retryDelay)
                        .WrapAsync(new ThrottlingPolicy(rateLimit, TimeSpan.FromSeconds(1)))
                );
            });
    }
}
