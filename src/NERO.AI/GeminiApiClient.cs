using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NERO.AI;

/// <summary>Minimal REST client for Gemini. API keys are sent only in the x-goog-api-key header.</summary>
public sealed class GeminiApiClient : IGeminiApiClient
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta/";
    private const string DefaultModel = "gemini-2.5-flash";
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public GeminiApiClient(HttpClient httpClient, string model = DefaultModel)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
    }

    public async Task ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        ValidateKey(apiKey);
        using var request = CreateRequest(HttpMethod.Get, new Uri(ApiBase + "models?pageSize=1"), apiKey);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<string> GenerateTextAsync(string apiKey, string prompt, CancellationToken cancellationToken = default)
    {
        ValidateKey(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var endpoint = new Uri(ApiBase + "models/" + Uri.EscapeDataString(_model) + ":generateContent");
        using var request = CreateRequest(HttpMethod.Post, endpoint, apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } } }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            var text = string.Join(Environment.NewLine,
                parts.EnumerateArray()
                    .Where(part => part.TryGetProperty("text", out _))
                    .Select(part => part.GetProperty("text").GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        throw new InvalidOperationException("Gemini returned no text candidate. The response may have been blocked or empty.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string apiKey)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        var status = response.StatusCode;
        var message = status switch
        {
            HttpStatusCode.Unauthorized => "Gemini rejected this API key. Check that it is active and belongs to the Gemini API.",
            HttpStatusCode.Forbidden => "Gemini denied access. Check API restrictions, project access and quota.",
            HttpStatusCode.TooManyRequests => "Gemini rate limit or quota reached. Try again later or check your Google AI Studio usage.",
            _ => "Gemini request failed (HTTP " + (int)status + "). Check the key, model availability and network connection."
        };
        response.Dispose();
        throw new GeminiApiException(status, message);
    }

    private static void ValidateKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (apiKey.Contains('\r') || apiKey.Contains('\n'))
            throw new ArgumentException("The Gemini API key must be a single line.", nameof(apiKey));
    }
}
