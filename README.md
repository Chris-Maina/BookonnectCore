# BookonnectCore

Shared .NET 8 library for Bookonnect, providing book and user data access,
preference management, and vector-based book discovery.

This repository builds a library and NuGet package, not a standalone application.
A consuming application supplies configuration, logging, HTTP clients, and the
application entry point. There are no HTTP routes or Swagger UI in this project.

## Features

- Book CRUD and bulk insertion backed by PostgreSQL and Entity Framework Core.
- External identity lookup and user creation, with explicit account-linking
  conflicts rather than automatic email-based linking.
- User preference storage and profile-vector generation from liked and disliked books.
- Single and batch text embeddings through Google's `gemini-embedding-001` model.
- ChromaDB storage for book embeddings and user profiles, plus similarity queries.
- CSV book import, synchronization of missing book embeddings, orphan cleanup,
  and synchronization status reporting.

## Prerequisites

- .NET 8 SDK.
- PostgreSQL and ChromaDB for database-backed operations. The supplied Compose
  file uses PostgreSQL 18 and ChromaDB `0.6.4.dev226`.
- A Gemini API key and network access to the Gemini API for embedding generation.
- Docker, Bash, `curl`, and `docker-compose` if using the local infrastructure scripts.
- An EF Core 8 CLI tool and a configured startup application for migrations.

## Installation

From the repository root:

```bash
dotnet restore BookonnectCore.sln
dotnet build BookonnectCore.sln --configuration Release --no-restore
```

Add a project reference from your consuming application, replacing both paths:

```bash
dotnet add /path/to/YourApp.csproj reference /path/to/BookonnectCore/BookonnectCore.csproj
```

The project enables `GeneratePackageOnBuild`, so building also produces a NuGet
package. To explicitly create a Release package:

```bash
dotnet pack BookonnectCore.csproj --configuration Release
```

Packages are written to `bin/Release/`. The package ID is `BookonnectCore`;
publication to a package feed is not configured in this repository.

## Configuration

`AddBookonnectCore` requires all three configuration values below, even when the
host only uses relational services:

| Key | Purpose |
| --- | --- |
| `RelationalDB:Uri` | Npgsql PostgreSQL connection string |
| `VectorDB:Uri` | ChromaDB server URL |
| `ApiKey:Gemini` | Gemini API key |

For example, set these environment variables before starting your consuming
ASP.NET Core application:

```bash
export RelationalDB__Uri='Host=localhost;Port=5432;Database=bookonnect;Username=bookonnect_user;Password=replace-with-your-local-password'
export VectorDB__Uri='http://localhost:8000'
export ApiKey__Gemini='replace-with-your-api-key'
```

ASP.NET Core's default configuration maps `__` to `:`. Other hosts must load an
equivalent `IConfiguration`. Use environment variables, host user secrets, or a
secret manager for credentials; do not commit them.

The infrastructure `.env` configures containers only. It does not automatically
configure the consuming .NET application.

## Usage

In an existing ASP.NET Core host's `Program.cs`, register the library before
building the application:

```csharp
using BookonnectCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddBookonnectCore(builder.Configuration);

var app = builder.Build();

// Configure the host's endpoints here.
app.Run();
```

ASP.NET Core supplies logging. Other hosts must also register logging services.
`AddHttpClient()` supplies the `HttpClient` dependency used by the embedding and
vector services; `AddBookonnectCore` does not register it itself.

Services and `DatabaseContext` are scoped. Inject their interfaces into a
request-scoped consumer, or create a scope for background work:

```csharp
using BookonnectCore.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

// Run in the host, with access to app, before app.Run().
using var scope = app.Services.CreateScope();
var books = scope.ServiceProvider.GetRequiredService<IBookService>();
var book = await books.GetBookByIdAsync("9780140328721");
Console.WriteLine(book?.Title ?? "Book not found");
```

### Core service interfaces

Interfaces are defined in [`Services/Interfaces`](Services/Interfaces).

| Interface | Responsibilities |
| --- | --- |
| `IBookService` | Read, insert, update, and delete books |
| `IUserAccountService` | Find or create an external user; retrieve a user by ID |
| `IPreferenceService` | Manage preferences and process updated user profiles |
| `IEmbeddingGenerationService` | Generate single or batch text embeddings |
| `IVectorOperationsService` | Manage embeddings, query similar books, and build user vectors |
| `IDatabaseSyncService` | Import CSV data, synchronize books, remove orphans, and report status |
| `IRelationalOperationsService` | Initialize the relational database and wrap database operations |

For example, inside a service scope:

```csharp
var sync = scope.ServiceProvider.GetRequiredService<IDatabaseSyncService>();
await sync.SyncSingleBookAsync("9780140328721");
var status = await sync.GetSyncStatusAsync();
Console.WriteLine($"Books missing embeddings: {status.MissingInVectorDBCount}");
```

Synchronization requires running databases and a working Gemini API key.
`SyncRelationalToVectorDBAsync` adds missing embeddings and removes orphaned
entries; use `SyncSingleBookAsync` to refresh a specific book.

The vector service currently uses collection names `bookstest` and `userstest`.
These names are hardcoded, not configuration options. The consuming application
must schedule synchronization and preference processing; this library does not
register a background worker.

## Local infrastructure: PostgreSQL and ChromaDB

### Prerequisites

- Start Docker Desktop (or a Docker Engine daemon).
- Ensure Bash, `curl`, and `docker-compose` are installed. The infrastructure
  scripts currently use `docker-compose`, not `docker compose`.
- Run the commands below from the repository root.

### 1. Configure the environment

Create or update `scripts/infrastructure/.env`:

```bash
POSTGRES_USER=bookonnect_user
POSTGRES_PASSWORD='replace-with-your-local-password'
POSTGRES_DB=bookonnect
POSTGRES_PORT=5432
CHROMADB_PORT=8000
CHROMADB_URL=http://localhost:8000
```

Use your own local password and do not commit credentials. The file must contain
valid Bash assignments, without spaces around `=`. Quote values containing spaces
or shell-special characters.

The infrastructure scripts load `.env` from their own directory using `set -a`, `source`,
and `set +a`. This exports its variables to Docker Compose, so you do not need to
source the file manually. Values assigned in `.env` replace existing shell values.
Only use a trusted file, because Bash executes its contents.

For the current startup script, keep `POSTGRES_USER=bookonnect_user`,
`CHROMADB_PORT=8000`, and `CHROMADB_URL=http://localhost:8000`: its readiness checks
hardcode that username and endpoint.

### 2. Start the services

```bash
bash scripts/infrastructure/start-services.sh
```

The script loads the environment, checks Docker, and starts PostgreSQL and
ChromaDB together in the background. There is no separate service startup order;
do not execute `.env` or `docker-compose.yml` directly.

With the example configuration, PostgreSQL is available at `localhost:5432` and
ChromaDB at `http://localhost:8000`.

### 3. Inspect the services

```bash
docker-compose -f scripts/infrastructure/docker-compose.yml ps
docker-compose -f scripts/infrastructure/docker-compose.yml logs --tail=100 postgres chromadb
```

Direct Compose commands load the `.env` beside the Compose file by default.
Already-exported shell variables can override those values.

The startup script's readiness loops have no timeout. If it keeps printing dots,
inspect the container status and logs in another terminal. Pressing Ctrl+C stops
the script but does not stop containers it has already started.

The ChromaDB readiness check does not reject HTTP error responses, so its success
message alone does not guarantee the API is healthy.

### 4. Stop the services

```bash
bash scripts/infrastructure/stop-services.sh
```

The script loads `.env`, checks Docker, and runs Compose `down`. This removes the
containers and network but preserves named volumes.

If only modern `docker compose` is installed, use that spelling for the direct
Compose commands above. The infrastructure scripts still require `docker-compose`.

## Database migrations

EF Core migrations are committed in [`Migrations`](Migrations). Apply them before
using external-account operations, which do not initialize the database themselves.

If the EF CLI is not installed, install a version matching the project's EF Core
dependencies:

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
```

Start the infrastructure and configure the consuming application's connection
string first. From this repository root, apply migrations using a startup
application that registers `DatabaseContext` through `AddBookonnectCore`:

```bash
dotnet ef database update \
  --project BookonnectCore.csproj \
  --startup-project /path/to/YourApp.csproj \
  --context BookonnectCore.Models.DatabaseContext \
  -- --environment Development
```

To add a migration after changing the models:

```bash
dotnet ef migrations add YourMigrationName \
  --project BookonnectCore.csproj \
  --startup-project /path/to/YourApp.csproj \
  --context BookonnectCore.Models.DatabaseContext \
  -- --environment Development
```

The helpers in [`scripts/migrations`](scripts/migrations) assume a separate startup
project at `../BookonnectAPI/BookonnectAPI` relative to this repository. That project
is not included here. `init-database.sh` delegates to `run-migrations.sh`, which
starts infrastructure and applies migrations. Use the explicit commands above if
your startup project lives elsewhere.

## Project structure

```text
Configuration/        Strongly typed API and database options
DTO/                  Embedding request/response and external identity types
Exceptions/           Domain-specific exceptions
Extensions/           Dependency injection registration
Migrations/           EF Core schema migrations and model snapshot
Models/               Entities and DatabaseContext
Services/             Data access, embeddings, preferences, and synchronization
  Interfaces/         Public service contracts
scripts/
  infrastructure/     Docker Compose and service lifecycle scripts
  migrations/         EF CLI helpers for the companion API layout
```

## Development and testing

Build the solution after code changes:

```bash
dotnet build BookonnectCore.sln
```

The solution currently contains only the library project. No automated test
project or CI workflow is included. A successful build checks compilation, not
database connectivity or embedding behavior. Integration validation requires a
configured consuming host, migrated PostgreSQL database, ChromaDB, and Gemini API
access for embedding operations.
