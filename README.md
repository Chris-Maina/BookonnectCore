# BookonnectCore

## Local infrastructure: PostgreSQL and ChromaDB

### Prerequisites

- Start Docker Desktop (or a Docker Engine daemon).
- Ensure Bash, `curl`, and `docker-compose` are installed. The startup script
  currently uses `docker-compose`, not `docker compose`.
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

The startup script loads `.env` from its own directory using `set -a`, `source`,
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

`scripts/infrastructure/stop-services.sh` is currently empty. Use:

```bash
docker-compose -f scripts/infrastructure/docker-compose.yml down
```

This removes the containers and network but preserves named volumes. Do not add
`--volumes` unless you intend to delete the stored volume data.

If only modern `docker compose` is installed, use that spelling for the direct
Compose commands above. The startup script still requires `docker-compose`.
