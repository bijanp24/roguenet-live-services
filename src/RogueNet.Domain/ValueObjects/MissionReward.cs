namespace RogueNet.Domain.ValueObjects;

public record MissionReward(
    int ExperiencePoints,
    decimal Cash,
    Dictionary<string, int> Items)
{
    public static MissionReward Empty => new(0, 0m, new Dictionary<string, int>());
}
