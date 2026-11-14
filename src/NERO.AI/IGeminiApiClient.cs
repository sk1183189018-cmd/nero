namespace NERO.AI;

public interface IGeminiApiClient
{
    Task ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string> GenerateTextAsync(string apiKey, string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateJsonAsync(string apiKey, string prompt, CancellationToken cancellationToken = default);
    Task<string> TranscribeAudioAsync(string apiKey, ReadOnlyMemory<byte> wavAudio, string language = "auto", CancellationToken cancellationToken = default);
}
