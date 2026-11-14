namespace NERO.Voice;

/// <summary>Speaks text using an installed Windows speech voice.</summary>
public interface ITextToSpeech : IDisposable
{
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
    void Stop();
}
