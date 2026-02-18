// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WcpBrowserTabs;

internal sealed partial class WcpBrowserTabsPage : DynamicListPage
{
    public WcpBrowserTabsPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Browser Tabs";
        Name = "Open";
        PlaceholderText = "Search open Chrome and Edge tabs";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icon,
            Title = "No browser tabs found",
            Subtitle = "Open Chrome or Edge tabs, then try again.",
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var searchText = SearchText?.Trim() ?? string.Empty;
        var matchingTabs = BrowserTabDiscoveryService
            .GetTabs()
            .Where(tab => MatchesSearch(tab, searchText))
            .ToArray();

        if (matchingTabs.Length == 0)
        {
            return [];
        }

        var items = new List<IListItem>(matchingTabs.Length);
        foreach (var tab in matchingTabs)
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

    private static bool MatchesSearch(BrowserTab tab, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return tab.TabTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            tab.WindowTitle.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            tab.Browser.Contains(searchText, StringComparison.OrdinalIgnoreCase);
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
