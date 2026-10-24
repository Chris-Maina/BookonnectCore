namespace BookonnectCore.Services.Interfaces
{
  using BookonnectCore.Models;
  public interface IDatabaseSyncService
  {
    Task SyncFromCsvAsync(string csvFilePath);
    Task SyncRelationalToVectorDBAsync();
    Task SyncSingleBookAsync(string isbn13);
    Task RemoveOrphanedVectorEntriesAsync();
    Task<SyncStatus> GetSyncStatusAsync();
  }
}