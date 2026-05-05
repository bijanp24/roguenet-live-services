namespace RogueNet.Domain.Entities;

public class InventoryItem
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime AcquiredAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
