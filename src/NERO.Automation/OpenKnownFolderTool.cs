using System.Diagnostics;
using System.Text.Json;

namespace NERO.Automation;

/// <summary>Opens only common user folders, never arbitrary system paths.</summary>
public sealed class OpenKnownFolderTool : IAgentTool
{
    private static readonly IReadOnlyDictionary<string, Environment.SpecialFolder> Folders =
        new Dictionary<string, Environment.SpecialFolder>(StringComparer.OrdinalIgnoreCase)
        {
            ["desktop"] = Environment.SpecialFolder.DesktopDirectory,
            ["documents"] = Environment.SpecialFolder.MyDocuments,
            ["pictures"] = Environment.SpecialFolder.MyPictures,
            ["music"] = Environment.SpecialFolder.MyMusic,
            ["videos"] = Environment.SpecialFolder.MyVideos
        };

    public string Name => "open_folder";

    public string BuildApprovalSummary(JsonElement arguments)
    {
        var folder = ResolveFolder(ToolArguments.RequiredString(arguments, "folder", 30));
        return $"Allow NERO to open your {folder.Name} folder?";
    }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new ToolResult(false, "Opening Windows folders is supported only on Windows."));

        var folder = ResolveFolder(ToolArguments.RequiredString(arguments, "folder", 30));
        Process.Start(new ProcessStartInfo(folder.Path) { UseShellExecute = true });
        return Task.FromResult(new ToolResult(true, $"Opened your {folder.Name} folder."));
    }

    private static (string Name, string Path) ResolveFolder(string name)
    {
        if (name.Equals("downloads", StringComparison.OrdinalIgnoreCase))
        {
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads)) return ("Downloads", downloads);
            throw new DirectoryNotFoundException("The Downloads folder was not found.");
        }

        if (!Folders.TryGetValue(name, out var specialFolder))
            throw new ArgumentException("Choose one of: Desktop, Documents, Downloads, Pictures, Music, or Videos.");

        var path = Environment.GetFolderPath(specialFolder);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException($"The {name} folder was not found.");
        return (name, path);
    }
}
