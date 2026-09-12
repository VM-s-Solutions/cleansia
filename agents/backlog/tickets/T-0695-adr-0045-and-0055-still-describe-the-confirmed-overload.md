---
id: T-0695
title: ADR-0045 and ADR-0055 still describe the Confirmed overload that ADR-0057 removed
status: todo
size: S
owner: —
created: 2026-09-08
updated: 2026-09-08
depends_on: [T-0691]
blocks: []
stories: []
adrs: [ADR-0057, ADR-0045, ADR-0055, ADR-0037]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

ADR-0057 (2026-09-08) made `Confirmed` mean **only** that a cleaner took the job. Two earlier accepted
ADRs still tell the reader it means two things, and one states a card-path behaviour that is now false:

| Where | What it still says |
|---|---|
| `docs/decisions/adr-0055.md:24` | *"`Confirmed` is the most overloaded status in this domain — money settled OR…"* |
| `docs/decisions/adr-0045.md:1287` | *"`Order.CurrentStatus == Confirmed` means money settled **or** a cleaner took it"* |
| `docs/decisions/adr-0045.md:352` | *"`New → Confirmed` on the card webhook, as today"* — the webhook no longer writes it |
| `docs/decisions/adr-0045.md:891` | same claim, and cites **ADR-0037 D5** for the one-source-of-truth rule; the status-term clause is **D1** |

An accepted ADR is immutable, so these are not rewritten. `agents/architecture/decisions/README.md`
sets the precedent: a **dated correction banner**, with the individual phrases left standing.

The D5/D1 miscitation is a separate, factual slip — the same one made and corrected during T-0691 —
and is a candidate for the same banner rather than an inline edit.

## Acceptance criteria

- [ ] **AC1** — ADR-0045 and ADR-0055 each carry a dated banner naming ADR-0057 and stating which of
      their claims it supersedes. No original phrase is rewritten.
- [ ] **AC2** — The banner distinguishes the two kinds of staleness: the *status meaning* (superseded by
      ADR-0057) and the *D5-should-be-D1 citation* (a factual repair).
- [ ] **AC3** — `node agents/tools/check-catalog-claims.mjs` stays green.

## Out of scope

- Rewriting either ADR's body. Accepted ADRs are immutable.
- Any code change. Both ADRs' shipped behaviour is unaffected — only their prose is stale.

## Implementation notes

Follow `agents/architecture/decisions/README.md` on correction banners. ADR-0053 is the precedent for a
partial supersede; ADR-0057 already follows it.

## Status log

- 2026-09-08 — filed from the T-0691 out-of-scope list; all four line references re-verified against the
  tree the same day.
