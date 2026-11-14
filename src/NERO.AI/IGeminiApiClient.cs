namespace NERO.AI;

public interface IGeminiApiClient
{
    Task ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string> GenerateTextAsync(string apiKey, string prompt, CancellationToken cancellationToken = default);
}
