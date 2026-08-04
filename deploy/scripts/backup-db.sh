#!/usr/bin/env sh
# Backup PostgreSQL informational data. Does not modify the live volume.

set -eu

ROOT_DIR="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)"
cd "$ROOT_DIR"

BACKUP_DIR="${ROOT_DIR}/backups"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
OUT="${BACKUP_DIR}/attendance_${STAMP}.sql.gz"

if [ -z "$(docker compose ps -q --status running db)" ]; then
  echo "ERROR: db service is not running."
  exit 1
fi

DB_USER="$(docker compose exec -T db printenv POSTGRES_USER)"
DB_NAME="$(docker compose exec -T db printenv POSTGRES_DB)"

docker compose exec -T db pg_dump -U "$DB_USER" -d "$DB_NAME" | gzip > "$OUT"
echo "Backup written: $OUT"
