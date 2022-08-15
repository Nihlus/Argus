//
//  Startup.cs
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
using System.Collections.Generic;
using System.Net;
using Argus.API.Authentication;
using Argus.API.Configuration;
using Argus.API.Database;
using Argus.Common.Json;
using Argus.Common.Services.Elasticsearch;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Nest;
using Puzzle;
using Remora.Extensions.Options.Immutable;

namespace Argus.API;

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

            var securityScheme = new OpenApiSecurityScheme
            {
                BearerFormat = "guid",
                Description = "Simple API key authorization",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Scheme = "Bearer",
                Type = SecuritySchemeType.Http
            };

            c.AddSecurityDefinition("Argus API Key", securityScheme);

            var securityRequirement = new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Argus API Key"
                        }
                    },
                    new List<string>()
                }
            };

            c.AddSecurityRequirement(securityRequirement);
        });

        services.AddSingleton<SignatureGenerator>();

        services.Configure(() =>
        {
            var options = new APIOptions
            (
                new Uri("about:blank"),
                string.Empty,
                string.Empty,
                null
            );

            this.Configuration.Bind(nameof(APIOptions), options);
            return options;
        });

        services.AddCors(options =>
        {
            options.AddPolicy
            (
                "_allowSpecificOrigins",
                policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            );
        });

        // Authentication
        services
            .AddAuthentication("Key")
            .AddScheme<AuthenticationSchemeOptions, APIKeyAuthenticationHandler>("Key", null);

        // Rate limiting
        services.AddMemoryCache();

        services.Configure<IpRateLimitOptions>(ConfigureRateLimitDefaults);
        services.Configure<ClientRateLimitOptions>(ConfigureRateLimitDefaults);

        services.Configure<IpRateLimitOptions>(this.Configuration.GetSection("IPRateLimiting"));
        services.Configure<IpRateLimitPolicies>(this.Configuration.GetSection("IPRateLimitPolicies"));

        services.Configure<ClientRateLimitOptions>(this.Configuration.GetSection("ClientRateLimiting"));
        services.Configure<ClientRateLimitPolicies>(this.Configuration.GetSection("ClientRateLimitPolicies"));

        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        // Elasticsearch services
        services
            .AddTransient
            (
                transientServices =>
                {
                    var (node, username, password, fingerprint) = transientServices
                        .GetRequiredService<IOptions<APIOptions>>().Value;

                    var settings = new ConnectionSettings(node);

                    settings.BasicAuthentication(username, password);
                    settings.DefaultIndex("argus");
                    settings.EnableApiVersioningHeader();

                    if (fingerprint is not null)
                    {
                        settings.CertificateFingerprint(fingerprint);
                    }

                    return settings;
                }
            )
            .AddTransient(s => new ElasticClient(s.GetRequiredService<ConnectionSettings>()))
            .AddTransient<NESTService>();

        // Database
        services.AddDbContext<ArgusAPIContext>(options =>
        {
            options.UseNpgsql
                (
                    this.Configuration.GetConnectionString("API")
                )
                .UseSnakeCaseNamingConvention();
        });
    }

    private static void ConfigureRateLimitDefaults(RateLimitOptions options)
    {
        options.ClientIdHeader = "Authorization";

        // Don't rate limit the local network
        options.IpWhitelist = new List<string>
        {
            "127.0.0.1",
            "::1",
            "192.168.0.0/24"
        };

        options.GeneralRules = new List<RateLimitRule>
        {
            new()
            {
                Endpoint = "*",
                PeriodTimespan = TimeSpan.FromSeconds(1),
                Limit = 10
            },
            new()
            {
                Endpoint = "*",
                PeriodTimespan = TimeSpan.FromMinutes(15),
                Limit = 100
            },
            new()
            {
                Endpoint = "*",
                PeriodTimespan = TimeSpan.FromHours(12),
                Limit = 1000
            },
            new()
            {
                Endpoint = "*",
                PeriodTimespan = TimeSpan.FromDays(7),
                Limit = 10000
            }
        };
    }

    /// <summary>
    /// Configures the HTTP request pipeline of the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="env">The environment builder.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseIpRateLimiting();
        app.UseClientRateLimiting();

        app.UseForwardedHeaders(new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            KnownProxies = { IPAddress.Loopback, IPAddress.IPv6Loopback }
        });

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Argus.API v1"));
        }

        app.UseRouting();

        app.UseCors("_allowSpecificOrigins");

        app
            .UseAuthentication()
            .UseAuthorization();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}
