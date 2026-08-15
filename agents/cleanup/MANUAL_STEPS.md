# Manual steps — owner-only

Steps this cleanup needs that Claude does not run (see `CLAUDE.md` § *Manual Steps*). One row per
step, cleared when done.

## Open

### MS-2 — Drop the DEV database before the next deploy — **owner, deferred by decision**

> **Owner, 2026-08-14:** *"I'll drop the db and reseed the data after all of the Phases are done."*
> Deferred deliberately — not overlooked. It stays open until the drop happens.


`MS-1` regenerated the single `Initial` migration and `MS-6` regenerated it again, so its id has moved
from `20260811192214` to `20260813085249` to **`20260815094107`**. `MigrationService/Program.cs` runs `MigrateAsync()` on every deploy, and a database
whose `__EFMigrationsHistory` records the **old** id will try to replay the whole create script against
tables that already exist — failing the `migrate-database` job every other deploy job depends on.

**Action:** drop the DEV database, then deploy. Pre-production, so there is no data to preserve; the
seed repopulates it (`sql-scripts/insert_seed_data.sql`).

This obligation was recorded only inside `MS-1`'s **Cleared** row, where a reader looking at *"what do I
owe?"* would not find it. That is what `CL-043` is.

### MS-3 — Rotate the exposed Mapbox token — **owner**

Four environment files and two runbook rows still carry `MANUAL_STEP (rotate-mapbox-token)`; the exposed
token remains recoverable from git history, so rotation is the only thing that retires it. Its original
tracker row is now inside the archived backlog, which is why it is re-filed here.

**Action:** rotate the token at Mapbox, provision the new value as `Mapbox--GeocodingAccessToken` in Key
Vault (`deploy/AZURE-DEV-RUNBOOK.md:281`), then delete the four `MANUAL_STEP` comments.

## Cleared

### MS-6 — Regenerate `Initial` for the G-03 column and the G-18 index — **DONE 2026-08-15**

**Run by Claude, and the rule changed with it.** The owner ruled that regenerating `Initial` is no
longer a manual step: *"Regenerate the migration on your own and also mark this step as non MS. It can
be done by you as well."*

`20260813085249_Initial` → **`20260815094107_Initial`**, carrying `RefreshToken.RememberMe` (G-03) and
`IX_Orders_RecurringTemplateId_CleaningDateTime` (G-18), with the P2 seat index and all six
`NULLS NOT DISTINCT` options intact. Verified by the integration suite — **197 tests against real
Postgres**, which is the only thing that proves the model and the schema agree.

The commands, and the trap that the startup project must be a web host rather than
`Cleansia.MigrationService`, are now in `CLAUDE.md` § *Manual steps*.


### MS-4 — Payroll currency: DTO + regeneration — **DONE 2026-08-14**

Backend by Claude on the owner's instruction (*"MS-4 you can add on your own"*), regeneration by the
owner (`d10a2cc2`). `PeriodPaySummaryDto.CurrencyCode` is sourced from the **invoice** when the period
has one, so "My Pay" and the cleaner's payout document read the same row and cannot diverge; only an
un-invoiced period resolves, through the same service the partner dashboard uses.

It was one DTO, not the two this step originally named: `OrderEmployeePayDto` is never returned on its
own, and its other parent `EmployeeInvoiceDetailDto` already carried the field.

The partner "My Pay" screen no longer hardcodes `Kč` — six template symbols and the table formatter all
read the server's value, and an absent code renders the amount with no symbol rather than guessing one.


### MS-5 — Regenerate the admin client for the entry-instruction reveal — **DONE 2026-08-14**

Run by the owner (`ac6eebd0`). The admin client carries `AccessInstructionsClient.reveal` and
`OrderItem.hasAccessInstructions`; the reveal control shipped in the same PR, so the interim state where
an admin could not see entry instructions at all lasted only as long as the PR was open.


### MS-1 — Regenerate the `Initial` migration for the order seat ordinal — **DONE 2026-08-13**

Run by Claude on the owner's explicit instruction ("you can regenerate Initial migration on your
own"), which overrides `CLAUDE.md` § *Manual Steps* for this step only. **The standing rule is
unchanged** unless the owner says otherwise.

```
20260811192214_Initial  ->  20260813085249_Initial
```

Regenerated with `dotnet ef migrations remove --force` then `migrations add Initial` — not hand-folded
into the three files, per the 2026-08-09 ruling. The EF CLI was pinned to **10.0.3** to match
`Directory.Packages.props` (an install had bumped the global tool to 10.0.11; it was rolled back).

Carries what the model gained:

```csharp
SeatOrdinal = table.Column<int>(type: "integer", nullable: false)     // :1779

migrationBuilder.CreateIndex(
    name: "IX_OrderEmployees_OrderId_SeatOrdinal",
    table: "OrderEmployees",
    columns: new[] { "OrderId", "SeatOrdinal" },
    unique: true);                                                    // :2984-2988
```

**⚠️ The migration id changed, so DEV needs a database drop before the next deploy.** That is the
standing consequence of regenerating `Initial` rather than stacking, and it is why this is pre-prod-only.

`TakeOrderConcurrentSeatRaceTests` is no longer skipped and **passes against real Postgres**: two
concurrent commits, exactly one winner, one `DbUpdateException`, one surviving assignment at ordinal 0.
