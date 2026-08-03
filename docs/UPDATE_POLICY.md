# Update Policy — Schema vs Informational Data

## Rule

Application updates may change **code** and **database structure (schema/types)**.
They must **never replace, reset, or overwrite customer informational data**
without an explicit, separate backup-restore operation performed by an administrator.

## What updates

| Layer | Examples | On product update |
|-------|----------|-------------------|
| Application images | API, Worker, Web, Admin | Rebuilt / replaced |
| Schema | New tables, additive columns, indexes | Applied via migrations |
| Meta / version | Product version, migration history | Updated |

## What never updates via product update

| Layer | Examples | On product update |
|-------|----------|-------------------|
| Informational data | Employees, attendance events, summaries, users | Untouched |
| Docker volume `attendance_db_data` | PostgreSQL files | Must not be deleted |

## Allowed migrations

- `CREATE TABLE`
- `ADD COLUMN` (nullable or with safe default)
- New indexes
- Forward-only migration versions

## Forbidden by default in product updates

- `docker volume rm` / recreate of `db_data`
- `DROP DATABASE` / truncate of business tables
- Seed scripts that overwrite existing production rows
- Destructive `DROP COLUMN` without a major-version + explicit approval path

## Safe update sequence

1. Run `deploy/scripts/backup-db.sh`
2. Run `deploy/scripts/update.sh` (build + migrate + restart)
3. Verify `/health`
4. On failure: roll back application images; restore from backup only if data corruption is confirmed

## First install only

Seed admin user and company row run only when the informational database is empty.
