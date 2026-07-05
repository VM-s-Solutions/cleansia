---
id: T-0369
title: "iOS partner app — remove the outer pathless NavigationStack + convert the typed in-tab paths to NavigationPath (the T-0368 crash-topology mirror, minimal fix)"
status: done
size: S
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0014, ADR-0020, ADR-0022]
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
- [x] **AC1 (topology)** — The outer pathless `NavigationStack` in `PartnerRootView.swift:17` is deleted (if
  any partner auth screen actually uses nav-bar APIs, scope a stack around the auth cases ONLY — never around
  the shell); grep proves no `NavigationStack` nests another anywhere in `CleansiaPartner/Sources`.
  *(No pre-shell screen uses nav-bar APIs; the root switch sits in a ZStack so the `forcedSignOutStream`
  `.task` hangs off a stable container.)*
- [x] **AC2 (path conversion)** — The typed in-tab path arrays convert to type-erased `NavigationPath`
  (`OrdersListView.swift:35`, `EarningsView.swift:26`, `ProfileView.swift:47`), keeping every
  `.navigationDestination(for:)` registration as-is; `removeLast`/`isEmpty` call sites updated to the
  `NavigationPath` API. *(NOTED EXTENSION: a 5th typed-path mutation site the ruling's line refs didn't
  name — `RegistrationLockView` incl. its replace-last subscript — converted too; the lock's OWN stack
  stays, per AC4.)*
- [x] **AC3 (device-floor smoke)** — On the iOS 16.4 simulator: order list → order detail (incl. photos),
  earnings → invoice detail (QuickLook), profile hub → each section editor, devices — all push correctly, no
  crash, no yellow-⚠️ placeholder, tab bar behavior correct (T-0374 leg). *(DEVIATION, recorded: the 16.4
  leg ran install + launch + screenshots with ZERO NavigationAuthority/comparisonTypeMismatch hits in the
  unified os_log + the full test suite; the SIGNED-IN push walk is the owner device pass — no credentials
  in the harness.)*
- [x] **AC4 (non-regression)** — CleansiaPartner 366-test suite + CleansiaCore suite green;
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
- 2026-07-03 — implemented by ios (Slice B) in `4e38a93f`: the outer pathless stack deleted; ALL FIVE
  typed-path sites converted to `NavigationPath` (the four named + `RegistrationLockView`'s replace-last
  subscript); `PushTapRouting`'s `[OrderRoute]`-returning deep-link helper became the pure resolver
  `deepLinkRoute(orderId) -> OrderRoute?` (tests rewritten FIRST); every `.navigationDestination(for:)`
  registration byte-identical; `.navigationDestination(isPresented:)` — zero occurrences. Evidence: Partner
  366/366 (iPhone 17); the iOS 16.4 floor smoke (install + launch + screenshots) with ZERO
  NavigationAuthority/comparisonTypeMismatch hits in the unified os_log; swiftformat 0.60.1 + swiftlint
  --strict clean on the partner target.
- 2026-07-03 — phase ride-along (no ticket; recorded here because the Slice-B lint run surfaced it):
  `ios-ci` never ran on master PUSHES (how `6bf55f14` landed with 11 swiftformat + 3 swiftlint violations
  no CI saw) — closed by `197352a9` (ios-ci now also runs on pushes to master); the violations themselves
  fixed within this phase.
- 2026-07-03 — **done** by pm at phase close (reviewer **APPROVE, clean**). Final-tree gates: Partner suite
  green; Core 272/272 on iPhone 17 AND iOS 16.4; swiftformat 0.60.1 0/528 + swiftlint --strict clean
  tree-wide; the 16.4 boot-install-launch smoke of the partner app: 0 hits. The signed-in walk rides the
  owner device pass (phase PR).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 reviewer (Slice B, concurrent): **APPROVE — clean.** The minimal fix verified against ADR-0022
  D1/D4: no restructure, ADR-0020 preserved for the partner, the RegistrationLock gate #24 byte-unchanged
  (its own stack is a sibling, not a nesting), all five path conversions correct, the deep-link resolver
  purity an improvement, no nested stacks remain (grep). No changes requested. PM reconciled 2026-07-03.
