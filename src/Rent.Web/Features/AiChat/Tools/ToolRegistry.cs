using Rent.Web.Features.AiChat.Services;

namespace Rent.Web.Features.AiChat.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools;

    public ToolRegistry(IEnumerable<IAiTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<IAiTool> All => _tools.Values;

    public IAiTool? Resolve(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;

    public List<ToolDefinition> ToOpenAISchema() =>
        _tools.Values.Select(t => new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }
        }).ToList();
}
