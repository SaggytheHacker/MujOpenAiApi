namespace MujOpenAiApi.Models;

public sealed class AgentAction
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    public AgentRun? AgentRun { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string ArgumentsJson { get; set; } = string.Empty;

    public string ResultJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
