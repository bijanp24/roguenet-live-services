namespace RogueNet.Application.MissionCompletions;

public sealed record CompleteMissionCommand(
    Guid PlayerId,
    Guid CompletionId,
    string MissionId,
    int Score,
    int DurationSeconds,
    string Difficulty);
