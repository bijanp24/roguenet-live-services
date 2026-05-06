using System.Data;
using System.Text.Json;

using Dapper;

using Microsoft.Data.SqlClient;

using RogueNet.Application.MissionCompletions;

namespace RogueNet.Infrastructure.Repositories;

public sealed class MissionCompletionRepository : IMissionCompletionRepository
{
    private const int MaxOccAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;

    public MissionCompletionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<MissionCompletionPersistResult> CompleteAsync(
        MissionCompletionWrite write,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Fast path: replays bypass the transaction entirely.
        var preExisting = await TryReadIdempotencyKey(connection, transaction: null, write, cancellationToken);
        if (preExisting is not null)
        {
            return BuildReplayOrConflict(preExisting, write.RequestHash);
        }

        for (var attempt = 1; attempt <= MaxOccAttempts; attempt++)
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            var playerExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT 1 FROM Players WHERE Id = @PlayerId",
                new { write.PlayerId },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (playerExists is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new MissionCompletionPersistResult.PlayerNotFound();
            }

            var profile = await connection.QuerySingleAsync<ProfileRow>(new CommandDefinition(
                "SELECT Id, ExperiencePoints, Level, CashBalance, [Version] FROM PlayerProfiles WHERE PlayerId = @PlayerId",
                new { write.PlayerId },
                transaction: transaction,
                cancellationToken: cancellationToken));

            var now = DateTime.UtcNow;
            var missionCompletionId = Guid.NewGuid();
            var newExperience = profile.ExperiencePoints + write.ExperienceGranted;
            var newCash = profile.CashBalance + write.CashGranted;
            var newVersion = profile.Version + 1;

            // Build the response up front so it can be stored as the idempotency replay payload.
            var result = new MissionCompletionResult(
                CompletionId: write.CompletionId,
                PlayerId: write.PlayerId,
                MissionId: write.MissionId,
                ExperienceGranted: write.ExperienceGranted,
                CashGranted: write.CashGranted,
                ItemsGranted: write.ItemsGranted,
                NewExperiencePoints: newExperience,
                NewLevel: profile.Level,
                NewCashBalance: newCash,
                NewProfileVersion: newVersion,
                CompletedAt: now);

            var responsePayload = JsonSerializer.Serialize(result, JsonOptions);

            // Insert the idempotency key first so a duplicate request that slipped past the fast path
            // fails before we do any of the other writes.
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO IdempotencyKeys (Id, PlayerId, [Key], RequestHash, ResponsePayload, CreatedAt)
                    VALUES (@Id, @PlayerId, @Key, @RequestHash, @ResponsePayload, @CreatedAt)
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        write.PlayerId,
                        Key = write.CompletionId.ToString("D"),
                        write.RequestHash,
                        ResponsePayload = responsePayload,
                        CreatedAt = now,
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }
            catch (SqlException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                var winner = await TryReadIdempotencyKey(connection, transaction: null, write, cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Idempotency unique violation but no row visible after rollback");
                return BuildReplayOrConflict(winner, write.RequestHash);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO MissionCompletions (Id, PlayerId, MissionId, CompletionId, Score, DurationSeconds, Difficulty, ExperienceGranted, CashGranted, CompletedAt)
                VALUES (@Id, @PlayerId, @MissionId, @CompletionId, @Score, @DurationSeconds, @Difficulty, @ExperienceGranted, @CashGranted, @CompletedAt)
                """,
                new
                {
                    Id = missionCompletionId,
                    write.PlayerId,
                    write.MissionId,
                    write.CompletionId,
                    write.Score,
                    write.DurationSeconds,
                    write.Difficulty,
                    write.ExperienceGranted,
                    write.CashGranted,
                    CompletedAt = now,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            foreach (var item in write.ItemsGranted)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO InventoryTransactions (Id, PlayerId, ItemId, QuantityChange, TransactionType, SourceId, Reason, CreatedAt)
                    VALUES (@Id, @PlayerId, @ItemId, @QuantityChange, 'Grant', @SourceId, @Reason, @CreatedAt)
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        write.PlayerId,
                        ItemId = item.Key,
                        QuantityChange = item.Value,
                        SourceId = missionCompletionId.ToString("D"),
                        Reason = $"mission:{write.MissionId}",
                        CreatedAt = now,
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE PlayerProfiles
                SET ExperiencePoints = @NewExperience,
                    CashBalance = @NewCash,
                    [Version] = @NewVersion,
                    UpdatedAt = @Now
                WHERE Id = @ProfileId AND [Version] = @ExpectedVersion
                """,
                new
                {
                    NewExperience = newExperience,
                    NewCash = newCash,
                    NewVersion = newVersion,
                    Now = now,
                    ProfileId = profile.Id,
                    ExpectedVersion = profile.Version,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == MaxOccAttempts)
                {
                    throw new InvalidOperationException(
                        $"Profile concurrency conflict for player {write.PlayerId} after {MaxOccAttempts} attempts");
                }

                continue;
            }

            var outboxPayload = new MissionCompletedEvent(
                missionCompletionId,
                write.PlayerId,
                write.MissionId,
                write.ExperienceGranted,
                write.CashGranted,
                write.ItemsGranted,
                now);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO OutboxEvents (Id, EventType, Topic, Payload, Status, RetryCount, CreatedAt)
                VALUES (@Id, @EventType, @Topic, @Payload, 'Pending', 0, @CreatedAt)
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(MissionCompletedEvent),
                    Topic = "mission-completion",
                    Payload = JsonSerializer.Serialize(outboxPayload, JsonOptions),
                    CreatedAt = now,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new MissionCompletionPersistResult.Persisted(result);
        }

        throw new InvalidOperationException("Unreachable: OCC retry loop exited without returning");
    }

    private static async Task<IdempotencyKeyRow?> TryReadIdempotencyKey(
        SqlConnection connection,
        IDbTransaction? transaction,
        MissionCompletionWrite write,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<IdempotencyKeyRow>(new CommandDefinition(
            "SELECT RequestHash, ResponsePayload FROM IdempotencyKeys WHERE PlayerId = @PlayerId AND [Key] = @Key",
            new { write.PlayerId, Key = write.CompletionId.ToString("D") },
            transaction: transaction,
            cancellationToken: cancellationToken));
    }

    private static MissionCompletionPersistResult BuildReplayOrConflict(IdempotencyKeyRow row, string expectedHash)
    {
        if (!string.Equals(row.RequestHash, expectedHash, StringComparison.Ordinal))
        {
            return new MissionCompletionPersistResult.IdempotencyConflict();
        }

        var deserialized = JsonSerializer.Deserialize<MissionCompletionResult>(row.ResponsePayload, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize stored mission completion response payload");
        return new MissionCompletionPersistResult.Replayed(deserialized);
    }

    private static bool IsUniqueViolation(SqlException ex)
    {
        // 2627 = unique constraint, 2601 = unique index
        return ex.Number is 2627 or 2601;
    }

    private sealed record IdempotencyKeyRow(string RequestHash, string ResponsePayload);

    private sealed record ProfileRow(Guid Id, int ExperiencePoints, int Level, decimal CashBalance, int Version);
}
