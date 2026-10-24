
namespace BookonnectCore.DTO
{
  using System.Text.Json.Serialization;
  using BookonnectCore.Services;
  public class EmbedRequest
  {
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public EmbedContent Content { get; set; } = new EmbedContent();

    [JsonPropertyName("taskType")]
    public string TaskType { get; } = "SEMANTIC_SIMILARITY";

    [JsonPropertyName("outputDimensionality")]
    public int OutputDimensionality { get; } = EmbeddingGenerationService.OutputDimensionality;
  }
}