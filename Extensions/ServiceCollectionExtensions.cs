namespace BookonnectCore.Extensions
{

  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.DependencyInjection;
  using BookonnectCore.Models;
  using BookonnectCore.Services;
  using BookonnectCore.Services.Interfaces;
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection AddBookonnectCore(this IServiceCollection services, IConfiguration configuration)
    {
      // Validate required configuration
      var vectorDbOptions = configuration.GetSection(Configuration.VectorDBOptions.SectionName);
      var vectorDbUri = vectorDbOptions.Get<Configuration.VectorDBOptions>()?.Uri;
      if (string.IsNullOrEmpty(vectorDbUri))
      {
        throw new InvalidOperationException("VectorDB URI is not configured. Please set the VectorDB:Uri configuration value.");
      }

      var apiKeyOptions = configuration.GetSection(Configuration.ApiKeyOptions.SectionName);
      var geminiApiKey = apiKeyOptions.Get<Configuration.ApiKeyOptions>()?.Gemini;
      if (string.IsNullOrEmpty(geminiApiKey))
      {
        throw new InvalidOperationException("Gemini API key is not configured. Please set the ApiKey:Gemini configuration value.");
      }

      var relationalDBSection = configuration.GetSection(Configuration.RelationalDBOptions.SectionName);
      var connectionString = relationalDBSection.Get<Configuration.RelationalDBOptions>()?.Uri;
      if (string.IsNullOrEmpty(connectionString))
      {
        throw new InvalidOperationException("RelationalDB connection string is not configured. Please set the RelationalDB:Uri configuration value.");
      }

      // configure options
      services.Configure<Configuration.VectorDBOptions>(vectorDbOptions);
      services.Configure<Configuration.ApiKeyOptions>(apiKeyOptions);
      services.Configure<Configuration.RelationalDBOptions>(relationalDBSection);

      // register services
      services.AddScoped<IVectorOperationsService, VectorOperationsService>();
      services.AddScoped<IRelationalOperationsService, RelationalOperationsService>();
      services.AddScoped<IDatabaseSyncService, DatabaseSyncService>();
      services.AddScoped<IBookService, BookService>();
      services.AddScoped<IUserAccountService, UserAccountService>();
      services.AddScoped<IPreferenceService, PreferenceService>();
      services.AddScoped<IEmbeddingGenerationService, EmbeddingGenerationService>();

      services.AddDbContext<DatabaseContext>(options =>
      {
        options.UseNpgsql(connectionString);
      });

      return services;
    }
  }
}