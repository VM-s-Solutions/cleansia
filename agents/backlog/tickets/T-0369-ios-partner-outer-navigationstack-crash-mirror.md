---
id: T-0369
title: "iOS partner app — remove the outer pathless NavigationStack + convert the typed in-tab paths to NavigationPath (the T-0368 crash-topology mirror, minimal fix)"
status: ready
size: S
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0014, ADR-0020]
layers: [ios]
security_touching: false
priority: high
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster navigation-shell — NOTES: the partner mirror)
---

> **iOS-16-specific crash class, mirrored from the customer app.** The diagnosis NOTES call it out
> explicitly: the partner app carries the **identical crash topology** — `PartnerRootView.swift:17` wraps its
> shell in an outer pathless `NavigationStack` over typed in-tab stacks (`OrdersListView.swift:35`,
> `EarningsView.swift:26`, `ProfileView.swift:47`). On iOS 16, programmatic pushes into homogeneous typed
> paths under an outer stack throw `comparisonTypeMismatch` (or render the yellow-⚠️ missing-destination
> placeholder on 16.0–16.3). Invisible on the latest-runtime simulator — hence never caught (see T-0374).

## Context
Same root cause as T-0368's findings 1–2, in the partner target. Unlike the customer app, the partner shell
has NO island-bar/pager/FAB parity debt in this phase, so the ruling here is the **minimal fix** — it
preserves ADR-0020 exactly as recorded (per-tab in-tab stacks stay).

## Acceptance criteria
- [ ] **AC1 (topology)** — The outer pathless `NavigationStack` in `PartnerRootView.swift:17` is deleted (if
  any partner auth screen actually uses nav-bar APIs, scope a stack around the auth cases ONLY — never around
  the shell); grep proves no `NavigationStack` nests another anywhere in `CleansiaPartner/Sources`.
- [ ] **AC2 (path conversion)** — The typed in-tab path arrays convert to type-erased `NavigationPath`
  (`OrdersListView.swift:35`, `EarningsView.swift:26`, `ProfileView.swift:47`), keeping every
  `.navigationDestination(for:)` registration as-is; `removeLast`/`isEmpty` call sites updated to the
  `NavigationPath` API.
- [ ] **AC3 (device-floor smoke)** — On the iOS 16.4 simulator: order list → order detail (incl. photos),
  earnings → invoice detail (QuickLook), profile hub → each section editor, devices — all push correctly, no
  crash, no yellow-⚠️ placeholder, tab bar behavior correct (T-0374 leg).
- [ ] **AC4 (non-regression)** — CleansiaPartner 366-test suite + CleansiaCore suite green;
  swiftformat 0.60.1 + swiftlint --strict clean; RegistrationLock's own stack (T-0310, gate #24) untouched.

## Out of scope
- Any partner shell restructure (pill bar / pager / single-stack) — the customer-side ruling (T-0368) does
  NOT extend here; ADR-0020 stays as recorded for the partner.
- The partner data-layer mirrors (apiResponseQueue, date decoder) — T-0370 carries those for both apps.

## Implementation notes
- This is deliberately the fallback-shaped fix from the diagnosis: delete the outer stack + the
  `NavigationPath` conversion — no pattern change, no ADR edit, no new component.
- Mind the RegistrationLock: it owns its OWN `NavigationStack` by design (fail-closed gate, T-0310) — that
  stack is a sibling under the root switch, not a nesting; leave it byte-unchanged.
- Reviewer runs concurrently (reviewer-per-developer invariant).

## Status log
- 2026-07-03 — filed `ready` by pm from the phase/ios-fix1 diagnosis (navigation-shell cluster NOTES: the
  partner mirror). Minimal fix, independent of the T-0368 architect ruling; high priority (same crash class
  on every partner iOS 16 device).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
