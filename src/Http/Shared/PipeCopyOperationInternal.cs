// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Http
{
    // FYI: In most cases the source will be a FileStream and the destination will be to the network.
    internal static class PipeCopyOperationInternal
    {
        private const int DefaultBufferSize = 4096;

        /// <summary>Asynchronously reads the given number of bytes from the source stream and writes them using pipe writer.</summary>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        /// <param name="source">The stream from which the contents will be copied.</param>
        /// <param name="writer"></param>
        /// <param name="count">The count of bytes to be copied.</param>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        public static Task CopyToAsync(Stream source, PipeWriter writer, long? count, CancellationToken cancel)
        {
            return CopyToAsync(source, writer, count, DefaultBufferSize, cancel);
        }

        /// <summary>Asynchronously reads the given number of bytes from the source stream and writes them using pipe writer.</summary>
        /// <returns>A task that represents the asynchronous copy operation.</returns>
        /// <param name="source">The stream from which the contents will be copied.</param>
        /// <param name="writer"></param>
        /// <param name="count">The count of bytes to be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer. This value must be greater than zero. The default size is 4096.</param>
        /// <param name="cancel">The token to monitor for cancellation requests.</param>
        public static async Task CopyToAsync(Stream source, PipeWriter writer, long? count, int bufferSize, CancellationToken cancel)
        {
            long? bytesRemaining = count;

            Debug.Assert(source != null);
            Debug.Assert(writer != null);
            Debug.Assert(!bytesRemaining.HasValue || bytesRemaining.GetValueOrDefault() >= 0);

            while (true)
            {
                // The natural end of the range.
                if (bytesRemaining.HasValue && bytesRemaining.GetValueOrDefault() <= 0)
                {
                    return;
                }

                cancel.ThrowIfCancellationRequested();

                var readLength = bufferSize;
                if (bytesRemaining.HasValue)
                {
                    readLength = (int)Math.Min(bytesRemaining.GetValueOrDefault(), readLength);
                }

                var memory = writer.GetMemory(readLength);
                var read = await source.ReadAsync(memory, cancel);

                if (bytesRemaining.HasValue)
                {
                    bytesRemaining -= read;
                }

                // End of the source stream.
                if (read == 0)
                {
                    break;
                }

                writer.Advance(read);

                cancel.ThrowIfCancellationRequested();

                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
    }
}
