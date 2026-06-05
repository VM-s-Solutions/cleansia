# Architect Living Decision Docs

Owned by the **Architect panel**. One file per topic (`<topic>.md`), holding the evolving design
notes + trade-off space + current shape for that area. These are the **living companion** to the
**immutable ADRs** in `../../backlog/adr/` — the ADR is the dated, immutable record of a decision (with
its defended alternatives); this doc is the always-current explanation of where the design stands now.

Updated **in parallel** when a decision is finalized through the defense panel
(`../../process/deliberation.md`). Cross-links the matching `../../analysts/<domain>.md` (business
view) and `docs/architecture/*` (dev/published view).

Expected early topics (from the audit's 5 pre-/Wave-1 ADRs): `authz`, `outbox`, `ratelimit`, `refund`,
`integration`, plus `soft-delete`, `multi-tenancy`, `fiscal-modes`, `pay-calculation`.
