namespace BookonnectCore.Services
{
  using BookonnectCore.Models;
  using BookonnectCore.Exceptions;
  using BookonnectCore.Services.Interfaces;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging;
  using System;
  using Npgsql;
  using System.Net.Sockets;

  public class RelationalOperationsService : IRelationalOperationsService
  {
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RelationalOperationsService> _logger;
    private volatile bool _isInitialized = false;
    private readonly SemaphoreSlim _initializationSemaphore = new SemaphoreSlim(1, 1);

    public RelationalOperationsService(IServiceScopeFactory serviceScopeFactory, ILogger<RelationalOperationsService> logger)
    {
      _serviceScopeFactory = serviceScopeFactory;
      _logger = logger;
    }

    public async Task EnsureDatabaseInitializedAsync(CancellationToken cancellationToken)
    {
      if (_isInitialized) return;

      await _initializationSemaphore.WaitAsync(cancellationToken);

      // Perform database initialization logic here
      try
      {
        if (_isInitialized) return;

        using var scope = _serviceScopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        // Check if database can be connected to
        var canConnect = await databaseContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
          _logger.LogInformation("Database not accessible, initializing...");
          await InitializeDatabaseSchemaAsync(databaseContext, cancellationToken);
        }
        else
        {
          //  Database is accessible, verify schema
          _logger.LogDebug("Database is accessible, verifying schema...");
          await VerifyAndUpdateSchemaAsync(databaseContext, cancellationToken);
        }

        _isInitialized = true;
        _logger.LogInformation("Database initialized successfully.");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("PostgreSQL initialization was cancelled");
        throw;
      }
      catch (PostgresException ex)
      {
        _logger.LogError(ex, "PostgreSQL error during database initialization");
        throw new InvalidOperationException("RelationalDB operation failed. Check database permissions and configuration.", ex);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error for DatabaseContext");
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.", ex);
      }
      catch (InvalidOperationException ex) when (ex.Source?.Contains("EntityFramework") == true)
      {
        _logger.LogError(ex, "Entity Framework configuration error during database initialization");
        throw new InvalidOperationException("RelationalDB configuration error. Check DbContext configuration and connection string.", ex);
      }
      catch (ObjectDisposedException ex)
      {
        _logger.LogError(ex, "Service provider or scope was disposed during initialization");
        throw new InvalidOperationException("Service scope was disposed prematurely.", ex);
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Timeout during database initialization");
        throw new InvalidOperationException("RelationalDB initialization timed out. Check network connectivity and server performance.", ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        _logger.LogError(ex, "Access denied during database initialization");
        throw new InvalidOperationException("Insufficient permissions to create or access database. Check database user permissions.", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during database initialization");
        throw new InvalidOperationException("RelationalDB initialization failed due to unexpected error.", ex);
      }
      finally
      {
        _initializationSemaphore.Release();
      }
    }
    
    #region Private Methods
    private async Task InitializeDatabaseSchemaAsync(DatabaseContext dbContext, CancellationToken cancellationToken)
    {
      var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
      if (pendingMigrations.Any())
      {
        _logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
          pendingMigrations.Count(), string.Join(", ", pendingMigrations));
        await dbContext.Database.MigrateAsync(cancellationToken);
      }
      else
      {
        _logger.LogInformation("All migrations already applied");
      }
    } 

    private async Task VerifyAndUpdateSchemaAsync(DatabaseContext dbContext, CancellationToken cancellationToken)
    {
      var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
      if (pendingMigrations.Any())
      {
        _logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
          pendingMigrations.Count(), string.Join(", ", pendingMigrations));
        await dbContext.Database.MigrateAsync(cancellationToken);
      }
      // Verify tables exist by attempting to query them
      await dbContext.Books.AnyAsync(cancellationToken);
      await dbContext.Users.AnyAsync(cancellationToken);
      await dbContext.Preferences.AnyAsync(cancellationToken);
      await dbContext.ExternalAccounts.AnyAsync(cancellationToken);
      _logger.LogDebug("Database schema verified - all tables accessible");
    }
    #endregion

    public async Task<T> SafeDatabaseReadAsync<T>(
      Func<DatabaseContext, Task<T>> dbOperation, string operationName,
      CancellationToken cancellationToken = default, bool initializeDatabase = true)
    {
      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (initializeDatabase)
        {
          await EnsureDatabaseInitializedAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbOperation(dbContext);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error during '{Operation}'", operationName);
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.", ex);
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Database operation timed out during '{Operation}'", operationName);
        throw new TimeoutException("Database operation timed out", ex);
      }
      catch (PostgresException ex)
      {
        _logger.LogError(ex, "PostgreSQL error during '{Operation}': {SqlState}", operationName, ex.SqlState);

        return ex.SqlState switch
        {
          "28000" => throw new UnauthorizedAccessException("Database authentication failed", ex),
          "3D000" => throw new InvalidOperationException("Database does not exist", ex),
          "42P01" => throw new InvalidOperationException("Required table does not exist", ex),
          _ => throw new InvalidOperationException($"PostgreSQL error during {operationName}", ex)
        };
      }
      catch (OperationCanceledException ex)
      {
        _logger.LogInformation(ex, "Database operation '{Operation}' was canceled", operationName);
        throw;
      }
      catch (SocketException ex)
      {
        _logger.LogError(ex, "Network error connecting to database during '{Operation}'", operationName);
        throw new InvalidOperationException("Cannot connect to database server", ex);
      }
      catch (ObjectDisposedException ex)
      {
        _logger.LogError(ex, "Database context was disposed during operation");
        throw new InvalidOperationException("Database context was disposed prematurely", ex);
      }
    }

    public async Task<T> SafeDatabaseWriteAsync<T>(
      Func<DatabaseContext, Task<T>> dbOperation, string operationName,
      CancellationToken cancellationToken = default, bool initializeDatabase = true)
    {
      try
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (initializeDatabase)
        {
          await EnsureDatabaseInitializedAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        return await dbOperation(dbContext);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
      {
        _logger.LogError(ex, "Service resolution error during '{Operation}'", operationName);
        throw new InvalidOperationException("DatabaseContext not properly registered in dependency injection container.", ex);
      }
      catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx)
      {
        _logger.LogError(ex, "PostgreSQL constraint violation during '{Operation}': {SqlState}", operationName, pgEx.SqlState);

        return pgEx.SqlState switch
        {
          "23505" => throw new InvalidOperationException("A record with the same unique key already exists", ex), // Unique constraint violation
          "23503" => throw new InvalidOperationException("Foreign key constraint violation. Referenced record does not exist", ex), // Foreign key violation
          "23502" => throw new InvalidOperationException("Required field cannot be null", ex), // Not null constraint violation
          "23514" => throw new InvalidOperationException("Data violates check constraint", ex), // Check constraint violation
          "42804" => throw new InvalidOperationException("Data type mismatch", ex), // Data type error
          _ => throw new InvalidOperationException($"Database constraint violation during {operationName}", ex)
        };
      }
      catch (DbUpdateConcurrencyException ex)
      {
        _logger.LogError(ex, "Concurrency conflict during '{Operation}'", operationName);
        throw new InvalidOperationException("The record was modified by another user. Please refresh and try again", ex);
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Database update error during '{Operation}'", operationName);
        throw new InvalidOperationException("Database update operation failed", ex);
      }
      catch (PostgresException ex)
      {
        _logger.LogError(ex, "PostgreSQL error during '{Operation}': {SqlState}", operationName, ex.SqlState);

        return ex.SqlState switch
        {
          "28000" => throw new UnauthorizedAccessException("Database authentication failed", ex),
          "3D000" => throw new InvalidOperationException("Database does not exist", ex),
          "42P01" => throw new InvalidOperationException("Required table does not exist", ex),
          "42601" => throw new InvalidOperationException("SQL syntax error", ex),
          "53300" => throw new InvalidOperationException("Database storage full", ex),
          _ => throw new InvalidOperationException($"PostgreSQL error during {operationName}", ex)
        };
      }
      catch (TimeoutException ex)
      {
        _logger.LogError(ex, "Database operation timed out during '{Operation}'", operationName);
        throw new TimeoutException("Database operation timed out", ex);
      }
      catch (OperationCanceledException ex)
      {
        _logger.LogInformation(ex, "Database operation '{Operation}' was canceled", operationName);
        throw;
      }
      catch (SocketException ex)
      {
        _logger.LogError(ex, "Network error connecting to database during '{Operation}'", operationName);
        throw new InvalidOperationException("Cannot connect to database server", ex);
      }
      catch (ObjectDisposedException ex)
      {
        _logger.LogError(ex, "Database context was disposed during operation '{Operation}'", operationName);
        throw new InvalidOperationException("Database context was disposed prematurely", ex);
      }
      catch (AccountLinkRequiredException)
      {
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during database add operation '{Operation}'", operationName);
        throw new InvalidOperationException($"Database add operation '{operationName}' failed", ex);
      }
    }
  }
}