using System.Diagnostics;
using System.Text.Json;

namespace NERO.Automation;

/// <summary>Opens only applications explicitly present in the built-in allow-list.</summary>
public sealed class OpenApplicationTool : IAgentTool
{
    private static readonly IReadOnlyDictionary<string, string> Applications =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["notepad"] = "notepad.exe",
            ["calculator"] = "calc.exe",
            ["paint"] = "mspaint.exe",
            ["file explorer"] = "explorer.exe",
            ["chrome"] = "chrome.exe",
            ["edge"] = "msedge.exe",
            ["word"] = "winword.exe",
            ["excel"] = "excel.exe",
            ["powerpoint"] = "powerpnt.exe",
            ["outlook"] = "outlook.exe"
        };

    public string Name => "open_application";

    public string BuildApprovalSummary(JsonElement arguments)
    {
        var application = ToolArguments.RequiredString(arguments, "application", 40);
        if (!Applications.ContainsKey(application))
            throw new ArgumentException("That application is not in NERO's safe launch list.");
        return $"Allow NERO to open {application}?";
    }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new ToolResult(false, "Opening Windows applications is supported only on Windows."));

        var application = ToolArguments.RequiredString(arguments, "application", 40);
        if (!Applications.TryGetValue(application, out var executable))
            return Task.FromResult(new ToolResult(false, "That application is not in NERO's safe launch list."));

        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        return Task.FromResult(new ToolResult(true, $"Opened {application}."));
    }
}
