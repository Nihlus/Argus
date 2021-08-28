//
//  ServiceStatusReport.cs
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

using Argus.Common.Messages;
using JetBrains.Annotations;

namespace Argus.Coordinator.Model
{
    /// <summary>
    /// Represents a service report stored in the database.
    /// </summary>
    public class ServiceStatusReport
    {
        /// <summary>
        /// Gets the database ID of the service state.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets the report.
        /// </summary>
        public StatusReport Report { get; init; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceStatusReport"/> class. Required by EF Core.
        /// </summary>
        [UsedImplicitly]
        protected ServiceStatusReport()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceStatusReport"/> class.
        /// </summary>
        /// <param name="report">The status report.</param>
        public ServiceStatusReport(StatusReport report)
        {
            this.Report = report;
        }
    }
}
