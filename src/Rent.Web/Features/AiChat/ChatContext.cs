namespace Rent.Web.Features.AiChat;

public record ChatContext(
    string? CurrentPage,
    string? CurrentCity,
    Guid? CurrentPropertyId);
