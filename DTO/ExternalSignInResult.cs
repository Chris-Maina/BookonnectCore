using BookonnectCore.Models;

namespace BookonnectCore.DTO;

public sealed record ExternalSignInResult(User User, bool IsNewUser);
