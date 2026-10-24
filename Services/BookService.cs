namespace BookonnectCore.Services
{
  using BookonnectCore.Models;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Logging;
  using System;
  using EFCore.BulkExtensions;
  using BookonnectCore.Services.Interfaces;

  public class BookService : IBookService
  {
    private readonly IRelationalOperationsService _relationalOperationsService;
    private ILogger<BookService> _logger;

    public BookService(ILogger<BookService> logger, IRelationalOperationsService relationalOperationsService)
    {
      _logger = logger;
      _relationalOperationsService = relationalOperationsService;
    }

    public virtual async Task<List<Book>> GetAllBooksAsync()
    {
      _logger.LogInformation("Fetching all books from the database");
      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Books.AsNoTracking().ToListAsync(),
        nameof(GetAllBooksAsync)
      );
    }

    public virtual async Task<Book?> GetBookByIdAsync(string isbn13)
    {
      _logger.LogInformation("Fetching book with isbn13: {isbn13}", isbn13);
      if (string.IsNullOrWhiteSpace(isbn13))
      {
        _logger.LogWarning("isbn13 is null or empty");
        throw new ArgumentException("isbn13 cannot be null or empty", nameof(isbn13));
      }

      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Books.AsNoTracking().FirstOrDefaultAsync(b => b.ISBN13 == isbn13),
        nameof(GetBookByIdAsync)
      );
    }

    public virtual async Task AddBookAsync(Book book)
    {
      _logger.LogInformation("Adding a book to the database");
      ArgumentNullException.ThrowIfNull(book);

      if (string.IsNullOrWhiteSpace(book.ISBN13))
      {
        throw new ArgumentException("ISBN13 cannot be null or empty", nameof(book));
      }

      await _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          bool bookExists = await dbContext.Books.AnyAsync(b => b.ISBN13 == book.ISBN13);
          if (bookExists)
          {
            _logger.LogWarning("Book with ISBN13 {ISBN13} already exists", book.ISBN13);
            throw new InvalidOperationException($"Book with ISBN13 '{book.ISBN13}' already exists");
          }

          dbContext.Books.Add(book);
          await dbContext.SaveChangesAsync();
          return book; // Return the added book
        },
        nameof(AddBookAsync)
      );
    }

    public virtual async Task UpdateBookAsync(Book book)
    {
      _logger.LogInformation("Updating a book in the database");
      ArgumentNullException.ThrowIfNull(book);
      if (string.IsNullOrWhiteSpace(book.ISBN13))
      {
        throw new ArgumentException("ISBN13 cannot be null or empty", nameof(book));
      }

      await _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          var existingBook = await dbContext.Books.AsNoTracking().FirstOrDefaultAsync(b => b.ISBN13 == book.ISBN13);
          if (existingBook == null)
          {
            _logger.LogWarning("Book with ISBN13 {ISBN13} not found for update", book.ISBN13);
            throw new InvalidOperationException($"Book with ISBN13 '{book.ISBN13}' does not exist");
          }

          dbContext.Entry(existingBook).CurrentValues.SetValues(book);
          await dbContext.SaveChangesAsync();
          return book; // Return the updated book
        },
        nameof(UpdateBookAsync)
      );
    }

    public virtual async Task DeleteBookAsync(string isbn13)
    {
      if (string.IsNullOrWhiteSpace(isbn13))
      {
        _logger.LogWarning("isbn13 is null or empty");
        throw new ArgumentException("isbn13 cannot be null or empty", nameof(isbn13));
      }
      await _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          var book = await dbContext.Books.AsNoTracking().FirstOrDefaultAsync(b => b.ISBN13 == isbn13);
          if (book == null)
          {
            throw new InvalidOperationException("Book not found");
          }
          dbContext.Books.Remove(book);
          await dbContext.SaveChangesAsync();
          return true;
        },
        nameof(DeleteBookAsync)
      );
    }

    public virtual async Task AddBooksAsync(List<Book> books)
    {
      ArgumentNullException.ThrowIfNull(books);
      if (books.Count == 0)
      {
        _logger.LogWarning("Empty book list provided to AddBooksAsync");
        return;
      }

      await _relationalOperationsService.SafeDatabaseWriteAsync(
        async dbContext =>
        {
          // Bulk insert with ignore on conflict
          await dbContext.BulkInsertAsync(books);
          return true;
        },
        nameof(AddBooksAsync)
      );
    }

    public async Task<List<string>> GetAllBookIdsAsync()
    {
      return await _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Books.AsNoTracking().Select(b => b.ISBN13).ToListAsync(),
        nameof(GetAllBookIdsAsync)
      );
    }

    public Task<List<Book>> GetBooksByIdsAsync(List<string> isbn13s)
    {
      if (isbn13s == null || isbn13s.Count == 0)
      {
        _logger.LogWarning("Empty or null isbn13s list provided to GetBooksByIdsAsync");
        throw new ArgumentException("isbn13s cannot be null or empty", nameof(isbn13s));
      }
      return _relationalOperationsService.SafeDatabaseReadAsync(
        async dbContext => await dbContext.Books.AsNoTracking().Where(b => isbn13s.Contains(b.ISBN13)).ToListAsync(),
        nameof(GetBooksByIdsAsync)
      );
    }
  }
}