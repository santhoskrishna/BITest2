// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Quic;
using Microsoft.AspNetCore.Testing;
using Microsoft.Win32;

namespace Microsoft.AspNetCore.Server.HttpSys
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class Http3SupportedAttribute : Attribute, ITestCondition
    {
        // We have the same OS and TLS version requirements as MsQuic so check that first.
        public bool IsMet => QuicImplementationProviders.MsQuic.IsSupported && IsRegKeySet;

        public string SkipReason => "HTTP/3 is not supported or enabled on the current test machine";

        private static bool IsRegKeySet
        {
            get
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\HTTP\Parameters");
                    var value = key.GetValue("EnableHttp3");
                    var enabled = value as int? == 1;
                    return enabled;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
