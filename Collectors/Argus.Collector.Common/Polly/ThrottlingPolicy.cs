//
//  ThrottlingPolicy.cs
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

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Argus.Collector.Common.Polly;

/// <summary>
/// Acts as a preemptive throttling policy, allowing at most N requests inside the interval T.
/// </summary>
public class ThrottlingPolicy : AsyncPolicy<HttpResponseMessage>
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottlingPolicy"/> class.
    /// </summary>
    /// <param name="requestCount">The number of requests allowed within a certain interval.</param>
    /// <param name="interval">The interval between requests.</param>
    public ThrottlingPolicy(int requestCount, TimeSpan interval)
    {
        _semaphore = new SemaphoreSlim(requestCount, requestCount);
        _interval = interval;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> ImplementationAsync
    (
        Func<Context, CancellationToken, Task<HttpResponseMessage>> action,
        Context context,
        CancellationToken cancellationToken,
        bool continueOnCapturedContext
    )
    {
        await _semaphore.WaitAsync(cancellationToken);

        var actionTask = action(context, cancellationToken);

        // While not very nice, the actual release itself will execute as an unobserved task
        _ = actionTask.ContinueWith
        (
            async _ =>
            {
                await Task.Delay(_interval, cancellationToken);
                _semaphore.Release();
            },
            cancellationToken
        );

        return await actionTask;
    }
}
