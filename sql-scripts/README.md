# SQL Scripts

Operational SQL scripts for the Cleansia database. These are executed via the **Execute SQL Script** GitHub Actions workflow.

## Usage

1. Go to **Actions** → **Execute SQL Script**
2. Select **DEV** or **PRO** environment
3. Enter the script filename (e.g., `check-db-health.sql`)
4. For PRO: type `execute` to confirm

## Naming conventions

- `check-*.sql` — read-only diagnostic queries (safe to run anytime)
- `fix-*.sql` — data fixes wrapped in transactions
- `migrate-*.sql` — schema or data migrations wrapped in transactions

## `seed/` — dev fixture data

18 scripts that populate an empty database with plausible data to develop against: catalog rows
(countries, currencies, languages, services, packages), then users, employees, orders, payroll and
disputes on top. They are **not** run by any workflow and are **not** part of a migration — the
migrator owns the schema and never touches these.

They arrived here in 2026-08 from a `Cleansia.Infra.Scripts` project that contained no C# at all: a
compiled assembly that existed only to carry SQL, referenced by nothing. Deleting the project without
moving these would have lost the only copy.

Run one against a local database with `psql -h localhost -p 5432 -U postgres -d Cleansia -f <file>`.
The catalog scripts come first; the rest assume those ids exist.
