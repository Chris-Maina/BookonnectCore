using BookonnectCore.Models;

namespace BookonnectCore.Services.Interfaces
{
  public interface IRelationalOperationsService
  {
    Task EnsureDatabaseInitializedAsync(CancellationToken cancellationToken = default);
    Task<TResult> SafeDatabaseReadAsync<TResult>(Func<DatabaseContext, Task<TResult>> dbOperation, string operationName);
    Task<TResult> SafeDatabaseWriteAsync<TResult>(Func<DatabaseContext, Task<TResult>> dbOperation, string operationName);
  }
}