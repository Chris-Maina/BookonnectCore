set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
STARTUP_PROJECT="$CORE_DIR/../BookonnectAPI/BookonnectAPI"

echo "🔄 Applying database migrations..."

# Ensure services are running
"$CORE_DIR/scripts/infrastructure/start-services.sh"

# Applying migrations
dotnet ef database update --project "$CORE_DIR" \
    --startup-project "$STARTUP_PROJECT" --context BookonnectCore.Models.DatabaseContext \
    -- --environment Development
echo "✅ Database migrations completed!"