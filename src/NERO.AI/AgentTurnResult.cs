namespace NERO.AI;

public sealed record AgentTurnResult(bool ActionRequested, bool ActionSucceeded, string Message);
