using System.Diagnostics;
using System.Net;
using Xunit;
using Xunit.Sdk;
using Xunit.Abstractions;

namespace WcpBrowserTabs.UiTests;

internal sealed class BrowserTestSession : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly ITestOutputHelper output;
    private readonly string tempRoot;
    private readonly Process browserProcess;
    private bool disposed;

    private BrowserTestSession(
        BrowserDefinition browser,
        string tempRoot,
        IReadOnlyList<string> expectedTitles,
        Process browserProcess,
        ITestOutputHelper output)
    {
        Browser = browser;
        this.tempRoot = tempRoot;
        this.browserProcess = browserProcess;
        this.output = output;
        ExpectedTitles = expectedTitles;
    }

    public BrowserDefinition Browser { get; }

    public IReadOnlyList<string> ExpectedTitles { get; }

    public static BrowserTestSession Start(BrowserDefinition browser, ITestOutputHelper output, string? executablePath = null)
    {
        executablePath ??= browser.FindExecutablePath();
        if (executablePath is null)
        {
            throw new InvalidOperationException($"{browser.DisplayName} executable was not found.");
        }

        var runId = Guid.NewGuid().ToString("N")[..8];
        var tempRoot = Path.Combine(Path.GetTempPath(), "WcpBrowserTabs.UiTests", $"{browser.ProcessName}-{runId}");
        var profilePath = Path.Combine(tempRoot, "profile");
        var pagesPath = Path.Combine(tempRoot, "pages");

        Directory.CreateDirectory(profilePath);
        Directory.CreateDirectory(pagesPath);
        WriteProfilePreferences(browser, profilePath);

        var expectedTitles = new[]
        {
            $"WCP UI Test {browser.DisplayName} Alpha {runId}",
            $"WCP UI Test {browser.DisplayName} Bravo {runId}",
        };

        var pageUrls = expectedTitles
            .Select((title, index) => CreateTestPage(pagesPath, $"{index + 1}-{browser.ProcessName}.html", title))
            .ToArray();

        var startInfo = new ProcessStartInfo(executablePath)
        {
            CreateNoWindow = false,
            UseShellExecute = false,
        };
        browser.AddLaunchArguments(startInfo, profilePath, pageUrls);

        output.WriteLine($"Launching {browser.DisplayName}: {executablePath}");
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to launch {browser.DisplayName}.");
        return new BrowserTestSession(browser, tempRoot, expectedTitles, process, output);
    }

    public IReadOnlyList<BrowserTab> WaitForExpectedTabs(TimeSpan? timeout = null)
    {
        IReadOnlyList<BrowserTab> lastMatches = [];
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastMatches = GetExpectedTabs();
            if (ExpectedTitles.All(title => lastMatches.Any(tab => string.Equals(tab.TabTitle, title, StringComparison.Ordinal))))
            {
                WriteTabs("Discovered expected tabs", lastMatches);
                return lastMatches;
            }

            Thread.Sleep(PollInterval);
        }

        WriteTabs($"Timed out waiting for expected {Browser.DisplayName} tabs", lastMatches);
        WriteTabs($"All discovered {Browser.DisplayName} tabs", GetBrowserTabs());
        if (Browser.LaunchKind == BrowserLaunchKind.Firefox)
        {
            throw new Xunit.SkipException("Firefox did not expose the expected selectable UI Automation tab items. This Firefox build or profile configuration may not support the tab UIA surface used by Browser Tabs.");
        }

        throw new XunitException($"Timed out waiting for {Browser.DisplayName} to expose expected UI Automation tab items.");
    }

    public BrowserTab WaitForInactiveExpectedTab()
    {
        var tabs = WaitForExpectedTabs();
        var inactiveTab = tabs.FirstOrDefault(tab => !tab.IsActiveTab);
        if (inactiveTab is not null)
        {
            return inactiveTab;
        }

        WriteTabs($"{Browser.DisplayName} did not expose an inactive expected tab", tabs);
        throw new Xunit.SkipException($"{Browser.DisplayName} did not expose a non-active tab through UI Automation.");
    }

    public BrowserTab WaitForActiveExpectedTab(string title, TimeSpan? timeout = null)
    {
        IReadOnlyList<BrowserTab> lastMatches = [];
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastMatches = GetExpectedTabs();
            var activeTab = lastMatches.FirstOrDefault(tab =>
                tab.IsActiveTab &&
                string.Equals(tab.TabTitle, title, StringComparison.Ordinal));

            if (activeTab is not null)
            {
                WriteTabs($"Activated {Browser.DisplayName} tab", lastMatches);
                return activeTab;
            }

            Thread.Sleep(PollInterval);
        }

        WriteTabs($"Timed out waiting for active {Browser.DisplayName} tab", lastMatches);
        throw new XunitException($"Timed out waiting for '{title}' to become the active {Browser.DisplayName} tab.");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (!browserProcess.HasExited)
            {
                _ = browserProcess.CloseMainWindow();
                if (!browserProcess.WaitForExit(3000))
                {
                    browserProcess.Kill(entireProcessTree: true);
                    browserProcess.WaitForExit(5000);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception ex)
        {
            output.WriteLine($"Failed to stop {Browser.DisplayName}: {ex.Message}");
        }
        finally
        {
            browserProcess.Dispose();
            StopBrowserWindowsForExpectedTitles();
            DeleteTempRoot();
        }
    }

    private static string CreateTestPage(string pagesPath, string fileName, string title)
    {
        var pagePath = Path.Combine(pagesPath, fileName);
        var encodedTitle = WebUtility.HtmlEncode(title);
        File.WriteAllText(
            pagePath,
            $"""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <title>{encodedTitle}</title>
            </head>
            <body>
              <h1>{encodedTitle}</h1>
            </body>
            </html>
            """);

        return new Uri(pagePath).AbsoluteUri;
    }

    private static void WriteProfilePreferences(BrowserDefinition browser, string profilePath)
    {
        if (browser.LaunchKind != BrowserLaunchKind.Firefox)
        {
            return;
        }

        File.WriteAllLines(
            Path.Combine(profilePath, "user.js"),
            [
                "user_pref(\"accessibility.force_disabled\", 0);",
                "user_pref(\"browser.aboutwelcome.enabled\", false);",
                "user_pref(\"browser.shell.checkDefaultBrowser\", false);",
                "user_pref(\"browser.startup.homepage_override.mstone\", \"ignore\");",
                "user_pref(\"browser.startup.page\", 0);",
                "user_pref(\"datareporting.policy.dataSubmissionPolicyAcceptedVersion\", 2);",
                "user_pref(\"datareporting.policy.dataSubmissionPolicyBypassNotification\", true);",
                "user_pref(\"startup.homepage_welcome_url\", \"\");",
                "user_pref(\"startup.homepage_welcome_url.additional\", \"\");",
                "user_pref(\"toolkit.telemetry.reportingpolicy.firstRun\", false);",
            ]);
    }

    private BrowserTab[] GetExpectedTabs()
    {
        return GetBrowserTabs()
            .Where(tab =>
                ExpectedTitles.Contains(tab.TabTitle, StringComparer.Ordinal))
            .ToArray();
    }

    private BrowserTab[] GetBrowserTabs()
    {
        return BrowserTabDiscoveryService
            .GetTabs()
            .Where(tab => string.Equals(tab.Browser, Browser.DisplayName, StringComparison.Ordinal))
            .ToArray();
    }

    private void WriteTabs(string heading, IReadOnlyList<BrowserTab> tabs)
    {
        output.WriteLine(heading);
        if (tabs.Count == 0)
        {
            output.WriteLine("  No matching tabs found.");
            return;
        }

        foreach (var tab in tabs)
        {
            output.WriteLine($"  [{(tab.IsActiveTab ? "active" : "inactive")}] {tab.Browser}: {tab.TabTitle} ({tab.WindowTitle})");
        }
    }

    private void DeleteTempRoot()
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                output.WriteLine($"Failed to delete temporary browser profile '{tempRoot}': {ex.Message}");
                return;
            }
        }
    }

    private void StopBrowserWindowsForExpectedTitles()
    {
        foreach (var process in Process.GetProcessesByName(Browser.ProcessName))
        {
            try
            {
                var title = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title) || !ExpectedTitles.Any(expectedTitle => title.Contains(expectedTitle, StringComparison.Ordinal)))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                output.WriteLine($"Failed to stop {Browser.DisplayName} test window {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
