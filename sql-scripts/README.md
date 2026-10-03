# SQL Scripts

Operational SQL scripts for the Cleansia database. These are executed via the **Execute SQL Script** GitHub Actions workflow.

## Usage

1. Go to **Actions** → **Execute SQL Script**
2. Select **DEV** or **PRO** environment
3. Enter the script filename (e.g., `check-db-health.sql`)
4. For PRO: type `execute` to confirm

## Naming conventions

The prefix says what a script does. Scripts outside these prefixes keep their names, because the
workflow, `CleansiaStartupBase`, the tests and the docs call them by name.

- `check-*.sql` — read-only diagnostic queries (safe to run anytime)
- `fix-*.sql` — data fixes wrapped in transactions
- `backfill-*.sql` — one-off, idempotent fills for a column a schema change added
- `insert_*.sql` — development fixtures: `insert_seed_data.sql` and `insert_local_dev_admin.sql` here,
  and most of `seed/` (below)
- `prod-bootstrap.sql` — the reference data every database needs (below)
- `set-admin-role.sql` — a hand tool: set the e-mail and the role at its top, then run it
- `reset-database.sql` — drops every table, type and extension so the migrations can be reapplied;
  for a local database, run with `psql`

## The seed: `prod-bootstrap.sql`, then `insert_seed_data.sql`

- **`prod-bootstrap.sql`** — the reference data a freshly migrated database needs before anyone can
  register or book: the operating company, languages, countries, the Czech service cities, CZK, the
  Czech market with its operator, its invoice configuration and cleaner document requirements, the
  e-mail template copy, the loyalty tiers and the Czech size ladder. No users, orders, promo codes,
  catalogue, prices, Plus plans or company record: in production those are typed into the admin
  console. One transaction, idempotent. **Production runs it once, by the owner, as runbook step P7
  (`deploy/AZURE-DEV-RUNBOOK.md`); an agent never runs anything against PRO.**
- **`insert_seed_data.sql`** — the DEV fixtures on top of it: the sample catalogue with prices and pay
  rates, Plus plans, promo codes, the company record and the markets DEV configures but does not
  operate. It needs the bootstrap first and never runs on its own; `execute-sql.yml` refuses it
  against PRO.

A Development boot runs both, then `insert_local_dev_admin.sql`
(`CleansiaStartupBase.DevelopmentSeedScripts`). To re-seed the shared DEV database, run
`prod-bootstrap.sql` and then `insert_seed_data.sql` through **Execute SQL Script** with **DEV**,
then `insert_local_dev_admin.sql` for the published local administrator (owner ruling 2026-09-30;
the workflow refuses it for PRO).

## DEV data fixes

`insert_seed_data.sql` never updates a row that already exists, so re-seeding does not move a DEV
database seeded before the seed changed. Run these through **Execute SQL Script** with **DEV**.

- **`fix-company-contact-placeholders.sql`** — the company record's contact values (owner rulings
  2026-10-03): an e-mail of `info@cleansia.cz` becomes `support@cleansia.cz`, and a phone of
  `+420 123 456 789` becomes the literal placeholder `<company_phone_number>`. Each value moves only
  on a row that still holds the old seeded one, so a value an administrator has typed in is kept. One
  transaction, idempotent. Written for DEV: production's company record is typed into the admin
  console, and `execute-sql.yml` refuses it against PRO.
- **`fix-plus-trial-14-days.sql`** — the two seeded Cleansia Plus plans' free trial (owner ruling
  2026-09-30): a `TrialPeriodDays` of 0 becomes 14. It touches only the two seeded plans, by their seed
  ids, and only while their trial is 0, so a trial an administrator has set is kept. One transaction,
  idempotent. Written for DEV: production's plans are typed into the admin console.
- **`fix-deactivate-local-dev-admin.sql`** — retires `admin@cleansia.local`, the administrator whose
  password is published in the root README: the account is deactivated, so sign-in and refresh refuse
  it. It refuses to run while that account is the only active Administrator, so create a named one
  first with `set-admin-role.sql`. One transaction, idempotent.

## `seed/` — dev fixture data

> ### ⚠️ This is NOT the seed the DEV database uses.
>
> The live one is **`prod-bootstrap.sql` followed by `insert_seed_data.sql`** in the directory above,
> executed by the host at startup in Development. Every seeded-data test reads those files. Nothing in
> `seed/` is run by any workflow, any host or any test.

Seven scripts that populate an empty database with plausible catalogue and translation data. They
arrived here in 2026-08 from a `Cleansia.Infra.Scripts` project that contained no C# at all — a
compiled assembly that existed only to carry SQL, referenced by nothing. There were twenty; an audit
on 2026-09-10 found that twelve could not run against the current schema, and all twelve were deleted
on 2026-09-12 (the eight no accepted ADR cited first, then, on the owner's ruling, the four that ADRs
cite as evidence — `insert_users_employees.sql`, `insert_employee_payroll.sql`,
`insert_employee_invoices.sql`, `insert_disputes.sql`; ADR-0041 and ADR-0046 still read true, and
every file is in git history).

**The seven that still work** are the catalogue and translation fixtures: `insert_countries.sql`,
`insert_languages.sql`, `insert_addresses.sql`, `insert_property_size_presets.sql`, the two
`insert_email_template_translations_*.sql`, and `update_existing_users_language.sql`
(`insert_email_translations.sql` went with the `EmailTranslations` table it seeded — the renderer
reads `EmailTemplateTranslations`).

> **`insert_languages.sql` is the prerequisite for every other script here.** It defines
> `generate_ulid()`, which all of them call. It also populates `Languages`, and the startup seeder
> skips its whole run when that table is non-empty — so running this file by hand against an empty
> DEV database silences the seeder that would otherwise have filled it properly.

Run one against a local database with `psql -h localhost -p 5432 -U postgres -d Cleansia -f <file>`.
