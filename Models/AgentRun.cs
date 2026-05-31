namespace MujOpenAiApi.Models;

public sealed class AgentRun
{
    public Guid Id { get; set; }

    public Guid ChatId { get; set; }

    public Chat? Chat { get; set; }

    public string LessonId { get; set; } = string.Empty;

    public string LessonTitle { get; set; } = string.Empty;

    public string UserPrompt { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? MetadataJson { get; set; }

    public List<AgentAction> Actions { get; set; } = [];

    public List<GeneratedArtifact> Artifacts { get; set; } = [];
}
