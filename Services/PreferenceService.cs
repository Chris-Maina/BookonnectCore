namespace BookonnectCore.Services
{
  using BookonnectCore.Exceptions;
  using BookonnectCore.Models;
  using BookonnectCore.Services.Interfaces;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;

  public class PreferenceService : IPreferenceService
  {
    private readonly IRelationalOperationsService _relationalOperationsService;
    private readonly IVectorOperationsService _vectorOperationsService;
    private readonly ILogger<PreferenceService> _logger;

    public PreferenceService(
        IRelationalOperationsService relationalOperationsService,
        IVectorOperationsService vectorOperationsService,
        ILogger<PreferenceService> logger)
    {
        _relationalOperationsService = relationalOperationsService;
        _vectorOperationsService = vectorOperationsService;
        _logger = logger;
    }

    public async Task ProcessUpdatedPreferencesAsync(DateTime since)
    {
      _logger.LogDebug("Starting to process preferences updated since {Since}", since);
  
      var userIdsOfUpdatedPreferences = await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Preferences
          .AsNoTracking()
          .Where(p => p.UpdatedAt >= since || p.CreatedAt >= since)
          .Select(p => p.UserId)
          .Distinct()
          .ToListAsync(),
        nameof(ProcessUpdatedPreferencesAsync)
      );

      if (userIdsOfUpdatedPreferences.Count == 0)
      {
        _logger.LogInformation("No updated preferences found since {Since}", since);
        return;
      }

      foreach (var userId in userIdsOfUpdatedPreferences)
      {

        try
        {
          await ProcessUserPreferenceAsync(userId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to process preferences for user {UserId}", userId);
        }
      }
      _logger.LogDebug("Successfully completed processing preferences");
    }

    public virtual async Task<IEnumerable<Preference>?> GetPreferencesByUserIdAsync(int userId)
    {
      if (userId <= 0)
      {
        throw new ArgumentException("User ID must be positive", nameof(userId));
      }

      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Preferences.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(),
        nameof(GetPreferencesByUserIdAsync)
      );
    }

    private async Task ProcessUserPreferenceAsync(int userId)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(userId);
        var preferences = await GetPreferencesByUserIdAsync(userId);
        if (preferences == null || !preferences.Any())
        {
          _logger.LogWarning("No preferences found for user ID {UserId}", userId);
          return;
        }

        var likedBooks = preferences.Where(p => p.IsLiked).Select(p => p.BookId).ToList();
        var dislikedBooks = preferences.Where(p => !p.IsLiked).Select(p => p.BookId).ToList();

        if (likedBooks.Count == 0 && dislikedBooks.Count == 0)
        {
          _logger.LogWarning("User {UserId} has no valid book preferences", userId);
          return;
        }

        var userProfileVector = await _vectorOperationsService.CreateUserProfileVectorAsync(userId, likedBooks, dislikedBooks);

        if (userProfileVector == null || userProfileVector.Length == 0)
        {
          _logger.LogWarning("Failed to create profile vector for user {UserId}", userId);
          return;
        }
        await _vectorOperationsService.UpsertUserProfileVectorAsync(userId, userProfileVector);
        _logger.LogDebug("Successfully processed user ID {UserId} with {LikedCount} liked and {DislikedCount} disliked books", userId, likedBooks.Count, dislikedBooks.Count);
      }
      catch (ArgumentException ex)
      {
        _logger.LogError(ex, "Invalid argument processing user {UserId} preferences", userId);
        throw new InvalidOperationException($"Invalid data for user {userId}", ex);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Database"))
      {
        _logger.LogError(ex, "Database error processing user {UserId} preferences", userId);
        throw; // Re-throw database errors
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Timeout processing user {UserId} preferences", userId);
        throw new TimeoutException($"Processing timeout for user {userId}", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error processing user {UserId} preferences", userId);
        throw new InvalidOperationException($"Failed to process preferences for user {userId}", ex);
      }
    }

    public async Task<IEnumerable<Preference>?> GetAllPreferencesAsync()
    {
      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Preferences.AsNoTracking().ToListAsync(),
        nameof(GetAllPreferencesAsync)
      );
    }

    public async Task AddPreferenceAsync(Preference preference)
    {
      _logger.LogInformation("Adding a preference to the database");
      ArgumentNullException.ThrowIfNull(preference);

      if (preference.UserId <= 0)
      {
        throw new ArgumentException("Preference must have a valid UserId", nameof(preference.UserId));
      }
      if (string.IsNullOrWhiteSpace(preference.BookId))
      {
        throw new ArgumentException("Preference must have a valid BookId", nameof(preference.BookId));
      }

      await _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          bool exists = await dbContext.Preferences.AnyAsync(p => p.UserId == preference.UserId && p.BookId == preference.BookId);
          if (exists)
          {
            _logger.LogWarning("Preference for UserId {UserId} and BookId {BookId} already exists", preference.UserId, preference.BookId);
            throw new AlreadyExistsException("Preference already exists for this user and book");
          }
          dbContext.Preferences.Add(preference);
          await dbContext.SaveChangesAsync();
          return preference; // Return the added preference
        },
        nameof(AddPreferenceAsync)
      );
    }

    public Task UpdatePreferenceAsync(Preference preference)
    {
      _logger.LogInformation("Updating a preference in the database");
      ArgumentNullException.ThrowIfNull(preference);
      if (preference.Id <= 0)
      {
        throw new ArgumentException("Preference must have a valid Id", nameof(preference.Id));
      }

      return _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          var existingPreference = await dbContext.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.Id == preference.Id);
          if (existingPreference == null)
          {
            _logger.LogWarning("Preference with Id {Id} not found", preference.Id);
            throw new NotFoundException("Preference not found");
          }

          dbContext.Entry(existingPreference).CurrentValues.SetValues(preference);
          await dbContext.SaveChangesAsync();
          return preference; // Return the updated preference
        },
        nameof(UpdatePreferenceAsync)
      );
    }

    public Task DeletePreferenceAsync(int preferenceId)
    {
      if (preferenceId <= 0)
      {
        throw new ArgumentException("Preference ID must be positive", nameof(preferenceId));
      }

      return _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          var existingPreference = await dbContext.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.Id == preferenceId);
          if (existingPreference == null)
          {
            _logger.LogWarning("Preference with Id {Id} not found", preferenceId);
            throw new NotFoundException("Preference not found");
          }

          dbContext.Preferences.Remove(existingPreference);
          await dbContext.SaveChangesAsync();
          return true;
        },
        nameof(DeletePreferenceAsync)
      );
    }

    public async Task<Preference?> GetPreferenceByIdAsync(int preferenceId)
    {
      if (preferenceId <= 0)
      {
        throw new ArgumentException("Preference ID must be positive", nameof(preferenceId));
      }
      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Preferences.AsNoTracking().FirstOrDefaultAsync(p => p.Id == preferenceId),
        nameof(GetPreferenceByIdAsync));
    }
  }
} 