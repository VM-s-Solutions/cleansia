---
id: T-0294
title: Remove now-unused `private readonly router` + `Router` import in confirm-email.component.ts (T-0280 residue)
status: done
size: XS
owner: frontend
created: 2026-06-23
updated: 2026-06-23
depends_on: [T-0280]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 11
---

> **No-decision note (panel skipped):** dead-code removal of an injected dependency left orphaned by a
> comment deletion. Zero behavior change, no decision, no new pattern.

## Context

Surfaced by **T-0280** (FE comment-noise sweep). When T-0280 removed a commented-out `router.navigate`
call from `confirm-email.component.ts`, the only consumer of the component's injected
`private readonly router: Router` field disappeared — so the field **and** its `Router` import are now
**unused**. Angular/TS lint does **not** flag unused *private* class members, so neither T-0280's
mechanical checks nor CI catches it; it is genuine dead code that only a human (or this ticket) removes.
T-0280 deliberately did **not** fix it — its scope was comments-only ("if a comment hides a real code
smell, file a separate ticket"), and removing an injected field + import is a code change. This is that
ticket.

## Acceptance criteria

- [x] **AC1 — Unused field + import removed.** The `private readonly router: Router` injection in
  `confirm-email.component.ts` is removed, along with the now-unused `import { Router } ...`, **only if**
  the dev confirms there is **no** remaining `this.router.*` reference anywhere in the component (template
  included). If any reference remains, the dev **stops** and notes it — the field stays.
- [x] **AC2 — Zero behavior change.** The component compiles and behaves identically (the removed field
  had no live consumer post-T-0280). No other field, import, or statement changes.
- [x] **AC3 — Mechanical checks green.** `nx build` for the affected app + `nx affected -t test` (the
  confirm-email component's suite) pass; `check-consistency.mjs` no new violation.

## Out of scope
- **Any other unused-injection cleanup** elsewhere — this is the single field T-0280's comment removal
  orphaned. If the dev spots sibling dead injections, **file a follow-up**, don't widen this XS.
- **No comment changes** (that was T-0280) and **no behavior change** of any kind.

## Implementation notes
Single-file edit in `confirm-email.component.ts` (the customer-app confirm-email feature). Grep the
component + its template for `this.router` / `router.` before deleting to confirm zero live references
(the boot/route still works because the navigation was already commented out before T-0280). `[frontend]`,
one dev + one reviewer; do not over-fan-out (mechanical XS).

**Routing:** `[frontend]`. `reviewer` confirms the field/import are genuinely unused + behavior-preserving.
`qa` = affected-app build clean + the component suite unchanged. No `security`, no `optimizer`.

## Status log
- 2026-06-23 — draft → ready (created by pm). **Latent smell surfaced by T-0280** (FE comment sweep): the
  removed commented-out `router.navigate` was the last consumer of the injected `router` field → field +
  `Router` import now dead, and lint does not flag unused private members. Dedup-checked: not in
  INDEX/`audits/`. DoR met: AC observable; sized **XS** (one field + one import in one file);
  `depends_on: [T-0280]` (`done` — this cleans its residue); `layers: [frontend]`;
  `security_touching: false`; `manual_steps: []`. One-line no-decision note, no panel.
- 2026-06-23 — ready → **done** (commit `093ed944` on `feature/wave8-pre-ios-cleanup`, pushed; landed in
  the same FE commit as T-0290's frontend half). **AC1/AC2/AC3 SATISFIED.** Removed the now-unused
  `private readonly router: Router` field **and** its `Router` import from `confirm-email.component.ts`
  (the last `this.router.*` consumer was the commented-out `router.navigate` T-0280 deleted; dev confirmed
  zero remaining references, template included). Zero behavior change — no other field/import/statement
  touched. **Run evidence (orchestrator combined-tree re-run): `nx build cleansia-app` (customer, SSR)
  clean; the confirm-email component suite unchanged + green; `check-consistency.mjs` no new violation.**
  Reviewer + qa confirmed genuinely-unused + behavior-preserving. No security/optimizer (mechanical XS).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
