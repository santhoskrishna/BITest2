// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Testing;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.Tests
{
    public class HttpsConnectionAdapterOptionsTests
    {
        private static X509Certificate2 _x509Certificate2 = TestResources.GetTestCertificate();
        private static X509Certificate2 _x509Certificate2NoExt = TestResources.GetTestCertificate("no_extensions.pfx");

        [Fact]
        public void HandshakeTimeoutDefault()
        {
            Assert.Equal(TimeSpan.FromSeconds(10), new HttpsConnectionAdapterOptions().HandshakeTimeout);
        }

        [Fact]
        public void AllowAnyCertificateOverridesValidationFunc()
        {
            var connectionAdapterOptions = new HttpsConnectionAdapterOptions
            {
                ServerCertificate = _x509Certificate2,
                ClientCertificateMode = ClientCertificateMode.RequireCertificate,
                ClientCertificateValidation = (x509Cert, x509Chain, sslPolicyErrors) => false,
                AllowAnyClientCertificate = true
            };

            Assert.True(connectionAdapterOptions.ClientCertificateValidation(null, null, SslPolicyErrors.None));
        }

        [Theory]
        [MemberData(nameof(TimeoutValidData))]
        public void HandshakeTimeoutValid(TimeSpan value)
        {
            Assert.Equal(value, new HttpsConnectionAdapterOptions { HandshakeTimeout = value }.HandshakeTimeout);
        }

        [Fact]
        public void HandshakeTimeoutCanBeSetToInfinite()
        {
            Assert.Equal(TimeSpan.MaxValue, new HttpsConnectionAdapterOptions { HandshakeTimeout = Timeout.InfiniteTimeSpan }.HandshakeTimeout);
        }

        [Theory]
        [MemberData(nameof(TimeoutInvalidData))]
        public void HandshakeTimeoutInvalid(TimeSpan value)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new HttpsConnectionAdapterOptions { HandshakeTimeout = value });

            Assert.Equal("value", exception.ParamName);
            Assert.StartsWith(CoreStrings.PositiveTimeSpanRequired, exception.Message);
        }

        public static TheoryData<TimeSpan> TimeoutValidData => new TheoryData<TimeSpan>
        {
            TimeSpan.FromTicks(1),
            TimeSpan.MaxValue,
        };

        public static TheoryData<TimeSpan> TimeoutInvalidData => new TheoryData<TimeSpan>
        {
            TimeSpan.MinValue,
            TimeSpan.FromTicks(-1),
            TimeSpan.Zero
        };
    }
}
