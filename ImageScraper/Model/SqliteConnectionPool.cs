//
//  SqliteConnectionPool.cs
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
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace ImageScraper.Model
{
    /// <summary>
    /// Connection pooling for SQLite. Based on https://github.com/dotnet/efcore/issues/13837#issuecomment-821717602.
    /// </summary>
    public class SqliteConnectionPool : IDisposable
    {
        /// <summary>
        /// Defines the default pool size.
        /// </summary>
        private const int PoolSize = 10;

        private readonly object _lock = new();
        private readonly ILogger<SqliteConnectionPool> _log;
        private readonly string _dbPath;

        private readonly List<DbConnection> _availableConnections;
        private readonly List<DbConnection> _leasedConnections;
        private readonly List<DbConnection> _additionalConnections;

        /// <summary>
        /// Gets or sets a value indicating whether the pool has been disposed.
        /// </summary>
        public bool IsDisposed { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteConnectionPool"/> class.
        /// </summary>
        /// <param name="log">The logging instance.</param>
        public SqliteConnectionPool(ILogger<SqliteConnectionPool> log)
        {
            _log = log;

            _availableConnections = new List<DbConnection>(PoolSize);
            _leasedConnections = new List<DbConnection>(PoolSize);
            _additionalConnections = new List<DbConnection>();

            var cacheFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var applicationName = "image-indexer";

            _dbPath = Path.Combine(cacheFolder, applicationName, "indexer.sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? throw new InvalidOperationException());
        }

        /// <summary>
        /// Leases a connection from the pool.
        /// </summary>
        /// <returns>A database connection.</returns>
        public DbConnection LeaseConnection()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("Connection pool was finalized.");
            }

            lock (_lock)
            {
                if (_availableConnections.Count == 0)
                {
                    var connection = CreateAndOpenConnection();
                    if (_leasedConnections.Count >= PoolSize)
                    {
                        _log.LogWarning("Additional Sqlite connection created (pool exhaustion)");
                        _additionalConnections.Add(connection);

                        return connection;
                    }

                    _leasedConnections.Add(connection);
                    return connection;
                }
                else
                {
                    var connection = _availableConnections[0];
                    _availableConnections.RemoveAt(0);

                    _leasedConnections.Add(connection);
                    return connection;
                }
            }
        }

        /// <summary>
        /// Returns the given connection to the pool.
        /// </summary>
        /// <param name="connection">The connection to return.</param>
        public void ReturnConnection(DbConnection connection)
        {
            lock (_lock)
            {
                if (!_leasedConnections.Contains(connection) && !_additionalConnections.Contains(connection))
                {
                    throw new InvalidOperationException("That connection does not belong to the pool.");
                }

                if (_additionalConnections.Contains(connection))
                {
                    _additionalConnections.Remove(connection);
                    CloseAndDisposeConnection(connection);
                }
                else
                {
                    _leasedConnections.Remove(connection);

                    if (this.IsDisposed)
                    {
                        CloseAndDisposeConnection(connection);
                    }
                    else
                    {
                        _availableConnections.Add(connection);
                    }
                }
            }
        }

        private DbConnection CreateAndOpenConnection()
        {
            // create connection string
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            };

            // create and open connection
            var conn = new SqliteConnection(connectionString.ToString());
            conn.Open();

            // enable write-ahead log
            var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = 'wal'";
            cmd.ExecuteNonQuery();

            return conn;
        }

        private static void CloseAndDisposeConnection(IDbConnection connection)
        {
            if (connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }

            connection.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var connection in _leasedConnections)
                {
                    CloseAndDisposeConnection(connection);
                }

                foreach (var connection in _availableConnections)
                {
                    CloseAndDisposeConnection(connection);
                }

                foreach (var connection in _additionalConnections)
                {
                    CloseAndDisposeConnection(connection);
                }

                this.IsDisposed = true;
            }
        }
    }
}
