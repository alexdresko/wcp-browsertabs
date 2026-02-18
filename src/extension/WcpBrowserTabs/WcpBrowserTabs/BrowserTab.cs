// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WcpBrowserTabs;

internal sealed record BrowserTab(
    string Browser,
    int ProcessId,
    nint WindowHandle,
    string WindowTitle,
    string TabTitle,
    bool IsActiveTab);

