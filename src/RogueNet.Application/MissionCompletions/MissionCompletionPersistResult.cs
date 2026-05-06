namespace RogueNet.Application.MissionCompletions;

public abstract record MissionCompletionPersistResult
{
    private MissionCompletionPersistResult()
    {
    }

    public sealed record Persisted(MissionCompletionResult Result) : MissionCompletionPersistResult;

    public sealed record Replayed(MissionCompletionResult Result) : MissionCompletionPersistResult;

    public sealed record IdempotencyConflict : MissionCompletionPersistResult;

    public sealed record PlayerNotFound : MissionCompletionPersistResult;
}
