namespace BookonnectCore.DTO;

public sealed record ExternalIdentity(
    string Provider, string Subject, string Email, string Name, string? Image);
