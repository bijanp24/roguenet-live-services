namespace RogueNet.Domain.Entities;

public class CloudSaveSlot
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public int SlotNumber { get; set; }
    public string SaveData { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
