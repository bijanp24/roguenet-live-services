using RogueNet.Application.MissionCompletions;

namespace RogueNet.Api.Contracts;

public sealed record MissionCompletionResponse(
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
    DateTime CompletedAt)
{
    public static MissionCompletionResponse FromResult(MissionCompletionResult result) =>
        new(
            result.CompletionId,
            result.PlayerId,
            result.MissionId,
            result.ExperienceGranted,
            result.CashGranted,
            result.ItemsGranted,
            result.NewExperiencePoints,
            result.NewLevel,
            result.NewCashBalance,
            result.NewProfileVersion,
            result.CompletedAt);
}
