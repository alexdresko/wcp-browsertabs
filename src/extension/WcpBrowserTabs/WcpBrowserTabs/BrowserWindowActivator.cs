// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace WcpBrowserTabs;

internal static class BrowserWindowActivator
{
    public static bool TryBringToFront(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return false;
        }

        try
        {
            if (NativeMethods.IsIconic(windowHandle))
            {
                _ = NativeMethods.ShowWindow(windowHandle, ShowWindowCommand.Restore);
            }
            else
            {
                _ = NativeMethods.ShowWindow(windowHandle, ShowWindowCommand.Show);
            }

            return NativeMethods.SetForegroundWindow(windowHandle);
        }
        catch
        {
            return false;
        }
    }

    private enum ShowWindowCommand
    {
        Show = 5,
        Restore = 9,
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(nint hWnd, ShowWindowCommand nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(nint hWnd);
    }
}

