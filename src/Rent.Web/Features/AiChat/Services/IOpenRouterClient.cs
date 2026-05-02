namespace Rent.Web.Features.AiChat.Services;

public interface IOpenRouterClient
{
    bool IsConfigured { get; }

    Task<ChatCompletionResponse> ChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);

    IAsyncEnumerable<ChatCompletionStreamChunk> ChatCompletionStreamAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default);
}
