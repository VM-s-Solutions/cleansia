---
id: T-0483
title: Entrance instructions are printed in plain sight on the partner order detail — put them behind an explicit reveal
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0018]
layers: [analyst, architect, ios, android]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Owner remark #4, third half (2026-08-02):** *"…Also: **hide entrance instructions** behind an
explicit reveal — a button or a Telegram-style animation."*

### Ground truth — PM-verified on `master` at `0e4ede1b`

The field is `Order.accessInstructions` — free text, backend-capped at 2000 UTF-16 units
(`Features/Orders/CreateOrder.cs:136-138`). Its realistic contents are **door codes, gate codes,
key-box combinations and lockbox locations** — the partner preview fixture in the repo says it
outright: `OrderDetailContent.swift:268` → `accessInstructions: "Code 1234 at the gate."`

**Where it renders today — four surfaces, all plain text, all visible on load:**

| App | Surface | File:line |
|---|---|---|
| **Partner Android** | `AccessCard` | `partner-app/.../orders/AccessCard.kt:67` (`text = accessInstructions`), gated in at `OrderDetailScreen.kt:450`, `:549` |
| **Partner iOS** | access card inside the sheet | `CleansiaPartner/.../Orders/OrderDetailContent.swift:99`, gated at `:17` |
| **Customer Android** | `InstructionsCard` | `customer-app/.../orders/OrderDetailDetailsCards.kt:238-239` |
| **Customer iOS** | `OrderInstructionsCard` | `CleansiaCustomer/.../Orders/OrderDetailDetailsCards.swift:139-140` |

**The partner surfaces are the ones that matter and the customer ones probably are not.** On the
partner side the reader is a **third party** — a cleaner, holding a phone, in a stairwell, possibly
with the customer's neighbours behind them, and the app is a shared-device risk (`T-0406` exists
because the partner app has forced-signout semantics). On the customer side the reader is the person
who **wrote** the code about their own home. Hiding it from them is friction with no threat model.
**The owner said "entrance instructions"; the PM's read is that this is the partner app. AC1 forces
that to be decided rather than assumed.**

### Why this needs a panel and is not a `showSecret` boolean

1. **"Explicit reveal" has a security property or it has none.** A tap-to-reveal that keeps the string
   in the view hierarchy behind an opacity mask protects against **nothing** — not a screenshot, not
   an accessibility dump, not a screen recording, not a bystander who watches the tap. If the point is
   shoulder-surfing, the string must not be composed until revealed, and it must auto-hide. If the
   point is merely tidiness, none of that is needed. **These are different tickets and only a panel
   can say which one this is.**
2. **The "Telegram-style animation" is a specific, known interaction** (spoiler blur, tap to
   dissolve). Ported literally it is a `.blur` + `.redacted` on iOS and a custom `Modifier` on
   Android — **and both leave the text in the tree**. Naming the animation is not the same as adopting
   its security model.
3. **Accessibility cuts directly against it.** VoiceOver / TalkBack will read a masked string unless
   the semantics are also suppressed — and suppressing them makes the door code **unreachable** for a
   blind cleaner. That is a real conflict with a real answer, and it must be written down.
4. **The backlog already has a rule-shaped gap here.** `T-0460` (sprint-14, post-demo) exists because
   **nothing in S1–S11 covers bytes inside a stored artifact served by a URL**. "A door code displayed
   on a third party's screen" is adjacent and may want the same rule amendment.

## Acceptance criteria

- [ ] **AC1 — SCOPE IS RULED FIRST: partner only, or all four surfaces.** The panel states which
      surfaces are in and gives the threat model in one sentence each. The PM's default is
      **partner only** (see `## Context`); the panel may overrule it. Evidence: the ruling.
- [ ] **AC2 — the reveal's SECURITY PROPERTY is stated, and the implementation matches it.** One of:
      **(a)** cosmetic only — the string stays in the tree, no claim is made, and the ticket says so
      in the code comment; **(b)** shoulder-surf resistant — the string is not composed until
      revealed, and it re-hides on backgrounding / after a stated timeout / on navigation away.
      **A (b)-shaped promise with an (a)-shaped implementation fails this AC.** Evidence: the stated
      property plus the diff that honours it.
- [ ] **AC3 — accessibility is answered, not traded away.** State what VoiceOver and TalkBack
      announce **before** and **after** the reveal, **verified by executing the read on both
      platforms**. A blind cleaner must be able to obtain the door code. Evidence: two transcripts.
- [ ] **AC4 — the two platforms produce the same interaction, or the divergence is ruled.** ADR-0018.
      If iOS gets a spoiler-blur and Android gets a button, that is a divergence and it needs a
      sentence. Evidence: side-by-side screenshots, both states, both platforms.
- [ ] **AC5 — screenshot / screen-recording exposure is stated, not silently ignored.** Say plainly
      whether the revealed state is capturable (it is, on both platforms, unless `FLAG_SECURE` /
      `isSecureTextEntry` are used — and neither is proposed here). Recorded so nobody later reads
      this ticket as having closed that. Evidence: the sentence in `## Review`.
- [ ] **AC6 — no string is dropped.** The instruction text itself is unchanged; only its presentation
      moves. The i18n bundles gain **at most** a reveal-affordance label ×5 locales ×platform.
      Evidence: the diff.
- [ ] **AC7 — the security-rule question is ROUTED, not absorbed.** State whether S1–S11 should gain
      a rule about *displaying a third party's access credential*, and if so route it to **T-0460**
      (which already owns the adjacent gap) rather than filing a second rule ticket. Evidence: the
      routing note, or an argued "no rule needed".
- [ ] **AC8 (Gate 0.5)** — Android `:partner-app` (+ `:customer-app` if AC1 widens) compile + unit
      tests **un-cached** (`--rerun-tasks --no-build-cache`); iOS `xcodebuild build test` for the
      affected scheme(s) on the **16.4 floor** + SwiftFormat/SwiftLint. Any state-machine assertion
      added is **mutation-proved**. Screenshots are leg-3.

## Out of scope

- **`FLAG_SECURE` / screenshot blocking.** AC5 records the exposure; blocking screenshots on a
  cleaner's working screen is a separate product decision with real usability cost.
- **Changing where `accessInstructions` is stored, logged or transmitted.** Note for the record and
  **do not fix here**: `T-0457` (sprint-14, `ready`, P1) covers PII in Information-level request logs,
  and `T-0470` covers credential-shaped values that no redaction list names. **A door code is exactly
  a T-0470-class value.** Recorded on both tickets; not widened into either.
- **The booking-time capture UX.** T-0440 / T-0441 shipped it; T-0469 owns its validation parity.
- **Web.** Not named in the remark.

## Implementation notes

**Panel first, and it is step 1 of the dispatch — not a precondition to wait on.** An `analyst` panel
(author + 2–3 challengers + lead) on the threat model and the interaction, with the `architect`
ruling AC2's security property and the ADR-0018 parity consequence. **The owner's decision — that it
should be hidden — is not up for debate.** What the panel decides is *which surfaces*, *what the
reveal actually protects against*, and *what happens to the accessibility tree*.

**Challenge the panel should expect:** *"Telegram spoiler = blur + tap, ship it"* — the counter is
AC2: that is presentation (a), and the ticket must then stop claiming to hide a door code.

**Fan-out after the ruling: two developer instances in parallel, one reviewer each.** Disjoint files —
`partner-app/.../orders/AccessCard.kt` and `CleansiaPartner/.../Orders/OrderDetailContent.swift`.
Neither has another sprint-15 claimant (PM-checked); note `OrderDetailContent.swift` on the partner
side is **not** the customer file of the same name.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.**

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #4, third half).** All four render sites
  PM-verified at file:line, including the repo's own `"Code 1234 at the gate."` preview fixture, which
  is what settles that this field really does hold credentials. Filed `security_touching: true` and
  **partner-scoped by default** with AC1 forcing the scope ruling. Needs a panel: "hide it" is an
  owner decision, but *what hiding means* is not, and getting that wrong ships a security claim the
  code does not honour.

## Review
