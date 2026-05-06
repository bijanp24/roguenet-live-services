namespace RogueNet.Application.MissionCompletions;

public abstract record CompleteMissionOutcome
{
    private CompleteMissionOutcome()
    {
    }

    public sealed record Success(MissionCompletionResult Result, bool WasReplay) : CompleteMissionOutcome;

    public sealed record PlayerNotFound(Guid PlayerId) : CompleteMissionOutcome;

    public sealed record IdempotencyConflict(Guid CompletionId) : CompleteMissionOutcome;
}
