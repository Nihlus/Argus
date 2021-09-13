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
using Argus.Common.Configuration;
using MassTransit;
using MassTransit.ExtensionsDependencyInjectionIntegration;
using MassTransit.MessageData.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Remora.Extensions.Options.Immutable;

namespace Argus.Common.Extensions
{
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
            Action<IServiceCollectionBusConfigurator>? busConfigurator = null
        )
        {
            busConfigurator ??= _ => { };

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
                MessageDataDefaults.TimeToLive = TimeSpan.FromDays(1);
                MessageDataDefaults.Threshold = 16_000_000; // 16MB
                MessageDataDefaults.AlwaysWriteToRepository = false;

                services.AddMassTransit(busConfig =>
                {
                    busConfig.SetKebabCaseEndpointNameFormatter();
                    busConfig.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.UseMessageData(s => s.FileSystem(brokerOptions.DataRepository));
                        cfg.Host(brokerOptions.Host, "/argus", h =>
                        {
                            h.Username(brokerOptions.Username);
                            h.Password(brokerOptions.Password);
                        });

                        cfg.ConfigureEndpoints(context);
                    });

                    busConfigurator(busConfig);
                });

                services.AddMassTransitHostedService();
            });
        }
    }
}
