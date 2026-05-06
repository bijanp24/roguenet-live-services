namespace RogueNet.Application.MissionCompletions;

public sealed record MissionCompletionWrite(
    Guid PlayerId,
    Guid CompletionId,
    string MissionId,
    int Score,
    int DurationSeconds,
    string Difficulty,
    int ExperienceGranted,
    decimal CashGranted,
    IReadOnlyDictionary<string, int> ItemsGranted,
    string RequestHash);
