namespace RogueNet.Application.MissionCompletions;

public sealed record MissionCompletionResult(
    Guid CompletionId,
    Guid PlayerId,
    string MissionId,
    int ExperienceGranted,
    decimal CashGranted,
    IReadOnlyDictionary<string, int> ItemsGranted,
    int NewExperiencePoints,
    int NewLevel,
    decimal NewCashBalance,
    int NewProfileVersion,
    DateTime CompletedAt);
