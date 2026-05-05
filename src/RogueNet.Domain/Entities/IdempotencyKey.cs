namespace RogueNet.Domain.Entities;

public class IdempotencyKey
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
