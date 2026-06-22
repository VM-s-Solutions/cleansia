---
id: T-0280
title: Strip comment noise violating conventions "default is no comment" (FE auth services + audit pockets)
status: ready
size: S
owner: —
created: 2026-06-22
updated: 2026-06-22
depends_on: [T-0272]
blocks: []
stories: []
adrs: []
layers: [frontend, backend]
security_touching: false
manual_steps: []
sprint: 9
---

> **No-decision note (panel skipped):** comment-only cleanup against the ratified `conventions.md`
> "default is no comment" rule. Zero code/behavior change — comments deleted, nothing else.

## Context

Owner P2. Strip comment noise that violates `conventions.md` ("the default is no comment; a comment
earns its place by explaining *why*, not restating *what*"). Concentrated in the **FE auth services**
(`{customer,partner,admin}-auth.service.ts`), where the `trustedDeviceToken` explainer appears **3×** —
those explainers describe a field that **disappears from the web clients when T-0272 lands**, so this
ticket runs **after** T-0272's regen so the cleanup matches the new shape (no comment about a field that
no longer exists). Plus any other comment pockets the audit/dev finds (e.g. restate-the-obvious
header comments — the audit noted order-wizard comment noise was already folded into the prior F10 work;
this sweep catches the residue).

## Acceptance criteria

- [ ] **AC1 — trustedDeviceToken explainers gone.** The 3 `trustedDeviceToken` explainer comments in the
  FE auth services are removed (the field they explain is gone from the web clients post-T-0272). The
  services compile and their tests pass unchanged.
- [ ] **AC2 — "What"-comment noise stripped.** Comments that merely restate the code (the
  `// POST /api/... — matches the backend route` / `// Body shape mirrors ...Command exactly` class of
  noise, and equivalents the dev finds in the touched auth/marketing areas) are removed. Comments that
  explain a non-obvious **why** (e.g. the cookie-vs-body trusted-device security rationale, the
  "fan-out runs unstoppable once enqueued" warning) are **kept**.
- [ ] **AC3 — Zero behavior/code change.** The diff is **comments only** — no statement, signature,
  import, or value changes. Reviewer confirms the AST is unchanged (a `git diff` showing only comment
  lines is the evidence).
- [ ] **AC4 — Mechanical checks green.** Affected app(s) `nx build` + `nx affected -t test` (FE) and, if
  any backend comment pocket is touched, `dotnet build` + the three test projects pass;
  `check-consistency.mjs` no new violation.

## Out of scope
- **No code change of any kind** — comments only. If a comment hides a real code smell, **file a
  separate ticket**; do not fix it here.
- **No deletion of a "why" comment** that documents a non-obvious decision, security rationale, or a
  gotcha (the bar is "restates the what" → delete; "explains the why" → keep).
- **No doc/`docs/**` changes** — this is source comments only.
- **The auth contract change itself** is T-0272 (this only cleans the comments left behind).

## Implementation notes

**Depends on T-0272** (and its owner regen) so the FE auth-service edits land against the post-shrink
client shape — otherwise the cleanup would reference a field the regen is about to remove. **Single dev
per stack** (frontend primary; backend only if a pocket is found), one reviewer. Per quality-gates
"match agent count to risk", this is a narrow deterministic sweep — do not over-fan-out.

**Routing:** `[frontend]` (primary), `[backend]` (only if a backend pocket is confirmed). `reviewer`
confirms comments-only. `qa` = build/test green + the comments-only diff. No `security`, no `optimizer`.

## Status log
- 2026-06-22 — draft → ready (created by pm). Owner P2. **`depends_on: [T-0272]`** — the 3 FE
  `trustedDeviceToken` explainers are removed *after* the field leaves the web clients (else the cleanup
  fights the regen). No-decision (comment-only). `manual_steps: []`. Sized **S**.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
