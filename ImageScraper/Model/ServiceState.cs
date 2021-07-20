//
//  ServiceState.cs
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
using JetBrains.Annotations;

namespace ImageScraper.Model
{
    /// <summary>
    /// Represents persisted state of a service indexer.
    /// </summary>
    public class ServiceState
    {
        /// <summary>
        /// Gets the database ID of the service state.
        /// </summary>
        public virtual Guid Id { get; init; }

        /// <summary>
        /// Gets or sets the name of the service.
        /// </summary>
        public virtual string Name { get; set; } = null!;

        /// <summary>
        /// Gets or sets the serialized representation of a resume point that the service understands. This can be an
        /// ID, a nonce, a token, etc.
        /// </summary>
        public virtual string? ResumePoint { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceState"/> class. Required by EF Core.
        /// </summary>
        [UsedImplicitly]
        protected ServiceState()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceState"/> class.
        /// </summary>
        /// <param name="name">The name of the service that uses this state.</param>
        public ServiceState(string name)
        {
            this.Id = Guid.Empty;
            this.Name = name;
        }
    }
}
