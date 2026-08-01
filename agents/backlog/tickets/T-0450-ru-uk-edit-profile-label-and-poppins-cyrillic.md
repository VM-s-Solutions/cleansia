---
id: T-0450
title: ru/uk profile hero — the edit-profile label overflows, and Poppins has no Cyrillic at all
status: draft
size: M
owner: analyst
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0440, T-0441, T-0451]
blocks: [T-0448, T-0449]
stories: []
adrs: []
layers: [analyst, architect, android, ios]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Two defects on **one surface** (the customer profile hero) in **the same two locales** (`ru`, `uk`),
both surfaced by real work on T-0442. They are ticketed together because a fix for either one alone
leaves the same screen visibly wrong in the same two languages.

### Defect A — the label is too long for the chip it lives in

`profile_row_edit` is `"Edit profile"` in `en` and `"Редактировать профиль"` in `ru`. Reported
measurement from T-0442's implementation work: **216.8dp** for the `ru` string against **120.2dp** for
`en`, at the chip's `labelLarge` (Nunito Bold 14sp) with a 14dp icon and 14dp horizontal padding.

T-0442 shipped a cap rather than a fix, and said so in the code
(`customer-app/.../features/profile/ProfileTab.kt:246-248`):

```
// Feasible band at 320dp: >= 0.43 or the English label ellipsizes, <= 0.45 to hold the name column
// at >= 56dp. Only 5.8dp of English headroom, so re-measure if the label, icon, padding or font moves.
private const val EditChipMaxWidthFraction = 0.45f
```

At `0.45 × 320dp = 144dp` a 216.8dp string cannot fit, so the chip renders **"Редактиров…"**. The cap
is correct — it stops the chip starving the name column (`ProfileTab.kt:266-269`) — but it converts an
overflow into a truncation. The real fix is a **shorter string**, and a shorter Russian/Ukrainian
string is a translation decision that needs **native-speaker sign-off**, not a PM's guess.

Affected keys, all currently carrying the long form:
- Android customer: `customer-app/src/main/res/values-ru/strings.xml:367` `profile_row_edit`
  = `Редактировать профиль`; `values-uk/strings.xml:367` = `Редагувати профіль`.
- Android customer (screen title, same string, different surface):
  `values-ru/strings.xml:393` / `values-uk/strings.xml:393` `profile_edit_title`.
- Android partner: `partner-app/src/main/res/values-ru/strings.xml:461` `edit_profile`
  = `Редактировать профиль`.
- iOS: `CleansiaCustomer/Resources/Localizable.xcstrings` → `profile_row_edit` and
  `profile_edit_title`, both `ru = Редактировать профиль`, `uk = Редагувати профіль`.

**This also improves iOS.** `ProfileTab.swift:332-350` (`EditProfileChip`) has **no `.lineLimit`
and no width cap**, so the same long label wraps the capsule to **two lines** rather than truncating.
Shortening the string fixes the wrap without touching the layout.

### Defect B — `poppins_semibold.ttf` covers zero Cyrillic code points

The hero name renders in `headlineSmall`, which is **Poppins SemiBold**
(`core/.../ui/theme/Type.kt:66-69`), and the hero name is the one string on that screen that is
guaranteed to be Cyrillic for a Russian or Ukrainian user.

**PM measurement, 2026-07-30** — parsed the `cmap` of all six bundled TTFs directly:

| Font | Cyrillic code points present | Total code points |
|---|---|---|
| `poppins_semibold.ttf` | **0 / 98** | 471 |
| `poppins_medium.ttf` | **0 / 98** | 471 |
| `poppins_bold.ttf` | **0 / 98** | 471 |
| `nunito_regular.ttf` | 98 / 98 | 938 |
| `nunito_semibold.ttf` | 98 / 98 | 938 |
| `nunito_bold.ttf` | 98 / 98 | 938 |

(Target set = `U+0400`–`U+045F` plus `ҐґЄєІіЇї`. The brief said 0 of 96; the exact figure for that
set is **0 of 98**, and it is **all three Poppins weights**, not only SemiBold.)

So every `ru`/`uk` user's name falls back to **Roboto**, sitting beside Nunito body text in the same
hero — three typefaces on one card.

**The blast radius is wider than this ticket, and cross-platform.** The six Android TTFs are
**byte-identical** to the six iOS ones (verified by sha1: `622ca6ccbe2f…` for
`poppins_semibold.ttf` ↔ `Poppins-SemiBold.ttf`, and the same for the other five — the comment at
`Type.kt:12-14` already claims this and it holds). So **every Poppins slot on both platforms**
(`displayLarge/Medium/Small`, `headlineLarge/Medium/Small`, plus the hard-coded call sites at
`WordmarkSplash.kt:87,135`, `CleansiaErrorState.kt:73`, `CodeInput.kt:97`,
`ProfileTab.kt:437`, `EditProfileScreen.kt:214`) falls back for `ru`/`uk`. That is a **design-system**
question, and it is escalated to the owner as **Q-BRAND-01** — this ticket fixes the hero and the
panel rules on the strategy; it does not swap the platform typeface unilaterally.

## Acceptance criteria

- [ ] **AC1** — Given locale `ru` (and separately `uk`), When the Android customer profile hero renders
      at 320dp, Then the edit chip shows its **complete** label with no ellipsis, and the name column
      still measures ≥ 56dp. Evidence: screenshots at 320dp in both locales, plus the re-measured
      label width against the `EditChipMaxWidthFraction` band recorded in `## Review`.
- [ ] **AC2** — Given the shortened `ru`/`uk` strings, When they are proposed, Then each has
      **explicit native-speaker sign-off recorded** (owner answer to **Q-I18N-02**) before merge. A
      machine-translated or PM-invented shortening fails this AC. Evidence: the answered question,
      quoted in `## Review`.
- [ ] **AC3** — Given locale `ru`/`uk`, When the **iOS** customer profile hero renders, Then the edit
      chip is **one line**. Evidence: iOS simulator screenshots in both locales, before and after.
- [ ] **AC4** — Given a `ru` or `uk` user name, When the Android customer profile hero renders it,
      Then the glyphs come from a **Cyrillic-capable bundled family** and **not** from a system
      fallback. Evidence: a resolved-typeface assertion (or a documented visual A/B at 3× zoom
      against a known Roboto reference), named in `## Review`.
- [ ] **AC5** — Given the same name on **iOS**, When the hero renders it, Then it resolves the same
      way as Android. Evidence: side-by-side screenshots.
- [ ] **AC6** — Given the architect ruling from the panel, When it lands, Then the **full list of
      Poppins slots and hard-coded call sites** above is triaged: each is either **in scope and
      fixed**, or **explicitly deferred with the follow-up ticket id**. A silent skip fails.
      Evidence: the triage table in `## Review`.
- [ ] **AC7** — Gate 0.5: the Android suite is re-run **un-cached** (`--rerun-tasks`) with task
      outcomes recorded, and the iOS build/test is either run or declared unverifiable under leg 3.
      Leg 1 (mutation) applies to AC4's assertion if it is executable; if the AC4 evidence is visual,
      say so under leg 3 rather than inventing a mutation.

## Out of scope

- **Swapping the brand typeface platform-wide.** The panel may recommend it; shipping it is a separate
  ticket and an owner brand decision (Q-BRAND-01).
- Web (`Cleansia.App`). The web apps load Poppins from Google Fonts, which serves the same
  Latin+Devanagari-only family — the same defect exists there and is **not** fixed here. Record it as
  a follow-up; do not widen this ticket.
- `cs` / `sk` / `en`. Both defects are Cyrillic-locale-specific: `cs` "Upravit profil" and `sk`
  "Upraviť profil" are 14 characters and are entirely inside Poppins' coverage.
- The avatar image itself — T-0448 / T-0449.

## Implementation notes

**Two panels, both required before this leaves `draft`:**
1. **Analyst panel** (author + 2–3 challengers + lead) on the string. The question is not "what is
   shorter" but "what does a Russian/Ukrainian speaker read on a 144dp chip that still means *edit
   your profile*" — and whether the chip label and the screen title (`profile_edit_title`) should
   diverge, since only the chip is width-constrained. Update `agents/analysts/<domain>.md`.
2. **Architect panel** on the font strategy. At minimum these options, with why-not for each:
   (a) add a Cyrillic-capable face as an additional `Font(...)` in the `Poppins` `FontFamily` — note
   Compose resolves within a family by **weight/style, not by coverage**, so verify this actually
   falls through per-glyph before proposing it; (b) a per-locale family swap at theme level;
   (c) subset-merge a Cyrillic donor into the Poppins binaries; (d) replace Poppins with a family that
   has Cyrillic (a brand change → owner). Record in `agents/architecture/decisions/`.

**Shared-file lanes — this ticket sits in the middle of three of them:**
- Android customer `values-{ru,uk}/strings.xml` → **T-0441 → T-0450 → T-0448**.
- Android customer `ProfileTab.kt` → T-0442 (done) → **T-0450 → T-0448 → T-0453**.
- iOS `Localizable.xcstrings` → **T-0440 → T-0450 → T-0449**.
- iOS `ProfileTab.swift` → **T-0451 → T-0450 → T-0449**.
Hence `depends_on: [T-0440, T-0441, T-0451]` — those are lane dependencies, not logical ones.

**Trap:** re-measure, do not trust the 216.8/120.2 figures. They are a report from T-0442's dev, not
a PM re-derivation, and the `EditChipMaxWidthFraction` comment explicitly says the band is only 5.8dp
wide for English — so any change to the icon, padding or font invalidates it.

**Do not** delete `EditChipMaxWidthFraction` when the string gets shorter. It is the guard that stops
the chip starving the name column; a shorter label makes it non-binding, not unnecessary.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, from T-0442's implementation and
  review; two panels required, plus two owner questions Q-I18N-02 and Q-BRAND-01)

## Review
<!-- reviewer writes verdict here; AC6's triage table and AC1's re-measurement go here -->
