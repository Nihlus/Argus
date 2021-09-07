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
using Argus.Collector.Booru.Configuration;
using Argus.Collector.Booru.Services;
using Argus.Collector.Common.Extensions;
using Argus.Collector.Driver.Minibooru;
using Argus.Collector.Driver.Minibooru.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetMQ;

namespace Argus.Collector.Booru
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
            .UseCollector<BooruCollectorService, BooruOptions>
            (
                "hypnohub",
                () => new BooruOptions(string.Empty, string.Empty, new Uri("about:blank"))
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
                var options = new BooruOptions(string.Empty, string.Empty, new Uri("about:blank"));
                hostContext.Configuration.Bind(nameof(BooruOptions), options);

                var rateLimit = options.RateLimit;
                if (rateLimit == 0)
                {
                    rateLimit = 1;
                }

                switch (options.DriverName)
                {
                    case "moebooru":
                    {
                        services.AddBooruDriver<MoebooruDriver>(options.BaseUrl.ToString(), rateLimit);
                        break;
                    }
                    case "e621":
                    {
                        services.AddBooruDriver<E621Driver>(options.BaseUrl.ToString(), rateLimit);
                        break;
                    }
                    default:
                    {
                        throw new ArgumentOutOfRangeException($"Unknown driver name \"{options.DriverName}\"");
                    }
                }
            });
    }
}
