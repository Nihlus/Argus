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
using System.Threading.Tasks;
using ImageScraper.Services.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using Puzzle;

namespace ImageScraper
{
    /// <summary>
    /// Defines the main class of the program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The main entrypoint of the program.
        /// </summary>
        /// <param name="args">The arguments passed to the program.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task Main(string[] args) =>
            CreateHostBuilder(args).Build().RunAsync();

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).ConfigureServices
            (
                services =>
                {
                    // Signature generation services
                    services
                        .AddSingleton<SignatureGenerator>();

                    // Elasticsearch services
                    services
                        .AddTransient
                        (
                            _ =>
                            {
                                var node = new Uri("http://192.168.0.11:9200");
                                var settings = new ConnectionSettings(node);

                                settings.DefaultIndex("images");

                                return settings;
                            }
                        )
                        .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                        .AddTransient<NESTService>();

                    // Other services
                    services
                        .AddMemoryCache()
                        .AddHttpClient()
                        .AddLogging
                        (
                            c => c
                                .AddConsole()
                        );
                }
            );
    }
}
