namespace BookonnectCore.Models;

public class ExternalAccount
{
    public int ID { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderSubject { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}
