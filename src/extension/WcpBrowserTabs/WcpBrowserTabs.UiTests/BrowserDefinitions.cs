using Xunit;

namespace WcpBrowserTabs.UiTests;

public static class BrowserDefinitions
{
    public static readonly BrowserDefinition Chrome = new(
        "Chrome",
        "chrome",
        "chrome.exe",
        BrowserLaunchKind.Chromium,
        [
            InProgramFiles(@"Google\Chrome\Application\chrome.exe"),
            InProgramFilesX86(@"Google\Chrome\Application\chrome.exe"),
            InLocalAppData(@"Google\Chrome\Application\chrome.exe"),
        ]);

    public static readonly BrowserDefinition Edge = new(
        "Edge",
        "msedge",
        "msedge.exe",
        BrowserLaunchKind.Chromium,
        [
            InProgramFilesX86(@"Microsoft\Edge\Application\msedge.exe"),
            InProgramFiles(@"Microsoft\Edge\Application\msedge.exe"),
            InLocalAppData(@"Microsoft\Edge\Application\msedge.exe"),
        ]);

    public static readonly BrowserDefinition Firefox = new(
        "Firefox",
        "firefox",
        "firefox.exe",
        BrowserLaunchKind.Firefox,
        [
            InProgramFiles(@"Mozilla Firefox\firefox.exe"),
            InProgramFilesX86(@"Mozilla Firefox\firefox.exe"),
            InLocalAppData(@"Mozilla Firefox\firefox.exe"),
        ]);

    public static IReadOnlyList<BrowserDefinition> All { get; } = [Chrome, Edge, Firefox];

    public static TheoryData<BrowserDefinition> AllTheoryData()
    {
        var data = new TheoryData<BrowserDefinition>();
        foreach (var browser in All)
        {
            data.Add(browser);
        }

        return data;
    }

    private static string InProgramFiles(string relativePath) => InFolder(Environment.SpecialFolder.ProgramFiles, relativePath);

    private static string InProgramFilesX86(string relativePath) => InFolder(Environment.SpecialFolder.ProgramFilesX86, relativePath);

    private static string InLocalAppData(string relativePath) => InFolder(Environment.SpecialFolder.LocalApplicationData, relativePath);

    private static string InFolder(Environment.SpecialFolder folder, string relativePath)
    {
        var root = Environment.GetFolderPath(folder);
        return string.IsNullOrWhiteSpace(root) ? relativePath : Path.Combine(root, relativePath);
    }
}
