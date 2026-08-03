# Ditak WorkTime

Single-tenant employee attendance and working hours monitoring server for TrueNAS SCALE & Docker.

- **Core + Web API + Worker:** .NET 8
- **Employee Web UI:** React (EN / HY / RU, day-night theme)
- **Admin:** PHP Laravel-style app calling the same Web API
- **Database:** PostgreSQL on a persistent volume (informational data never wiped by updates)

## Quick start (Docker)

```bash
cp .env.example .env
# edit secrets in .env
docker compose up -d --build
```

Open:

- Web: http://localhost/
- Admin: http://localhost/admin/
- API health: http://localhost/health
- API: http://localhost/api/

Default seed admin (first empty DB only): values from `.env` (`SEED_ADMIN_*`).

## Architecture

```text
Browser → Nginx → Web | Admin | API
API / Worker → Core domain → PostgreSQL (db_data volume)
```

PHP Admin and React Web never write attendance calculations directly to the database.

## TrueNAS SCALE & Docker Guides

- **TrueNAS SCALE Setup:** [docs/TRUENAS_GUIDE.md](docs/TRUENAS_GUIDE.md)
- **Safe Update Policy:** [docs/UPDATE_POLICY.md](docs/UPDATE_POLICY.md)

```bash
sh deploy/scripts/backup-db.sh
sh deploy/scripts/update.sh
```

## Smoke test (v1)

1. `cp .env.example .env` and set passwords (or use provided local `.env`)
2. Prefer building from a local (non-OneDrive) copy if Docker reports `invalid file request` for cloud placeholders:
   `robocopy . C:\DitakWorkTimeBuild /E /XD node_modules vendor .git`
3. `docker compose up -d --build` (from that folder)
4. Wait until `docker compose ps` shows healthy services
5. Open Web: http://localhost/ — login `admin@company.local` / `ChangeMe123!`
6. Check-in / check-out, open Presence and Reports
7. Open Admin: http://localhost/admin/login — same credentials
8. API health: http://localhost/health

## Local development notes

Host machines without `dotnet` / `php` should use Docker builds. Node can develop `src/Web` against a running API container.
