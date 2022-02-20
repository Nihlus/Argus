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
using Argus.Common.Configuration;
using MassTransit;
using MassTransit.ExtensionsDependencyInjectionIntegration;
using MassTransit.MessageData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Remora.Extensions.Options.Immutable;

namespace Argus.Common.Extensions;

/// <summary>
/// Defines extension methods for the <see cref="IHostBuilder"/> interface.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Adds and configures MassTransit services.
    /// </summary>
    /// <param name="hostBuilder">The host builder.</param>
    /// <param name="busConfigurator">The bus configurator.</param>
    /// <returns>The host builder, with MassTransit configured.</returns>
    public static IHostBuilder UseMassTransit
    (
        this IHostBuilder hostBuilder,
        Action<IServiceCollectionBusConfigurator, BrokerOptions>? busConfigurator = null
    )
    {
        busConfigurator ??= (_, _) => { };

        return hostBuilder.ConfigureServices((hostContext, services) =>
        {
            var brokerOptions = new BrokerOptions
            (
                new Uri("about:blank"),
                string.Empty,
                string.Empty,
                string.Empty
            );

            hostContext.Configuration.Bind(nameof(BrokerOptions), brokerOptions);
            services.Configure(() => brokerOptions);

            // MassTransit
            var dataRepository = new FileSystemMessageDataRepository
            (
                new DirectoryInfo(brokerOptions.DataRepository)
            );

            services.AddSingleton<IMessageDataRepository>(dataRepository);

            MessageDataDefaults.TimeToLive = TimeSpan.FromDays(1);
            MessageDataDefaults.AlwaysWriteToRepository = false;

            services.AddMassTransit(busConfig =>
            {
                busConfig.SetKebabCaseEndpointNameFormatter();
                busConfig.UsingRabbitMq((context, cfg) =>
                {
                    cfg.UseMessageData(dataRepository);
                    cfg.Host(brokerOptions.Host, "/argus", h =>
                    {
                        h.Username(brokerOptions.Username);
                        h.Password(brokerOptions.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });

                busConfigurator(busConfig, brokerOptions);
            });

            services.AddMassTransitHostedService();
        });
    }
}
