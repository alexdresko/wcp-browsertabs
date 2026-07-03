using System.Diagnostics;
using Microsoft.Win32;

namespace WcpBrowserTabs.UiTests;

public enum BrowserLaunchKind
{
    Chromium,
    Firefox,
}

public sealed record BrowserDefinition(
    string DisplayName,
    string ProcessName,
    string ExecutableName,
    BrowserLaunchKind LaunchKind,
    IReadOnlyList<string> StandardExecutablePaths)
{
    public string? FindExecutablePath()
    {
        foreach (var candidate in StandardExecutablePaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var appPath = FindRegisteredAppPath(ExecutableName);
        if (appPath is not null)
        {
            return appPath;
        }

        return FindOnPath(ExecutableName).FirstOrDefault(File.Exists);
    }

    public void AddLaunchArguments(ProcessStartInfo startInfo, string profilePath, IReadOnlyList<string> pageUrls)
    {
        if (LaunchKind == BrowserLaunchKind.Chromium)
        {
            startInfo.ArgumentList.Add($"--user-data-dir={profilePath}");
            startInfo.ArgumentList.Add("--new-window");
            startInfo.ArgumentList.Add("--no-first-run");
            startInfo.ArgumentList.Add("--no-default-browser-check");
            startInfo.ArgumentList.Add("--disable-default-apps");
            startInfo.ArgumentList.Add("--disable-sync");
        }
        else
        {
            startInfo.ArgumentList.Add("-no-remote");
            startInfo.ArgumentList.Add("-profile");
            startInfo.ArgumentList.Add(profilePath);
        }

        foreach (var pageUrl in pageUrls)
        {
            startInfo.ArgumentList.Add(pageUrl);
        }
    }

    private static string[] FindOnPath(string executableName)
    {
        try
        {
            using var whereProcess = Process.Start(new ProcessStartInfo("where.exe", executableName)
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (whereProcess is null)
            {
                return [];
            }

            var output = whereProcess.StandardOutput.ReadToEnd();
            _ = whereProcess.StandardError.ReadToEnd();
            if (!whereProcess.WaitForExit(5000))
            {
                return [];
            }

            if (whereProcess.ExitCode != 0)
            {
                return [];
            }

            return output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch
        {
            return [];
        }
    }

    private static string? FindRegisteredAppPath(string executableName)
    {
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");
            var value = key?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
            {
                return value;
            }
        }

        return null;
    }
}
