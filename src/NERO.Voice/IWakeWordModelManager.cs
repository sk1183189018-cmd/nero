namespace NERO.Voice;

/// <summary>Ensures the locally licensed English wake-word model is available.</summary>
public interface IWakeWordModelManager
{
    Task<string> EnsureModelAsync(CancellationToken cancellationToken = default);
}
