using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Interop.UIAutomationClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/browser-tabs", () =>
{
    try
    {
        var tabs = BrowserTabReader.GetOpenTabs();
        return Results.Ok(tabs);
    }
    catch (PlatformNotSupportedException ex)
    {
        return Results.Problem(
            title: "Browser tab discovery is only available on Windows.",
            detail: ex.Message,
            statusCode: StatusCodes.Status501NotImplemented);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Failed to read browser tabs.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GetBrowserTabs");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

internal sealed record BrowserTab(string Browser, string WindowTitle, string TabTitle, bool IsActiveTab);

internal static class BrowserTabReader
{
    public static IReadOnlyList<BrowserTab> GetOpenTabs()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This endpoint currently only supports Windows hosts.");
        }

        return RunInStaThread(GetOpenTabsWindows);
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<BrowserTab> GetOpenTabsWindows()
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
        var tabs = new List<BrowserTab>();
        var seenTabs = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < windows.Length; i++)
        {
            var window = windows.GetElement(i);
            if (!TryGetBrowserName(window.CurrentProcessId, out var browser))
            {
                continue;
            }

            var windowTitle = window.CurrentName?.Trim() ?? string.Empty;
            var tabItems = window.FindAll(TreeScope.TreeScope_Descendants, tabCondition);

            for (var j = 0; j < tabItems.Length; j++)
            {
                var tab = tabItems.GetElement(j);
                var tabTitle = tab.CurrentName?.Trim();
                if (string.IsNullOrWhiteSpace(tabTitle))
                {
                    continue;
                }

                var tabKey = $"{browser}\u001f{windowTitle}\u001f{tabTitle}";
                if (!seenTabs.Add(tabKey))
                {
                    continue;
                }

                tabs.Add(new BrowserTab(browser, windowTitle, tabTitle, IsActiveTab(tab)));
            }
        }

        return tabs
            .OrderBy(tab => tab.Browser, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(tab => tab.IsActiveTab)
            .ThenBy(tab => tab.TabTitle, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetBrowserName(int processId, out string browser)
    {
        browser = string.Empty;

        if (processId <= 0)
        {
            return false;
        }

        try
        {
            var processName = Process.GetProcessById(processId).ProcessName;
            if (processName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
            {
                browser = "Chrome";
                return true;
            }

            if (processName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
            {
                browser = "Edge";
                return true;
            }

            if (processName.Equals("firefox", StringComparison.OrdinalIgnoreCase))
            {
                browser = "Firefox";
                return true;
            }

            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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

    [SupportedOSPlatform("windows")]
    private static T RunInStaThread<T>(Func<T> work)
    {
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
}
