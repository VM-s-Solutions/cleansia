# Architect Living Decision Docs

> ## ⚠️ `ef-migration` is no longer owner-only — 2026-08-15
>
> Several docs in this folder describe a schema change as costing *"an owner-only `ef-migration`"* and
> size the work around waiting for the owner. **That is retired.** Pre-prod, an agent regenerates the
> single `Initial` itself and proves it with the integration suite — the commands are in `CLAUDE.md`
> § *Manual steps*.
>
> **What is still the owner's is the DEV database drop** a regenerated `Initial` forces, because the
> migration id changes. So the sequencing arguments in those docs still hold; only the *actor* on the
> regeneration half changed, and every one of them is cheaper than it says.
>
> The individual phrases are not rewritten — that would edit reasoning nobody has re-decided — so this
> banner is the correction and it applies to all of them at once.
>
> **Retires when:** no file under this folder describes a migration as owner-only.

Owned by the **Architect panel**. One file per topic (`<topic>.md`), holding the evolving design
notes + trade-off space + current shape for that area. These are the **living companion** to the
**immutable ADRs** in `docs/decisions/` — the ADR is the dated, immutable record of a decision (with
its defended alternatives); this doc is the always-current explanation of where the design stands now.

Updated **in parallel** when a decision is finalized through the defense panel
(`../../process/deliberation.md`). Cross-links the matching `../../analysts/<domain>.md` (business
view) and `docs/architecture/*` (dev/published view).

Expected early topics (from the audit's 5 pre-/Wave-1 ADRs): `authz`, `outbox`, `ratelimit`, `refund`,
`integration`, plus `soft-delete`, `multi-tenancy`, `fiscal-modes`, `pay-calculation`.
