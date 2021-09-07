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

using Argus.Collector.Common.Extensions;
using Argus.Collector.Driver.Minibooru;
using Argus.Collector.Driver.Minibooru.Extensions;
using Argus.Collector.Hypnohub.Configuration;
using Argus.Collector.Hypnohub.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;

namespace Argus.Collector.Hypnohub
{
    /// <summary>
    /// The main class of the program.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();
            var log = host.Services.GetRequiredService<ILogger<Program>>();

            using var runtime = new NetMQRuntime();
            runtime.Run(host.RunAsync());

            log.LogInformation("Shutting down...");
        }

        private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseCollector<HypnohubCollectorService, HypnohubOptions>
            (
                "hypnohub",
                () => new HypnohubOptions()
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
                var rateLimit = hostContext.Configuration
                    .GetSection(nameof(HypnohubOptions))
                    .GetValue<int>(nameof(HypnohubOptions.RateLimit));

                if (rateLimit == 0)
                {
                    rateLimit = 1;
                }

                services.AddBooruDriver<MoebooruDriver>("https://hypnohub.net", rateLimit);
            });
    }
}
