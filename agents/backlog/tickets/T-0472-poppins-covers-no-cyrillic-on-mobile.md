---
id: T-0472
title: Poppins covers 0 of 98 Cyrillic code points — every ru/uk heading falls back to a system face on both mobile platforms
status: draft
size: M
owner: architect
created: 2026-08-01
updated: 2026-08-01
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, android, ios]
security_touching: false
manual_steps: []
sprint: 14
---

> **Split out of `T-0450` on 2026-08-01.** T-0450 was two defects on one surface; the owner's answer to
> **Q-I18N-02** settled one of them (the label) and left this one entirely untouched. **This half blocks
> nothing** — that is the point of the split. T-0448 and T-0449 (the avatar clients) depend on T-0450,
> **not** on this ticket.

## Context

The customer profile hero renders the user's name in `headlineSmall`, which is **Poppins SemiBold**
(`core/.../ui/theme/Type.kt:66-69`). For a Russian or Ukrainian user that name is guaranteed to be
Cyrillic — and Poppins has no Cyrillic at all.

**PM measurement, 2026-07-30** — the `cmap` of all six bundled TTFs parsed directly:

| Font | Cyrillic code points present | Total code points |
|---|---|---|
| `poppins_semibold.ttf` | **0 / 98** | 471 |
| `poppins_medium.ttf` | **0 / 98** | 471 |
| `poppins_bold.ttf` | **0 / 98** | 471 |
| `nunito_regular.ttf` | 98 / 98 | 938 |
| `nunito_semibold.ttf` | 98 / 98 | 938 |
| `nunito_bold.ttf` | 98 / 98 | 938 |

(Target set = `U+0400`–`U+045F` plus `ҐґЄєІіЇї`. The original brief said "0 of 96"; the exact figure for
that set is **0 of 98**, and it is **all three Poppins weights**, not only SemiBold.)

So every `ru`/`uk` user's name falls back to **Roboto** (Android) / the system face (iOS), sitting beside
Nunito body text in the same hero — **three typefaces on one card**.

### The blast radius is wider than the hero, and it is cross-platform

The six Android TTFs are **byte-identical** to the six iOS ones (sha1-verified: `622ca6ccbe2f…` for
`poppins_semibold.ttf` ↔ `Poppins-SemiBold.ttf`, and the same for the other five — the comment at
`Type.kt:12-14` already claims this and it holds). So **every Poppins slot on both platforms** falls
back for `ru`/`uk`:

- The type-scale slots: `displayLarge/Medium/Small`, `headlineLarge/Medium/Small`.
- The hard-coded call sites: `WordmarkSplash.kt:87,135`, `CleansiaErrorState.kt:73`, `CodeInput.kt:97`,
  `ProfileTab.kt:437`, `EditProfileScreen.kt:215` *(PM re-verified these two line numbers on
  `f649c3bd`; the earlier record said `EditProfileScreen.kt:214`)*.

The web apps load the same family from Google Fonts and have the same defect. That whole question is
**Q-BRAND-01** (`blocking: no`, `pre-prod`, unanswered) — replacing a brand typeface is an **owner**
decision, not an engineering one. **This ticket fixes the mobile profile hero and rules on the
mechanism; it does not swap the platform typeface unilaterally.**

### Why this was split off, and why it is not urgent

**The Q-I18N-02 answer does not touch it.** The owner shortened the chip label to `Редактировать`;
that string is still Cyrillic and still falls back. The two defects sat on one surface but never had
one cause. Keeping them in one ticket meant the two avatar client tickets (T-0448, T-0449) waited on an
architect panel and an unanswered brand question that neither of them needs.

## Acceptance criteria

- [ ] **AC1 (Android)** — Given a `ru` or `uk` user name, When the customer profile hero renders it,
      Then the glyphs come from a **Cyrillic-capable bundled family** and **not** from a system fallback.
      Evidence: a resolved-typeface assertion if one is executable, or a documented visual A/B at 3× zoom
      against a known Roboto reference — **named in `## Review`**, with which one was used.
- [ ] **AC2 (iOS)** — Given the same name on iOS, When the hero renders it, Then it resolves **the same
      way** as Android. Evidence: side-by-side screenshots. A fix that lands on one platform only is a
      new divergence closing an old one, and fails this AC.
- [ ] **AC3 — the architect ruling exists and names its alternatives.** Given the panel, When it lands in
      `agents/architecture/decisions/`, Then it rules on the mechanism with a why-not for **each** of:
      **(a)** add a Cyrillic-capable face as an additional `Font(...)` inside the `Poppins`
      `FontFamily` — **verify this first**: Compose resolves within a family by **weight/style, not by
      coverage**, so this may simply not do what it looks like it does; **(b)** a per-locale family swap
      at theme level; **(c)** subset-merge a Cyrillic donor into the Poppins binaries; **(d)** replace
      Poppins outright — a **brand** change, therefore owner-gated via Q-BRAND-01 and **not** shippable
      from this ticket.
- [ ] **AC4 — the full slot list is TRIAGED, and a silent skip fails.** Given the ruling, When it lands,
      Then every slot and hard-coded call site listed in `## Context` is either **in scope and fixed**, or
      **explicitly deferred with the follow-up ticket id**. Evidence: the triage table in `## Review`.
- [ ] **AC5 — the licence question is answered before any font binary changes.** If the ruling adds,
      merges or replaces a font file, the **licence of the donor/replacement** is named and confirmed
      compatible with redistribution in a shipped app. A subset-merge (option c) produces a **derivative
      font binary** — say explicitly whether the source licence permits it. This is a legal question, not
      a technical one; if it cannot be answered, that option is not available. Evidence: the licence
      named in `## Review`.
- [ ] **AC6 — binary parity is preserved or its loss is stated.** The Android and iOS Poppins binaries
      are byte-identical today and `Type.kt:12-14` documents that as an invariant. If the fix changes the
      binaries, they **stay** byte-identical across platforms, or the ticket records why parity was
      broken and what now guarantees the two platforms render alike. Evidence: sha1s in `## Review`.
- [ ] **AC7 (Gate 0.5)** — Android: suite re-run **un-cached** (`--rerun-tasks`) with task outcomes
      recorded. iOS: build/test run, or declared unverifiable under leg 3. **Leg 1** applies to AC1's
      assertion **if it is executable**; if the AC1 evidence is visual, say so under **leg 3** rather than
      inventing a mutation or an asset-exists test. **Leg 5 — bundle size:** a merged or additional font
      binary changes app size on both platforms; record the delta (Gate 5).

## Out of scope

- **Swapping the brand typeface platform-wide.** The panel may recommend it; shipping it is a separate
  ticket and an **owner** brand decision (Q-BRAND-01).
- **Web** (`Cleansia.App`). The web apps load Poppins from Google Fonts, which serves the same
  Latin+Devanagari-only family — **the same defect exists there and is not fixed here.** Record it as a
  follow-up under Q-BRAND-01; do not widen this ticket.
- **`cs` / `sk` / `en`.** Entirely inside Poppins' coverage.
- **The chip label.** That is T-0450 and it is a different defect with a different cause.
- **The avatar image** — T-0448 / T-0449.

## Implementation notes

**Architect panel required before this leaves `draft`** (author + 2–3 challengers + lead,
`process/deliberation.md`). This is a design-system decision with a real trade-off space, four
alternatives and a licence dimension — exactly what a panel is for. Record in
`agents/architecture/decisions/`.

**The challengers should press hardest on option (a)**, because it is the one that looks free. Compose's
`FontFamily` resolves by weight/style; there is no documented per-glyph coverage fallback *within* a
family. If (a) does not work the way the ADR assumes, the cheap option evaporates and the cost picture
changes completely — so **verify it empirically before it is written down as viable**, not after.

**⚠️ The split does NOT fully decouple the shared-file lanes, and pretending it does would be the
error.** This ticket's primary file — `core/.../ui/theme/Type.kt` and the font binaries — is
uncontended. But if the ruling touches the **hard-coded call sites**, it writes
`customer-app/.../profile/ProfileTab.kt:437` and `EditProfileScreen.kt:215`, which are **inside the
avatar lane**:

```
ProfileTab.kt        : T-0442 ✅ → T-0450 → T-0448 → T-0453
EditProfileScreen.kt : T-0448 (the photo-picker TODO at :230)
```

**Sequence T-0472 LAST in that lane** — after T-0448, and coordinate with T-0453 — or scope AC4's fix to
`Type.kt` only and defer the hard-coded call sites to a follow-up. **Do not dispatch this concurrently
with T-0450, T-0448 or T-0453.** Say which choice was made in `## Review`.

**Do not touch `src/cleansia_ios/**/Info.plist` or `**/project.yml`** — the owner's live Stripe key is in
the working copies. Adding a font to an iOS target normally means an `UIAppFonts` entry in `Info.plist`;
**that file is xcodegen-generated from `project.yml` `info.properties`**. If the ruling needs a new
`UIAppFonts` entry, **write it up as a note for the PM to hand to the owner** — do not edit either file.
*(See T-0475 — moving the owner's secrets out of `project.yml` would remove this constraint entirely.)*

## Status log
- 2026-08-01 — **created by pm as the split half (B) of T-0450.** T-0450 carried two defects on one
  surface; **Q-I18N-02's answer settled the label and left this untouched**, so the coupling now costs
  more than it buys. Concretely: T-0448 and T-0449 depend on T-0450 for their shared-file lanes, and
  leaving the font work in T-0450 would have made both avatar tickets wait on an architect panel plus
  the unanswered brand question **Q-BRAND-01** — neither of which they need. **This half blocks nothing.**
  All AC and context text carried over from T-0450 (its AC4/AC5/AC6 became AC1/AC2/AC4 here); **AC3, AC5
  and AC6 are new** — the alternatives-with-why-not obligation, the font-licence question, and the
  byte-identical-binary invariant, none of which the original ticket stated.
- 2026-08-01 — **stays `draft` on its own content, not on a dependency.** `depends_on: []` is correct and
  nothing sequences ahead of it. What it needs is the **architect panel** (DoR item 2 — the AC are not
  finalizable without the ruling) and it should record its position in the `ProfileTab.kt` lane before
  dispatch. **Q-BRAND-01 does not block it** — that question is the *platform-wide* strategy; this ticket
  fixes the mobile hero and feeds the answer.

## Review
<!-- architect panel verdict + AC4's triage table + AC5's licence + AC6's sha1s go here -->
