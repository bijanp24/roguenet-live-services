using Microsoft.EntityFrameworkCore;
using RogueNet.Domain.Entities;

namespace RogueNet.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<MissionCompletion> MissionCompletions => Set<MissionCompletion>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<CloudSaveSlot> CloudSaveSlots => Set<CloudSaveSlot>();
    public DbSet<LeaderboardScore> LeaderboardScores => Set<LeaderboardScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Player configuration
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Username).IsUnique();
            entity.Property(p => p.Username).HasMaxLength(50).IsRequired();
            entity.Property(p => p.CreatedAt);
            entity.Property(p => p.UpdatedAt);
        });

        // PlayerProfile configuration
        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.PlayerId).IsUnique();
            entity.Property(p => p.CashBalance).HasPrecision(18, 2);
            entity.Property(p => p.Version).IsConcurrencyToken();
            entity.Property(p => p.UpdatedAt);

            entity.HasOne(p => p.Player)
                .WithOne(p => p.Profile)
                .HasForeignKey<PlayerProfile>(p => p.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryItem configuration
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.PlayerId, i.ItemId }).IsUnique();
            entity.Property(i => i.ItemId).HasMaxLength(100).IsRequired();
            entity.Property(i => i.AcquiredAt);
            entity.Property(i => i.UpdatedAt);

            entity.HasOne(i => i.Player)
                .WithMany()
                .HasForeignKey(i => i.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryTransaction configuration (append-only ledger)
        modelBuilder.Entity<InventoryTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.PlayerId);
            entity.HasIndex(t => t.SourceId);
            entity.HasIndex(t => t.CreatedAt);
            entity.Property(t => t.ItemId).HasMaxLength(100).IsRequired();
            entity.Property(t => t.TransactionType).HasMaxLength(50).IsRequired();
            entity.Property(t => t.SourceId).HasMaxLength(100);
            entity.Property(t => t.Reason).HasMaxLength(500);
            entity.Property(t => t.CreatedAt);

            entity.HasOne(t => t.Player)
                .WithMany()
                .HasForeignKey(t => t.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MissionCompletion configuration
        modelBuilder.Entity<MissionCompletion>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.PlayerId, m.CompletionId }).IsUnique();
            entity.HasIndex(m => m.MissionId);
            entity.Property(m => m.MissionId).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Difficulty).HasMaxLength(50).IsRequired();
            entity.Property(m => m.CashGranted).HasPrecision(18, 2);
            entity.Property(m => m.CompletedAt);

            entity.HasOne(m => m.Player)
                .WithMany()
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // IdempotencyKey configuration
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.HasIndex(k => new { k.PlayerId, k.Key }).IsUnique();
            entity.Property(k => k.Key).HasMaxLength(100).IsRequired();
            entity.Property(k => k.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(k => k.CreatedAt);

            entity.HasOne(k => k.Player)
                .WithMany()
                .HasForeignKey(k => k.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OutboxEvent configuration
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => e.Topic);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Topic).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt);
        });

        // CloudSaveSlot configuration
        modelBuilder.Entity<CloudSaveSlot>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => new { s.PlayerId, s.SlotNumber }).IsUnique();
            entity.Property(s => s.Version).IsConcurrencyToken();
            entity.Property(s => s.CreatedAt);
            entity.Property(s => s.UpdatedAt);

            entity.HasOne(s => s.Player)
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LeaderboardScore configuration
        modelBuilder.Entity<LeaderboardScore>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.LeaderboardId, l.PlayerId }).IsUnique();
            entity.HasIndex(l => new { l.LeaderboardId, l.Rank });
            entity.Property(l => l.LeaderboardId).HasMaxLength(100).IsRequired();
            entity.Property(l => l.UpdatedAt);

            entity.HasOne(l => l.Player)
                .WithMany()
                .HasForeignKey(l => l.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
