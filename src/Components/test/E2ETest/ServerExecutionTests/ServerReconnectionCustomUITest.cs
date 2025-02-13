// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BasicTestApp.Reconnection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.Components.E2ETest.ServerExecutionTests;
using Microsoft.AspNetCore.E2ETesting;
using Microsoft.AspNetCore.Hosting;
using OpenQA.Selenium;
using TestServer;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETest.ServerExecutionTests;

public class ServerReconnectionCustomUITest : ServerTestBase<BasicTestAppServerSiteFixture<ServerStartupWithCsp>>
{
    public ServerReconnectionCustomUITest(
        BrowserFixture browserFixture,
        BasicTestAppServerSiteFixture<ServerStartupWithCsp> serverFixture,
        ITestOutputHelper output)
        : base(browserFixture, serverFixture, output)
    {
    }

    protected override void InitializeAsyncCore()
    {
        Navigate($"{ServerPathBase}?useCustomReconnectModal=true");
        Browser.MountTestComponent<ReconnectionComponent>();
        Browser.Exists(By.Id("count"));
    }

    [Fact]
    public void CustomReconnectUIIsDisplayed()
    {
        Browser.Exists(By.Id("increment")).Click();

        var js = (IJavaScriptExecutor)Browser;
        js.ExecuteScript("Blazor._internal.forceCloseConnection()");

        // We should see the 'reconnecting' UI appear
        Browser.Equal("block", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));
        Browser.NotEqual(null, () => Browser.Exists(By.Id("components-reconnect-modal")).GetAttribute("open"));

        // The reconnect modal should not be a 'div' element created by the fallback JS code
        Browser.Equal("dialog", () => Browser.Exists(By.Id("components-reconnect-modal")).TagName);

        // Then it should disappear
        Browser.Equal("none", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));
        Browser.Equal(null, () => Browser.Exists(By.Id("components-reconnect-modal")).GetAttribute("open"));

        Browser.Exists(By.Id("increment")).Click();

        // Can dispatch events after reconnect
        Browser.Equal("2", () => Browser.Exists(By.Id("count")).Text);
    }

    [Fact]
    public void StyleSrcCSPIsNotViolated()
    {
        var js = (IJavaScriptExecutor)Browser;
        js.ExecuteScript("Blazor._internal.forceCloseConnection()");

        // We should see the 'reconnecting' UI appear
        Browser.Equal("block", () => Browser.Exists(By.Id("components-reconnect-modal")).GetCssValue("display"));

        // Check that there is no CSP-related error in the browser console
        var logs = Browser.Manage().Logs.GetLog(LogType.Browser);
        var styleErrors = logs.Where(
            log => log.Message.Contains("Refused to apply inline style because it violates the following Content Security Policy directive"));

        Assert.Empty(styleErrors);
    }
}
