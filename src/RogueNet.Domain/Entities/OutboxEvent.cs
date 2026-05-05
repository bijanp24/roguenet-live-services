namespace RogueNet.Domain.Entities;

public class OutboxEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Pending, Processing, Completed, Failed, PermanentlyFailed
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
