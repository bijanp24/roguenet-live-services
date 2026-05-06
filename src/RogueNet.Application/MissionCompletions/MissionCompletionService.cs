using System.Security.Cryptography;
using System.Text;

using RogueNet.Domain.Services;

namespace RogueNet.Application.MissionCompletions;

public sealed class MissionCompletionService : IMissionCompletionService
{
    private readonly IMissionCompletionRepository _repository;

    public MissionCompletionService(IMissionCompletionRepository repository)
    {
        _repository = repository;
    }

    public async Task<CompleteMissionOutcome> ExecuteAsync(
        CompleteMissionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);

        var reward = RewardCalculator.CalculateReward(
            command.MissionId,
            command.Score,
            command.DurationSeconds,
            command.Difficulty);

        var write = new MissionCompletionWrite(
            command.PlayerId,
            command.CompletionId,
            command.MissionId,
            command.Score,
            command.DurationSeconds,
            command.Difficulty,
            reward.ExperiencePoints,
            reward.Cash,
            reward.Items,
            ComputeRequestHash(command));

        var persisted = await _repository.CompleteAsync(write, cancellationToken);

        return persisted switch
        {
            MissionCompletionPersistResult.Persisted p =>
                new CompleteMissionOutcome.Success(p.Result, WasReplay: false),
            MissionCompletionPersistResult.Replayed r =>
                new CompleteMissionOutcome.Success(r.Result, WasReplay: true),
            MissionCompletionPersistResult.IdempotencyConflict =>
                new CompleteMissionOutcome.IdempotencyConflict(command.CompletionId),
            MissionCompletionPersistResult.PlayerNotFound =>
                new CompleteMissionOutcome.PlayerNotFound(command.PlayerId),
            _ => throw new InvalidOperationException(
                $"Unexpected persist result: {persisted.GetType().Name}"),
        };
    }

    private static void ValidateCommand(CompleteMissionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.MissionId))
        {
            throw new ArgumentException("MissionId is required", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Difficulty))
        {
            throw new ArgumentException("Difficulty is required", nameof(command));
        }

        if (command.Score < 0)
        {
            throw new ArgumentException("Score must be non-negative", nameof(command));
        }

        if (command.DurationSeconds < 0)
        {
            throw new ArgumentException("DurationSeconds must be non-negative", nameof(command));
        }
    }

    // The hash covers only the request body. PlayerId and CompletionId are the lookup key
    // and are excluded so that "same key, same body" is detected as a replay even if URL
    // parameters happen to differ in formatting. Difficulty is normalized because the reward
    // calculator treats it case-insensitively — two requests that differ only in difficulty
    // case produce identical rewards and must share a hash.
    private static string ComputeRequestHash(CompleteMissionCommand command)
    {
        var canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{command.MissionId}|{command.Score}|{command.DurationSeconds}|{command.Difficulty.ToLowerInvariant()}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}
