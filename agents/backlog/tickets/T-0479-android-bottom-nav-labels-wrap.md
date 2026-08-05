---
id: T-0479
title: Android bottom-nav labels wrap to two lines instead of truncating (customer + partner)
status: draft
size: S
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #3 (2026-08-02):** *"Bottom nav bar text wraps — must truncate when too long, iOS +
Android."* This is the **Android half**; iOS is **T-0480** and needs a mechanism decision Android does
not.

### Ground truth — PM-verified on `master` at `0e4ede1b`

**Customer app**, `features/main/MainShell.kt:445-449`:

```kotlin
Text(
    stringResource(labelRes),
    style = MaterialTheme.typography.labelSmall.copy(fontWeight = ...),
    color = color,
)
```

**No `maxLines`. No `overflow`. No `softWrap = false`.** It sits in a `Column` inside a
`Row(horizontalArrangement = Arrangement.SpaceEvenly)` on a pill of fixed **64.dp** height
(`:385`), with the centre 72.dp consumed by a spacer for the Book FAB (`:399`). Four labels plus a
72.dp hole in a screen-width pill leaves each slot roughly a quarter of the remaining width, so a
long label has nowhere to go but a second line — and the pill's height is fixed, so the second line
**overflows the pill**.

**The labels that trigger it are shipped today** (`values-uk/strings.xml:70,72`):

| key | en | uk | ru |
|---|---|---|---|
| `nav_orders` | Orders (6) | **Замовлення (10)** | Заказы (6) |
| `nav_rewards` | Rewards (7) | **Винагороди (10)** | Награды (7) |
| `nav_home` | Home (4) | Головна (7) | Главная (7) |
| `nav_profile` | Profile (7) | Профіль (7) | Профиль (7) |

**Partner app** has the same shape at `features/main/FloatingIslandBottomBar.kt:86` — its own private
`NavSlot`, with the header comment at `:54` stating *"Matches customer-app's `CustomBottomBar`
exactly."* **PM-checked: it is the same duplicated composable.** Fixing one and not the other leaves
the identical bug in the identical code under a different package name.

**The owner's remedy is already decided and does not need a panel:** truncate. It is the same ruling
the owner gave for the profile chip in `Q-I18N-02` (*"truncate it if it doesn't fit"*), which
T-0450 is implementing on `ProfileTab.kt`. This ticket applies the ruling the owner already made.

## Acceptance criteria

- [ ] **AC1 — both `NavSlot` labels truncate.** Given `uk` at 320dp width, When the bottom bar
      renders, Then `nav_orders` and `nav_rewards` show on **exactly one line**, ellipsized, in
      **both** the customer app (`MainShell.kt:445`) and the partner app
      (`FloatingIslandBottomBar.kt` `NavSlot`). `maxLines` and `overflow` are set **explicitly** —
      relying on a default fails this AC even if the pixels come out right (same standard as T-0450
      AC4). Evidence: `uk` screenshots of both apps' bars at 320dp.
- [ ] **AC2 — the pill height is not breached.** Given the fixed `.height(64.dp)` at
      `MainShell.kt:385`, When any label truncates, Then no glyph, indicator dot or descender is
      clipped by the pill edge and the four slots stay on one baseline. Evidence: the same
      screenshots, with the pill boundary visible.
- [ ] **AC3 — the accessibility label announces the FULL string.** When a label is visually
      truncated, TalkBack announces the complete label. **Verified by executing the read**, not
      asserted. Evidence: the TalkBack transcript or the `contentDescription`/semantics at file:line
      with the mechanism named.
- [ ] **AC4 — truncation mode is named with a reason.** Tail ellipsis is the default and matches
      T-0450's Android behaviour (`ProfileTab.kt:339-346`, `TextOverflow.Ellipsis`). If something
      else is chosen, say why. Evidence: the stated choice in `## Review`.
- [ ] **AC5 — the two duplicated `NavSlot`s are recorded, not silently reconciled.** Both are fixed.
      Whether they should be **one** shared `:core` component is stated as a recommendation in
      `## Review` with a proposed ticket id — **and not done here.** Hoisting a component into
      `:core` mid-lane is the T-0277↔T-0278 serial-lane class. Evidence: the recommendation, or an
      argued "no".
- [ ] **AC6 — a test that goes red against the current code (Gate 0.5 leg 1).** The repo's own idiom
      for this situation is a source-reading guard (`NotificationsScreenTogglesTest.kt:17-21`,
      cited as precedent by T-0473's Android leg). Prove it fails when `maxLines`/`overflow` are
      removed. Evidence: the red run, then green.
- [ ] **AC7 (Gate 0.5)** — `:customer-app` **and** `:partner-app` compile + `testDebugUnitTest`
      **un-cached** (`--rerun-tasks --no-build-cache`), task outcomes recorded for both.

## Out of scope

- **iOS** — **T-0480**. Its mechanism is genuinely different (stock `TabView`), which is why it is
  a separate ticket rather than a second lane on this one.
- **Shortening any `nav_*` string.** The owner asked for truncation, not a relabel. Changing a label
  reopens the `values-*/strings.xml` lane for no reason. **Zero i18n churn is expected in this diff.**
- **Hoisting `NavSlot` into `:core`.** AC5 recommends; it does not implement.
- **The Book FAB and the 72.dp centre spacer.** Untouched.

## Implementation notes

**No panel — one-line "no-decision" note:** the behaviour was decided by the owner (truncate, don't
wrap) and ratified for this exact class in `Q-I18N-02`. This applies it to two more call sites. Two
`maxLines`/`overflow` pairs and a guard test.

**Fan-out: ONE developer instance.** Both files are Android and both edits are three lines; splitting
them across two instances buys nothing and risks divergent choices on AC4.

**Shared-file lanes:** `MainShell.kt` and `FloatingIslandBottomBar.kt` have **no other sprint-15
claimant** (PM-checked against the lane list). Neither is in the `ProfileTab.kt` pile-up.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #3).** The missing `maxLines`/`overflow`
  at `MainShell.kt:445-449`, the fixed 64.dp pill, the four shipped `uk` labels at 10 characters, and
  the duplicated partner `NavSlot` were all PM-verified at `0e4ede1b`.
- 2026-08-02 — **implemented (android)** on `fix/PR-B-android-nav-and-invoice-back` (shared with
  T-0490; disjoint files). Red→green recorded below. **The ticket's premise needed one correction:
  `maxLines`/`overflow` alone would have shipped a worse bug than the wrap** — see AC1.
- 2026-08-05 — **merged in `bd520b15` (#186) and independently re-verified by a second android
  instance.** Both mutations re-run from scratch: deleting `maxLines = 1` from `MainShell.kt` reddens
  only *the nav label renders on one line with a tail ellipsis* (3 tests, 1 failed); replacing
  `Modifier.weight(1f)` with `Modifier` in `FloatingIslandBottomBar.kt` reddens only *every nav slot
  takes an equal share of the pill* (3 tests, 1 failed). Both restored byte-exact, and the md5s match
  the ones this ticket recorded three days ago (`18fb28e318c8ccd0638e1ec4981aab99`,
  `cd4d97833535bfcc82a0b92e86c29f85`) — so the files have not drifted since. **AC1/AC2/AC3 remain
  open on their screenshot/TalkBack evidence**, which still needs a device.

## Review — android (2026-08-02)

### The correction: `maxLines = 1` on its own makes the last tab disappear

A `Row` measures each **unweighted** child against `mainAxisMax - (what earlier siblings took)`, so the
slots are served greedily left to right and the last one is handed the remainder. Walking the customer
bar in `uk` at 411dp (Pixel-class — the owner's screenshot), pill content = 411−32−16 = **363dp**:

| child | width taken | remaining |
|---|---|---|
| Home "Головна" | 53.1 + 16 pad = 69.1 | 293.9 |
| Orders "Замовлення" | 77.6 + 16 = 93.6 | 200.3 |
| FAB spacer | 72.0 | 128.3 |
| Rewards "Винагороди" | 77.2 + 16 = 93.2 | **35.1** |
| Profile "Профіль" (needs 70.9) | gets 35.1 → 19.1dp of text | 0 |

19dp of text is where "Про філь" comes from — it is not that the label is long, it is that Profile is
measured **last, out of scraps**. Add only `maxLines = 1` and that 19dp becomes `"Про…"`; at 320dp the
same walk hands Profile **0dp**, so the column collapses and takes its **icon** with it. A wrapped tab
is at least visible. **So the weight is not a nicety here, it is the fix**; `maxLines`/`overflow` is
what stops the second line once the budgets are equal. (Widths measured from the shipped
`core/res/font/nunito_bold.ttf` at the real `labelSmall` metrics — Nunito **Bold 12sp, letterSpacing
0.6sp** per `Type.kt:113` — not estimated.)

**AC1 — both `NavSlot` labels truncate.** `maxLines = 1` + `overflow = TextOverflow.Ellipsis` set
**explicitly** at `MainShell.kt:450-451` and `FloatingIslandBottomBar.kt:131-132`; every call site now
passes `Modifier.weight(1f)` (`MainShell.kt:399,400,403,404`, `FloatingIslandBottomBar.kt:83`).
**Screenshots are UNVERIFIED — there is no emulator or device in this environment and nothing rendered
was observed.** In place of a screenshot the ticket gets the measured budget table below, plus
`@Preview(widthDp = 320, locale = "uk"/"ru")` on both bars (`MainShell.kt:500-505`,
`FloatingIslandBottomBar.kt:151-158`) so the narrow-worst-case render is one click away in the IDE and
cannot be lost.

What each label actually shows once the slots are equal (320dp = the AC's bar, 360dp = the common
modern phone):

| app | 320dp / uk | 360dp / uk | 360dp / en |
|---|---|---|---|
| customer | Голо… · Замо… · Вина… · Про… | Голов… · Замов… · Винаг… · Профі… | Home · Orders · Rewar… · Profile |
| partner | Панель… · Замовл… · Рахунки · Профіль | Панель к… · Замовле… · Рахунки · Профіль | all four fit |

**One argued deviation, called out because it is not in the ticket.** The slot's own
`padding(horizontal = 8.dp)` is now `4.dp` in both files. With `weight(1f)` the slot width is fixed and
that padding is spent **out of** the text budget, which at 320dp is the binding constraint: 8dp per
side leaves 34dp and truncates **English** to `"Ho…"` / `"Ord…"` / `"Re…"`; 4dp leaves 42dp and English
fits (bar `Rewards`). It still leaves an 8dp gutter between adjacent labels. This is inside the same
composable as the required change and reverses a regression the required change would otherwise cause
— but it is a visual delta, so flagging it rather than burying it.

**AC2 — the pill height is not breached.** One line at `labelSmall` lineHeight 16sp inside a slot of
24 (icon) + 2 + 16 (label) + 3 + 3 (dot) + 12 (vertical padding) = **60dp** in a 64dp pill; nothing can
now add a second line, which was the only way to exceed it. `textAlign = TextAlign.Center` was added
with the pair for a reason that is easy to miss: an **ellipsized** `Text` lays out to its full width
constraint, so a `Start`-aligned truncated label would sit visibly left of its centred icon while the
short ones stayed centred. Baseline alignment across the four slots is unchanged — they are siblings in
a `CenterVertically` row of identical construction. **Visually UNVERIFIED** (no device).

**AC3 — the accessibility label announces the FULL string.** Mechanism, named at file:line:
`Modifier.clickable` wraps its node in `semantics(mergeDescendants = true)`, so the slot's merged label
is the child `Text`'s semantics `text` — and Compose puts the `AnnotatedString` it was **handed** into
the semantics tree, not the laid-out ellipsized line. `maxLines`/`overflow` are paint-time only.
Call sites: `MainShell.kt:437` (clickable) → `:447` (`stringResource(labelRes)`);
`FloatingIslandBottomBar.kt:111` → `:126`. **This is NOT the executed TalkBack read the AC asks
for — there is no emulator or device here, so no transcript exists.** What *is* enforced is the
regression that would break it: `the label string reaches the Text whole so TalkBack still reads it`
fails if the label stops being a bare `stringResource` or acquires a Kotlin-side
`.take(`/`.substring(`/`.dropLast(`. **AC3 stays open pending a TalkBack pass on a device.**

**AC4 — truncation mode.** Tail ellipsis (`TextOverflow.Ellipsis`), matching T-0450's
`ProfileTab.kt:339-346`. No reason to diverge: these labels are front-loaded (the distinguishing
morpheme is at the start in all five locales — Замов/Винаг/Голов/Профі), so a head or middle ellipsis
would delete exactly the part that identifies the tab.

**AC5 — the two duplicated `NavSlot`s.** Both fixed, **not** reconciled. **Recommendation: hoist one
`CleansiaNavSlot` into `:core` — proposed `T-0491`.** The two are now not merely similar but
*character-identical* in the parts that matter (same `Column`+`Icon`+`Text`+dot, same 24/2/16/3/3
metrics, same `animateDpAsState` 20dp/200ms dot, and after this change the same
`maxLines`/`overflow`/`textAlign`/`weight`/`4.dp`), and this ticket is the **second** time one defect
had to be fixed twice. The bars differ only in slot count and the customer's FAB hole, both of which
are the *parent's* business. **Not done here** — `:core` is the serialized lane (T-0277↔T-0278) and
hoisting mid-lane is exactly the collision that rule exists to prevent.

**Second recommendation, i18n, NOT done (Out-of-scope says zero i18n churn).** The partner
`dashboard` label is the one label that truncation cannot rescue at any supported width: `values-uk`
**"Панель керування" (16 chars)** and `values-ru` **"Панель управления" (17)** render as `"Панель…"`
at 320dp and `"Панель к…"` even at 411dp. It is at least not ambiguous — "Панель" is a real word and no
sibling tab starts with it — so this ships as *readable*, not as the `"Про…"` failure mode the fix was
meant to prevent. But a 16-character label in a 68dp slot is a labelling problem, not a layout one, and
shortening uk/ru to **"Панель"** (or "Огляд"/"Обзор") would let it fit outright. **Owner call** — the
owner asked for truncation, not a relabel. Customer `uk` "Замовлення"/"Винагороди" (10 each) truncate
to a 5-character stem at 360dp, which is legible; no recommendation there.

**AC6 — a test that goes red against the current code.** Two new source-reading guards on the cited
precedent, one per app (3 tests each):
`customer-app/src/test/java/cz/cleansia/customer/features/main/BottomNavLabelTruncationTest.kt`,
`partner-app/src/test/java/cz/cleansia/partner/features/main/BottomNavLabelTruncationTest.kt`.
Against the pre-fix files: customer **3 completed, 2 failed**; partner **3 completed, 2 failed** (the
third, the TalkBack-whole-string guard, correctly passed before *and* after — it is a regression
guard, not a red-first assertion, and is reported as such rather than dressed up).
Named mutations (Gate 0.5 leg 1), one per app, each reddening exactly one test and no other:
- deleting `maxLines = 1` from `MainShell.kt` → **`the nav label renders on one line with a tail
  ellipsis`**, 1 failed pre-restore / 0 after.
- deleting `modifier = Modifier.weight(1f)` from `FloatingIslandBottomBar.kt` → **`every nav slot takes
  an equal share of the pill`**, 1 failed pre-restore / 0 after.

Restores confirmed **byte-exact** by md5 (`18fb28e318c8ccd0638e1ec4981aab99` MainShell.kt,
`cd4d97833535bfcc82a0b92e86c29f85` FloatingIslandBottomBar.kt — identical before and after).

**AC7 (Gate 0.5) — both modules, un-cached.**
`./gradlew :customer-app:compileDebugKotlin :customer-app:testDebugUnitTest :partner-app:compileDebugKotlin :partner-app:testDebugUnitTest --rerun-tasks --no-build-cache --no-daemon`
→ **BUILD SUCCESSFUL**, exit 0, **88 actionable tasks: 88 executed** — zero `FROM-CACHE`; the only
`UP-TO-DATE` lines are the actionless `pre*Build` lifecycle anchors. `:customer-app` **358 tests,
0 failed, 0 skipped**; `:partner-app` **185 tests, 0 failed, 0 skipped** (JUnit XML, not the console
tail). `check-consistency.mjs` → **22 violations = the master baseline of 22**, none in a touched file.
Encoding: every touched file `utf-8`, no BOM.

**Zero i18n churn**, as required — no `values-*/strings.xml` was opened.

**Harvested back** (charter: fold the reusable idiom into the catalog in the same change):
`agents/knowledge/patterns-mobile.md`, new note in *Shared UI & theme* — *"A `Row` of fixed-count
labels — truncating needs `weight`, not just `maxLines`"* — carrying the greedy-measure trap, the
required modifier set, the "budget it at the narrowest width × longest locale, padding comes out of the
budget" rule, the semantics-are-unaffected fact, and the explicit note that **iOS does not inherit
this** because `.tabItem` is UIKit-rendered (T-0480). This is a gotcha clarification, not a new "one
way to do X"; the `:core` component question is left to the Architect via the T-0491 recommendation
above.

**UNVERIFIED-LOCALLY:** the AC1/AC2 `uk` screenshots at 320dp and the AC3 TalkBack transcript. No
emulator or device exists in this environment — **nothing rendered was observed**, and the `@Preview`s
are an IDE affordance, not evidence that anything was seen. AC3 in particular is argued from the
Compose semantics contract plus a regression guard, **not** from an executed screen-reader pass.
