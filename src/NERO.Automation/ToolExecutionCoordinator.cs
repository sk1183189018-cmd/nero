using System.Text.Json;

namespace NERO.Automation;

public sealed class ToolExecutionCoordinator
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;
    private readonly IAgentActionApproval _approval;

    public ToolExecutionCoordinator(IEnumerable<IAgentTool> tools, IAgentActionApproval approval)
    {
        _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        _approval = approval ?? throw new ArgumentNullException(nameof(approval));
    }

    public IReadOnlyCollection<string> AvailableTools => _tools.Keys.ToArray();

    public async Task<ToolResult> ExecuteAsync(
        string name,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(name, out var tool))
            return new ToolResult(false, $"NERO does not support the action '{name}'.");

        string summary;
        try
        {
            summary = tool.BuildApprovalSummary(arguments);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return new ToolResult(false, ex.Message);
        }

        var approved = await _approval.ConfirmAsync(summary, cancellationToken).ConfigureAwait(false);
        if (!approved) return new ToolResult(false, "Action cancelled. No change was made.");

        try
        {
            return await tool.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"The action failed: {ex.Message}");
        }
    }
}
