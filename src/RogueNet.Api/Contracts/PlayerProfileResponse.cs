namespace RogueNet.Api.Contracts;

public record PlayerProfileResponse(
    Guid PlayerId,
    string Username,
    int ExperiencePoints,
    int Level,
    decimal CashBalance,
    int Reputation,
    int Version,
    DateTime UpdatedAt);
