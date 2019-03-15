﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Http2.Internal
{
    internal class ThreadPoolAwaitable : ICriticalNotifyCompletion
    {
        public static ThreadPoolAwaitable Instance = new ThreadPoolAwaitable();

        private ThreadPoolAwaitable()
        {
        }

        public ThreadPoolAwaitable GetAwaiter() => this;
        public bool IsCompleted => false;

        public void GetResult()
        {
        }

        public void OnCompleted(Action continuation)
        {
            ThreadPool.UnsafeQueueUserWorkItem(state => ((Action)state)(), continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }
    }
}
