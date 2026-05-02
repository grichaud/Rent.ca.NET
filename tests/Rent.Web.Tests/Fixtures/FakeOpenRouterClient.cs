using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Rent.Web.Features.AiChat.Services;

namespace Rent.Web.Tests.Fixtures;

public class FakeOpenRouterClient : IOpenRouterClient
{
    private readonly ConcurrentQueue<ChatCompletionResponse> _responses = new();
    private readonly object _failureLock = new();
    private Exception? _failure;

    public List<ChatCompletionRequest> Calls { get; } = new();

    public bool IsConfigured => true;

    public void Reset()
    {
        while (_responses.TryDequeue(out _)) { }
        Calls.Clear();
        lock (_failureLock) _failure = null;
    }

    public void EnqueueText(string content)
    {
        _responses.Enqueue(new ChatCompletionResponse
        {
            Choices = new List<ChatCompletionChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatCompletionMessage { Role = "assistant", Content = content },
                    FinishReason = "stop"
                }
            }
        });
    }

    public void EnqueueToolCall(string toolName, string argumentsJson, string? assistantText = null)
    {
        _responses.Enqueue(new ChatCompletionResponse
        {
            Choices = new List<ChatCompletionChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = assistantText,
                        ToolCalls = new List<ToolCall>
                        {
                            new()
                            {
                                Id = $"call_{Guid.NewGuid():N}",
                                Type = "function",
                                Function = new ToolCallFunction
                                {
                                    Name = toolName,
                                    Arguments = argumentsJson
                                }
                            }
                        }
                    },
                    FinishReason = "tool_calls"
                }
            }
        });
    }

    public void EnqueueFailure(Exception ex)
    {
        lock (_failureLock) _failure = ex;
    }

    public Task<ChatCompletionResponse> ChatCompletionAsync(
        ChatCompletionRequest request, CancellationToken ct = default)
    {
        Calls.Add(request);

        lock (_failureLock)
        {
            if (_failure is not null)
            {
                var f = _failure;
                _failure = null;
                throw f;
            }
        }

        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        return Task.FromResult(new ChatCompletionResponse
        {
            Choices = new List<ChatCompletionChoice>
            {
                new()
                {
                    Index = 0,
                    Message = new ChatCompletionMessage { Role = "assistant", Content = "(no response queued)" },
                    FinishReason = "stop"
                }
            }
        });
    }

    public async IAsyncEnumerable<ChatCompletionStreamChunk> ChatCompletionStreamAsync(
        ChatCompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Calls.Add(request);
        yield return new ChatCompletionStreamChunk
        {
            Choices = new List<ChatCompletionStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new ChatCompletionStreamDelta { Content = "(fake stream)" },
                    FinishReason = "stop"
                }
            }
        };
        await Task.CompletedTask;
    }
}
