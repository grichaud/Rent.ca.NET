using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rent.Web.Domain;
using Rent.Web.Features.AiChat.Tools;
using Rent.Web.Infrastructure.Data;

namespace Rent.Web.Features.AiChat.Services;

public class AiChatService : IAiChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly IOpenRouterClient _openRouter;
    private readonly ToolRegistry _tools;
    private readonly AiOptions _options;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        AppDbContext db,
        IOpenRouterClient openRouter,
        ToolRegistry tools,
        IOptions<AiOptions> options,
        ILogger<AiChatService> logger)
    {
        _db = db;
        _openRouter = openRouter;
        _tools = tools;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActiveConversation?> GetActiveConversationAsync(
        Guid? userId, Guid sessionId, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

        IQueryable<AiConversation> q = _db.AiConversations.AsNoTracking();
        q = userId is { } uid
            ? q.Where(c => c.UserId == uid)
            : q.Where(c => c.SessionId == sessionId);

        var candidates = await q.ToListAsync(ct);
        var convo = candidates
            .Where(c => c.UpdatedAt >= cutoff)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefault();
        if (convo is null) return null;

        var raw = await _db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == convo.Id
                && m.Role != AiMessageRole.System
                && m.Role != AiMessageRole.Tool)
            .ToListAsync(ct);

        var messages = raw
            .OrderBy(m => m.CreatedAt)
            .Take(_options.HistoryWindow * 2)
            .Select(m => new ActiveMessage(m.Id, m.Role.ToString(), m.Content, m.ToolName, m.CreatedAt))
            .ToList();

        return new ActiveConversation(convo.Id, convo.Title, convo.UpdatedAt, messages);
    }

    public async Task<AiChatResult> ProcessAsync(
        ChatRequest request,
        Guid? userId,
        Guid sessionId,
        Func<string, CancellationToken, Task> emitChunkAsync,
        CancellationToken ct = default)
    {
        var conversation = await ResolveConversationAsync(request.ConversationId, userId, sessionId, request.Message, ct);

        var userMessage = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.User,
            Content = request.Message
        };
        _db.AiMessages.Add(userMessage);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var systemPrompt = AiSystemPrompt.Build(new ChatContext(
            request.Context?.CurrentPage,
            request.Context?.CurrentCity,
            request.Context?.CurrentPropertyId,
            request.Locale));

        var messages = new List<ChatCompletionMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        var historyAll = await _db.AiMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.Id != userMessage.Id)
            .ToListAsync(ct);
        var history = historyAll
            .OrderByDescending(m => m.CreatedAt)
            .Take(_options.HistoryWindow)
            .OrderBy(m => m.CreatedAt)
            .ToList();
        foreach (var msg in history)
        {
            messages.Add(MaterializeHistoryMessage(msg));
        }

        messages.Add(new ChatCompletionMessage { Role = "user", Content = request.Message });

        var toolDefinitions = _tools.ToOpenAISchema();
        var toolStepsExecuted = 0;
        string finalText = string.Empty;

        for (int step = 1; step <= _options.MaxIterations; step++)
        {
            var apiRequest = new ChatCompletionRequest
            {
                Model = _options.Model,
                Messages = messages,
                Tools = toolDefinitions,
                ToolChoice = "auto",
                MaxTokens = _options.MaxTokens
            };

            var response = await _openRouter.ChatCompletionAsync(apiRequest, ct);
            var choice = response.Choices.FirstOrDefault()
                ?? throw new InvalidOperationException("OpenRouter returned no choices.");
            var assistantMessage = choice.Message;

            if (assistantMessage.ToolCalls is { Count: > 0 } toolCalls)
            {
                toolStepsExecuted++;
                var assistantPlaceholder = new ChatCompletionMessage
                {
                    Role = "assistant",
                    Content = assistantMessage.Content,
                    ToolCalls = toolCalls
                };
                messages.Add(assistantPlaceholder);

                if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                {
                    _db.AiMessages.Add(new AiMessage
                    {
                        ConversationId = conversation.Id,
                        Role = AiMessageRole.Assistant,
                        Content = assistantMessage.Content
                    });
                }

                foreach (var call in toolCalls)
                {
                    var toolResultJson = await DispatchToolAsync(call, conversation.Id, userId, sessionId, ct);

                    messages.Add(new ChatCompletionMessage
                    {
                        Role = "tool",
                        ToolCallId = call.Id,
                        Name = call.Function.Name,
                        Content = toolResultJson
                    });
                }

                await _db.SaveChangesAsync(ct);

                if (step >= _options.MaxIterations)
                {
                    finalText = assistantMessage.Content
                        ?? "I've gathered some information but reached the tool-call limit. Please ask me to summarize what we have so far.";
                    _logger.LogWarning("Conversation {Id} hit MaxIterations={Max} with tool calls still pending",
                        conversation.Id, _options.MaxIterations);
                    break;
                }
                continue;
            }

            finalText = assistantMessage.Content ?? string.Empty;
            break;
        }

        if (string.IsNullOrEmpty(finalText))
        {
            finalText = "I'm sorry, I couldn't generate a response. Please try again.";
        }

        await StreamTextAsync(finalText, emitChunkAsync, ct);

        var assistantRecord = new AiMessage
        {
            ConversationId = conversation.Id,
            Role = AiMessageRole.Assistant,
            Content = finalText
        };
        _db.AiMessages.Add(assistantRecord);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new AiChatResult(conversation.Id, finalText, toolStepsExecuted);
    }

    private async Task<AiConversation> ResolveConversationAsync(
        Guid? conversationId, Guid? userId, Guid sessionId, string firstMessage, CancellationToken ct)
    {
        if (conversationId is { } id)
        {
            var existing = await _db.AiConversations.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (existing is not null && BelongsToCaller(existing, userId, sessionId))
                return existing;
        }

        var convo = new AiConversation
        {
            UserId = userId,
            SessionId = userId is null ? sessionId : null,
            Title = TruncateTitle(firstMessage)
        };
        _db.AiConversations.Add(convo);
        return convo;
    }

    private static bool BelongsToCaller(AiConversation c, Guid? userId, Guid sessionId) =>
        (userId is { } uid && c.UserId == uid) || (userId is null && c.SessionId == sessionId);

    private static string? TruncateTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..77] + "...";
    }

    private async Task<string> DispatchToolAsync(
        ToolCall call, Guid conversationId, Guid? userId, Guid sessionId, CancellationToken ct)
    {
        var tool = _tools.Resolve(call.Function.Name);
        if (tool is null)
        {
            var errorJson = JsonSerializer.Serialize(new { error = $"Unknown tool: {call.Function.Name}" });
            _db.AiMessages.Add(new AiMessage
            {
                ConversationId = conversationId,
                Role = AiMessageRole.Tool,
                ToolName = call.Function.Name,
                ToolArgsJson = call.Function.Arguments,
                ToolResultJson = errorJson,
                Content = errorJson
            });
            return errorJson;
        }

        ToolExecutionResult result;
        try
        {
            result = await tool.ExecuteAsync(
                call.Function.Arguments,
                new ToolExecutionContext(userId, sessionId),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {Tool} failed for conversation {Id}", call.Function.Name, conversationId);
            result = ToolExecutionResult.Fail(new { error = "Tool execution failed." });
        }

        var resultJson = JsonSerializer.Serialize(result.Data, JsonOptions);
        _db.AiMessages.Add(new AiMessage
        {
            ConversationId = conversationId,
            Role = AiMessageRole.Tool,
            ToolName = call.Function.Name,
            ToolArgsJson = call.Function.Arguments,
            ToolResultJson = resultJson,
            Content = resultJson
        });
        return resultJson;
    }

    private static ChatCompletionMessage MaterializeHistoryMessage(AiMessage msg)
    {
        if (msg.Role == AiMessageRole.Tool)
        {
            return new ChatCompletionMessage
            {
                Role = "tool",
                Content = msg.Content,
                Name = msg.ToolName,
                ToolCallId = msg.Id.ToString()
            };
        }

        var role = msg.Role switch
        {
            AiMessageRole.User => "user",
            AiMessageRole.Assistant => "assistant",
            AiMessageRole.System => "system",
            _ => "user"
        };
        return new ChatCompletionMessage { Role = role, Content = msg.Content };
    }

    private static async Task StreamTextAsync(
        string text, Func<string, CancellationToken, Task> emitChunkAsync, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text)) return;

        const int chunkSize = 24;
        var index = 0;
        while (index < text.Length)
        {
            ct.ThrowIfCancellationRequested();
            var len = Math.Min(chunkSize, text.Length - index);
            var chunk = text.Substring(index, len);
            await emitChunkAsync(chunk, ct);
            index += len;
            if (index < text.Length)
                await Task.Delay(20, ct);
        }
    }
}
