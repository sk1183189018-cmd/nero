using System.Text.Json;

namespace NERO.Automation;

/// <summary>Creates a single new folder directly under Desktop or Documents.</summary>
public sealed class CreateFolderTool : IAgentTool
{
    public string Name => "create_folder";

    public string BuildApprovalSummary(JsonElement arguments)
    {
        var (name, parent) = Resolve(arguments);
        return $"Allow NERO to create the folder '{name}' inside {parent.Name}?";
    }

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(new ToolResult(false, "Folder creation is supported only on Windows."));

        var (name, parent) = Resolve(arguments);
        var path = Path.Combine(parent.Path, name);
        if (Directory.Exists(path) || File.Exists(path))
            return Task.FromResult(new ToolResult(false, "A file or folder with that name already exists."));
        Directory.CreateDirectory(path);
        return Task.FromResult(new ToolResult(true, $"Created '{name}' in {parent.Name}."));
    }

    private static (string Name, (string Name, string Path) Parent) Resolve(JsonElement arguments)
    {
        var name = ToolArguments.RequiredString(arguments, "name", 80);
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\') || name.Contains(':'))
            throw new ArgumentException("Use a simple folder name without a path or invalid characters.");

        var location = ToolArguments.RequiredString(arguments, "location", 20);
        var parent = location.Equals("desktop", StringComparison.OrdinalIgnoreCase)
            ? ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))
            : location.Equals("documents", StringComparison.OrdinalIgnoreCase)
                ? ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                : throw new ArgumentException("New folders can be created only on Desktop or Documents.");

        if (string.IsNullOrWhiteSpace(parent.Item2) || !Directory.Exists(parent.Item2))
            throw new DirectoryNotFoundException($"The {parent.Item1} folder was not found.");
        return (name, parent);
    }
}
