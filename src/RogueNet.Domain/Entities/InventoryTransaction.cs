namespace RogueNet.Domain.Entities;

public class InventoryTransaction
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Grant, Remove, Adjustment
    public string? SourceId { get; set; } // Mission completion ID, purchase ID, etc.
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    public Player Player { get; set; } = null!;
}
