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
