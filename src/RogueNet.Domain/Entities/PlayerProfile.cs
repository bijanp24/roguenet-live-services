namespace RogueNet.Domain.Entities;

public class PlayerProfile
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public int ExperiencePoints { get; set; }
    public int Level { get; set; }
    public decimal CashBalance { get; set; }
    public int Reputation { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
