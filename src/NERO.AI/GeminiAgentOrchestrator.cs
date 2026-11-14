using System.Text.Json;
using NERO.Automation;

namespace NERO.AI;

/// <summary>
/// Converts a user's request into one allow-listed tool action. Every action is routed
/// through ToolExecutionCoordinator and requires explicit user approval.
/// </summary>
public sealed class GeminiAgentOrchestrator
{
    private readonly IGeminiApiClient _gemini;
    private readonly ToolExecutionCoordinator _tools;

    public GeminiAgentOrchestrator(IGeminiApiClient gemini, ToolExecutionCoordinator tools)
    {
        _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    public async Task<AgentTurnResult> ExecuteAsync(
        string apiKey,
        string userRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userRequest);
        var prompt = BuildPlannerPrompt(userRequest);
        var json = await _gemini.GenerateJsonAsync(apiKey, prompt, cancellationToken).ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String)
                return new AgentTurnResult(false, false, "NERO could not understand the requested action plan.");

            var action = actionElement.GetString()?.Trim();
            var message = root.TryGetProperty("message", out var messageElement) &&
                          messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.Equals(action, "none", StringComparison.OrdinalIgnoreCase))
                return new AgentTurnResult(false, false, string.IsNullOrWhiteSpace(message)
                    ? "I can answer that in chat, but I do not have a safe computer action for it."
                    : message);

            if (!root.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object)
                return new AgentTurnResult(false, false, "The action plan did not contain valid arguments.");

            var reason = root.TryGetProperty("reason", out var reasonElement) &&
                         reasonElement.ValueKind == JsonValueKind.String
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;

            var result = await _tools.ExecuteAsync(action ?? string.Empty, arguments, reason, cancellationToken)
                .ConfigureAwait(false);
            return new AgentTurnResult(true, result.Succeeded, result.Message);
        }
        catch (JsonException)
        {
            return new AgentTurnResult(false, false, "Gemini returned an invalid action plan. No computer action was performed.");
        }
    }

    private static string BuildPlannerPrompt(string request) =>
        """
        You are NERO's safe Windows action planner. Select at most one action from this allow-list.
        Never create shell commands, PowerShell, scripts, arbitrary executable paths, registry edits, file deletions,
        downloads, purchases, emails, or any action outside this list.
        
        Allowed actions and arguments:
        - open_application: {"application":"notepad|calculator|paint|file explorer|chrome|edge|word|excel|powerpoint|outlook"}
        - open_website: {"url":"an absolute http or https URL"}
        - search_web: {"query":"short search query"}
        - open_folder: {"folder":"desktop|documents|downloads|pictures|music|videos"}
        - create_folder: {"name":"one simple folder name","location":"desktop|documents"}
        
        Return one JSON object only with this shape:
        {"action":"one allowed action or none","arguments":{},"reason":"short explanation of the intended action","message":"short user-facing message"}
        
        Use action "none" for normal questions, unclear requests, unsupported actions, or anything risky. Never invent that an action succeeded.
        User request:
        """ + request;
}
