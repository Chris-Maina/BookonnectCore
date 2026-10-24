namespace BookonnectCore.Services.Interfaces
{
   public interface IEmbeddingGenerationService
   {
      public Task<float[]> GenerateBookEmbeddingAsync(string document);
      public Task<float[][]> BatchGenerateBookEmbeddingsAsync(IEnumerable<string> documents);
   }
}
