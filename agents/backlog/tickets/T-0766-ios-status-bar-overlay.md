---
id: T-0766
title: iOS: content draws under the status bar — audit every screen of both apps for safe-area handling and fix what overlays
status: todo
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
