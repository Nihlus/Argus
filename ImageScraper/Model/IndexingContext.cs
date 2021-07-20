//
//  IndexingContext.cs
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
using Microsoft.EntityFrameworkCore;

namespace ImageScraper.Model
{
    /// <summary>
    /// Represents the database context for the indexer.
    /// </summary>
    public class IndexingContext : DbContext
    {
        private readonly string _dbPath;

        /// <summary>
        /// Gets the service states.
        /// </summary>
        public DbSet<ServiceState> ServiceStates => Set<ServiceState>();

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingContext"/> class.
        /// </summary>
        public IndexingContext()
        {
            var cacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var applicationName = "image-indexer";

            _dbPath = Path.Combine(cacheFolder, applicationName, "indexer.sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? throw new InvalidOperationException());
        }

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder
                .UseSqlite($"Data Source={_dbPath}")
                .UseLazyLoadingProxies();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ServiceState>()
                .HasIndex(s => s.Name)
                .IsUnique();
        }
    }
}
