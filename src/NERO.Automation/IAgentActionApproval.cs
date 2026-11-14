namespace NERO.Automation;

/// <summary>Every computer action must be explicitly approved by the user before execution.</summary>
public interface IAgentActionApproval
{
    Task<bool> ConfirmAsync(string summary, CancellationToken cancellationToken = default);
}
