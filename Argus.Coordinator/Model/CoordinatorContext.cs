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

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Argus.Coordinator.Model
{
    /// <summary>
    /// Represents the database context for the coordinator.
    /// </summary>
    public class CoordinatorContext : DbContext
    {
        private readonly SqliteConnectionPool _connectionPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoordinatorContext"/> class.
        /// </summary>
        /// <param name="options">The context options.</param>
        /// <param name="connectionPool">The connection pool.</param>
        public CoordinatorContext(DbContextOptions options, SqliteConnectionPool connectionPool)
            : base(options)
        {
            _connectionPool = connectionPool;
        }

        /// <summary>
        /// Gets the service states.
        /// </summary>
        public DbSet<ServiceState> ServiceStates => Set<ServiceState>();

        /// <summary>
        /// Gets received status reports.
        /// </summary>
        public DbSet<ServiceStatusReport> ServiceStatusReports => Set<ServiceStatusReport>();

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder
                .UseSqlite(_connectionPool.LeaseConnection());

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ServiceState>()
                .HasIndex(s => s.Id)
                .IsUnique();

            modelBuilder.Entity<ServiceState>()
                .HasIndex(s => s.Name)
                .IsUnique();

            modelBuilder.Entity<ServiceStatusReport>()
                .HasIndex(s => s.Id)
                .IsUnique();

            var ownedReport = modelBuilder.Entity<ServiceStatusReport>()
                .OwnsOne(s => s.Report);

            ownedReport
                .HasIndex(s => s.ServiceName);

            ownedReport
                .HasIndex(s => s.Source);

            ownedReport
                .HasIndex(s => s.Image);

            ownedReport.WithOwner();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _connectionPool.ReturnConnection(this.Database.GetDbConnection());
            base.Dispose();
        }

        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            _connectionPool.ReturnConnection(this.Database.GetDbConnection());
            return base.DisposeAsync();
        }
    }
}
