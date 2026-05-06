namespace RogueNet.Application.MissionCompletions;

public interface IMissionCompletionService
{
    Task<CompleteMissionOutcome> ExecuteAsync(
        CompleteMissionCommand command,
        CancellationToken cancellationToken);
}
