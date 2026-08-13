---
id: T-0480
title: iOS tab-bar labels — verify the overflow behaviour, then choose a mechanism the stock TabView allows
status: draft
size: S
owner: architect
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0018, 0022]
layers: [architect, ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #3 (2026-08-02):** *"Bottom nav bar text wraps — must truncate when too long, iOS +
Android."* Android is **T-0479** and is a three-line mechanical fix. **iOS is not**, and that is the
whole reason this is a separate ticket with an `architect` owner.

### Ground truth — PM-verified on `master` at `0e4ede1b`

`CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift:138-205` renders a **stock SwiftUI
`TabView`** with `.tabItem { Label(tab.label, systemImage: tab.systemImage) }` on five slots. Per the
comment at `CustomerBottomBar.swift:35`, this is a **deliberate ADR-0022 supersede (2026-07-08)**:
*"the native `TabView` replaces the custom pill composite."*

**The consequence, and it is the load-bearing fact:** a `.tabItem` label is rendered by **UIKit's
`UITabBarItem`**, not by SwiftUI's text layout. `.lineLimit`, `.truncationMode`, `.minimumScaleFactor`
and `.fixedSize` applied inside `.tabItem` are **not honoured** — the system owns the label's layout.
So **the Android fix does not port.** An `ios` developer handed "add `.lineLimit(1)`" would produce a
diff that compiles, passes review, and changes nothing on device.

**What that leaves is a genuine choice with three shapes, and it is an architecture call:**

| | Mechanism | Cost |
|---|---|---|
| **A** | **Shorten the five `nav_*` strings** in `Localizable.xcstrings` for the long locales | Cheapest. But it reopens the `Localizable.xcstrings` lane, needs a native-speaker check for `uk`/`ru`, and **diverges from Android's labels** unless Android shortens too — which the owner explicitly did *not* ask for (`T-0479` is truncation) |
| **B** | `UITabBarItem.appearance()` / `UITabBarAppearance` text attributes at app launch | Keeps the strings. Global UIKit appearance mutation, invisible to SwiftUI previews and to every existing test, and it still cannot force *truncation* — only font metrics |
| **C** | **Revert to a custom bar** | `CustomerBottomBar.swift` still exists in the tree. Full control, and it **reverses an accepted ADR-0022 decision** — the most expensive option and the one nobody should take casually |

**And the first question is whether iOS is broken at all.** `UITabBarItem` truncates by default; it
does not wrap. The owner's report names both platforms, and **Android is definitively confirmed** —
so the iOS half may be an accurate generalization or may be the owner reporting one symptom on the
platform where it is real. **PM has not seen an iOS device.** Verify before choosing.

## Acceptance criteria

- [ ] **AC1 — VERIFY FIRST. Is iOS actually wrong?** Given the customer app on the **16.4 floor** in
      `uk` and `ru` at the narrowest supported width (iPhone SE), When the tab bar renders, Then a
      screenshot per language records the actual behaviour: truncated, wrapped, or scaled. **"It is
      already correct"** is a valid and successful outcome — it closes this ticket and reduces the
      owner's remark #3 to Android only. Evidence: two screenshots.
- [ ] **AC2 — the partner app is checked too.** `CleansiaPartner/.../Shell/PartnerShellView.swift`
      uses the same `.tabItem` pattern (PM-verified it is the only partner file containing
      `tabItem`). Same two screenshots. Evidence: two more screenshots.
- [ ] **AC3 — if AC1 reproduces, the mechanism is CHOSEN against A/B/C with a why-not for the two
      rejected.** The ruling states explicitly whether `.lineLimit` inside `.tabItem` was **tested on
      device** and what it did — because "it should work" is exactly the trap here. Evidence: the
      ruling in `## Review`, plus the device result of the naive attempt.
- [ ] **AC4 — if the ruling is (A), the label divergence from Android is stated and accepted.**
      Android is truncating full labels (T-0479); iOS would be showing shorter ones. That is an
      ADR-0018 parity divergence and it must be named, not discovered later. If the ruling wants
      Android to shorten too, that is a **new ticket id**, not a widening of T-0479.
- [ ] **AC5 — if the ruling is (C), it does NOT land in this ticket.** Reversing ADR-0022 is a
      superseding ADR plus a shell rewrite. This ticket would then produce **the ADR and a new
      `M` implementation ticket**, and stop. Sized `S` deliberately so it cannot silently become that.
- [ ] **AC6 (Gate 0.5)** — whatever lands: `xcodebuild build test` for the affected scheme(s) on the
      16.4 floor, SwiftFormat `--lint` + SwiftLint `--strict`. **Leg 1:** the evidence here is
      screenshots and a device observation — say so under leg 3.

## Out of scope

- **Android** — T-0479.
- **Any `nav_*` string change on Android.** Even if iOS takes option (A).
- **Reverting ADR-0022 inside this ticket.** AC5 forbids it explicitly.
- **The customer Book FAB / centre slot** (`Color.clear` + `.accessibilityHidden(true)` at
  `CustomerShellView.swift:181-184`). Untouched.

## Implementation notes

**Architect panel, short — author + 2 challengers + lead.** The decision is small but it is a real
trade-off with an ADR in force, which is exactly the deliberation trigger. The challenge the panel
should expect: *"just add `.lineLimit(1)`"* — the counter is that `.tabItem` does not honour it, and
AC3 requires that to be **demonstrated on device rather than asserted**.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

**Before starting:** `src/cleansia_ios/scripts/generate-api-clients.sh` + `xcodegen generate` in both
app dirs (**T-0474**'s trap).

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #3).** The stock-`TabView` topology, the
  ADR-0022 supersede comment, the five `.tabItem` call sites and the partner app's identical pattern
  were PM-verified at `0e4ede1b`. **Filed as verify-then-decide, not as a fix**, because the Android
  remedy provably does not port and because iOS may not be broken.

## Review
