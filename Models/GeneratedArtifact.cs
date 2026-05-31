namespace MujOpenAiApi.Models;

public sealed class GeneratedArtifact
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    public AgentRun? AgentRun { get; set; }

    public string LessonId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ContentJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
