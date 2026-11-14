using NERO.Security;

namespace NERO.AI;

/// <summary>Validates and manages the Gemini key stored only on this Windows device.</summary>
public sealed class GeminiDeviceConfiguration
{
    public const string CredentialTarget = "NERO.Gemini.ApiKey";
    private readonly IDeviceSecretStore _secretStore;
    private readonly IGeminiApiClient _gemini;

    public GeminiDeviceConfiguration(IDeviceSecretStore secretStore, IGeminiApiClient gemini)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_secretStore.Read(CredentialTarget));

    public async Task ConfigureAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        await _gemini.ValidateApiKeyAsync(apiKey, cancellationToken).ConfigureAwait(false);
        _secretStore.Save(CredentialTarget, apiKey.Trim());
    }

    public bool Remove() => _secretStore.Delete(CredentialTarget);

    public string GetApiKeyForRequest() =>
        _secretStore.Read(CredentialTarget)
        ?? throw new InvalidOperationException("Gemini is not configured on this device.");
}
