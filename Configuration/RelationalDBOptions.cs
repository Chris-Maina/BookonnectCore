namespace BookonnectCore.Configuration
{
  public class RelationalDBOptions
  {
    public const string SectionName = "RelationalDB";
    public string Uri { get; set; } = string.Empty;
  }
}