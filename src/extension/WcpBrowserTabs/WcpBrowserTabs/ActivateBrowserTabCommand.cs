// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace WcpBrowserTabs;

internal sealed partial class ActivateBrowserTabCommand : InvokableCommand
{
    private readonly BrowserTab _tab;

    public ActivateBrowserTabCommand(BrowserTab tab)
    {
        _tab = tab;
        Name = "Switch to tab";
        Icon = new IconInfo("\uE8A7");
    }

    public override ICommandResult Invoke()
    {
        if (!BrowserWindowActivator.TryBringToFront(_tab.WindowHandle))
        {
            return Fail("Could not activate the browser window.");
        }

        var activationResult = BrowserTabDiscoveryService.TryActivateTab(_tab);
        if (!activationResult.Success)
        {
            return Fail(activationResult.ErrorMessage);
        }

        return CommandResult.Dismiss();
    }

    private static CommandResult Fail(string message)
    {
        new ToastStatusMessage(message).Show();
        return CommandResult.KeepOpen();
    }
}
