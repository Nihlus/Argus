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

using System.Threading.Tasks;
using Argus.API.Database;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Argus.API;

/// <summary>
/// The main class of the program.
/// </summary>
public class Program
{
    private static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();

        // Perform migrations
        using var scope = host.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ArgusAPIContext>();
        await db.Database.MigrateAsync();

        // Seed policy stores
        var policyStore = scope.ServiceProvider.GetRequiredService<IIpPolicyStore>();
        await policyStore.SeedAsync();

        var clientPolicyStore = scope.ServiceProvider.GetRequiredService<IClientPolicyStore>();
        await clientPolicyStore.SeedAsync();

        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
}
