using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Rent.Web.Features.AiChat.Services;

public class OpenRouterClient : IOpenRouterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(HttpClient http, IOptions<AiOptions> options, ILogger<OpenRouterClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.OpenRouterApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.OpenRouterApiKey);
        }

        if (!string.IsNullOrWhiteSpace(_options.SiteUrl))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", _options.SiteUrl);
        }
        if (!string.IsNullOrWhiteSpace(_options.AppName))
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", _options.AppName);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenRouterApiKey);

    public async Task<ChatCompletionResponse> ChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default)
    {
        request.Stream = false;

        using var response = await _http.PostAsJsonAsync("chat/completions", request, JsonOptions, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenRouter returned {Status} for model {Model}: {Body}",
                (int)response.StatusCode, request.Model, body);
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("OpenRouter returned an empty body.");

        return parsed;
    }

    public async IAsyncEnumerable<ChatCompletionStreamChunk> ChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        request.Stream = true;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "OpenRouter stream returned {Status} for model {Model}: {Body}",
                (int)response.StatusCode, request.Model, body);
            response.EnsureSuccessStatusCode();
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line.AsSpan(5).Trim().ToString();
            if (payload.Length == 0) continue;
            if (payload == "[DONE]") yield break;

            ChatCompletionStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(payload, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse OpenRouter SSE chunk: {Payload}", payload);
                continue;
            }

            if (chunk is not null) yield return chunk;
        }
    }
}
