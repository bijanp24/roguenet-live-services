namespace RogueNet.Api.Contracts;

public sealed record MissionCompletionRequest(
    Guid CompletionId,
    string MissionId,
    int Score,
    int DurationSeconds,
    string Difficulty);
