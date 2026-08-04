# Ditak WorkTime

Self-hosted employee attendance and working hours monitoring server for TrueNAS SCALE & Docker.

- **Core + Web API + Worker:** .NET 8
- **Employee Web UI:** React (EN / HY / RU, day-night theme)
- **Admin:** PHP Laravel-style app calling the same Web API
- **Database:** PostgreSQL on a persistent volume (informational data never wiped by updates)

## Roles & Access

| Role | Access |
|---|---|
| **Admin** | Full management: employees, sites, users, manual corrections, reports |
| **Director** | Live presence board + all employee reports (read-only) |
| **Accountant** | All employee monthly reports + Excel/PDF export (read-only) |
| **Employee** | Personal check-in/out, today status, personal report history |

## Quick start (Docker)

```bash
cp .env.example .env
# Set POSTGRES_PASSWORD, JWT_SECRET, SEED_ADMIN_PASSWORD and APP_KEY.
# Keep HOST_BIND_IP=127.0.0.1 for local-only access.
docker compose up -d --build
```

Open (default port `8888`, configurable via `HOST_PORT` in `.env`):

- Web: http://localhost:8888/
- Admin: http://localhost:8888/admin/
- API health: http://localhost:8888/health
- Swagger API docs: http://localhost:8888/swagger/

> [!TIP]
> TrueNAS SCALE reserves port 80 for its own management UI. Use `HOST_PORT=8888` (or any free port) in your `.env`.
> Set `HOST_BIND_IP=0.0.0.0` only when the service must be reachable from your LAN.

Default seed admin (first empty DB only): values from `.env` (`SEED_ADMIN_*`).

## Architecture

```text
Browser → Nginx (HOST_PORT → 80) → Web | Admin | API
API / Worker → Core domain → PostgreSQL (db_data volume)
```

PHP Admin and React Web never write attendance calculations directly to the database.

## Smart Features

- **Auto Check-Out**: Worker automatically closes open shifts at midnight
- **Presence Board**: Real-time live presence with network hints (15-min window)
- **Reports Export**: Excel (xlsx) and PDF per employee, per month

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
4. Wait until `docker compose ps` shows all services healthy
5. Open Web: http://localhost:8888/ — use `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` from `.env`
6. Check-in / check-out, open Presence and Reports
7. Open Admin: http://localhost:8888/admin/login — same credentials
8. API health: http://localhost:8888/health

## Local development notes

Host machines without `dotnet` / `php` should use Docker builds. Node can develop `src/Web` against a running API container.
