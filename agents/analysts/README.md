# Analyst Living Docs — Business Logic + Diagrams

Owned by the **Analyst panel**. One file per domain (`<domain>.md`), holding the domain's business
rules in prose **+ Mermaid diagrams** (flows, state machines, decision trees) + the living story map.
Updated **in parallel** whenever a story is finalized — a finalized story with a stale doc is not
finalized. See `../process/documentation.md` for structure and diagram conventions, and
`../process/deliberation.md` for how stories are defended before they land here.

Domains: `orders-booking`, `payments-fiscal`, `pay-payroll`, `employees`, `identity-auth`,
`catalog-config`, `loyalty-growth`, `disputes-addresses`, `notifications`.

Each `<domain>.md` cross-links the matching `../architecture/decisions/*.md` (architect view) and the
`docs/architecture/*` (dev/published view) for the same area.
