namespace Rent.Web.Features.AiChat;

public class ChatRequest
{
    public Guid? ConversationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Locale { get; set; }
    public ChatRequestContext? Context { get; set; }
}

public class ChatRequestContext
{
    public string? CurrentPage { get; set; }
    public string? CurrentCity { get; set; }
    public Guid? CurrentPropertyId { get; set; }
}
