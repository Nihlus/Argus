//
//  ArgusAPIContext.cs
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
using Argus.API.Database.Model;
using Argus.Common.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Argus.API.Database
{
    /// <summary>
    /// Represents the database context for the REST API.
    /// </summary>
    public class ArgusAPIContext : DbContext
    {
        private readonly SqliteConnectionPool _connectionPool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArgusAPIContext"/> class.
        /// </summary>
        /// <param name="options">The context options.</param>
        /// <param name="connectionPool">The connection pool.</param>
        public ArgusAPIContext(DbContextOptions options, SqliteConnectionPool connectionPool)
            : base(options)
        {
            _connectionPool = connectionPool;
        }

        /// <summary>
        /// Gets the API keys in the database.
        /// </summary>
        public DbSet<APIKey> APIKeys => Set<APIKey>();

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder
                .UseSqlite(_connectionPool.LeaseConnection());

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<APIKey>()
                .HasIndex(k => k.ID)
                .IsUnique();

            modelBuilder.Entity<APIKey>()
                .Property(k => k.CreatedAt)
                .IsRequired();

            modelBuilder.Entity<APIKey>()
                .Property(k => k.Key)
                .IsRequired();

            modelBuilder.Entity<APIKey>()
                .HasIndex(k => k.Key)
                .IsUnique();
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
