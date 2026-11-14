using System.Text.Json;

namespace NERO.Automation;

public interface IAgentTool
{
    string Name { get; }
    string BuildApprovalSummary(JsonElement arguments);
    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default);
}
