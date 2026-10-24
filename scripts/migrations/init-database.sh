set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"

echo "🔄 Initializing database..."

# Ensure services are running
"$CORE_DIR/scripts/infrastructure/start-services.sh"

# Create initial migration if none exists
if [ ! -d "$CORE_DIR/Migrations" ]; then
    echo "📝 Creating initial migration..."
    dotnet ef migrations add InitialCreate --project "$CORE_DIR"
fi

# Apply migrations
echo "⚡ Applying migrations..."
dotnet ef database update --project "$CORE_DIR"

echo "✅ Database initialization completed!"