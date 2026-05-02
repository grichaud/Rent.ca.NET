namespace Rent.Web.Features.AiChat;

public class AiOptions
{
    public const string SectionName = "Ai";

    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";
    public string Model { get; set; } = "anthropic/claude-haiku-4-5";
    public int MaxTokens { get; set; } = 2000;
    public int MaxIterations { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int RateLimitPerHour { get; set; } = 20;
    public int HistoryWindow { get; set; } = 20;
    public string AppName { get; set; } = "Rent.ca.NET";
    public string SiteUrl { get; set; } = "https://rent-ca-net.azurewebsites.net";
}
