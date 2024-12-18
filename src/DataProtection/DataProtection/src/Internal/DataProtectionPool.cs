// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.DataProtection.Internal;
internal static class DataProtectionPool
{
    private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();

    public static byte[] Rent(int length) => _pool.Rent(length);
    public static void Return(byte[] array, bool clearArray = false) => _pool.Return(array, clearArray);
}
