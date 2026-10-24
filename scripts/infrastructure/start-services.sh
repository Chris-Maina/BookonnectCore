set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Environment variables with defaults
POSTGRES_USER=${POSTGRES_USER}
CHROMADB_URL=${CHROMADB_URL}

echo "🚀 Starting Bookonnect services..."
echo "   PostgreSQL User: $POSTGRES_USER"
echo "   ChromaDB URL: $CHROMADB_URL"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo "❌ Docker is not running. Please start Docker and try again."
  exit 1
fi

COMPOSE_CMD="docker-compose -f $SCRIPT_DIR/docker-compose.yml"
  
# Start services using Docker Compose
$COMPOSE_CMD up -d

# Wait for services
echo "⏳ Waiting for services to be ready..."

until $COMPOSE_CMD exec -T postgres pg_isready -U bookonnect_user > /dev/null 2>&1; do
  printf "."
  sleep 2
done
echo " ✅ PostgreSQL is ready!"

until curl -s http://localhost:8000/api/v1/heartbeat > /dev/null 2>&1; do
  printf "."
  sleep 2
done
echo " ✅ ChromaDB is ready!"

echo "🎉 All services are up and running!"