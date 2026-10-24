
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class EmbedBatchRequest
  {
    [JsonPropertyName("requests")]
    public List<EmbedRequest> Requests { get; set; } = new List<EmbedRequest>();
  }
}