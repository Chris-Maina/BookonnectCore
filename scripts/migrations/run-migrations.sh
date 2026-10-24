set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"

echo "🔄 Applying database migrations..."

# Ensure services are running
"$CORE_DIR/scripts/infrastructure/start-services.sh"

# Applying migrations
dotnet ef database update ---project "$CORE_DIR"
echo "✅ Database migrations completed!"