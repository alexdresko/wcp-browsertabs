// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WcpBrowserTabs;

internal sealed partial class WcpBrowserTabsPage : ListPage
{
    private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(2);

    private readonly Func<IReadOnlyList<BrowserTab>> getTabs;
    private readonly object refreshLock = new();
    private IReadOnlyList<BrowserTab> tabs = [];
    private DateTimeOffset lastRefreshUtc = DateTimeOffset.MinValue;
    private Task? refreshTask;

    public WcpBrowserTabsPage()
        : this(BrowserTabDiscoveryService.GetTabs)
    {
    }

    internal WcpBrowserTabsPage(Func<IReadOnlyList<BrowserTab>> getTabs)
    {
        ArgumentNullException.ThrowIfNull(getTabs);

        this.getTabs = getTabs;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Browser Tabs";
        Name = "Open";
        PlaceholderText = "Search open Chrome, Edge, and Firefox tabs";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = "No browser tabs found",
            Subtitle = "Open Chrome, Edge, or Firefox tabs, then try again.",
        };
    }

    public override IListItem[] GetItems()
    {
        EnsureRefreshStarted();

        var currentTabs = GetTabsSnapshot();

        if (currentTabs.Length == 0)
        {
            return [];
        }

        var items = new List<IListItem>(currentTabs.Length);
        foreach (var tab in currentTabs)
        {
            items.Add(new ListItem(new ActivateBrowserTabCommand(tab))
            {
                Title = tab.TabTitle,
                Subtitle = $"{tab.Browser} - {tab.WindowTitle}",
                Tags = GetTags(tab),
            });
        }

        return [.. items];
    }

    private BrowserTab[] GetTabsSnapshot()
    {
        lock (refreshLock)
        {
            return [.. tabs];
        }
    }

    private void EnsureRefreshStarted()
    {
        var now = DateTimeOffset.UtcNow;

        lock (refreshLock)
        {
            if (refreshTask is { IsCompleted: false })
            {
                return;
            }

            if (lastRefreshUtc != DateTimeOffset.MinValue && now - lastRefreshUtc < MinimumRefreshInterval)
            {
                return;
            }

            IsLoading = true;
            refreshTask = RefreshTabsAsync();
        }
    }

    private async Task RefreshTabsAsync()
    {
        IReadOnlyList<BrowserTab>? refreshedTabs = null;

        try
        {
            refreshedTabs = await Task.Run(getTabs).ConfigureAwait(false);
        }
        catch
        {
            // Keep the previous snapshot if discovery fails.
        }

        lock (refreshLock)
        {
            if (refreshedTabs is not null)
            {
                tabs = refreshedTabs;
            }

            lastRefreshUtc = DateTimeOffset.UtcNow;
        }

        IsLoading = false;
        RaiseItemsChanged();
    }

    private static Tag[] GetTags(BrowserTab tab)
    {
        var tags = new List<Tag> { new(tab.Browser) };
        if (tab.IsActiveTab)
        {
            tags.Add(new Tag("Active"));
        }

        return [.. tags];
    }
}
