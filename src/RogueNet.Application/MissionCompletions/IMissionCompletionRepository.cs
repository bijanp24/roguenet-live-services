namespace RogueNet.Application.MissionCompletions;

public interface IMissionCompletionRepository
{
    Task<MissionCompletionPersistResult> CompleteAsync(
        MissionCompletionWrite write,
        CancellationToken cancellationToken);
}
