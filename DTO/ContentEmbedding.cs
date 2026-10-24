
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class ContentEmbedding
  {
    [JsonPropertyName("values")]
    public float[] Values { get; set; } = Array.Empty<float>();
  }
}