using BookonnectCore.DTO;
using BookonnectCore.Exceptions;
using BookonnectCore.Models;
using BookonnectCore.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BookonnectCore.Services;

public class UserAccountService : IUserAccountService
{
    private readonly IRelationalOperationsService _relationalOperationsService;
    private readonly ILogger<UserAccountService> _logger;

    public UserAccountService(
        IRelationalOperationsService relationalOperationsService, ILogger<UserAccountService> logger)
    {
        _relationalOperationsService = relationalOperationsService;
        _logger = logger;
    }

    public Task<ExternalSignInResult> FindOrCreateExternalUserAsync(
        ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Name);
        if (identity.Provider.Length > 32 || identity.Subject.Length > 255 ||
            identity.Email.Length > 255 || identity.Name.Length > 255)
        {
            throw new ArgumentException("External identity fields exceed the supported length.", nameof(identity));
        }

        var provider = identity.Provider.Trim().ToLowerInvariant();
        var email = identity.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        return _relationalOperationsService.SafeDatabaseWriteAsync(
            async context =>
            {
                var existing = await FindAccountAsync(context, provider, identity.Subject, cancellationToken);
                if (existing != null)
                {
                    return new ExternalSignInResult(existing.User, false);
                }

                if (await context.Users.AnyAsync(
                    u => u.Email.ToUpper() == normalizedEmail, cancellationToken))
                {
                    _logger.LogWarning("External sign-in requires explicit linking to an existing user");
                    throw new AccountLinkRequiredException();
                }

                var account = new ExternalAccount
                {
                    Provider = provider,
                    ProviderSubject = identity.Subject,
                    NormalizedEmail = normalizedEmail,
                    User = new User { Name = identity.Name, Email = email, Image = identity.Image }
                };
                context.ExternalAccounts.Add(account);
                try
                {
                    // One SaveChanges transaction commits both the user and external identity.
                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Created Core user {UserId}", account.UserId);
                    return new ExternalSignInResult(account.User, true);
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: "IX_ExternalAccounts_Provider_ProviderSubject" or
                            "IX_ExternalAccounts_NormalizedEmail"
                    })
                {
                    context.ChangeTracker.Clear();
                    existing = await FindAccountAsync(context, provider, identity.Subject, cancellationToken);
                    if (existing != null)
                    {
                        _logger.LogInformation("Recovered concurrent external sign-in for user {UserId}", existing.UserId);
                        return new ExternalSignInResult(existing.User, false);
                    }

                    _logger.LogWarning("External identity conflicts with an existing account; linking required");
                    throw new AccountLinkRequiredException();
                }
            },
            nameof(FindOrCreateExternalUserAsync),
            cancellationToken,
            initializeDatabase: false);
    }

    public Task<User?> GetUserByIdAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        return _relationalOperationsService.SafeDatabaseReadAsync(
            context => context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.ID == userId, cancellationToken),
            nameof(GetUserByIdAsync),
            cancellationToken,
            initializeDatabase: false);
    }

    private static Task<ExternalAccount?> FindAccountAsync(
        DatabaseContext context, string provider, string subject, CancellationToken cancellationToken) =>
        context.ExternalAccounts.AsNoTracking().Include(a => a.User)
            .SingleOrDefaultAsync(a => a.Provider == provider && a.ProviderSubject == subject, cancellationToken);
}
