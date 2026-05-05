namespace RogueNet.Domain.Entities;

public class MissionCompletion
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string MissionId { get; set; } = string.Empty;
    public Guid CompletionId { get; set; } // Client-supplied idempotency key
    public int Score { get; set; }
    public int DurationSeconds { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public int ExperienceGranted { get; set; }
    public decimal CashGranted { get; set; }
    public DateTime CompletedAt { get; set; }

    public Player Player { get; set; } = null!;
}
