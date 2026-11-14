namespace NERO.Voice;

/// <summary>Listens locally for the configured wake phrase and invokes a callback once detected.</summary>
public interface IWakeWordListener
{
    Task PrepareAsync(CancellationToken cancellationToken = default);
    Task ListenAsync(Func<CancellationToken, Task> onWakeWord, CancellationToken cancellationToken = default);
}
