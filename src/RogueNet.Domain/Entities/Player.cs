namespace RogueNet.Domain.Entities;

public class Player
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public PlayerProfile? Profile { get; set; }
}
