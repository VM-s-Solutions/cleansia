---
id: T-0292
title: Remove dead `?? 0` on non-nullable extra.price in wizard-summary-step (NG8102)
status: ready
size: XS
owner: —
created: 2026-06-23
updated: 2026-06-23
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 11
---

> **No-decision note (panel skipped):** a one-token template cleanup removing a dead nullish-coalesce
> the Angular compiler already flags (NG8102). Zero behavior change, no decision, no new pattern.

## Context

During the Wave-8 8C E2E run, the customer app dev-server boot logged an **NG8102** diagnostic at
`libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.html:117`:

```
{{ formatPriceFn(extra.price ?? 0) }}
```

`extra.price` is already **non-nullable**, so the `?? 0` right-hand side is dead — NG8102 ("the right
side of the nullish coalescing operation is not reachable / unnecessary"). Tiny frontend hygiene
cleanup; quiets the compiler diagnostic the E2E boot surfaced.

## Acceptance criteria

- [ ] **AC1 — Dead `?? 0` removed.** Line 117 of `wizard-summary-step.component.html` becomes
  `{{ formatPriceFn(extra.price) }}` (the `?? 0` deleted), now that `extra.price` is non-nullable. If
  the developer finds `extra.price` is in fact nullable (so NG8102 would NOT fire), they **stop** and
  note it — but the boot diagnostic indicates it is non-nullable, so the expected fix is the deletion.
- [ ] **AC2 — NG8102 gone, no new diagnostic.** Booting / building the customer app no longer logs the
  NG8102 at that line, and introduces no new template diagnostic.
- [ ] **AC3 — Zero behavior change.** The rendered extra price is unchanged (the `?? 0` was never
  reached). `nx build cleansia.app --configuration=production` is clean and the order-wizard Jest suite
  passes unchanged.

## Out of scope
- **Any other `?? `/template cleanup** elsewhere — this is the single NG8102 line the E2E boot flagged.
  If the dev spots sibling dead-coalesces in the same file, **file a follow-up**, don't widen this XS.
- **No change to `formatPriceFn` or the extras model** — only the dead operand is removed.

## Implementation notes
Single-line edit in `wizard-summary-step.component.html`. Confirm `extra.price`'s type is non-nullable
(the NG8102 the boot logged is the authoritative signal) before deleting the operand. Rebuild the
customer app to confirm the diagnostic is gone.

**Routing:** `[frontend]`. `reviewer` confirms one-line, behavior-preserving, NG8102 cleared. `qa` =
customer-app build clean + order-wizard Jest unchanged. No `security`, no `optimizer`.

## Status log
- 2026-06-23 — draft → ready (created by pm). Captured from the Wave-8 8C E2E dev-server boot
  (NG8102 at `wizard-summary-step.component.html:117`). Dedup-checked: not in INDEX/`audits/`. DoR
  met: AC observable; sized **XS** (one token in one template line); `depends_on: []`; `layers:
  [frontend]`; `security_touching: false`; `manual_steps: []`. One-line no-decision note, no panel.
  Follow-up-batch candidate.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
