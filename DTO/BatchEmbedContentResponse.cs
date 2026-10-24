
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class BatchEmbedContentResponse
  {
    [JsonPropertyName("embeddings")]
    public List<ContentEmbedding> Embeddings { get; set; } = new List<ContentEmbedding>();
  }
}