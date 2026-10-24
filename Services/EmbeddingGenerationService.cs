using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BookonnectCore.Configuration;
using BookonnectCore.DTO;
using BookonnectCore.Services.Interfaces;

namespace BookonnectCore.Services
{
   public class EmbeddingGenerationService : IEmbeddingGenerationService
   {
      private readonly ILogger<EmbeddingGenerationService> _logger;
      private readonly HttpClient _httpClient;
      public static readonly int OutputDimensionality = 768; // Default gemini-embedding-001 embedding dimension is 3072. Scaling down to 768 for compatibility with ChromaDB storage.
      private const string Model = "models/gemini-embedding-001";
      private const string EmbedUrl = $"/v1beta/{Model}";
    private readonly string _apiKey;

      public EmbeddingGenerationService(ILogger<EmbeddingGenerationService> logger, HttpClient httpClient, IOptions<ApiKeyOptions> apiKeyOptions)
      {
         _logger = logger;
         _httpClient = httpClient;
         _apiKey = apiKeyOptions.Value.Gemini;
         _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
      }

      public async Task<float[]> GenerateBookEmbeddingAsync(string document)
      {
        try
        {
          object requestBody = new EmbedRequest
          {
            Model = Model,
            Content = new EmbedContent
            {
              Parts = new List<EmbedPart> { new EmbedPart { Text = document } }
            }
          };

          var (content, endpoint, isBatchRequest) = CreateRequestContent(requestBody, "embedContent", false);

          // Send the request and get the response
          var response = await SendEmbeddingRequestAsync(content, endpoint);

          var singleResponse = await response.Content.ReadFromJsonAsync<EmbedContentResponse>();
          if (singleResponse?.Embedding?.Values != null)
          {
            return singleResponse.Embedding.Values;
          }
          return Array.Empty<float>();

        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
          _logger.LogError(ex, "Request to Gemini API timed out");
          throw new TimeoutException("Gemini API request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
          _logger.LogError(ex, "HTTP request error");
          throw new HttpRequestException("Error calling Gemini API", ex);
        }
        catch (JsonException ex)
        {
          _logger.LogError(ex, "JSON serialization/deserialization error");
          throw new JsonException("Error processing JSON", ex);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Unexpected error generating embeddings from Gemini API");
          throw;
        }
      }

      public async Task<float[][]> BatchGenerateBookEmbeddingsAsync(IEnumerable<string> documents)
      {
         try
         {
          var requests = documents.Select(doc => new EmbedRequest
          {
            Model = Model,
            Content = new EmbedContent
            {
              Parts = new List<EmbedPart> { new EmbedPart { Text = doc } }
            }
          }).ToList();
          var requestBody =  new EmbedBatchRequest
          {
            Requests = requests
          };

          var (content, endpoint, isBatchRequest) = CreateRequestContent(requestBody, "batchEmbedContents", true);

          // Send the request and get the response
          var response = await SendEmbeddingRequestAsync(content, endpoint);
          var batchResponse = await response.Content.ReadFromJsonAsync<BatchEmbedContentResponse>();

          if (batchResponse?.Embeddings != null)
          {
            return batchResponse.Embeddings
              .Select(e => e.Values)
              .ToArray();
          }
          return Array.Empty<float[]>();
        }
       catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
          _logger.LogError(ex, "Request to Gemini API timed out");
          throw new TimeoutException("Gemini API request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
          _logger.LogError(ex, "HTTP request error");
          throw new HttpRequestException("Error calling Gemini API", ex);
        }
        catch (JsonException ex)
        {
          _logger.LogError(ex, "JSON serialization/deserialization error");
          throw new JsonException("Error processing JSON", ex);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Unexpected error generating embeddings from Gemini API");
          throw;
        }
     }

      /// <summary>
      /// Creates the request content for single or batch embedding requests.
      /// @param requestBody The request body containing the documents to embed.
      /// @param embedContent A string indicating the type of embedding content (embedContent or batchEmbedContents).
      /// @param isBatchRequest A boolean indicating if the request is for multiple documents.
      /// @returns A tuple containing the StringContent, endpoint URL, and a bool indicating if it's a batch request.
      /// </summary>
      private (StringContent Content, string Endpoint, bool IsBatch) CreateRequestContent(object requestBody, string embedContent, bool isBatchRequest)
      {
        // Serialize the request body to JSON
        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
        return (content, $"{EmbedUrl}:{embedContent}", isBatchRequest);
      }

      // <summary>
      /// Sends the embedding request to the specified endpoint.
      /// </summary>
      private async Task<HttpResponseMessage> SendEmbeddingRequestAsync(StringContent content, string endpoint)
      {
        // Set up the request including required headers
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
        requestMessage.Headers.Add("x-goog-api-key", _apiKey);
        requestMessage.Content = content;

        var response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        return response;
      }
   }
}
