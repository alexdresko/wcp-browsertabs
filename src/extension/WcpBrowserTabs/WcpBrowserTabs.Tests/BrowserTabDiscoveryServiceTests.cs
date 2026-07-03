using Xunit;

namespace WcpBrowserTabs.Tests;

public sealed class BrowserTabDiscoveryServiceTests
{
    [Theory]
    [InlineData("chrome", "Chrome")]
    [InlineData("CHROME", "Chrome")]
    [InlineData("msedge", "Edge")]
    [InlineData("firefox", "Firefox")]
    public void TryGetBrowserNameRecognizesSupportedBrowsers(string processName, string expectedBrowser)
    {
        var result = BrowserTabDiscoveryService.TryGetBrowserName(processName, out var browserName);

        Assert.True(result);
        Assert.Equal(expectedBrowser, browserName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notepad")]
    public void TryGetBrowserNameRejectsUnsupportedBrowsers(string? processName)
    {
        var result = BrowserTabDiscoveryService.TryGetBrowserName(processName, out var browserName);

        Assert.False(result);
        Assert.Empty(browserName);
    }

    [Fact]
    public void CreateTabsFiltersInvalidWindowsAndTabs()
    {
        var windows = new[]
        {
            Window(1, 100, " Chrome Window ", Tab("  Alpha  ", true), Tab("", false), Tab("   ", false), Tab(null, false)),
            Window(2, nint.Zero, "Edge Window", Tab("Ignored because handle is zero", true)),
            Window(3, 300, "Unsupported Window", Tab("Ignored because process is unsupported", true)),
            Window(4, 400, "Missing Process Window", Tab("Ignored because process name is missing", true)),
        };

        var tabs = BrowserTabDiscoveryService.CreateTabs(windows, ProcessNameFor).ToArray();

        var tab = Assert.Single(tabs);
        Assert.Equal("Chrome", tab.Browser);
        Assert.Equal(1, tab.ProcessId);
        Assert.Equal(100, tab.WindowHandle);
        Assert.Equal("Chrome Window", tab.WindowTitle);
        Assert.Equal("Alpha", tab.TabTitle);
        Assert.True(tab.IsActiveTab);
    }

    [Fact]
    public void CreateTabsDeduplicatesWithinSameBrowserWindowAndTitle()
    {
        var windows = new[]
        {
            Window(1, 100, "Chrome Window", Tab("Duplicate", true), Tab("Duplicate", false)),
            Window(1, 101, "Second Chrome Window", Tab("Duplicate", false)),
            Window(2, 200, "Edge Window", Tab("Duplicate", false)),
        };

        var tabs = BrowserTabDiscoveryService.CreateTabs(windows, ProcessNameFor).ToArray();

        Assert.Equal(3, tabs.Length);
        Assert.Contains(tabs, tab => tab.Browser == "Chrome" && tab.WindowHandle == 100 && tab.TabTitle == "Duplicate" && tab.IsActiveTab);
        Assert.Contains(tabs, tab => tab.Browser == "Chrome" && tab.WindowHandle == 101 && tab.TabTitle == "Duplicate");
        Assert.Contains(tabs, tab => tab.Browser == "Edge" && tab.WindowHandle == 200 && tab.TabTitle == "Duplicate");
    }

    [Fact]
    public void CreateTabsSortsActiveTabsBeforeBrowserAndTitle()
    {
        var windows = new[]
        {
            Window(2, 200, "Edge Window", Tab("Zulu", false), Tab("Alpha", true)),
            Window(1, 100, "Chrome Window", Tab("Bravo", false), Tab("Alpha", true)),
            Window(5, 500, "Firefox Window", Tab("Alpha", false)),
        };

        var tabs = BrowserTabDiscoveryService.CreateTabs(windows, ProcessNameFor).ToArray();

        Assert.Collection(
            tabs,
            tab => AssertTab(tab, "Chrome", "Alpha", isActive: true),
            tab => AssertTab(tab, "Edge", "Alpha", isActive: true),
            tab => AssertTab(tab, "Chrome", "Bravo", isActive: false),
            tab => AssertTab(tab, "Edge", "Zulu", isActive: false),
            tab => AssertTab(tab, "Firefox", "Alpha", isActive: false));
    }

    [Fact]
    public void CreateTabsValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => BrowserTabDiscoveryService.CreateTabs(null!, ProcessNameFor));
        Assert.Throws<ArgumentNullException>(() => BrowserTabDiscoveryService.CreateTabs([], null!));
    }

    [Fact]
    public void RunInStaThreadReturnsValueFromStaThread()
    {
        var apartmentState = ApartmentState.Unknown;

        var result = BrowserTabDiscoveryService.RunInStaThread(() =>
        {
            apartmentState = Thread.CurrentThread.GetApartmentState();
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Equal(ApartmentState.STA, apartmentState);
    }

    [Fact]
    public void RunInStaThreadRethrowsWorkerExceptions()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BrowserTabDiscoveryService.RunInStaThread<int>(() => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void TabActivationResultFactoriesSetExpectedState()
    {
        var success = TabActivationResult.Successful();
        var failure = TabActivationResult.Failure("nope");

        Assert.True(success.Success);
        Assert.Empty(success.ErrorMessage);
        Assert.False(failure.Success);
        Assert.Equal("nope", failure.ErrorMessage);
    }

    private static string? ProcessNameFor(int processId)
    {
        return processId switch
        {
            1 => "chrome",
            2 => "msedge",
            5 => "firefox",
            _ => null,
        };
    }

    private static BrowserTabDiscoveryService.DiscoveredBrowserWindow Window(
        int processId,
        nint windowHandle,
        string windowTitle,
        params BrowserTabDiscoveryService.DiscoveredBrowserTab[] tabs)
    {
        return new BrowserTabDiscoveryService.DiscoveredBrowserWindow(processId, windowHandle, windowTitle, tabs);
    }

    private static BrowserTabDiscoveryService.DiscoveredBrowserTab Tab(string? title, bool isActive)
    {
        return new BrowserTabDiscoveryService.DiscoveredBrowserTab(title, isActive);
    }

    private static void AssertTab(BrowserTab tab, string browser, string title, bool isActive)
    {
        Assert.Equal(browser, tab.Browser);
        Assert.Equal(title, tab.TabTitle);
        Assert.Equal(isActive, tab.IsActiveTab);
    }
}
