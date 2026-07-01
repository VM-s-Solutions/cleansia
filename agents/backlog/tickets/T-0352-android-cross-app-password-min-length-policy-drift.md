---
id: T-0352
title: "Cross-app password min-length policy drift — customer-app enforces >=12, partner-app >=8; pick one canonical policy"
status: proposed
size: S
owner: android
created: 2026-06-30
updated: 2026-06-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: HARDENING-1 T-0333 android review — cross-app inconsistency (NON-blocking)
---

> **NON-blocking consistency follow-up surfaced by the T-0333 android review (HARDENING-1).** Not a defect in
> either app on its own — each app's `register_pw_min_length` string matches its own threshold — but the two
> Android apps enforce **different** password minimum lengths, and neither is provably aligned with the backend.

## The gap
- The **customer-app** register flow enforces a password minimum of **≥ 12** characters; the **partner-app**
  register/validation flow enforces **≥ 8**. Each app's own `register_pw_min_length` user-facing string matches
  its own threshold (so neither app is internally inconsistent), but a user setting up both apps sees two
  different rules, and the platform has no single declared password-length policy.
- The canonical policy should ideally be aligned with the **backend** `BaseAuthValidator` (the server is the
  real authority — a client that under-enforces just gets a server-side rejection; a client that over-enforces
  blocks a password the server would accept). Pick **one** policy and make both apps (and the surfaced string)
  agree with it.

## Acceptance criteria
- [ ] **AC1 (one policy)** — A single canonical password-minimum-length is chosen (ideally the backend
  `BaseAuthValidator` value); the rationale is recorded. Both Android apps enforce the same minimum.
- [ ] **AC2 (strings match)** — Each app's `register_pw_min_length` (and any other length-bearing validation
  string) reflects the chosen minimum across all 5 locales (en/cs/sk/uk/ru); no app shows a number that
  disagrees with what it enforces.
- [ ] **AC3 (no regression)** — The register/validation predicates change only the minimum-length constant;
  no other rule changes; both apps build and their tests stay green.

## Out of scope
- The **backend** policy itself — if the canonical value is taken as-is from `BaseAuthValidator`, this is
  client alignment only. If the team decides the canonical number differs from the current backend value, that
  backend change is a **separate** ticket (this one aligns the two Android clients; the layers note flags the
  possible backend/iOS reach).
- The **iOS** apps — confirm/align them only if the team makes this the cross-platform canonical policy (then
  add an iOS slice or a sibling ticket; iOS is noted in layers as a possible extension, not in scope by default).

## Implementation notes
- Decide the canonical minimum first (recommend: read `BaseAuthValidator`'s password-length rule and adopt it
  as the single source of truth; clients should not be **stricter** than the server unless there's a recorded
  reason). This is a small decision — a one-line "no new architecture" note suffices, but the **number choice**
  should be explicit and recorded so it doesn't drift again.
- Touch only the length constant + the matching `register_pw_min_length` string ×5 per app.
- **Possible cross-platform reach:** layers `[android]` by default; if the team elevates this to the canonical
  platform policy, extend to `ios` (and `backend` if the server number is the one that changes).

## Status log
- 2026-06-30 — filed from the HARDENING-1 T-0333 android review. customer-app ≥12 vs partner-app ≥8; each
  app's string matches its own threshold, but the platform has no single declared policy and neither is
  provably aligned with the backend `BaseAuthValidator`. NON-blocking, low priority. `proposed`, not dispatched.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
