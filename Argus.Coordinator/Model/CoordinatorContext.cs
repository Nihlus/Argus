//
//  CoordinatorContext.cs
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

using Argus.Common.Messages.BulkData;
using Microsoft.EntityFrameworkCore;

namespace Argus.Coordinator.Model;

/// <summary>
/// Represents the database context for the coordinator.
/// </summary>
public class CoordinatorContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CoordinatorContext"/> class.
    /// </summary>
    /// <param name="options">The context options.</param>
    public CoordinatorContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets the service states.
    /// </summary>
    public DbSet<ServiceState> ServiceStates => Set<ServiceState>();

    /// <summary>
    /// Gets received status reports.
    /// </summary>
    public DbSet<StatusReport> ServiceStatusReports => Set<StatusReport>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure ServiceStates
        modelBuilder.Entity<ServiceState>()
            .HasIndex(r => r.Id)
            .IsUnique();

        // Configure StatusReports
        modelBuilder.Entity<StatusReport>()
            .HasKey(nameof(StatusReport.Source), nameof(StatusReport.Link));

        modelBuilder.Entity<StatusReport>()
            .HasIndex(nameof(StatusReport.Source), nameof(StatusReport.Link))
            .IsUnique();

        modelBuilder.Entity<StatusReport>()
            .HasIndex(r => r.Timestamp);

        modelBuilder.Entity<StatusReport>()
            .HasIndex(r => r.Status);
    }
}
