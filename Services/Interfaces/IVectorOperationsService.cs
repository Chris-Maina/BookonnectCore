namespace BookonnectCore.Services.Interfaces
{
  using ChromaDB.Client.Models;
  public interface IVectorOperationsService
   {
      Task InitializeAsync(CancellationToken cancellationToken = default);

      // Book embeddings
      Task<bool> UpsertBookEmbeddingAsync(string isbn13, ReadOnlyMemory<float> embedding, Dictionary<string, object>? metadata = null, string? document = null);
      Task<ChromaCollectionEntry?> GetBookEmbeddingAsync(string isbn13);
      Task<List<ChromaCollectionEntry>> GetBookEmbeddingsAsync(List<string> isbn13s);
      Task<List<ChromaCollectionQueryEntry>> GetSimilarBooksAsync(float[] embedding, int topK = 10);
      Task<IEnumerable<string>> GetAllBookIdsWithEmbeddingsAsync();
      Task<bool> DeleteBookEmbeddingAsync(string isbn13);

      // User profile vectors
      Task<float[]> CreateUserProfileVectorAsync(int userId, List<string> likedBookIds, List<string> dislikedBookIds);
      Task<bool> UpsertUserProfileVectorAsync(int userId, float[] profileVector, Dictionary<string, object>? metadata = null);
      Task<ChromaCollectionEntry?> GetUserProfileVectorAsync(int userId);
    // Task<IEnumerable<string>> GetAllUserIdsWithEmbeddingsAsync();
    // Task<bool> DeleteUserProfileVectorAsync(int userId);

    // Batch operations
    Task<bool> BatchUpsertBookEmbeddingsAsync(List<string> isbns, List<float[]> embeddings, List<Dictionary<string, object>?>? metadata = null, List<string>? documents = null);
    Task<bool> BatchDeleteBookEmbeddingsAsync(List<string> isbns);
   }
}
