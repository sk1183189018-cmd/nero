using System.Net;
using System.Text;
using NERO.AI;
using Xunit;

namespace NERO.Core.Tests;

public sealed class GeminiApiClientTests
{
    [Fact]
    public async Task ValidateApiKey_SendsKeyInHeaderNotUrl()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);
        var client = new GeminiApiClient(http);

        await client.ValidateApiKeyAsync("test-secret");

        Assert.DoesNotContain("test-secret", handler.RequestUri!.ToString());
        Assert.Equal("test-secret", handler.ApiKeyHeader);
        Assert.EndsWith("/models?pageSize=1", handler.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GenerateTextAsync_ParsesCandidateText()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Namaste from Gemini\"}]}}]}", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var client = new GeminiApiClient(http);

        var result = await client.GenerateTextAsync("secret", "Say hello");

        Assert.Equal("Namaste from Gemini", result);
        Assert.Equal("secret", handler.ApiKeyHeader);
    }


    [Fact]
    public async Task TranscribeAudioAsync_SendsAudioAsInlineWavData()
    {
        using var handler = new RecordingHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("audio/wav", body);
            Assert.Contains("AQIDBA==", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"Chrome kholo"}]}}]}""", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);
        var client = new GeminiApiClient(http);

        var transcript = await client.TranscribeAudioAsync("voice-secret", new byte[] { 1, 2, 3, 4 });

        Assert.Equal("Chrome kholo", transcript);
        Assert.Equal("voice-secret", handler.ApiKeyHeader);
        Assert.DoesNotContain("voice-secret", handler.RequestUri!.ToString());
    }

    [Fact]
    public async Task ValidateApiKey_WhenUnauthorized_ThrowsSafeError()
    {
        using var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var http = new HttpClient(handler);
        var client = new GeminiApiClient(http);

        var error = await Assert.ThrowsAsync<GeminiApiException>(() => client.ValidateApiKeyAsync("invalid"));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("invalid", error.Message);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.GetValues("x-goog-api-key").Single();
            return Task.FromResult(responseFactory(request));
        }
    }
}
