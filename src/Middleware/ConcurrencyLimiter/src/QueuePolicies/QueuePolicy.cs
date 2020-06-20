// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.ConcurrencyLimiter
{
    internal class QueuePolicy : IQueuePolicy, IDisposable
    {
        private readonly int _maxTotalRequest;
        private readonly SemaphoreSlim _serverSemaphore;

        private readonly object _totalRequestsLock = new object();

        private int _totalRequests;

        public int TotalRequests => _totalRequests;

        public QueuePolicy(IOptions<QueuePolicyOptions> options)
        {
            var queuePolicyOptions = options.Value;

            var maxConcurrentRequests = queuePolicyOptions.MaxConcurrentRequests;
            if (maxConcurrentRequests <= 0)
            {
                throw new ArgumentException(nameof(maxConcurrentRequests), "MaxConcurrentRequests must be a positive integer.");
            }

            var requestQueueLimit = queuePolicyOptions.RequestQueueLimit;
            if (requestQueueLimit < 0)
            {
                throw new ArgumentException(nameof(requestQueueLimit), "The RequestQueueLimit cannot be a negative number.");
            }

            _serverSemaphore = new SemaphoreSlim(maxConcurrentRequests);

            _maxTotalRequest = maxConcurrentRequests + requestQueueLimit;
        }

        public ValueTask<bool> TryEnterAsync()
        {
            // a return value of 'false' indicates that the request is rejected
            // a return value of 'true' indicates that the request may proceed
            // _serverSemaphore.Release is *not* called in this method, it is called externally when requests leave the server

            lock (_totalRequestsLock)
            {
                if (_totalRequests >= _maxTotalRequest)
                {
                    return new ValueTask<bool>(false);
                }

                _totalRequests++;
            }

            Task task = _serverSemaphore.WaitAsync();
            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<bool>(true);
            }

            return SemaphoreAwaited(task);
        }

        public void OnExit()
        {
            _serverSemaphore.Release();

            lock (_totalRequestsLock)
            {
                _totalRequests--;
            }
        }

        public void Dispose()
        {
            _serverSemaphore.Dispose();
        }

        private async ValueTask<bool> SemaphoreAwaited(Task task)
        {
            await task;

            return true;
        }
    }
}
