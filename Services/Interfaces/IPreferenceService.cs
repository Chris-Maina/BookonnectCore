namespace BookonnectCore.Services.Interfaces
{
  using BookonnectCore.Models;

  /// <summary>
  /// Service interface for managing user preferences.
  /// CRUD operations for Preference entities.
  /// </summary>
  public interface IPreferenceService
  {
    public Task ProcessUpdatedPreferencesAsync(DateTime since);
    public Task<IEnumerable<Preference>?> GetPreferencesByUserIdAsync(int userId);
    public Task<IEnumerable<Preference>?> GetAllPreferencesAsync();
    public Task<Preference?> GetPreferenceByIdAsync(int preferenceId);
    public Task AddPreferenceAsync(Preference preference);
    public Task UpdatePreferenceAsync(Preference preference);
    public Task DeletePreferenceAsync(int preferenceId);
  }
} 