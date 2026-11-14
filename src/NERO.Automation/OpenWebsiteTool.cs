using System.Diagnostics;
using System.Text.Json;

namespace NERO.Automation;

public sealed class OpenWebsiteTool : IAgentTool
{
    public string Name => "open_website";

    public string BuildApprovalSummary(JsonElement arguments)
    {
        var uri = ParseUri(ToolArguments.RequiredString(arguments, "url", 2048));
        return $"Allow NERO to open this website in your default browser?\n{uri.Host}";
    }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new ToolResult(false, "Opening websites is supported only on Windows."));

        var uri = ParseUri(ToolArguments.RequiredString(arguments, "url", 2048));
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return Task.FromResult(new ToolResult(true, $"Opened {uri.Host} in your browser."));
    }

    private static Uri ParseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException("NERO can open only valid HTTP or HTTPS website addresses.");
        return uri;
    }
}
