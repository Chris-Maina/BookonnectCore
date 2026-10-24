namespace BookonnectCore.Configuration
{
   public class VectorDBOptions
   {
       public const string SectionName = "VectorDB";
       public string Uri { get; set; } = string.Empty;
   }
}
