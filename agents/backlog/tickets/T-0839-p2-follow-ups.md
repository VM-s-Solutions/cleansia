---
id: T-0839
title: Finish the P2 follow-ups — Kč on iOS Pending offers, one Android support address, four wipe triggers in the catalog, the dark-colour read rule, the no-mail device check and the colour first-read measurement
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0822, T-0824, T-0825, T-0829]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

The native PR (#323) left six items open, and each is named in a P2 ticket's record. **Mike: "Finish the tasks from P2" (2026-10-12).**
- Each item was ground-truthed at `db2272a5d`. The ground truth is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_native.json`. Its scratch outputs are under `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/groundtruth/`.
- The catalog wording comes from the native design ruling of 2026-10-12, items 5 and 7. That ruling is in the same folder, `architect_native-lead.json`.

1. **iOS Pending offers prints "CZK"** (from T-0824's review).
   - `PendingOffersView.swift:165` hands `OrdersFormat.money` the ISO code as its symbol, and `OrdersFormat.swift:37-51` prints the symbol as given.
   - Core already has a resolver, `EarningsFormat.currencySymbol` (`EarningsFormat.swift:15-29`), and `WorkContractSheet.swift:188` uses it.
   - Android prints "Kč" (`OrderFormatters.kt:120-127`, via `PendingOffersScreen.kt:296`).
   - No test pins the price.
   - `patterns-mobile.md:1017` lists this screen as "Not reached". Its `:421` citation is also stale: the headline is at `EarningsSummaryScreen.kt:495`.
2. **Android keeps the support address in three private constants.**
   - They are in customer `HelpSupportScreen.kt:64`, and in partner `DashboardScreen.kt:96` and `RegistrationLockScreen.kt:84`.
   - iOS keeps it once: `CleansiaWeb.swift:12-13`, `contactEmail = "support@\(domain)"`.
   - Android's `core/config/CleansiaWeb.kt:12-21` mirrors that type but has no contact address, although its header (`:3-10`) says a domain move must be a one-line edit.
   - Three source pins assert the private literal: `MarketCopyStringsTest.kt:113`, `:127`, `:129`; `DashboardScreenTest.kt:22`, `:26`, `:30-33`; and `RegistrationLockStepRowTest.kt:39`, `:41`, `:49`.
3. **The catalog says three wipe triggers where S11 says four** (from T-0822's review).
   - S11 names four triggers (`security-rules.md:793-801`): sign-out, the forced 401, account deletion, and sign-in before the new tokens are stored.
   - `docs/mobile-app/patterns.md:11-12` and `:22-26` already say four.
   - These still say fewer:
     - `consistency.md:272-274` (E9);
     - `patterns-mobile.md:321-322` and `:325-327`;
     - `.claude/agents/security.md:51-52`;
     - `docs/mobile-app/features.md:203`, which names two.
4. **No rule says how to read a dynamic colour's dark value.**
   - T-0829 found that on the iOS 16 floor, `UIColor(Color)` keeps only a dynamic colour's light value, whatever it is resolved with.
   - `patterns-mobile.md:430` still calls the roundtrip "trait-dependent".
   - The one dark read through it in the tree is `DisputesListCardTests.swift:86`, driven with `.dark` at `:58`. On 16.x it re-checks the light value.
   - `StatusBarFade.rgb`, the read T-0829 built (`StatusBarFadeScrollView.swift:316-342`), is internal to Core (`:201`).
5. **T-0825's fallback has never run on a device with no mail app.**
   - Its tests read the source, and the render pass skipped Help (T-0825's review).
   - The screens:
     - partner Android: Dashboard Help (`DashboardScreen.kt:270-279`), and the Registration lock's Approval row (`RegistrationLockScreen.kt:462-483`), which acts only on a rejected registration;
     - iOS: Dashboard Help (`PartnerShellView.swift:91` → `:126-142`), and the Registration lock's `.approval` route (`RegistrationLockView.swift:184-200`).
   - The owned emulator CleansiaWaveC35 is a Google APIs image that ships Gmail. A tap there opens Gmail, not the copy.
   - The iOS 18.6 simulator's Mail is a stub with no executable, so `canSendMail()` should be false there. This was read from the files, not run.
6. **The fade may pay for the first read of a colour more than once.**
   - Its cache is keyed by `Color`, and it empties a scheme's map at eight entries (`StatusBarFadeScrollView.swift:321`, `:339-344`).
   - `BrandGradient.colors` builds a fresh `Color` on every access (`BrandGradients.swift:41-47`).
   - Two screens pass one as the hero tint: `ProfileHubContent.swift:26`, in both schemes, and `ProfileTab.swift:28`, in dark mode only.
   - T-0829's review put a miss at about 1–2 ms. That figure was not re-measured.

## Doing

1. **Pending offers.**
   - Use `symbol: EarningsFormat.currencySymbol(offer.currencyCode)` at `PendingOffersView.swift:165`, as `WorkContractSheet.swift:188` does.
   - Add a pin that goes RED then GREEN.
   - In `patterns-mobile.md:1017`, move the screen into the resolver list, and correct `:421` to `EarningsSummaryScreen.kt:495`.
2. **One Android support address.**
   - Add `const val CONTACT_EMAIL = "support@$DOMAIN"` to core `cz.cleansia.core.config.CleansiaWeb`, mirroring iOS.
   - The three screens use it, and their three source pins follow.
   - One test pins the value, as `HelpSupportContactTests.swift:41` does on iOS.
3. **Four wipe triggers.**
   - The five passages say four and point at S11, in the wording the ruling gives (its (i) to (v)).
   - E9's enforcement line names `AuthRepositorySignInWipeTest` and `SignInWipeTests` as a closed roster. That roster leaves out the customer Android sign-in wipe (`customer-app/…/core/auth/AuthRepository.kt:217-222`).
   - The `.claude/agents/security.md` edit rests on Mike's "Finish the tasks from P2". The `features.md:203` edit is a deliberate addition. The commit says both.
   - `agents/AUDIT-2026-10-07.md:1795` is a dated record and stays as it is.
4. **The dark-read rule**, in one commit, so its lint lands on a zero baseline:
   - `patterns-mobile.md:430` says the roundtrip keeps only a dynamic colour's light value on the iOS 16 floor.
   - A new entry goes after `:433`:
     - a test that needs what a dynamic colour draws in dark mode reads it through `StatusBarFade.rgb`, never through `UIColor(Color)`;
     - app tests reach it through `@testable import CleansiaCore`;
     - in production, only the fade reads through it;
     - the entry names what its lint cannot see: a colour argument with nested parentheses, a stored `UIColor`, and a dark read without `resolvedColor(with:)`.
   - A SwiftLint custom rule, `dynamic_colour_dark_read`, in `src/cleansia_ios/.swiftlint.yml`, at severity error, with comments, doc comments and strings excluded:
     `UIColor\([^()]*\)\s*\.resolvedColor\(\s*with:(?!\s*UITraitCollection\(userInterfaceStyle:\s*\.light\b)`
   - `DisputesListCardTests.swift:81-89` reads through `StatusBarFade.rgb`:
     - the file moves to `@testable import CleansiaCore`, and the class to `@MainActor`;
     - a self-check confirms that its dark read of `CleansiaColors.surface` differs from its light read.
   - `patterns-mobile.md:1009` stays as it is.
5. **The no-mail check**, against a local stack.
   - iOS, on the owned simulator: Help and the rejected-registration Approval route copy the address and say so.
   - Android, on an image with no mail handler: an AOSP `default` API 35 image, so the owned CleansiaWaveC35 is not changed.
     - Disabling Gmail on that guest instead (`pm disable-user com.google.android.gm`, which is reversible) would change an owned emulator. That waits for Mike's word.
   - The Approval row needs a cleaner whose registration was rejected.
   - The result goes here and on T-0825.
6. **The first-read measurement, then its decision rule.**
   - **Instrument:** a scratch build, never committed, with an `os_signpost` on the miss path of `StatusBarFade.rgb` and an interval around the render. Each signpost is tagged with what re-rendered the parent.
   - **Run on the 16.4 simulator:** the customer Profile in dark mode, and the partner Profile hub in both modes.
     1. Let the avatar load.
     2. Switch theme once.
     3. Scroll the hero past the status bar and back, five times.
     4. Count the misses per visit.
   - **If a physical iPhone is available:** Instruments' Hitches and Time Profiler time each miss, and show whether any hitch's stack holds `StatusBarFade.rgb`.
   - **Fix only if** a hitch includes `StatusBarFade.rgb`, or one visit records eight or more misses in one scheme.
     - The fix is a per-case memo of `BrandGradient.colors` in Core, with no call-site change.
     - A test pins it: `colors[i] == colors[i]` for every dynamic case.
     - The comment at `BrandGradients.swift:18-23` is updated.
   - **Otherwise**, the numbers go here, and the cost is accepted.

## NOT doing

- The customer Android sign-in wipe's missing test (T-0845), and the possible iOS 16 text-field defect (T-0846).
- No change to the copy of `booking_cash_needs_card` or `recurring_cash_needs_card` (T-0835's ruling).
- No stored tints at the two call sites, and no production reader of `StatusBarFade.rgb` other than the fade.
- Other P2 items the ground truth found still open, outside this list, reported for Mike:
  - iOS `SessionManager.registerSessionScopedCache` and `clearSessionScopedCaches` have no callers (`SessionManager.swift:38`, `:42`);
  - the Android partner `refreshPendingOffers` republishes with no session guard (`OrdersRepository.kt:264-270`);
  - `DashboardRepository.invalidate()` has no caller (`:120`, `:237`).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Pending offers prints "Kč". Its pin goes RED then GREEN, and the catalog line is current.
- [ ] **AC2** — Android holds the support address once. The three screens and their pins use it.
- [ ] **AC3** — No catalog, charter or docs page outside dated records names fewer than four wipe triggers. check-catalog-claims stays at C1 0 · C2 0 · C3 0.
- [ ] **AC4** — The lint and the self-check behave as follows:
  - at HEAD, the lint flags `DisputesListCardTests.swift:86` and nothing else: not `BrandTextInkTests.swift:245`, and not `MapMarkerTests.swift:187`;
  - after the migration, it flags nothing;
  - putting the old read back fails `swiftlint lint --strict`;
  - on the 16.4 runtime, the self-check fails with the old helper and passes with the new one.
- [ ] **AC5** — The no-mail check is recorded for both apps, here and on T-0825. Otherwise, its Android half is recorded as waiting for an image or for Mike's word.
- [ ] **AC6** — The miss counts are recorded here, with timings if a device was used, and the decision rule is applied. A fix lands only if the rule calls for it, with its pin going RED then GREEN.
- [ ] **AC7** — The full Android and iOS suites pass, and the iOS suites also pass on 16.4.

## Work record

- 2026-10-12 — Filed from the native ground truth and the native design ruling of 2026-10-12. Mike: "Finish the tasks from P2" (2026-10-12).

## Review

Pending.
