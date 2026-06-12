---
id: T-0235
title: Runtime 429 flood-harness test for the T-0194 rate-limit coverage sweep
status: draft
size: S
owner: —
created: 2026-06-12
updated: 2026-06-12
depends_on: [T-0194]
blocks: []
stories: []
adrs: [0003]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 4
source: T-0194 AC6 deviation (recorded 2026-06-12; accepted at Wave-3 close — Wave-4 test slice)
---

## Context
T-0194 shipped the S5 rate-limit coverage closure (38 + remediation attribute additions, merged
`66cc823d`) with a **recorded AC6 deviation**: the runtime 429 flood test against the T-0115 host
harness (`Cleansia.HostTests`) was not delivered — the structural reflection guard
(`RateLimitCoverageGuardTests`) was accepted as interim evidence, and the runtime proof was deferred
to the Wave-4 test slice. This ticket is that deferred slice. **No-decision note:** test-only, no
production behavior or contract change — skips the deliberation panel.

## Acceptance criteria
- [ ] **AC1** — Given the T-0115 host harness, When a representative covered money/side-effect
  endpoint per policy class (`"auth"` authenticated, `"auth"` anonymous, `"webhook"`) is flooded past
  its window, Then the host returns **429 with `Retry-After`** at runtime (not just
  attribute-presence), and a request under the window succeeds.
- [ ] **AC2** — At least one endpoint added by the T-0194 remediation pass (e.g. Partner
  `UserController.ChangePassword` or `AdminGdprController.DeleteUserAccount`) is among the flooded
  representatives, proving the remediation attributes are live middleware, not dead metadata.
- [ ] **AC3** — The tests live in `Cleansia.HostTests` (the limiter middleware is not exercisable
  from `BaseIntegrationTest`), run green in CI, and the status log maps each case to its policy.

## Out of scope
- Flooding every covered endpoint (the structural guard owns exhaustiveness; this proves the runtime
  contract per policy class).
- Any change to attributes, policies, or `CleansiaStartupBase.cs`.

## Implementation notes
Mirror the T-0115 harness patterns. Mind limiter-window clock control (use the harness's existing
time/window strategy; avoid wall-clock sleeps where the fixture supports replacement). Sequence with
the Wave-4 test wave (T-0210…T-0218).

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; the T-0194 AC6 deviation made a ticket)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
