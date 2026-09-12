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

## `seed/` — dev fixture data, and most of it no longer runs

> ### ⚠️ This is NOT the seed the DEV database uses.
>
> The live one is **`sql-scripts/insert_seed_data.sql`** in the directory above, executed by the host
> at startup in Development. Every seeded-data test reads that file and only that file. Nothing in
> `seed/` is run by any workflow, any host or any test.

20 scripts that were written to populate an empty database with plausible data: catalog rows
(countries, currencies, languages, services, packages), then users, employees, orders, payroll and
disputes on top. They arrived here in 2026-08 from a `Cleansia.Infra.Scripts` project that contained
no C# at all — a compiled assembly that existed only to carry SQL, referenced by nothing.

**Twelve of the twenty cannot run against the current schema**, audited 2026-09-10. They were written
before the multicurrency work and still name columns that no longer exist:

| Script | Why it cannot run |
|---|---|
| `insert_currencies.sql` | `Currencies.ExchangeRate` is deleted. Also seeds **twelve active currencies** — hand-run, it re-creates the exact mispricing the currency work closed |
| `insert_services.sql` | `Services.BasePrice` / `PerRoomPrice` are deleted; prices are rows in `ServicePrices` now |
| `insert_packages.sql` | `Packages.Price` is deleted; see `PackagePrices` |
| `insert_users_employees.sql` | `Employees.VatNumber` is deleted — a cleaner is IČO and never a VAT payer |
| `insert_orders.sql` | inserts `PackageServices`, not orders, and names ten columns while supplying two → T-0696 |
| `insert_employee_payroll.sql`, `insert_employee_invoices.sql`, `insert_order_employee_pay.sql`, `insert_employee_pay_config.sql`, `insert_pay_periods.sql` | four are byte-duplicated inside the first; all depend on orders/employees/pay periods that nothing runnable creates |
| `insert_disputes.sql`, `fix_employee_addresses.sql` | dead by dependency on the two above |

They are kept for now because several are cited as evidence in accepted ADRs (ADR-0041, ADR-0046),
and an accepted ADR's citations are not rewritten. **Do not run them, and do not copy from them** —
they describe a schema the platform no longer has.

**The eight that still work** are the catalogue and translation fixtures: `insert_countries.sql`,
`insert_languages.sql`, `insert_addresses.sql`, `insert_property_size_presets.sql`,
`insert_email_translations.sql`, the two `insert_email_template_translations_*.sql`, and
`update_existing_users_language.sql`.

> **`insert_languages.sql` is the prerequisite for every other script here.** It defines
> `generate_ulid()`, which all of them call. It also populates `Languages`, and the startup seeder
> skips its whole run when that table is non-empty — so running this file by hand against an empty
> DEV database silences the seeder that would otherwise have filled it properly.

Run one against a local database with `psql -h localhost -p 5432 -U postgres -d Cleansia -f <file>`.
