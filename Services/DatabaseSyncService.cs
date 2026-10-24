
namespace BookonnectCore.Services
{
  using Microsoft.Extensions.Logging;
  using BookonnectCore.Models;
  using System.Threading.Tasks;
  using CsvHelper;
  using System.Globalization;
  using BookonnectCore.Exceptions;
  using System;
  using BookonnectCore.Services.Interfaces;

  public class DatabaseSyncService : IDatabaseSyncService
  {
    private readonly IBookService _bookService;
    private readonly ILogger<DatabaseSyncService> _logger;
    private readonly IEmbeddingGenerationService _embeddingService;
    private readonly IVectorOperationsService _vectorOperationsService;

    public DatabaseSyncService(IBookService bookService, ILogger<DatabaseSyncService> logger, IEmbeddingGenerationService embeddingService, IVectorOperationsService vectorOperationsService)
    {
      _bookService = bookService;
      _logger = logger;
      _embeddingService = embeddingService;
      _vectorOperationsService = vectorOperationsService;
    }

    public async Task<SyncStatus> GetSyncStatusAsync()
    {
      try
      {
        var relationalBookIds = await _bookService.GetAllBookIdsAsync();
        var vectorBookIds = await _vectorOperationsService.GetAllBookIdsWithEmbeddingsAsync();

        var relationalBookIdSet = relationalBookIds.ToHashSet();
        var vectorBookIdSet = vectorBookIds.ToHashSet();

        var inSyncCount = relationalBookIdSet.Intersect(vectorBookIdSet).Count();
        var missingInVectorCount = relationalBookIdSet.Except(vectorBookIdSet).Count();
        var orphanedVectorCount = vectorBookIdSet.Except(relationalBookIdSet).Count();

        return new SyncStatus
        {
          RelationalBookCount = relationalBookIds.Count(),
          VectorBookCount = vectorBookIds.Count(),
          InSyncCount = inSyncCount,
          MissingInVectorDBCount = missingInVectorCount,
          OrphanedInVectorDBCount = orphanedVectorCount
        };
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while getting sync status");
        return new SyncStatus();
      }
    }

    public async Task SyncFromCsvAsync(string csvFilePath)
    {
      _logger.LogInformation($"Syncing books from CSV file: {csvFilePath}");
      try
      {
        // Load books from CSV
        var books = await LoadBooksFromCsv(csvFilePath);
        _logger.LogInformation($"Loaded {books.Count} books from CSV.");

        // Sync to relational database
        await _bookService.AddBooksAsync(books);
        _logger.LogInformation("Synced books to relational database.");

        // Sync relational database to vector DB
        await SyncRelationalToVectorDBAsync();
      }
      catch (BookSyncException ex)
      {
        _logger.LogError(ex, "Book sync error: {Message}", ex.Message);
        throw;
      }
      catch (System.Exception ex)
      {
        _logger.LogError(ex, "Error occurred while syncing from CSV");
        throw new BookSyncException("Error syncing books from CSV", ex);
      }
    }

    public async Task SyncRelationalToVectorDBAsync()
    {
      _logger.LogInformation("Syncing relational database to vector DB ...");
      try
      {
        // Get all books from relational database
        var books = await _bookService.GetAllBooksAsync();
        _logger.LogInformation($"Fetched {books.Count} books from relational database.");

        if (books.Count == 0)
        {
          _logger.LogWarning("No books found in relational database to sync.");
          return;
        }

        // Get existing vector entries
        var existingVectorIds = await _vectorOperationsService.GetAllBookIdsWithEmbeddingsAsync();
        if (existingVectorIds.Count() == 0)
        {
          _logger.LogInformation("No existing vector entries found.");
          // Process in batches
          await ProcessBooksInBatchesAsync(books);
        }
        else
        {
          _logger.LogInformation($"Found {existingVectorIds.Count()} existing vector entries.");
          var existingVectorIdSet = existingVectorIds.ToHashSet();
          // Identify books that need embeddings
          var booksToSync = books.Where(b => !existingVectorIdSet.Contains(b.ISBN13)).ToList();
          _logger.LogInformation($"{booksToSync.Count} books need embeddings.");

          // Process in batches
          await ProcessBooksInBatchesAsync(booksToSync);
        }

        // Remove orphaned vector entries
        await RemoveOrphanedVectorEntriesAsync();

        _logger.LogInformation("Completed syncing relational database to vector DB.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while syncing relational database to vector DB");
        throw new BookSyncException("Failed to sync relational database to vector DB", ex);
      }

    }

    public async Task RemoveOrphanedVectorEntriesAsync()
    {
      _logger.LogInformation("Removing orphaned vector entries ...");
      try
      {
        var relationalBookIds = await _bookService.GetAllBookIdsAsync();
        var relationalBookIdSet = relationalBookIds.ToHashSet();
        var vectorBookIds = await _vectorOperationsService.GetAllBookIdsWithEmbeddingsAsync();

        var orphanedVectorIds = vectorBookIds.Where(id => !relationalBookIdSet.Contains(id)).ToList();
        _logger.LogInformation($"Found {orphanedVectorIds.Count} orphaned vector entries to remove.");
        if (orphanedVectorIds.Count > 0)
        {
          await _vectorOperationsService.BatchDeleteBookEmbeddingsAsync(orphanedVectorIds);
          _logger.LogInformation("Orphaned vector entries removed successfully.");
        }
        else
        {
          _logger.LogInformation("No orphaned vector entries found.");
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while removing orphaned vector entries");
        throw new BookSyncException("Failed to remove orphaned vector entries", ex);
      }
    }

    public async Task SyncSingleBookAsync(string isbn13)
    {
      try
      {
        _logger.LogInformation("Syncing single book with ISBN13: {ISBN13}", isbn13);
        var book = await _bookService.GetBookByIdAsync(isbn13);
        if (book == null)
        {
          _logger.LogWarning("Book with ISBN13: {ISBN13} not found in relational database.", isbn13);

          // remove from vector DB if exists
          await _vectorOperationsService.DeleteBookEmbeddingAsync(isbn13);
          return;
        }

        // check if entry exists in vector DB
        var existingEntry = await _vectorOperationsService.GetBookEmbeddingAsync(isbn13);
        if (existingEntry != null)
        {
          _logger.LogInformation("Book with ISBN13: {ISBN13} already has an embedding in vector DB. Skipping.", isbn13);
          return;
        }

        // get embedding
        var embedding = await _embeddingService.GenerateBookEmbeddingAsync(book.Description);
        // upsert to vector DB
        await _vectorOperationsService.UpsertBookEmbeddingAsync(isbn13, embedding, null, book.Description);
        _logger.LogInformation("Successfully synced book with ISBN13: {ISBN13} to vector DB.", isbn13);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error occurred while syncing single book: {ISBN13}", isbn13);
        throw new BookSyncException("Failed to sync single book", ex);
      }
    }

    #region Private Methods
    private async Task<List<Book>> LoadBooksFromCsv(string csvFilePath)
    {
      int lineNumber = 0;
      try
      {
        _logger.LogInformation("Loading books from CSV file: {CsvFilePath}", csvFilePath);

        if (!File.Exists(csvFilePath))
        {
          _logger.LogError("CSV file not found: {CsvFilePath}", csvFilePath);
          throw new FileNotFoundException("CSV file not found", csvFilePath);
        }

        using var reader = new StreamReader(await File.ReadAllTextAsync(csvFilePath));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Read header
        await csv.ReadAsync();
        csv.ReadHeader();
        lineNumber = 1;

        var books = new List<Book>();
        while (await csv.ReadAsync())
        {
          lineNumber++;
          var book = new Book
          {
            ISBN13 = csv.GetField("isbn13") ?? throw new BookSyncException("Missing ISBN13 field", csvFilePath, lineNumber),
            ISBN10 = csv.GetField("isbn10") ?? throw new BookSyncException("Missing ISBN10 field", csvFilePath, lineNumber),
            Title = csv.GetField("title") ?? string.Empty,
            Authors = csv.GetField("authors") ?? string.Empty,
            Categories = csv.GetField("categories") ?? string.Empty,
            ThumbnailUrl = csv.GetField("thumbnail") ?? string.Empty,
            Description = csv.GetField("description") ?? throw new BookSyncException("Missing description field", csvFilePath, lineNumber),
            PublishedYear = DateTime.TryParse(csv.GetField("published_year"), out var publishedYear) ? publishedYear : DateTime.MinValue,
            AverageRating = float.TryParse(csv.GetField("average_rating"), out var averageRating) ? averageRating : 0.0f,
            PageCount = int.TryParse(csv.GetField("num_pages"), out var pageCount) ? pageCount : 0,
            RatingsCount = int.TryParse(csv.GetField("ratings_count"), out var ratingsCount) ? ratingsCount : 0
          };
          books.Add(book);
        }

        return books;
      }
      catch (FileNotFoundException ex)
      {
        _logger.LogError(ex, "CSV file not found: {CsvFilePath}", csvFilePath);
        throw new BookSyncException("CSV file not found", csvFilePath, ex);
      }
      catch (DirectoryNotFoundException ex)
      {
        _logger.LogError(ex, "Directory not found for CSV file: {CsvFilePath}", csvFilePath);
        throw new BookSyncException("Directory for CSV file not found. Check the file path.", csvFilePath, ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        throw new BookSyncException("Access denied to CSV file", csvFilePath, ex);
      }
      catch (IOException ex)
      {
        _logger.LogError(ex, "IO error reading CSV file: {CsvFilePath}", csvFilePath);
        throw new BookSyncException("IO error reading CSV file. File may be locked or corrupted.", csvFilePath, ex);
      }
      catch (CsvHelperException ex)
      {
        _logger.LogError(ex, "CSV parsing error in file: {CsvFilePath}", csvFilePath);
        throw new BookSyncException("Error parsing CSV file. Ensure the file format is correct.", csvFilePath, ex, lineNumber);
      }
      catch (BookSyncException)
      {
        // rethrow to be handled by caller
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error loading books from CSV: {CsvFilePath}", csvFilePath);
        throw new BookSyncException($"Unexpected error loading CSV on line {lineNumber}", csvFilePath, ex, lineNumber);
      }
    }

    private async Task ProcessBooksInBatchesAsync(List<Book> booksToSync)
    {
      int batchSize = 20;
      var totalBatches = (int)Math.Ceiling((double)booksToSync.Count / batchSize);
      for (int i = 0; i < booksToSync.Count; i += batchSize)
      {
        var batch = booksToSync.Skip(i).Take(batchSize).ToList();
        _logger.LogInformation($"Processing batch {i / batchSize + 1}: {batch.Count} books.");

        try
        {
          var documents = batch.Select(b => b.Description).ToList();
          var embeddings = await _embeddingService.BatchGenerateBookEmbeddingsAsync(documents);

          var ids = batch.Select(b => b.ISBN13).ToList();
          await _vectorOperationsService.BatchUpsertBookEmbeddingsAsync(ids, embeddings.ToList(), null, documents);

          // Respect API rate limits
          if (i + batchSize < booksToSync.Count)
          {
            await Task.Delay(TimeSpan.FromSeconds(2));
          }

          _logger.LogInformation($"Successfully processed batch {i / batchSize + 1} / {totalBatches}.");
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, $"Error processing batch {i / batchSize + 1}");
          throw new BookSyncException($"Error processing batch {i / batchSize + 1}", ex);
        }
      }
    }
    #endregion
  }
}