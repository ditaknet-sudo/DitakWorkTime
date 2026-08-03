#!/usr/bin/env sh
# Safe product update for TrueNAS / Docker Compose.
# Updates application images and applies additive schema migrations.
# NEVER deletes or recreates the db_data volume (customer informational data).

set -eu

ROOT_DIR="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)"
cd "$ROOT_DIR"

if [ ! -f .env ]; then
  echo "ERROR: .env missing. Copy .env.example to .env and configure secrets."
  exit 1
fi

echo "==> Creating database backup before update..."
sh deploy/scripts/backup-db.sh

echo "==> Pulling / rebuilding application images (data volume untouched)..."
docker compose build api worker web admin
docker compose up -d db
docker compose up -d api worker web admin proxy

echo "==> Waiting for API health..."
i=0
until docker compose exec -T api wget -qO- http://127.0.0.1:8080/health >/dev/null 2>&1; do
  i=$((i + 1))
  if [ "$i" -gt 60 ]; then
    echo "ERROR: API did not become healthy. Data volume was NOT modified by this script."
    exit 1
  fi
  sleep 2
done

echo "==> Update complete. Informational database volume preserved."
