using System.Diagnostics;
using System.Text.Json;

namespace NERO.Automation;

public sealed class SearchWebTool : IAgentTool
{
    public string Name => "search_web";

    public string BuildApprovalSummary(JsonElement arguments)
    {
        var query = ToolArguments.RequiredString(arguments, "query", 300);
        return $"Allow NERO to search the web for:\n{query}";
    }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new ToolResult(false, "Web search launching is supported only on Windows."));

        var query = ToolArguments.RequiredString(arguments, "query", 300);
        var uri = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        return Task.FromResult(new ToolResult(true, "Opened web search results."));
    }
}
