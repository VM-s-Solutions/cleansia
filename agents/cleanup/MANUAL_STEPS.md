# Manual steps — owner-only

Steps this cleanup needs that Claude does not run (see `CLAUDE.md` § *Manual Steps*). One row per
step, cleared when done.

## Open

### MS-1 — Regenerate the `Initial` migration for the order seat ordinal

**Raised by:** P2 / `CL-015` (G-15, the take race). **Blocks:** nothing shipped, but the fix is inert
until this lands.

`OrderEmployee` gained a `SeatOrdinal` column and a unique index
`IX_OrderEmployees_OrderId_SeatOrdinal`. Both exist in the EF **model** only — the committed migration
does not carry them.

Until this runs:

- The unique index does not exist in any database, so **the race is still open in DEV**. The C# is
  correct and the model is correct; nothing at the database is enforcing it yet.
- `TakeOrderConcurrentSeatRaceTests` is `[Fact(Skip = …)]`, naming this step as its unblocker. The
  integration fixture applies the committed migration, so it would otherwise fail on a missing column
  and prove nothing.

**Do:**

```bash
cd src
# Pre-prod: REGENERATE the single Initial migration — do not stack a second one.
# The migration id changes, so DEV needs a database drop.
dotnet ef migrations remove --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
dotnet ef migrations add Initial --project Cleansia.Infra.Database --startup-project Cleansia.Web.Partner
```

**Then:** delete the `Skip` argument on `TakeOrderConcurrentSeatRaceTests` (one line) and run
`dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj`. It should show exactly one
winner and one `DbUpdateException`.

**Pre-existing rows:** none to worry about pre-prod. If DEV is not dropped, a unique index over
`(OrderId, SeatOrdinal)` will fail to build on any order that already carries two or more assignments,
since every existing row defaults to `SeatOrdinal = 0`. Dropping DEV is the cheaper path and is already
the standing ruling for a regenerated `Initial`.

## Cleared

*(none yet)*
