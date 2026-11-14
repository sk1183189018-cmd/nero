namespace NERO.Voice;

/// <summary>Records microphone audio locally until the supplied cancellation token is cancelled.</summary>
public interface IMicrophoneRecorder
{
    Task<byte[]> RecordWaveUntilCancelledAsync(CancellationToken cancellationToken);
}
