namespace MujOpenAiApi.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ChatId { get; set; }

    public Chat? Chat { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string? Model { get; set; }

    public string? MetadataJson { get; set; }
}
