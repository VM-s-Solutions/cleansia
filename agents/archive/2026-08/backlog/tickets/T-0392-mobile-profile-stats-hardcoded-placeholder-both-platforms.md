---
id: T-0392
title: "Mobile BUG (iOS+Android) — Profile stats card ships hardcoded placeholders (3 bookings / 320 Kč saved / \"Feb 2025\" member-since / \"Regular\" tier) to every user; \"Feb 2025\" is also an untranslated English literal"
status: done
size: M
owner: pm
created: 2026-07-08
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0022]
layers: [backend, ios, android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-4 S3 review (major) — flagged on iOS, confirmed present identically on live Android
---

> **Found by the iOS fix-round-4 review (phase/ios-fix2, S3-design), confirmed as shared iOS+Android debt.**
> The customer Profile hero's floating **stats card** renders three per-user-looking figures that are in fact
> hardcoded constants — identical on both platforms. iOS shipped it as a *faithful* Android parity port
> (the owner asked for the Android profile design), so the fix must land on **both** platforms together to
> avoid iOS↔Android drift; fixing only one is explicitly out of scope.

## Context
Android `customer-app/.../features/profile/ProfileTab.kt:97-99`:
```
val totalBookings = 3
val savedCzk = 320
val memberSince = "Feb 2025"     // + val tier = "Regular"
```
iOS `CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift`:
```
static let androidParity = ProfileStats(bookings: "3", saved: "320 Kč", memberSince: "Feb 2025")
```
Both feed the `StatsCard` (bookings | saved | member-since) that overlaps the hero's bottom lip. Two problems:
1. **Fabricated per-user data** — a brand-new user with zero bookings is shown "3 bookings, 320 Kč saved,
   member since Feb 2025". The labels localize (`profile_stat_*`), but the **values are invented**.
2. **`"Feb 2025"` is a non-localized English literal** — it is a raw string constant on both platforms (not a
   `stringResource` / xcstrings key), so the cs/sk/uk/ru builds display English "Feb". (The iOS FIX-7b i18n
   sweep missed it because it is a `String` constant, not a `Text("…")` literal — corrected in the PR notes.)

**Data-availability reality (why it was hardcoded):** the mobile contract exposes none of these per user.
`CurrentUserProfile` (iOS) / `MyProfileDto` (both) has **no** registration/created date, **no** lifetime
savings, and the Profile tab has **no** orders view-model injected for a bookings count.

## Acceptance criteria
- [ ] **AC1 (no fabricated figures)** — Given any signed-in user, When the Profile stats card renders, Then no
  value is an invented constant. For each of the three stats, EITHER wire a real source OR hide that stat when
  no real value exists (never show a plausible-looking fake). Decide per stat and record:
  - **bookings** — wirable today from the orders list/count on both platforms (inject the orders source into
    the Profile tab); expected to become real.
  - **saved** — needs a backend field (lifetime Plus savings / discount total). If not delivered this round,
    **hide** the saved stat behind its availability.
  - **member-since** — needs a backend `MyProfileDto.createdOn` (registration date). Until it lands, **hide**
    the stat. When present, format via a **locale-aware** month+year formatter (never a hardcoded month).
- [ ] **AC2 (backend field, if in scope)** — if member-since/saved are delivered: add the field(s) to the
  customer/mobile `MyProfileDto`, re-dump the mobile spec, regenerate BOTH mobile clients (MANUAL_STEP), and
  the Android+iOS status/tier reads the real value. If deferred, split into a backend sub-ticket and land only
  the bookings-count wiring + hide-when-absent this round.
- [ ] **AC3 (i18n)** — no user-visible value in the stats card is a hardcoded English literal in any of the 5
  locales; any date value is locale-formatted; a `grep`/xcstrings audit shows zero baked month/tier strings.
- [ ] **AC4 (parity + non-regression)** — iOS and Android render the SAME set of real/hidden stats (no copy or
  behavior drift); both apps compile; existing profile tests green; the `tier` badge already reads real
  membership state on iOS (`membershipVM.current?.hasMembership`) — Android's `tier = "Regular"` constant is
  reconciled to the same real membership read.

## Out of scope
- Redesigning the Profile hero/stats layout (ADR-0022-adjacent shell work is done; this is data only).
- Fixing only one platform (would re-introduce the drift this ticket exists to prevent) — **EXCEPTION,
  owner-authorized 2026-07-08:** the owner explicitly disliked the fabricated strings on iOS ("I don't like the
  hardcoded strings"), so fix-round 5 **hid the iOS StatsCard as an interim**. This is a *tracked, deliberate*
  one-platform change (not the silent drift the rule forbids): Android still shows the placeholder card until
  this ticket lands the real cross-platform stats (or symmetrically hides it). See the status log.

## Implementation notes
- iOS `ProfileStats.androidParity` + its doc comment are the placeholder seam; the Profile tab currently has
  `profileVM`, `membershipVM`, `preferences` but **no orders source** — wiring bookings means injecting one.
- Keep the hero + stats **visual** exactly as shipped in fix-round-4; only the values (and hidden-state) change.
- Prefer "hide the stat" over "show 0/—" only if a hidden slot doesn't visually unbalance the 3-up card;
  otherwise show a neutral em-dash. Record the choice.

## Status log
- 2026-07-08 — filed `proposed` by pm from the phase/ios-fix2 fix-round-4 S3 review (major). iOS kept the
  faithful Android-parity placeholder for this round (owner asked for the Android design; no per-user source
  exists on the contract), and the divergence-avoidance rule routes the real fix here as cross-platform work.
  Medium priority: user-visible on every install, but non-blocking (cosmetic-until-real).
- 2026-07-08 (fix-round 5) — **owner-authorized interim divergence recorded.** On the 5th device pass the owner
  said "I don't like the hardcoded strings," so fix-round 5 **hid the iOS Profile StatsCard** (removed
  `ProfileStats`/`StatsCard`/`StatItem`/`StatDivider`, reflowed the hero). The round-5 D-review correctly flagged
  this as iOS↔Android drift vs this ticket's plan-of-record; resolution per its option (b) — logged here as a
  deliberate exception, NOT undone. **Android still shows the placeholder card.** When this ticket is worked:
  either wire real cross-platform stats OR hide Android's `StatsCard` symmetrically so the two converge again.
  Bumping toward the top of the follow-up queue since the platforms are now visibly diverged.
- 2026-07-19 — **done.** Backend fields + Android wiring had landed via PR #121/#122; the Android
  tier-literal gap and the iOS half both closed today (see Review). Both platforms now render the
  SAME set of real stats (all three SHOWN) from `MyProfileDto`, with the tier badge on real
  membership state — the fix-round-5 divergence is resolved by convergence on real data.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **Android half complete** (android). The backend + stats wiring had already landed via
  PR #121/#122 (`MyProfileDto.memberSince/totalBookings/totalSavings/savingsCurrencyCode` →
  `CurrentUser` → `StatsCard`); this pass closed the last AC4 gap — the hardcoded untranslated
  `tier = "Regular"` literal. **v1 contract for the iOS mirror:** all three stats SHOWN with real
  values (bookings = `totalBookings`; saved = `totalSavings` formatted in `savingsCurrencyCode`,
  symbol-less number when currency null; member-since = locale-formatted "MMM yyyy", em dash "—"
  while null). Tier badge = `MembershipRepository.current.hasMembership` → localized
  `profile_tier_plus`/`profile_tier_regular` (exposed as `ProfileViewModel.isPlus`, passed into
  `ProfileTab`) — identical to iOS's existing `membershipVM.current?.hasMembership` read, string
  values mirrored from the iOS xcstrings (en Plus/Regular, cs Základní, sk Základné, uk Стандартний,
  ru Обычный). Tests: +3 VM tier-mapping, +2 repo stats-mapping, +6 formatter (null → em dash /
  symbol-less); `:customer-app:testDebugUnitTest` 255/255 green.
- 2026-07-19 — **iOS half complete (ios) — both platforms converged, ticket done.** The stats card
  had already been restored with real DTO reads (PR #122/#124: `MyProfileDto.memberSince/
  totalBookings/totalSavings/savingsCurrencyCode` → `CurrentUserProfile` → `ProfileStatsCard`);
  this pass closed the two v1-contract gaps: (1) **saved** no longer routes through
  `OrdersFormat.price`, which defaulted a null currency to CZK (a no-realized-orders user saw
  "0 Kč" instead of the contract's bare number) — new `ProfileStatsFormat.saved` mirrors Android's
  `formatSaved` exactly (CZK→"Kč", EUR→"€", USD→"$" as "10 $", unknown code passthrough, symbol-less
  when currency null); (2) **member-since** no longer formats via `Date.formatted` (system locale) —
  `ProfileStatsFormat.memberSince` takes the app locale from `@Environment(\.locale)` (fed by
  `preferences.locale`, the OrdersTab pattern, so it reacts to the in-app language switch), "MMM y"
  template, "—" when null. Bookings (`totalBookings ?? 0`) and the tier badge
  (`membershipVM.current?.hasMembership`) were already contract-conformant — untouched.
  `MyProfileDto.toDomain` made internal for mapping tests. Tests: +6 formatter (known/unknown/null
  currency, en "Feb 2025" + cs month localization, null → "—") +2 DTO stats-mapping
  (values + absent-defaults); customer suite 586 tests on iPhone 17 AND the iPhone14-iOS16 floor
  (only the 2 known local Stripe-key artifacts fail); partner scheme BUILD SUCCEEDED;
  swiftformat 0.60.1 --lint + swiftlint 0.65.0 --strict clean. No new strings — the stat labels and
  tiers already exist ×5 locales, and "—" mirrors Android's unlocalized literal.
