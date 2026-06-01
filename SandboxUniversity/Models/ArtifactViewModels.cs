namespace SandboxUniversity.Models;

public sealed class ArtifactIndexViewModel
{
    public string Title { get; set; } = string.Empty;

    public string EmptyText { get; set; } = string.Empty;

    public List<ArtifactViewModel> Artifacts { get; set; } = [];
}

public sealed class ArtifactViewModel
{
    public Guid Id { get; set; }

    public string LessonId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string ContentJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
