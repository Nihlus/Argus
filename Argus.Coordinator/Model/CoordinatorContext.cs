//
//  CoordinatorContext.cs
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

    /// <summary>
    /// Gets indexed image sources.
    /// </summary>
    public DbSet<ImageSource> ServiceImageSources => Set<ImageSource>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure ServiceStates
        modelBuilder.Entity<ServiceState>()
            .HasIndex(s => s.Id)
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

        // Configure image sources
        modelBuilder.Entity<ImageSource>()
            .HasKey(nameof(ImageSource.ServiceName), nameof(ImageSource.Source));

        modelBuilder.Entity<ImageSource>()
            .HasIndex(nameof(ImageSource.ServiceName), nameof(ImageSource.Source))
            .IsUnique();

        modelBuilder.Entity<ImageSource>()
            .HasIndex(s => s.SourceIdentifier);

        modelBuilder.Entity<ImageSource>()
            .HasIndex(s => s.FirstVisitedAt);

        modelBuilder.Entity<ImageSource>()
            .HasIndex(s => s.RevisitCount);

        modelBuilder.Entity<ImageSource>()
            .HasIndex(s => s.LastRevisitedAt);
    }
}
