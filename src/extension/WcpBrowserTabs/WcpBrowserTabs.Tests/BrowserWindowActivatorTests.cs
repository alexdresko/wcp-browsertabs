using Xunit;

namespace WcpBrowserTabs.Tests;

public sealed class BrowserWindowActivatorTests
{
    [Fact]
    public void TryBringToFrontReturnsFalseForZeroHandle()
    {
        Assert.False(BrowserWindowActivator.TryBringToFront(nint.Zero));
    }
}
