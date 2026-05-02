namespace Rent.Web.Features.AiChat.Tools;

public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    object Parameters { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson,
        ToolExecutionContext context,
        CancellationToken ct = default);
}

public record ToolExecutionContext(Guid? UserId, Guid SessionId);

public record ToolExecutionResult(object Data, bool Success = true)
{
    public static ToolExecutionResult Ok(object data) => new(data, true);
    public static ToolExecutionResult Fail(object data) => new(data, false);
}
