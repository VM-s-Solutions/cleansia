# Manual steps — owner-only

Steps this cleanup needs that Claude does not run (see `CLAUDE.md` § *Manual Steps*). One row per
step, cleared when done.

## Open

*(none)*

## Cleared

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
