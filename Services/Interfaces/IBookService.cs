namespace BookonnectCore.Services.Interfaces
{
  using BookonnectCore.Models;
 
  /// <summary>
  /// Service interface for managing book-related operations in the relational database.
  /// CRUD operations for Book entities.
  /// </summary>
  public interface IBookService
  {
    Task<List<Book>> GetAllBooksAsync();
    Task<Book?> GetBookByIdAsync(string isbn13);
    Task AddBookAsync(Book book);
    Task UpdateBookAsync(Book book);
    Task DeleteBookAsync(string isbn13);
    Task AddBooksAsync(List<Book> books);
    Task<List<string>> GetAllBookIdsAsync();
    Task<List<Book>> GetBooksByIdsAsync(List<string> isbn13s);
  }
}