#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

set -a
source "$SCRIPT_DIR/.env"
set +a

echo "Stopping Bookonnect services..."

if ! docker info > /dev/null 2>&1; then
  echo "Docker is not running. Please start Docker and try again." >&2
  exit 1
fi

docker-compose -f "$SCRIPT_DIR/docker-compose.yml" down

echo "Bookonnect services stopped. Named volumes have been preserved."