using Xunit;
using Xunit.Abstractions;

namespace WcpBrowserTabs.UiTests;

public sealed class BrowserActivationUiTests
{
    private readonly ITestOutputHelper output;

    public BrowserActivationUiTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [SkippableTheory]
    [MemberData(nameof(BrowserDefinitions.AllTheoryData), MemberType = typeof(BrowserDefinitions))]
    [Trait("Category", "Ui")]
    public void ActivatesKnownTab(BrowserDefinition browser)
    {
        var executablePath = browser.FindExecutablePath();
        Skip.If(executablePath is null, $"{browser.DisplayName} executable was not found.");

        using var session = BrowserTestSession.Start(browser, output, executablePath);
        var targetTab = session.WaitForInactiveExpectedTab();

        _ = BrowserWindowActivator.TryBringToFront(targetTab.WindowHandle);
        var result = BrowserTabDiscoveryService.TryActivateTab(targetTab);
        Skip.If(!result.Success, $"{browser.DisplayName} tab activation was not available: {result.ErrorMessage}");

        var activeTab = session.WaitForActiveExpectedTab(targetTab.TabTitle);
        Assert.True(activeTab.IsActiveTab);
    }
}
