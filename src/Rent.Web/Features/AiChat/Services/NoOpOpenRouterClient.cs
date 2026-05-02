using System.Runtime.CompilerServices;

namespace Rent.Web.Features.AiChat.Services;

public class NoOpOpenRouterClient : IOpenRouterClient
{
    private readonly ILogger<NoOpOpenRouterClient> _logger;

    public NoOpOpenRouterClient(ILogger<NoOpOpenRouterClient> logger) => _logger = logger;

    public bool IsConfigured => false;

    public Task<ChatCompletionResponse> ChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOp] ChatCompletionAsync skipped (no API key). Model={Model}", request.Model);
        return Task.FromResult(BuildPlaceholderResponse());
    }

    public async IAsyncEnumerable<ChatCompletionStreamChunk> ChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("[NoOp] ChatCompletionStreamAsync skipped (no API key). Model={Model}", request.Model);
        yield return new ChatCompletionStreamChunk
        {
            Choices = new List<ChatCompletionStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new ChatCompletionStreamDelta
                    {
                        Role = "assistant",
                        Content = "(AI assistant is not configured. Set Ai:OpenRouterApiKey to enable.)"
                    },
                    FinishReason = "stop"
                }
            }
        };
        await Task.CompletedTask;
    }

    private static ChatCompletionResponse BuildPlaceholderResponse() => new()
    {
        Choices = new List<ChatCompletionChoice>
        {
            new()
            {
                Index = 0,
                Message = new ChatCompletionMessage
                {
                    Role = "assistant",
                    Content = "(AI assistant is not configured. Set Ai:OpenRouterApiKey to enable.)"
                },
                FinishReason = "stop"
            }
        }
    };
}
