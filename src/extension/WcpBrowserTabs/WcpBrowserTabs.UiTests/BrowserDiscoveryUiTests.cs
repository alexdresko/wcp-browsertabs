using Xunit;
using Xunit.Abstractions;

namespace WcpBrowserTabs.UiTests;

public sealed class BrowserDiscoveryUiTests
{
    private readonly ITestOutputHelper output;

    public BrowserDiscoveryUiTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [SkippableTheory]
    [MemberData(nameof(BrowserDefinitions.AllTheoryData), MemberType = typeof(BrowserDefinitions))]
    [Trait("Category", "Ui")]
    public void DiscoversKnownTabs(BrowserDefinition browser)
    {
        var executablePath = browser.FindExecutablePath();
        Skip.If(executablePath is null, $"{browser.DisplayName} executable was not found.");

        using var session = BrowserTestSession.Start(browser, output, executablePath);
        var tabs = session.WaitForExpectedTabs();

        Assert.All(session.ExpectedTitles, title =>
            Assert.Contains(tabs, tab =>
                string.Equals(tab.Browser, browser.DisplayName, StringComparison.Ordinal) &&
                string.Equals(tab.TabTitle, title, StringComparison.Ordinal)));
    }

    [SkippableFact]
    [Trait("Category", "Ui")]
    public void DiscoversKnownTabsForAllInstalledSupportedBrowsers()
    {
        using var sessions = new BrowserTestSessionCollection();

        foreach (var browser in BrowserDefinitions.All)
        {
            var executablePath = browser.FindExecutablePath();
            if (executablePath is null)
            {
                output.WriteLine($"{browser.DisplayName} executable was not found; skipping in combined run.");
                continue;
            }

            sessions.Add(BrowserTestSession.Start(browser, output, executablePath));
        }

        Skip.If(sessions.Count == 0, "No supported browser executable was found.");

        foreach (var session in sessions)
        {
            var tabs = session.WaitForExpectedTabs();
            Assert.All(session.ExpectedTitles, title =>
                Assert.Contains(tabs, tab =>
                    string.Equals(tab.Browser, session.Browser.DisplayName, StringComparison.Ordinal) &&
                    string.Equals(tab.TabTitle, title, StringComparison.Ordinal)));
        }
    }

    private sealed class BrowserTestSessionCollection : IDisposable
    {
        private readonly List<BrowserTestSession> sessions = [];

        public int Count => sessions.Count;

        public void Add(BrowserTestSession session) => sessions.Add(session);

        public List<BrowserTestSession>.Enumerator GetEnumerator() => sessions.GetEnumerator();

        public void Dispose()
        {
            foreach (var session in sessions.AsEnumerable().Reverse())
            {
                session.Dispose();
            }
        }
    }
}
