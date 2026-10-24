
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  public class EmbedPart
  {
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
  }
}