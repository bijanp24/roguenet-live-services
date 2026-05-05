namespace RogueNet.Api.Contracts;

public record PlayerResponse(
    Guid Id,
    string Username,
    DateTime CreatedAt);
