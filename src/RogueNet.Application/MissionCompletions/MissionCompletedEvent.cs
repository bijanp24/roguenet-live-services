namespace RogueNet.Application.MissionCompletions;

public sealed record MissionCompletedEvent(
    Guid MissionCompletionId,
    Guid PlayerId,
    string MissionId,
    int ExperienceGranted,
    decimal CashGranted,
    IReadOnlyDictionary<string, int> ItemsGranted,
    DateTime CompletedAt);
