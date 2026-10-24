namespace BookonnectCore.Services
{
  using ChromaDB.Client;
  using ChromaDB.Client.Models;
  using Microsoft.Extensions.Logging;
  using Microsoft.Extensions.Options;
  using MathNet.Numerics.LinearAlgebra;
  using BookonnectCore.Configuration;
  using BookonnectCore.Services.Interfaces;
  public class VectorOperationsService : IVectorOperationsService
  {
    private readonly ILogger<VectorOperationsService> _logger;
    private readonly ChromaClient _chromaClient;
    private readonly HttpClient _httpClient;
    private readonly VectorDBOptions _vectorDbOptions;
    private readonly string _booksCollectionName = "bookstest";
    private readonly string _usersCollectionName = "userstest";
    private ChromaCollectionClient? _booksCollectionClient;
    private ChromaCollectionClient? _usersCollectionClient;
    private readonly ChromaConfigurationOptions _chromaConfigurationOptions;

    public VectorOperationsService(ILogger<VectorOperationsService> logger, HttpClient httpClient, IOptions<VectorDBOptions> vectorDbOptions)
    {
      _logger = logger;
      _httpClient = httpClient;
      _vectorDbOptions = vectorDbOptions.Value;
      _chromaConfigurationOptions = new ChromaConfigurationOptions(uri: _vectorDbOptions.Uri);
      _chromaClient = new ChromaClient(_chromaConfigurationOptions, _httpClient);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        _logger.LogInformation("Starting ChromaDB initialization...");
        ChromaCollection booksCollection = await _chromaClient.GetOrCreateCollection(_booksCollectionName);
        _booksCollectionClient = new ChromaCollectionClient(booksCollection, _chromaConfigurationOptions, _httpClient);
        _logger.LogInformation("'{collectionName}' collection client is ready.", _booksCollectionName);

        ChromaCollection usersCollection = await _chromaClient.GetOrCreateCollection(_usersCollectionName);
        _usersCollectionClient = new ChromaCollectionClient(usersCollection, _chromaConfigurationOptions, _httpClient);
        _logger.LogInformation("'{collectionName}' collection client is ready.", _usersCollectionName);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        _logger.LogInformation("ChromaDB initialization was cancelled");
        throw;
      }
      catch (ChromaException ex) when (ex.Message.Contains("MethodNotAllowed"))
      {
        _logger.LogError(ex, "ChromaDB API method not allowed. Check ChromaDB server version and API compatibility.");
        throw new InvalidOperationException($"ChromaDB API incompatibility detected. Check server version.", ex);
      }
      catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
      {
        _logger.LogError(ex, "ChromaDB collection not found and could not be created");
        throw new InvalidOperationException($"Failed to create or access ChromaDB collection. Check your ChromaDB URI.", ex);
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "Error creating/retrieving collection");
        throw;
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error connecting to ChromaDB server for collection");
        throw new InvalidOperationException($"Cannot connect to ChromaDB server. Check if ChromaDB is running and accessible.", ex);
      }
      catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
      {
        _logger.LogError(ex, "Timeout connecting to ChromaDB server for collection");
        throw new InvalidOperationException($"ChromaDB server connection timed out for collection", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error initializing ChromaDB collection");
        throw;
      }
    }

    public async Task<ChromaCollectionEntry?> GetBookEmbeddingAsync(string isbn13)
    {
      try
      {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        if (_booksCollectionClient == null)
        {
          await InitializeAsync();
        }

        var result = await _booksCollectionClient!.Get(isbn13);

        if (result == null)
        {
          _logger.LogDebug("No embedding found for book {ISBN13}", isbn13);
        }

        return result;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
      {
        _logger.LogWarning("Book embedding not found in ChromaDB for {ISBN13}", isbn13);
        return null; // Return null instead of throwing for not found
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error retrieving book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException($"Failed to retrieve book embedding for {isbn13}", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error retrieving book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException("ChromaDB connection error", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException($"Failed to get book embedding for {isbn13}", ex);
      }
    }

    public async Task<List<ChromaCollectionEntry>> GetBookEmbeddingsAsync(List<string> isbn13s)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(isbn13s);

        if (isbn13s.Count == 0)
        {
          _logger.LogWarning("Empty ISBN13 list provided to GetBookEmbeddingsAsync");
          return new List<ChromaCollectionEntry>();
        }

        // Validate ISBNs
        var invalidIsbns = isbn13s.Where(isbn => string.IsNullOrWhiteSpace(isbn)).ToList();
        if (invalidIsbns.Any())
        {
          throw new ArgumentException($"Invalid ISBN13 values found: null or empty entries", nameof(isbn13s));
        }

        if (_booksCollectionClient == null)
        {
          await InitializeAsync();
        }

        var results = await _booksCollectionClient!.Get(isbn13s);

        if (results.Count != isbn13s.Count)
        {
          var foundIsbns = results.Select(r => r.Id).ToHashSet();
          var missingIsbns = isbn13s.Where(isbn => !foundIsbns.Contains(isbn)).ToList();
          _logger.LogWarning("Some book embeddings not found. Missing: {MissingISBNs}", string.Join(", ", missingIsbns));
        }

        return results;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error retrieving book embeddings for {Count} books", isbn13s?.Count ?? 0);
        throw new InvalidOperationException("Failed to retrieve book embeddings from ChromaDB", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error retrieving book embeddings");
        throw new InvalidOperationException("ChromaDB connection error", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving book embeddings for {Count} books", isbn13s?.Count ?? 0);
        throw new InvalidOperationException("Failed to get book embeddings", ex);
      }
    }

    public async Task<List<ChromaCollectionQueryEntry>> GetSimilarBooksAsync(float[] userProfileVector, int topK)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(userProfileVector);

        if (userProfileVector.Length == 0)
        {
          throw new ArgumentException("User profile vector cannot be empty", nameof(userProfileVector));
        }

        if (topK <= 0)
        {
          throw new ArgumentException("TopK must be positive", nameof(topK));
        }

        if (userProfileVector.Any(f => !float.IsFinite(f)))
        {
          throw new ArgumentException("User profile vector contains invalid values (NaN or Infinity)", nameof(userProfileVector));
        }

        if (_booksCollectionClient == null)
        {
          await InitializeAsync();
        }

        // Convert to ReadOnlyMemory<float>
        var userProfileVectorMemory = new ReadOnlyMemory<float>(userProfileVector);

        return await _booksCollectionClient!.Query(userProfileVectorMemory, topK);
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error querying similar books (topK: {TopK})", topK);
        throw new InvalidOperationException("Failed to query similar books from ChromaDB", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error querying similar books");
        throw new InvalidOperationException("ChromaDB connection error during similarity search", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error querying similar books (topK: {TopK})", topK);
        throw new InvalidOperationException("Failed to find similar books", ex);
      }
    }

    public async Task<float[]> CreateUserProfileVectorAsync(int userId, List<string> likedBookIds, List<string> dislikedBookIds)
    {
      // Implementation for creating user profile vector
      try
      {
        if (userId <= 0)
        {
          throw new ArgumentException("User ID must be positive", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(likedBookIds);
        ArgumentNullException.ThrowIfNull(dislikedBookIds);

        if (likedBookIds.Count == 0 && dislikedBookIds.Count == 0)
        {
          _logger.LogWarning("No book preferences provided for user {UserId}", userId);
          return Array.Empty<float>();
        }

        _logger.LogDebug("Creating profile vector for user {UserId} with {LikedCount} liked and {DislikedCount} disliked books",
          userId, likedBookIds.Count, dislikedBookIds.Count);

        var likedBookEmbeddings = new List<ChromaCollectionEntry>();
        var dislikedBookEmbeddings = new List<ChromaCollectionEntry>();
        try
        {
          likedBookEmbeddings = await GetBookEmbeddingsAsync(likedBookIds);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to retrieve liked book embeddings for user {UserId}", userId);
          throw new InvalidOperationException("Failed to retrieve liked book embeddings", ex);
        }
        try
        {
          dislikedBookEmbeddings = await GetBookEmbeddingsAsync(dislikedBookIds);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to retrieve disliked book embeddings for user {UserId}", userId);
          throw new InvalidOperationException("Failed to retrieve disliked book embeddings", ex);
        }

        if (likedBookEmbeddings.Count == 0 && dislikedBookEmbeddings.Count == 0)
        {
          _logger.LogWarning("No embeddings found for liked and disliked books.");
          return Array.Empty<float>();
        }

        var invalidEmbeddings = likedBookEmbeddings.Concat(dislikedBookEmbeddings)  // Creates new IEnumerable
          .Where(e => !e.Embeddings.HasValue || e.Embeddings.Value.Length != EmbeddingGenerationService.OutputDimensionality)
          .ToList();

        if (invalidEmbeddings.Any())
        {
          _logger.LogError("Invalid embedding dimensions found for user {UserId}. Expected {ExpectedDim}, found embeddings with different dimensions",
            userId, EmbeddingGenerationService.OutputDimensionality);
          throw new InvalidOperationException("Inconsistent embedding dimensions detected");
        }

        var userProfileVector = Vector<float>.Build.Dense(EmbeddingGenerationService.OutputDimensionality);
        var totalWeight = 0.0f;

        // Process liked books with positive weights
        if (likedBookEmbeddings.Count > 0)
        {
          var weight = CalculateLikedWeight(likedBookEmbeddings.Count);
          foreach (var embedding in likedBookEmbeddings)
          {
            // Convert ReadOnlyMemory<float>? to float[] safely
            var embArray = embedding.Embeddings!.Value.ToArray();

            // Validate embedding values
            if (embArray.Any(f => !float.IsFinite(f)))
            {
              _logger.LogWarning("Invalid embedding values found for liked book {BookId} for user {UserId}",
                embedding.Id, userId);
              continue; // Skip this embedding
            }
            var bookVector = Vector<float>.Build.Dense(embArray);
            userProfileVector += bookVector * weight;
            totalWeight += weight;
          }
        }

        // Process disliked books with negative weights
        if (dislikedBookEmbeddings.Count > 0)
        {
          var weight = CalculateDislikedWeight(dislikedBookEmbeddings.Count);
          foreach (var embedding in dislikedBookEmbeddings)
          { // Convert ReadOnlyMemory<float>? to float[] safely
            var embArray = embedding.Embeddings!.Value.ToArray();

            // Validate embedding values
            if (embArray.Any(f => !float.IsFinite(f)))
            {
              _logger.LogWarning("Invalid embedding values found for disliked book {BookId} for user {UserId}",
                embedding.Id, userId);
              continue; // Skip this embedding
            }

            var bookVector = Vector<float>.Build.Dense(embArray);
            userProfileVector -= bookVector * weight;
            totalWeight += weight * 0.5f; // Count disliked weights as half for normalization
          }
        }

        if (totalWeight == 0)
        {
          _logger.LogWarning("No valid embeddings processed for user {UserId}", userId);
          return Array.Empty<float>();
        }

        // Normalize the user profile vector by total weight
        userProfileVector = userProfileVector / totalWeight;

        // Apply final normalization for cosine similarity
        var magnitude = userProfileVector.L2Norm();
        if (magnitude > 0)
        {
          userProfileVector = userProfileVector / (float)magnitude;
        }
        else
        {
          _logger.LogWarning("User profile vector has zero magnitude for user {UserId}", userId);
          return Array.Empty<float>();
        }

        _logger.LogDebug("Successfully created profile vector for user {UserId}", userId);

        return userProfileVector.ToArray();
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (InvalidOperationException)
      {
        throw; // Re-throw business logic exceptions as-is
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error creating user profile vector for user {UserId}", userId);
        throw new InvalidOperationException($"Failed to create user profile vector for user {userId}", ex);
      }
    }

    public async Task<ChromaCollectionEntry?> GetUserProfileVectorAsync(int userId)
    {
      try
      {
        if (userId <= 0)
        {
          throw new ArgumentException("User ID must be positive", nameof(userId));
        }
        if (_usersCollectionClient == null)
        {
          await InitializeAsync();
        }

        var result = await _usersCollectionClient!.Get(userId.ToString());

        if (result == null)
        {
          _logger.LogDebug("No profile vector found for user {UserId}", userId);
        }

        return result;

      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex) when (ex.Message.Contains("NotFound"))
      {
        _logger.LogDebug("User profile vector not found in ChromaDB for user {UserId}", userId);
        return null; // Return null instead of throwing for not found
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error retrieving user profile vector for user {UserId}", userId);
        throw new InvalidOperationException($"Failed to retrieve user profile vector for user {userId}", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error retrieving user profile vector for user {UserId}", userId);
        throw new InvalidOperationException("ChromaDB connection error", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving user profile vector for user {UserId}", userId);
        throw new InvalidOperationException($"Failed to get user profile vector for user {userId}", ex);
      }
    }

    public async Task<bool> UpsertUserProfileVectorAsync(int userId, float[] userProfileVector, Dictionary<string, object>? metadata)
    {
      try
      {
        if (userId <= 0)
        {
          throw new ArgumentException("User ID must be positive", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(userProfileVector);

        if (userProfileVector.Length == 0)
        {
          throw new ArgumentException("User profile vector cannot be empty", nameof(userProfileVector));
        }

        if (userProfileVector.Any(f => !float.IsFinite(f)))
        {
          throw new ArgumentException("User profile vector contains invalid values (NaN or Infinity)", nameof(userProfileVector));
        }

        if (_usersCollectionClient == null)
        {
          await InitializeAsync(CancellationToken.None);
        }
        var userIdString = userId.ToString();
        var defaultMetadata = new Dictionary<string, object>
          {
            { "user_id", userIdString }
          };

        await _usersCollectionClient!.Upsert(
          ids: new List<string> { userIdString },
          embeddings: new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(userProfileVector) },
          metadatas: new List<Dictionary<string, object>> { metadata ?? defaultMetadata },
          documents: null
        );

        _logger.LogDebug("Successfully upserted user profile vector for user {UserId}", userId);
        return true;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error upserting user profile vector for user {UserId}", userId);
        throw new InvalidOperationException($"Failed to upsert user profile vector for user {userId}", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error upserting user profile vector for user {UserId}", userId);
        throw new InvalidOperationException("ChromaDB connection error during upsert", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error upserting user profile vector for user {UserId}", userId);
        throw new InvalidOperationException($"Failed to upsert user profile vector for user {userId}", ex);
      }
    }

    public async Task<bool> UpsertBookEmbeddingAsync(string isbn13, ReadOnlyMemory<float> bookEmbedding, Dictionary<string, object>? metadata, string? document)
    {
      try
      {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        if (bookEmbedding.Length == 0)
        {
          throw new ArgumentException("Book embedding cannot be empty", nameof(bookEmbedding));
        }

        var embeddingArray = bookEmbedding.ToArray();
        if (embeddingArray.Any(f => !float.IsFinite(f)))
        {
          throw new ArgumentException("Book embedding contains invalid values (NaN or Infinity)", nameof(bookEmbedding));
        }

        if (_booksCollectionClient == null)
        {
          await InitializeAsync();
        }

        var defaultMetadata = new Dictionary<string, object>
          {
            { "book_id", isbn13 }
          };
        await _booksCollectionClient!.Upsert(
          ids: new List<string> { isbn13 },
          embeddings: new List<ReadOnlyMemory<float>> { bookEmbedding },
          metadatas: new List<Dictionary<string, object>> { metadata ?? defaultMetadata },
          documents: document != null ? new List<string> { document } : null
        );

        _logger.LogDebug("Successfully upserted book embedding for {ISBN13}", isbn13);
        return true;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error upserting book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException($"Failed to upsert book embedding for {isbn13}", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error upserting book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException("ChromaDB connection error during book embedding upsert", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error upserting book embedding for {ISBN13}", isbn13);
        throw new InvalidOperationException($"Failed to upsert book embedding for {isbn13}", ex);
      }
    }

    public async Task<bool> BatchUpsertBookEmbeddingsAsync(List<string> isbns, List<float[]> embeddings, List<Dictionary<string, object>?>? metadata, List<string>? documents)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(isbns);
        ArgumentNullException.ThrowIfNull(embeddings);

        if (isbns.Count != embeddings.Count)
        {
          throw new ArgumentException("The number of ISBNs must match the number of embeddings");
        }

        if (_booksCollectionClient == null)
        {
          await InitializeAsync();
        }

        var readOnlyEmbeddings = embeddings.Select(e => new ReadOnlyMemory<float>(e)).ToList();

        // Ensure metadatas is List<Dictionary<string, object>> and replace nulls with default
        List<Dictionary<string, object>>? safeMetadatas = null;
        if (metadata != null)
        {
          safeMetadatas = metadata
            .Select(m => m ?? new Dictionary<string, object>())
            .ToList();
        }

        await _booksCollectionClient!.Upsert(
          ids: isbns,
          embeddings: readOnlyEmbeddings,
          metadatas: safeMetadatas,
          documents: documents
        );
        _logger.LogDebug("Successfully batch upserted {Count} book embeddings", isbns.Count);
        return true;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error during batch upsert of book embeddings");
        throw new InvalidOperationException("Failed to batch upsert book embeddings", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error during batch upsert of book embeddings");
        throw new InvalidOperationException("ChromaDB connection error during batch upsert", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during batch upsert of book embeddings");
        throw new InvalidOperationException("Failed to batch upsert book embeddings", ex);
      }
    }

    private float CalculateLikedWeight(int likedCount)
    {
      // Higher weight for users with fewer liked books
      // Weight decreases as the number of liked books increases to prevent overfitting
      return Math.Max(0.5f, 2.0f / (float)Math.Sqrt(likedCount));
    }

    private float CalculateDislikedWeight(int dislikedCount)
    {
      // Smaller weight for disliked books to reduce their negative impact/aggressiveness
      // Weight decreases as the number of disliked books increases
      return Math.Max(0.5f, 0.8f / (float)Math.Sqrt(dislikedCount));
    }

    public async Task<IEnumerable<string>> GetAllBookIdsWithEmbeddingsAsync()
    {
      try
      {
        if (_booksCollectionClient == null)
        {
          await InitializeAsync(CancellationToken.None);
        }

        var bookEntries = await _booksCollectionClient!.Get();

        return bookEntries.Select(be => be.Id);
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error retrieving all book IDs with embeddings");
        throw new InvalidOperationException("ChromaDB error", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error retrieving all book IDs with embeddings");
        throw new InvalidOperationException("ChromaDB connection error", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error retrieving all book IDs with embeddings");
        throw new InvalidOperationException("Failed to retrieve all book IDs with embeddings", ex);
      }
    }

    public async Task<bool> BatchDeleteBookEmbeddingsAsync(List<string> isbns)
    {
      try
      {
        ArgumentNullException.ThrowIfNull(isbns);

        if (_booksCollectionClient == null)
        {
          await InitializeAsync(CancellationToken.None);
        }

        await _booksCollectionClient!.Delete(isbns);
        _logger.LogDebug("Successfully batch deleted {Count} book embeddings", isbns.Count);
        return true;
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
      catch (ChromaException ex)
      {
        _logger.LogError(ex, "ChromaDB error during batch delete of book embeddings");
        throw new InvalidOperationException("Failed to batch delete book embeddings", ex);
      }
      catch (HttpRequestException ex)
      {
        _logger.LogError(ex, "Network error during batch delete of book embeddings");
        throw new InvalidOperationException("ChromaDB connection error during batch delete", ex);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error during batch delete of book embeddings");
        throw new InvalidOperationException("Failed to batch delete book embeddings", ex);
      }
    }

    public Task<bool> DeleteBookEmbeddingAsync(string isbn13)
    {
      try
      {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn13);

        return BatchDeleteBookEmbeddingsAsync(new List<string> { isbn13 });
      }
      catch (ArgumentException)
      {
        throw; // Re-throw argument exceptions as-is
      }
    }
  }
}