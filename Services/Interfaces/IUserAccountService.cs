using BookonnectCore.DTO;
using BookonnectCore.Models;

namespace BookonnectCore.Services.Interfaces;

public interface IUserAccountService
{
    Task<ExternalSignInResult> FindOrCreateExternalUserAsync(
        ExternalIdentity identity, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
}
