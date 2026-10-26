set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"

echo "🔄 Initializing database..."

# Create initial migration if none exists
if [ ! -d "$CORE_DIR/Migrations" ]; then
    echo "📝 Creating initial migration..."
    bash "$SCRIPT_DIR/add-migration.sh" InitialCreate
fi

# Apply migrations
echo "⚡ Applying migrations..."
bash "$SCRIPT_DIR/run-migrations.sh"

echo "✅ Database initialization completed!"