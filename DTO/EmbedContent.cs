
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class EmbedContent
  {
    [JsonPropertyName("parts")]
    public List<EmbedPart> Parts { get; set; } = new List<EmbedPart>();
  }
}