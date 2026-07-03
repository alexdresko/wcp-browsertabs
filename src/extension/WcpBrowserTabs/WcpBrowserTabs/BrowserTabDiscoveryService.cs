// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Threading;
using Interop.UIAutomationClient;

namespace WcpBrowserTabs;

internal static class BrowserTabDiscoveryService
{
    public static IReadOnlyList<BrowserTab> GetTabs()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return RunInStaThread(GetTabsWindows);
    }

    public static TabActivationResult TryActivateTab(BrowserTab targetTab)
    {
        ArgumentNullException.ThrowIfNull(targetTab);

        if (!OperatingSystem.IsWindows())
        {
            return TabActivationResult.Failure("Browser tab activation is only supported on Windows.");
        }

        return RunInStaThread(() => TryActivateTabWindows(targetTab));
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<BrowserTab> GetTabsWindows()
    {
        var automation = new CUIAutomation8();
        var root = automation.GetRootElement();
        var windowCondition = automation.CreatePropertyCondition(
            UIA_PropertyIds.UIA_ControlTypePropertyId,
            UIA_ControlTypeIds.UIA_WindowControlTypeId);
        var tabCondition = automation.CreatePropertyCondition(
            UIA_PropertyIds.UIA_ControlTypePropertyId,
            UIA_ControlTypeIds.UIA_TabItemControlTypeId);

        var windows = root.FindAll(TreeScope.TreeScope_Children, windowCondition);
        var windowsSnapshot = new List<DiscoveredBrowserWindow>();

        for (var i = 0; i < windows.Length; i++)
        {
            try
            {
                var window = windows.GetElement(i);
                var windowHandle = window.CurrentNativeWindowHandle;
                var windowTitle = window.CurrentName?.Trim() ?? string.Empty;
                var tabItems = window.FindAll(TreeScope.TreeScope_Descendants, tabCondition);
                var tabs = new List<DiscoveredBrowserTab>();

                for (var j = 0; j < tabItems.Length; j++)
                {
                    try
                    {
                        var tab = tabItems.GetElement(j);
                        tabs.Add(new DiscoveredBrowserTab(tab.CurrentName, IsActiveTab(tab)));
                    }
                    catch
                    {
                        // Ignore individual tab failures and continue building partial snapshots.
                    }
                }

                windowsSnapshot.Add(new DiscoveredBrowserWindow(
                    window.CurrentProcessId,
                    windowHandle,
                    windowTitle,
                    tabs));
            }
            catch
            {
                // Ignore individual window failures and continue building partial snapshots.
            }
        }

        return CreateTabs(windowsSnapshot, GetProcessName);
    }

    internal static IReadOnlyList<BrowserTab> CreateTabs(
        IEnumerable<DiscoveredBrowserWindow> windows,
        Func<int, string?> getProcessName)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(getProcessName);

        var tabs = new List<BrowserTab>();
        var seenTabs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var window in windows)
        {
            if (!TryGetBrowserName(getProcessName(window.ProcessId), out var browser))
            {
                continue;
            }

            if (window.WindowHandle == nint.Zero)
            {
                continue;
            }

            foreach (var tab in window.Tabs)
            {
                var tabTitle = tab.Title?.Trim();
                if (string.IsNullOrWhiteSpace(tabTitle))
                {
                    continue;
                }

                var tabKey = $"{browser}\u001f{window.WindowHandle}\u001f{tabTitle}";
                if (!seenTabs.Add(tabKey))
                {
                    continue;
                }

                tabs.Add(new BrowserTab(
                    browser,
                    window.ProcessId,
                    window.WindowHandle,
                    window.WindowTitle.Trim(),
                    tabTitle,
                    tab.IsActiveTab));
            }
        }

        return tabs
            .OrderByDescending(tab => tab.IsActiveTab)
            .ThenBy(tab => tab.Browser, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tab => tab.TabTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static TabActivationResult TryActivateTabWindows(BrowserTab targetTab)
    {
        try
        {
            var automation = new CUIAutomation8();
            var targetWindow = automation.ElementFromHandle(targetTab.WindowHandle);
            if (targetWindow is null)
            {
                return TabActivationResult.Failure("The browser window is no longer available.");
            }

            if (targetWindow.CurrentProcessId != targetTab.ProcessId)
            {
                return TabActivationResult.Failure("The browser window changed before activation could complete.");
            }

            var tabCondition = automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ControlTypePropertyId,
                UIA_ControlTypeIds.UIA_TabItemControlTypeId);
            var tabItems = targetWindow.FindAll(TreeScope.TreeScope_Descendants, tabCondition);

            for (var i = 0; i < tabItems.Length; i++)
            {
                try
                {
                    var tab = tabItems.GetElement(i);
                    var tabTitle = tab.CurrentName?.Trim() ?? string.Empty;
                    if (!string.Equals(tabTitle, targetTab.TabTitle, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var selectionPattern = tab.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) as IUIAutomationSelectionItemPattern;
                    if (selectionPattern is null)
                    {
                        continue;
                    }

                    selectionPattern.Select();
                    return TabActivationResult.Successful();
                }
                catch
                {
                    // Keep searching for another matching tab item.
                }
            }

            return TabActivationResult.Failure("The selected tab is no longer available.");
        }
        catch (Exception ex)
        {
            return TabActivationResult.Failure($"Unable to activate the tab: {ex.Message}");
        }
    }

    private static string? GetProcessName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    internal static bool TryGetBrowserName(string? processName, out string browserName)
    {
        browserName = string.Empty;
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        if (processName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Chrome";
            return true;
        }

        if (processName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Edge";
            return true;
        }

        if (processName.Equals("firefox", StringComparison.OrdinalIgnoreCase))
        {
            browserName = "Firefox";
            return true;
        }

        return false;
    }

    internal static T RunInStaThread<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        T? result = default;
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }

    internal sealed record DiscoveredBrowserWindow(
        int ProcessId,
        nint WindowHandle,
        string WindowTitle,
        IReadOnlyList<DiscoveredBrowserTab> Tabs);

    internal sealed record DiscoveredBrowserTab(string? Title, bool IsActiveTab);

    [SupportedOSPlatform("windows")]
    private static bool IsActiveTab(IUIAutomationElement tab)
    {
        try
        {
            var selectionPattern = tab.GetCurrentPattern(UIA_PatternIds.UIA_SelectionItemPatternId) as IUIAutomationSelectionItemPattern;
            return selectionPattern?.CurrentIsSelected == 1;
        }
        catch
        {
            return false;
        }
    }
}

internal readonly record struct TabActivationResult(bool Success, string ErrorMessage)
{
    public static TabActivationResult Successful() => new(true, string.Empty);

    public static TabActivationResult Failure(string errorMessage) => new(false, errorMessage);
}
