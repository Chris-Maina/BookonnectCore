set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CORE_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
STARTUP_PROJECT="$CORE_DIR/../BookonnectAPI/BookonnectAPI"

MIGRATION_NAME=${1:-"Migration_$(date +%Y%m%d_%H%M%S)"}

echo "📝 Creating new migration: $MIGRATION_NAME"
dotnet ef migrations add "$MIGRATION_NAME" --project "$CORE_DIR" \
    --startup-project "$STARTUP_PROJECT" --context BookonnectCore.Models.DatabaseContext \
    -- --environment Development
echo "✅ Migration '$MIGRATION_NAME' created!"