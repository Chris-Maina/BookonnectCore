
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class EmbedContentResponse
  {
    [JsonPropertyName("embedding")]
    public ContentEmbedding Embedding { get; set; } = new ContentEmbedding();
  }
}