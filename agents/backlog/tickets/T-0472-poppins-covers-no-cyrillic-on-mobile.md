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

<!-- architect panel verdict goes here -->

### Android lane — implemented 2026-08-07 (android)

Built under the owner's outcome ruling ("until a user can read the text and no problems occur"):
legible Cyrillic via a fallback to the already-bundled Nunito. **No font binary was added, replaced,
re-subset or regenerated**, so AC5 (licence) and AC6 (binary parity) are **not engaged** — the six
sha1s below are unchanged from the ticket's own record.

**Mechanism: `AndroidFont` + `Typeface.CustomFallbackBuilder`** (`core/…/ui/theme/GlyphFallbackFont.kt`).
`Poppins` stays one `FontFamily`; each of its three entries is now a `GlyphFallbackFont` naming a
Poppins primary and its Nunito counterpart. Every existing `fontFamily = Poppins` call site is
unchanged.

**AC3 option (a) is confirmed non-viable — this is the finding the panel was told to press on.**
A second `Font(...)` inside the family gives **per-run** behaviour, never per-glyph. Established by
reading the shipped `compose-ui-text 1.7.8` bytecode (`ui-text-release.aar`, BOM `2025.02.00`), not
from docs:

- `FontMatcher.matchFont(FontListFontFamily, FontWeight, FontStyle) → List<Font>` delegates to
  `filterByClosestWeight(...)`. Weight and style are its only inputs; no cmap/coverage is read.
- `FontListFontFamilyTypefaceAdapterKt.firstImmediatelyAvailable(...)` walks that list and returns on
  the **first font that loads** (`PlatformFontLoader.loadBlocking`; the failure path throws
  `"Unable to load font"`). The list is a *load-failure* fallback, not a coverage fallback.
- `TypefaceResult.Immutable` holds **one** `Object`. One (family, weight, style) resolves to exactly
  one `android.graphics.Typeface` for the whole run, so a Latin heading containing one Russian word
  would flip **entirely** to the fallback face.

Per-glyph substitution lives one level down, in the platform typeface's own family chain.
`Typeface.CustomFallbackBuilder` (API 29+) is the only app-level way to write that chain; Minikin
then itemizes **per glyph** across [Poppins → Nunito → system sans-serif] — the default system
fallback is retained, so emoji/CJK behaviour is unchanged. Compose reaches it because
`AndroidFontLoader.loadBlocking` dispatches `AndroidFont` to `font.typefaceLoader.loadBlocking(context, font)`
(verified in the same bytecode), which is public API and keeps `Poppins` a top-level `val`.

Also rejected: an **XML `<font-family>` resource** — `Typeface.createFromResources` folds every
`<font>` entry into **one** native family whose coverage is computed from a single representative
face, then appends only the *system* fallback; and a **per-locale family swap**, which is per-run by
construction and is AC3 option (b).

**Known limit — API 26–28 (minSdk is 26).** `CustomFallbackBuilder` is API 29. Below it the primary
loads exactly as today and uncovered glyphs still come from the system face: readable (Roboto has
full Cyrillic), off-brand, **unchanged from before** — no regression, no crash. The API-gated code is
in its own methods so ART's per-method verifier never touches them on older devices; R8 additionally
outlines them (`GlyphFallbackTypefaceLoader$$InternalSyntheticApiModelOutline$*` in
`mapping/release/usage.txt`). Closing the 26–28 gap requires a merged binary — out of scope here and
owner-gated by Q-BRAND-01.

**AC4 triage — the ticket's slot list was wrong and is re-derived.** The ticket names 6 hard-coded
call sites; there are **36**, in 24 files (4 in `:core`, 32 in `customer-app`, 0 in `partner-app`),
plus the 6 type-scale slots — 42 occurrences of `fontFamily = Poppins`. Two of the six line numbers
it gives have moved (`ProfileTab.kt:437 → :451`, `EditProfileScreen.kt:215 → :272`, and that file has
a second site at `:120`). **All 42 are fixed, none are deferred, and no call site was edited** —
every one reads the `Poppins` val.

| Surface | Count | Status |
|---|---|---|
| Type-scale slots `displayLarge/Medium/Small`, `headlineLarge/Medium/Small` (`Type.kt:44,48,56,60,64,68`) | 6 | fixed via `Poppins` |
| `:core` call sites — `CleansiaErrorState.kt:73`, `CodeInput.kt:97`, `WordmarkSplash.kt:87,135` | 4 | fixed via `Poppins` |
| `customer-app` call sites — `AddressManagerScreen.kt:254,849`, `BookingBottomSheet.kt:487`, `CleansiaBrandWordmark.kt:38`, `DeleteAccountScreen.kt:95`, `DevicesScreen.kt:119`, `DisputeDetailScreen.kt:218,373,836`, `DisputesListScreen.kt:108,372`, `EditProfileScreen.kt:120,272`, `HelpSupportScreen.kt:77`, `HomeTab.kt:661,1271`, `LanguageScreen.kt:79`, `NotificationsScreen.kt:93`, `OrderDetailHeroAndAddress.kt:70,83`, `OrderPhotosScreen.kt:87,146`, `OrdersTab.kt:159,645`, `AppearanceScreen.kt:82`, `ProfileOnboardingScreen.kt:176`, `ProfileTab.kt:451`, `RewardsActivityScreen.kt:84`, `RewardsTab.kt:181,335,353`, `SecurityScreen.kt:74` | 32 | fixed via `Poppins` |
| `partner-app` | 0 | none exist |
| `FontFamily.Monospace` — `RewardsTab.kt:901`, `InvoiceDetailScreen.kt:568` | 2 | out of scope: a system generic, already Cyrillic-capable via the system chain |

**The shared-file-lane hazard the ticket warns about does not arise.** The fix is confined to `:core`
(`Type.kt`, the new `GlyphFallbackFont.kt`, `build.gradle.kts`, tests). `ProfileTab.kt` and
`EditProfileScreen.kt` are **not touched**, so T-0472 no longer needs to sequence after T-0448/T-0453.

**AC1 evidence is executable, so leg 1 applies, not leg 3.** The profile hero (`ProfileTab.kt:305`,
`headlineSmall`) is covered by the type-scale sweep; a resolved-typeface assertion is impossible
off-device, so what is asserted is the data the renderer is handed plus the coverage premise.

**AC7 / Gate 0.5.** `--rerun-tasks`, `EXIT` captured before any pipe, counts from the JUnit XML,
`testDebugUnitTest` executed (not `UP-TO-DATE`/`FROM-CACHE`) in all three modules.
`:core` **165 → 171** (+3 `BundledFontCoverageTest`, +3 `CyrillicFallbackTest`; `CleansiaTypographyTest`
stays 4 — its hand-listed 15-slot roster was replaced by the same reflection the new sweep uses).
`:partner-app` **237 → 237**, `:customer-app` **571 → 571**. `assembleDebug` green for both apps;
`minifyReleaseWithR8` green for both, and `mapping/release/resources.txt` shows all six fonts
`reachable=true` under `isShrinkResources`.

**Leg 5 — bundle size: 0 bytes.** No font added; the fallback reuses `nunito_*`, already packaged.

**A real gap found and closed:** `:core:testDebugUnitTest` did not treat `core/src/main/res/font/**`
as an input, so a swapped TTF left the task cacheable — the first attempt at mutation M5 came back
`FROM-CACHE` and reported a stale green. `core/build.gradle.kts` now declares the font tree as a test
input, the same way it already declares the apps' `strings.xml` for `ConsentCatalogTest`.

**Mutation table** — one at a time, restored byte-exact and confirmed with `shasum -c`:

| # | Mutation | Test turned red |
|---|---|---|
| M1 | `Poppins` back to plain `Font(...)` entries | `CyrillicFallbackTest` sweep **and** Poppins-composition |
| M2 | chain reversed (Nunito primary, Poppins fallback) | Poppins-composition — *"the Poppins family draws nunito_regular.ttf"* |
| M3 | fallback repointed at `poppins_bold` | sweep **and** Poppins-composition |
| M4 | `titleLarge` moved to a bare `FontFamily(Font(poppins_bold))` | sweep names `titleLarge` — proves it is reflection, not a roster of the six known slots |
| M5 | `poppins_semibold.ttf` ← `nunito_semibold` bytes | `BundledFontCoverageTest` Poppins premise (**only after the input fix**; before it, `FROM-CACHE`) |
| M6 | `nunito_bold.ttf` ← `poppins_bold` bytes | Nunito premise, Nunito-composition, Poppins-composition, sweep |
| M7 | Poppins Bold falls back to `nunito_regular` | Poppins-composition — weight drift |
| M8 | `poppins_medium` declared `SemiBold` to Compose | Poppins-composition — OS/2 mismatch |
| M9 | a seventh face added to `res/font` | corpus guard + Nunito premise |
| M10 | `Nunito` family repointed at `poppins_medium` | Nunito-composition + sweep |
| M11 | `poppins_bold.ttf` truncated | corpus guard + the Latin positive control (as an error) |

No test survived every mutation, so none was deleted. The Latin positive control exists because
*"Poppins covers 0 of 98"* passes vacuously if the cmap parser returns nothing (M11 is its killer).

**AC6 sha1s — unchanged, binaries untouched:**
`poppins_semibold 622ca6ccbe2f22c5611dffa016b745bd26be154c`,
`poppins_medium e837165aedb031ea74872c5983a3217d1c190a1a`,
`poppins_bold 45e4d582cbb4dab2bbad3f624fad9ae567c66547`,
`nunito_regular 00939200fea0402ab0105297ff0f9f283f483bef`,
`nunito_semibold a05cc583b9700378985c83a8027c9c5927bd6506`,
`nunito_bold d242cf397381e5e57bbc2a653fbc6f714ee294fc`.
The PM's coverage table is reproduced exactly (0/98 and 471 total for all three Poppins; 98/98 and
938 for all three Nunito) and is now asserted in CI.

**Parity note for the iOS lane (AC2).** Reproduce this surface, not this API: `Poppins` remains one
family of three weights; the pairing is medium→nunito_regular, semibold→nunito_semibold,
bold→nunito_bold; substitution must be **per glyph**, the brand face must win for Latin, and the
system fallback must stay last in the chain. Nothing is added to `UIAppFonts` — Nunito is already
registered — so no `Info.plist` / `project.yml` edit is needed and the owner's Stripe key is not at
risk. Zero call sites change on either platform.

**Catalog-edit routing — routed to the Architect, not taken inline.** Candidate entry: *"per-glyph
font fallback on Android is `AndroidFont` + `Typeface.CustomFallbackBuilder`; a second `Font` in a
Compose `FontFamily` is weight matching, not coverage fallback."*
- Test 1 (code in violation): **does not fire.** Sweep run: `grep -rn "FontFamily(" --include="*.kt"`
  across `src/cleansia_android` excluding `build/` — **zero** family constructions outside `Type.kt`;
  the only other hits are two `FontFamily.Monospace` system generics. Zero baseline.
- Test 2 (narrowing): searched `patterns-mobile.md`, `consistency.md`, `conventions.md` for
  `font`, `Font`, `FontFamily`, `fallback`, `Typography`, `Poppins`, `Nunito`. The only candidate is
  `patterns-mobile.md:250-253`: *"Colors/typography via `MaterialTheme.colorScheme.*` /
  `MaterialTheme.typography.*` inside `CleansiaTheme` (which applies `CleansiaTypography`). Never
  style raw components one-off."* **Both readings recorded** per conventions' unresolved-"governs"
  instruction: (a) it governs only how a *call site obtains* type, and is silent on what a family
  *contains* → floor, inline; (b) it governs the theme's typography wholesale, so a rule about family
  composition carves out of it → narrowing, Architect. Unresolved either way, because —
- **AC3 of this ticket reserves the mechanism ruling to an architect panel with an ADR.** Writing the
  canonical form into the catalog would be ratifying exactly that ruling. So the entry is **not**
  written inline; the drafted text plus the bytecode evidence above is handed to the panel. If
  ratified it prices as **`T1-CI`** — enforcer `core/src/test/…/ui/theme/CyrillicFallbackTest.kt` +
  `BundledFontCoverageTest.kt`, which `android-ci.yml`'s *"Unit tests (all modules)"* step runs on
  every PR; baseline is zero and the rule is mechanically expressible, which is exactly when `T1-CI`
  is required.
