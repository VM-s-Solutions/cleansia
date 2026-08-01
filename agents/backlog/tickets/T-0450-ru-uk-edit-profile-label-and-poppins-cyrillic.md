---
id: T-0450
title: Profile edit chip — shorten the label to the verb alone in all five locales, and truncate rather than wrap
status: ready
size: S
owner: android
created: 2026-07-30
updated: 2026-08-01
depends_on: []
blocks: [T-0448, T-0449]
stories: []
adrs: []
layers: [android, ios]
security_touching: false
manual_steps: []
sprint: 14
---

> **⚠️ SCOPE CHANGED 2026-08-01 — this ticket is now HALF of what it used to be.**
> Q-I18N-02 is **answered**, and the ticket was **split**. What remains here is **defect (A) — the
> label**. **Defect (B) — Poppins covering 0/98 Cyrillic code points — moved out to `T-0472`.**
> The filename still says `-and-poppins-cyrillic`; that is deliberate (link stability across ~20
> cross-references), not a leftover. **If you are here for the font, go to T-0472.**

## Context

The customer profile hero carries an "Edit profile" chip. In `ru` it renders **"Редактиров…"** — the
label is far wider than the chip, which is capped at `0.45 × width`
(`customer-app/.../features/profile/ProfileTab.kt:248`, applied at `:269`) to stop it starving the
name column. T-0442 shipped that cap and said in the code that it converts an overflow into a
truncation; the real fix is a shorter string, and a shorter Russian/Ukrainian string needed a native
speaker rather than a PM's guess. That was **Q-I18N-02**, `blocking: yes`.

### The owner answered it on 2026-08-01. Verbatim:

> *"the ios and android apps have 'Edit profile'. And when translated then it's a long one. I want
> just to keep 'Edit'/'Редактировать' and truncate it if it doesn't fit by the whole length."*

**Two rulings, and they are separate:**

1. **The label is the verb alone.** `Edit` / `Редактировать`, and the equivalent verb in `cs`, `sk`
   and `uk`. The noun is dropped.
2. **Overflow is handled by TRUNCATION** — not by wrapping to a second line, not by shrinking the
   type. This is live, not theoretical: `Редактировать` is **13** characters against `Edit`'s **4**,
   so the verb alone may still exceed the cap at 320dp.

### Ground truth, re-measured by the PM on `f649c3bd` (2026-08-01)

Do not work from the T-0442-era figures; these are the current call sites.

| Platform | Call site | State today |
|---|---|---|
| **Android** | `customer-app/.../features/profile/ProfileTab.kt:339-346` | `Text(stringResource(R.string.profile_row_edit), style = labelLarge, color = White, maxLines = 1, overflow = TextOverflow.Ellipsis)` — **a tail ellipsis is already in force.** Android needs only the string. |
| **Android** | `ProfileTab.kt:248` / `:269` | `EditChipMaxWidthFraction = 0.45f`, applied as `maxWidth * EditChipMaxWidthFraction`. **Keep it.** |
| **iOS** | `CleansiaCustomer/.../Profile/ProfileTab.swift:332-350` (`EditProfileChip`) | `Text(L10n.Profile.rowEditProfile).font(CleansiaTypography.labelLarge)` — **no `.lineLimit`, no `.truncationMode`, no width cap.** The long label **wraps the capsule to two lines** rather than truncating. iOS needs the string **and** an explicit one-line + truncation policy. |

So the two platforms need **different work** from the same decision: Android is a resource change,
iOS is a resource change **plus** a two-modifier layout change. That asymmetry is the whole reason
AC4 exists separately from AC3.

### The strings — derived, not translated

The four non-English forms are the **leading verb already present in the shipped long string**:

| Locale | Today | Verb-only form |
|---|---|---|
| `en` | `Edit profile` | `Edit` *(owner-given)* |
| `ru` | `Редактировать профиль` | `Редактировать` *(owner-given)* |
| `uk` | `Редагувати профіль` | `Редагувати` |
| `cs` | `Upravit profil` | `Upravit` |
| `sk` | `Upraviť profil` | `Upraviť` |

**This is a derivation from strings a native speaker already approved, not a new translation.** Do
**not** run any of these through a machine translator to "check" them — that is how the wrong word
gets substituted for the right one. If you believe a locale needs a different verb, that is a **new
question in `questions/open.md`**, not a silent edit.

## Acceptance criteria

- [ ] **AC1** — Given the five locale bundles for **both** customer apps, When `profile_row_edit` is
      read, Then each holds the **verb-only** form from the table above, byte-clean (no BOM, no
      mojibake, `uk` keeps the typographic apostrophe **U+2019** wherever it appears in neighbouring
      strings — see the carry-forward below). Evidence: the five before/after pairs quoted in
      `## Review`, per platform, plus an explicit statement that no machine translation was used.
- [ ] **AC2** — Given Q-I18N-02, When this ticket is reviewed, Then the owner's answer is quoted
      **verbatim** in `## Review` as the sign-off for the wording. *(This replaces the original AC2's
      "needs a native speaker" gate — the gate is satisfied by the answer, not by a fresh sign-off.)*
- [ ] **AC3 (Android)** — Given locale `ru`, and separately `uk`, When the profile hero renders at
      **320dp**, Then the chip is **one line**; the name column still measures **≥ 56dp**; and if the
      verb-only label *still* exceeds the `0.45` cap it **truncates with a tail ellipsis** — it does not
      wrap, does not shrink the type, and does not widen past the cap. Evidence: screenshots at 320dp
      in both locales, **plus the re-measured label width against the `EditChipMaxWidthFraction` band**
      recorded in `## Review`. **`EditChipMaxWidthFraction` is not deleted** — a shorter label makes it
      non-binding, not unnecessary.
- [ ] **AC4 (iOS) — the truncation policy is DECIDED AND NAMED, not inherited.** Given locale `ru`, and
      separately `uk`, When `EditProfileChip` renders, Then it is **one line** and its overflow behaviour
      is set by an **explicit** `.lineLimit(1)` **and** an **explicit** `.truncationMode(…)` at the call
      site. The chosen mode is **named in `## Review` with the reason**. **Default, if nothing argues
      otherwise: `.tail`** — it matches Android's already-shipped `TextOverflow.Ellipsis`, and
      cross-platform divergence in *where the ellipsis lands* is a parity defect. **Relying on SwiftUI's
      unstated default fails this AC**, even if it happens to produce the right pixels. Evidence:
      simulator screenshots in both locales, **before and after**.
- [ ] **AC5 — the accessibility label under truncation is VERIFIED, not assumed.** Given the label is
      visually truncated on either platform, When TalkBack / VoiceOver reads the chip, Then it announces
      the **complete** verb-only string, not the truncated glyph run. **Execute the read** (TalkBack /
      VoiceOver, or a semantics/`accessibilityLabel` assertion) — do not reason from "the framework
      probably does this". If the platform *does* guarantee it, **name the mechanism and cite it**
      (Compose's `text` semantics property / SwiftUI's `Text` accessibility value) so the next reader
      does not re-derive it. Evidence: the executed check, per platform, in `## Review`.
- [ ] **AC6 — the surface scope is RECORDED, not inferred.** Three keys carry this wording today and
      only one of them is width-constrained:
      | Key | Surface | Width-constrained? |
      |---|---|---|
      | `profile_row_edit` | the customer profile hero **chip** (Android + iOS) | **yes** — this is the defect |
      | `profile_edit_title` | the customer **edit-profile screen title** (Android + iOS) | no |
      | `edit_profile` | the **partner** Android app | not measured |
      **PM default: change `profile_row_edit` only.** The owner's complaint was truncation, and only the
      chip truncates; a screen header that reads "Edit" is a different (and arguably worse) change than
      the one asked for. **State in `## Review` which keys were changed and why** — a silent change to
      the screen title fails this AC exactly as much as a silent skip. If the implementer thinks the
      title should follow, that is a one-line question back to the owner, not a decision to make here.
- [ ] **AC7 (Gate 0.5)** — Android: the suite is re-run **un-cached** (`--rerun-tasks`) with task
      outcomes recorded (`UP-TO-DATE` is a non-run). iOS: `xcodebuild build test` for `CleansiaCustomer`
      plus SwiftFormat `--lint` / SwiftLint `--strict`, or an honest leg-3 declaration of what could not
      run and why. **Leg 1:** the evidence for AC3/AC4 is *screenshots*, so leg 1 does not apply to them
      — say so under leg 3 rather than inventing a mutation. If you add any executable assertion (a
      resource guard, a semantics test for AC5), **mutation-prove that one** and name it.

## Out of scope

- **Poppins and Cyrillic coverage — `T-0472`.** Every `ru`/`uk` glyph in the hero still renders in a
  system fallback face after this ticket, because Poppins covers 0 of 98 Cyrillic code points. **That
  is not a regression this ticket introduces and not a defect this ticket fixes.** Do not touch
  `Type.kt`, the font binaries, or any `fontFamily =` call site here.
- **Widening or removing `EditChipMaxWidthFraction`.** It guards the name column.
- **The screen title and the partner label**, unless AC6's record says otherwise.
- **Web** (`Cleansia.App`). Not affected — the web profile has no width-capped chip of this shape.
- **The avatar image itself** — T-0448 / T-0449. This ticket runs *first* on the same files.

## Implementation notes

**Panel: NONE — and this is a ruling, not an omission.** This ticket previously required an analyst
panel. That panel existed for exactly one purpose: to produce a defensible answer to *"what is the
right shorter `ru`/`uk` wording"*. **The owner answered it directly, and an owner decision outranks a
panel** (`process/deliberation.md` — a panel converts individual judgement into
surviving-the-best-objections judgement *where no decision has been made*; it is not a ratification
step for the owner). The residual choices — truncation mode, accessibility text, surface scope — are
written as **AC4, AC5 and AC6** precisely so they are *decided and recorded* rather than invented
silently. If the implementer hits a genuine language question, file it in `questions/open.md`.

**Fan-out: two developer instances in parallel, one reviewer each.** The Android leg and the iOS leg
touch **disjoint files**, so they do not serialize against each other:
- Android: `customer-app/src/main/res/values{,-cs,-ru,-sk,-uk}/strings.xml`
- iOS: `CleansiaCustomer/Resources/Localizable.xcstrings` + `.../Profile/ProfileTab.swift`

**Shared-file lanes — this ticket is the current head on all four:**
- Android `values-*/strings.xml` ×5 → T-0441 ✅ merged → **T-0450** → T-0448
- Android `.../profile/ProfileTab.kt` → T-0442 ✅ → **T-0450** → T-0448 → T-0453 *(this ticket may not
  need to touch `ProfileTab.kt` at all — the ellipsis is already there. If it doesn't, say so; a lane
  head that writes nothing releases the lane sooner.)*
- iOS `Localizable.xcstrings` → T-0440 ✅ merged → **T-0450** → T-0449
- iOS `.../Profile/ProfileTab.swift` → T-0451 ✅ → **T-0450** → T-0449

**Keep `SWIFT_EMIT_LOC_STRINGS: NO` in force** — do not let an xcstrings re-sync produce a 33k-line
churn (the T-0372 lesson).

**Never read or modify `src/cleansia_ios/**/Info.plist` or `**/project.yml`** — the owner's live Stripe
key lives in the working copies. Nothing in this ticket needs either file.

## ⛔ Two carry-forwards this ticket must not undo

- **`uk` uses the typographic apostrophe U+2019** (`необов’язково`), not ASCII `'`. The shipped iOS form
  is the correct one. If you are diffing locale files and see `’`, **leave it alone.**
- **Do NOT apply a "hint no longer than its sibling" constraint to iOS.** The T-0440 reviewer
  **refuted** it: Android's float label ellipsizes, which is where the rule came from; iOS's hint is
  plain wrapping text with no line limit in a container with ample headroom. **It is not in this ticket
  today** (PM-verified 2026-08-01) — this note is prevention, not removal. Do not add it, and do not let
  a reviewer request it by analogy with Android.

Related, and also not a defect: the **ru/uk two-line placeholder** on iOS. Whether it reads as
*intentional* beside the sibling's one line is a **QA judgement on a real device**, open under T-0440 —
not a constraint to design against here.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, from T-0442's implementation and
  review; two panels required, plus two owner questions Q-I18N-02 and Q-BRAND-01)
- 2026-08-01 — **ALL FOUR of this ticket's lane heads have MERGED.** PM-verified on `master` at
  `1c8fdd00`: T-0441 (`1d85b35f` #178) · T-0442 (`ce2416a0` #174) · T-0440 (`a10e1f88` #179) ·
  T-0451 (`1c8fdd00` #180). *(T-0440 and T-0441 are still `qa` on owed screenshots. A screenshot does
  not gate a code lane; what this ticket waited on was their **writes**, and those are on `master`.)*
- 2026-08-01 — stayed `draft` on **Q-I18N-02**, an unanswered `blocking: yes` owner question with no
  PM default, deliberately.
- 2026-08-01 — split recommendation recorded: half (B) needs an architect panel and blocks nothing.
- 2026-08-01 — **Q-I18N-02 ANSWERED by the owner. This ticket is SPLIT and moves `draft` → `ready`.**
  Five changes, each deliberate:

  | # | Change | Why |
  |---|---|---|
  | 1 | **Half (B) — Poppins Cyrillic — moved OUT to `T-0472`** | It is untouched by the answer (a shorter Russian string still falls back to a system face), it needs an architect panel + the unanswered `Q-BRAND-01`, and **it blocks nothing downstream**. Leaving it here would keep T-0448/T-0449 waiting on a brand decision they do not need. |
  | 2 | **`size` M → S** | One resource change ×5 ×2 platforms plus two SwiftUI modifiers. |
  | 3 | **`layers` `[analyst, architect, android, ios]` → `[android, ios]`; `owner` `analyst` → `android`** | The analyst panel is discharged by the owner's answer; the architect panel went to T-0472. |
  | 4 | **AC rewritten** — old AC1/AC2 replaced, old AC4/AC5/AC6 moved to T-0472, **new AC4/AC5/AC6 added** | The owner explicitly did **not** decide the truncation mode, the accessibility text, or the surface scope. They are written as ACs so a developer must decide-and-record rather than invent. |
  | 5 | **`depends_on: [T-0440, T-0441, T-0451]` → `[]`** | **This is a discharge, not a drop — read the next entry before questioning it.** |

- 2026-08-01 — **`depends_on` DISCHARGED, and the reasoning is on the record because it looks like a
  dropped dependency and is not.** All three were **lane dependencies, not logical ones** — this
  ticket's own `## Implementation notes` said so from the day it was created (*"those are lane
  dependencies, not logical ones"*). A lane dependency is satisfied when **the head's write lands**,
  and all four writes are on `master`:

  | Was | Lane it held | Write on `master` |
  |---|---|---|
  | T-0440 | iOS `Localizable.xcstrings` | ✅ `a10e1f88` (#179) |
  | T-0441 | Android `values-*/strings.xml` ×5 | ✅ `1d85b35f` (#178) |
  | T-0451 | iOS `ProfileTab.swift` | ✅ `1c8fdd00` (#180) — ticket `done` |
  | *(T-0442)* | Android `ProfileTab.kt` | ✅ `ce2416a0` (#174) — ticket `done`, never listed |

  **T-0440 and T-0441 are still `qa`**, each owing an AC screenshot. Keeping them in `depends_on` would
  have made this ticket — and the entire avatar chain behind it — un-`ready` **on a screenshot**, which
  is the failure the sprint doc warned about at `status/sprint-14.md` §8.2. The prior pass already ruled
  *"a screenshot does not gate a code lane"*; this is that ruling applied.
- 2026-08-01 — **`ready`.** DoR re-checked item by item: **(1)** not a duplicate — the split half lives
  in T-0472 and nothing else in `INDEX.md` or `backlog/audits/` covers the chip label; **(2)** AC are
  observable, and the three that used to be inventable are now forced-and-recorded (AC4/AC5/AC6);
  **(3)** sized **S**, no split needed; **(4)** dependencies discharged above; **(5)** `manual_steps`
  none — no DTO, no endpoint, no schema, no generated client; **(6)** `security_touching: false`,
  `layers: [android, ios]`.

## Review
<!-- reviewer writes verdict here. Required by AC: the verbatim Q-I18N-02 quote (AC2), the
     re-measured Android width vs the 0.45 band (AC3), the named iOS truncation mode + reason (AC4),
     the executed accessibility read per platform (AC5), and the list of keys changed (AC6). -->
