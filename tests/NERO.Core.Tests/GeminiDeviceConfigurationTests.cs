using NERO.AI;
using NERO.Security;
using Xunit;

namespace NERO.Core.Tests;

public sealed class GeminiDeviceConfigurationTests
{
    [Fact]
    public async Task ConfigureAsync_ValidatesBeforeSaving()
    {
        var store = new FakeSecretStore();
        var client = new FakeGeminiClient();
        var service = new GeminiDeviceConfiguration(store, client);

        await service.ConfigureAsync(" valid-key ");

        Assert.True(client.Validated);
        Assert.Equal("valid-key", store.Secret);
        Assert.True(service.IsConfigured);
        Assert.Equal("valid-key", service.GetApiKeyForRequest());
    }

    [Fact]
    public async Task ConfigureAsync_WhenValidationFails_DoesNotSave()
    {
        var store = new FakeSecretStore();
        var client = new FakeGeminiClient { FailValidation = true };
        var service = new GeminiDeviceConfiguration(store, client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfigureAsync("bad-key"));

        Assert.Null(store.Secret);
    }

    [Fact]
    public async Task Remove_DeletesDeviceCredential()
    {
        var store = new FakeSecretStore { Secret = "existing" };
        var service = new GeminiDeviceConfiguration(store, new FakeGeminiClient());

        Assert.True(service.Remove());
        Assert.Null(store.Secret);
        Assert.False(service.IsConfigured);
    }

    private sealed class FakeSecretStore : IDeviceSecretStore
    {
        public string? Secret { get; set; }
        public void Save(string targetName, string secret) => Secret = secret;
        public string? Read(string targetName) => Secret;
        public bool Delete(string targetName) { var existed = Secret is not null; Secret = null; return existed; }
    }

    private sealed class FakeGeminiClient : IGeminiApiClient
    {
        public bool Validated { get; private set; }
        public bool FailValidation { get; init; }
        public Task ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            Validated = true;
            return FailValidation ? Task.FromException(new InvalidOperationException("Rejected")) : Task.CompletedTask;
        }
        public Task<string> GenerateTextAsync(string apiKey, string prompt, CancellationToken cancellationToken = default) =>
            Task.FromResult("ok");
    }
}
