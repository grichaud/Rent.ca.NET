namespace Rent.Web.Features.AiChat.Services;

public interface IAiChatService
{
    Task<AiChatResult> ProcessAsync(
        ChatRequest request,
        Guid? userId,
        Guid sessionId,
        Func<string, CancellationToken, Task> emitChunkAsync,
        CancellationToken ct = default);

    Task<ActiveConversation?> GetActiveConversationAsync(
        Guid? userId,
        Guid sessionId,
        CancellationToken ct = default);
}

public record AiChatResult(Guid ConversationId, string AssistantText, int ToolStepsExecuted);

public record ActiveConversation(
    Guid ConversationId,
    string? Title,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ActiveMessage> Messages);

public record ActiveMessage(
    Guid Id,
    string Role,
    string Content,
    string? ToolName,
    DateTimeOffset CreatedAt);
