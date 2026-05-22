namespace MujOpenAiApi.Models;

public sealed class Chat
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string? SourceApp { get; set; }

    public string? MetadataJson { get; set; }

    public List<ChatMessage> Messages { get; set; } = [];
}
