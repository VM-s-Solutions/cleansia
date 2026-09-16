---
id: T-0766
title: iOS: content draws under the status bar — audit every screen of both apps for safe-area handling and fix what overlays
status: in_progress
size: S
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Item 8 of the owner's 2026-09-13 improvement list ("iOS status bar overlay"). No screen was named; the lane audits every screen of `CleansiaCustomer` and `CleansiaPartner` for safe-area handling (`ignoresSafeArea`, custom top bars, sheets, full-screen covers) and fixes what draws under the status bar. SwiftFormat/SwiftLint per touched file; XCTest cannot run on this machine — write the tests, say they are unrun; the owner compiles on the Mac.

**Doing** — the audit list (screen → finding → fix), the fixes, a note per app in `src/cleansia_ios/docs/`.
**NOT** — no redesign; no Android change.
**Done looks like** — no screen draws content under the status bar on iPhone with a notch/Dynamic Island and on the iOS 16 floor; SwiftFormat clean; CI green.

## Implementation checkpoint — 2026-09-16

The source audit covered both app route/modal trees and all 134 app files declaring 455 SwiftUI View
types, plus shared presentation containers. It found unsafe scrolling in the customer profile,
partner profile and customer Plus offer; a shared sheet ornament could extend above the safe
viewport, and the partner's approximate-map legend could enter the top inset at an expanded sheet.

The three scrolling surfaces now keep their safe viewport and extend only decorative backgrounds.
Redundant manual top-inset plumbing was removed. The shared ornament remains attached to the sheet
edge and is clipped to the safe viewport; moving it downward would instead overlap sheet controls.
The approximate legend uses the measured safe top and renders only when its full height fits in the
uncovered strip. No generated API members or Android sources changed for this ticket.

Five source-binding XCTest cases guard these regressions; existing sheet-anchor geometry tests
remain. SwiftFormat 0.60.1 passed separately for all eight touched Swift files. XCTest and SwiftLint
are not runnable locally on this Windows machine. The per-app audit notes list source coverage and
the outstanding device matrix. All seven CI workflows passed for `b5470030`, including SwiftFormat,
strict SwiftLint and the Core (720), Partner (856) and Customer (1,187, one skipped) XCTest suites.
Rendered device confirmation remains pending, so this ticket stays open.

Independent source review approved the changes with no blockers or majors: caller safe-area chains,
clipping and fitting behavior are consistent, and both app project manifests include the new tests.
That review does not replace XCTest, SwiftLint or rendered device acceptance.

- [Customer audit](../../../src/cleansia_ios/docs/customer-safe-area-audit.md)
- [Partner audit](../../../src/cleansia_ios/docs/partner-safe-area-audit.md)
