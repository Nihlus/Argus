//
//  Startup.cs
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
using Argus.API.Configuration;
using Argus.Common.Json;
using Argus.Common.Services.Elasticsearch;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Nest;
using Puzzle;
using Remora.Extensions.Options.Immutable;

namespace Argus.API
{
    /// <summary>
    /// Represents the main startup class.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Gets the configuration of the application.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        /// <summary>
        /// Configures the services of the application.
        /// </summary>
        /// <param name="services">The services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                o.JsonSerializerOptions.Converters.Add(new Base64FingerprintConverter());
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Argus.API", Version = "v1" });
            });

            services.AddSingleton<SignatureGenerator>();

            services.Configure(() =>
            {
                var options = new APIOptions
                (
                    new Uri("about:blank"),
                    string.Empty,
                    string.Empty
                );

                this.Configuration.Bind(nameof(APIOptions), options);
                return options;
            });

            // Elasticsearch services
            services
                .AddTransient
                (
                    transientServices =>
                    {
                        var (node, username, password) = transientServices
                            .GetRequiredService<IOptions<APIOptions>>().Value;

                        var settings = new ConnectionSettings(node);

                        settings.BasicAuthentication(username, password);

                        settings.DefaultIndex("images");

                        return settings;
                    }
                )
                .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
                .AddTransient<NESTService>();
        }

        /// <summary>
        /// Configures the HTTP request pipeline of the application.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The environment builder.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Argus.API v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
