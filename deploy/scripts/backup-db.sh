#!/usr/bin/env sh
# Backup PostgreSQL informational data. Does not modify the live volume.

set -eu

ROOT_DIR="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)"
cd "$ROOT_DIR"

BACKUP_DIR="${ROOT_DIR}/backups"
mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
OUT="${BACKUP_DIR}/attendance_${STAMP}.sql.gz"

if ! docker compose ps db --status running >/dev/null 2>&1; then
  echo "ERROR: db service is not running."
  exit 1
fi

# shellcheck disable=SC1091
. ./.env

docker compose exec -T db pg_dump -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" | gzip > "$OUT"
echo "Backup written: $OUT"
