namespace BookonnectCore.Models
{
  using Microsoft.EntityFrameworkCore;
  public class DatabaseContext : DbContext
  {
    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Preference> Preferences { get; set; }
    public DbSet<ExternalAccount> ExternalAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      modelBuilder.Entity<ExternalAccount>(account =>
      {
        account.Property(a => a.Provider).HasMaxLength(32);
        account.Property(a => a.ProviderSubject).HasMaxLength(255);
        account.Property(a => a.NormalizedEmail).HasMaxLength(255);
        account.HasIndex(a => new { a.Provider, a.ProviderSubject }).IsUnique();
        account.HasIndex(a => a.NormalizedEmail).IsUnique();
        account.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
      });

      // Configure relationships
      modelBuilder.Entity<Preference>()
        .HasOne<User>()
        .WithMany()
        .HasForeignKey(p => p.UserId);

      modelBuilder.Entity<Preference>()
        .HasOne<Book>()
        .WithMany()
        .HasForeignKey(p => p.BookId);

      // Configure composite unique index on UserId + BookId
      modelBuilder.Entity<Preference>()
        .HasIndex(p => new { p.UserId, p.BookId })
        .IsUnique();
    }
  }
}