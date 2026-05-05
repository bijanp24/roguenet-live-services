namespace RogueNet.Domain.Entities;

public class LeaderboardScore
{
    public Guid Id { get; set; }
    public string LeaderboardId { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public int Score { get; set; }
    public int Rank { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
