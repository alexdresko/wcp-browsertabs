using Microsoft.CommandPalette.Extensions;
using Xunit;

namespace WcpBrowserTabs.Tests;

public sealed class WcpBrowserTabsPageTests
{
    [Fact]
    public async Task GetItemsStartsDiscoveryWithoutBlocking()
    {
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var discoveryMayFinish = new ManualResetEventSlim();
        var page = new WcpBrowserTabsPage(() =>
        {
            discoveryStarted.SetResult();
            discoveryMayFinish.Wait();
            return [Tab("Chrome", "Chrome Window", "Alpha Docs")];
        });

        try
        {
            var initialItems = page.GetItems();

            Assert.Empty(initialItems);
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            page.SearchText = "alpha";
            Assert.Empty(page.GetItems());

            discoveryMayFinish.Set();
            var loadedItems = await WaitForItemsAsync(page, expectedCount: 1);

            Assert.Equal("Alpha Docs", loadedItems[0].Title);
        }
        finally
        {
            discoveryMayFinish.Set();
        }
    }

    [Fact]
    public async Task SearchTextChangesDoNotRediscoverOrFilterInExtension()
    {
        var discoveryCalls = 0;
        var page = new WcpBrowserTabsPage(() =>
        {
            Interlocked.Increment(ref discoveryCalls);
            return
            [
                Tab("Chrome", "Research Window", "Alpha Docs"),
                Tab("Edge", "Work Window", "Beta Notes"),
            ];
        });

        var loadedItems = await WaitForItemsAsync(page, expectedCount: 2);
        Assert.Equal(2, loadedItems.Length);
        Assert.Equal(1, Volatile.Read(ref discoveryCalls));

        page.SearchText = "edge";
        var itemsAfterSearchChange = page.GetItems();

        Assert.Equal(2, itemsAfterSearchChange.Length);
        Assert.Contains(itemsAfterSearchChange, item => string.Equals(item.Title, "Alpha Docs", StringComparison.Ordinal));
        Assert.Contains(itemsAfterSearchChange, item => string.Equals(item.Title, "Beta Notes", StringComparison.Ordinal));
        Assert.Equal(1, Volatile.Read(ref discoveryCalls));
    }

    private static async Task<IListItem[]> WaitForItemsAsync(WcpBrowserTabsPage page, int expectedCount)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var items = page.GetItems();
            if (items.Length == expectedCount)
            {
                return items;
            }

            await Task.Delay(20);
        }

        return page.GetItems();
    }

    private static BrowserTab Tab(string browser, string windowTitle, string tabTitle)
    {
        return new BrowserTab(browser, 1, 100, windowTitle, tabTitle, IsActiveTab: false);
    }
}
