---
id: T-0839
title: Finish the P2 follow-ups — Kč on iOS Pending offers, one Android support address, four wipe triggers in the catalog, the dark-colour read rule, the no-mail device check and the colour first-read measurement
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0822, T-0824, T-0825, T-0829]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

The native PR (#323) left six items open, and each is named in a P2 ticket's record. **Mike: "Finish the tasks from P2" (2026-10-12).**
- Each item was ground-truthed at `db2272a5d`, the P5 branch's first commit (master `6952149` plus backlog edits). The ground truth is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_native.json`. It names the commit `b405e9cc5`, the same tree under its id before the P5 branch's second reword; `p5/sha-map.md` row 1 maps it (`p5/sha-map-p6.md` does not). Its scratch outputs are under `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/groundtruth/`.
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

- [x] **AC1** — Pending offers prints "Kč". Its pin goes RED then GREEN, and the catalog line is current.
- [x] **AC2** — Android holds the support address once. The three screens and their pins use it.
- [x] **AC3** — No catalog, charter or docs page outside dated records names fewer than four wipe triggers. check-catalog-claims stays at C1 0 · C2 0 · C3 0.
  - Met in `3bd7b2b` (Work record). Nineteen code comments still name two wipe paths, eleven Android and eight iOS, and two test comments do too; a code comment is not a catalog, charter or docs page (Reported).
- [x] **AC4** — The lint and the self-check behave as follows:
  - at HEAD, the lint flags `DisputesListCardTests.swift:86` and nothing else: not `BrandTextInkTests.swift:245`, and not `MapMarkerTests.swift:187`;
  - after the migration, it flags nothing;
  - putting the old read back fails `swiftlint lint --strict`;
  - on the 16.4 runtime, the self-check fails with the old helper and passes with the new one.
- [ ] **AC5** — The no-mail check is recorded for both apps, here and on T-0825. Otherwise, its Android half is recorded as waiting for an image or for Mike's word.
  - Not met. On iOS the Help check passed, but Doing 5's iOS half also covers the rejected-registration Approval route, which needs a cleaner whose registration was rejected and which no device pass reached (receipt-003: "neither capture lane reached it"). The Android half is recorded here and on T-0825 as waiting for an image or for Mike's word, as the criterion allows (Work record).
- [x] **AC6** — The miss counts are recorded here, with timings if a device was used, and the decision rule is applied. A fix lands only if the rule calls for it, with its pin going RED then GREEN.
- [x] **AC7** — The full Android and iOS suites pass, and the iOS suites also pass on 16.4.

## Work record

- 2026-10-12 — Filed from the native ground truth and the native design ruling of 2026-10-12. Mike: "Finish the tasks from P2" (2026-10-12).
- 2026-10-12 — The design ruling's items 5 and 7 (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`).
  - Item 5 reached consensus. The wipe-trigger wording was sustained with one change to E9's enforcement line, and the colour-read rule with challenge 5a conceded and three amendments. The Pending-offers line is an inline edit. Its note for Mike: "None for the edits."
  - Item 7: "CONSENSUS. Sustained, with both nits conceded." Then: "Leave it. Store no tint now; CLAUDE.md §4 says do nothing first. Fix it only if the measurement below shows a frame paying for it."
- 2026-10-12 — Implemented on `fix/post-e-p6-native-followups`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`. The result files are in `p5/impl/`: `android_native-items.json`, `ios_native-items.json`, `docs_native.json`, `native_fix-pass.json` and `optimizer_colour-reads.json`. Every run was local; nothing was pushed. The evidence files and the reviews quoted here name this branch's commits by the ids they had before its commit messages were reworded before push; each has the same tree as the commit cited here, and `p5/sha-map-p6.md` in the Wave F folder maps them.
  - **1. Pending offers.**
    - `109d76a` — `PendingOffersView.swift:165-168` resolves the code through `EarningsFormat.currencySymbol(offer.currencyCode)`, as the work contract's reward does. `PendingOffersPriceTests` pins the call site.
    - The same commit edits the catalog's currency-symbol row inline: Pending offers moves from "Not reached" to the resolver list, and `:421` becomes `EarningsSummaryScreen.kt:495`.
    - RED on iOS 18.6, 2 failures: "the offer price does not resolve its currency's symbol" and "the offer price prints the ISO code" (`p5/ios-native/logs/partner-red-186.log`). GREEN on 18.6 and on the iOS 16.4 simulator.
    - `f8473c7` — the row said "on iOS only the Pending offers price is pinned", but `DashboardFormatTests` already pinned the dashboard's Kč. The row now names both pins (`patterns-mobile.md:1049` at the head).
    - A scratch render harness, never committed, drew the price as "1 200 Kč" on 16.4 and 18.6, light and dark (`p5/native-fixpass/render/partner-pending-offers-*.png`).
  - **2. One Android support address.**
    - `23ba6e3` — `CleansiaWeb.CONTACT_EMAIL = "support@$DOMAIN"` in `:core` (`CleansiaWeb.kt:18`). The customer Help screen, the partner dashboard's Help shortcut and the registration lock's support row read it. Their pins (`MarketCopyStringsTest`, `DashboardScreenTest`, `RegistrationLockStepRowTest`) follow.
    - The new `ContactEmailTest` pins the value. It also fails if any Kotlin or XML file under the three modules' main sources, other than `CleansiaWeb.kt`, spells out "support@".
    - RED, three steps. First the test failed to compile: "Unresolved reference 'CONTACT_EMAIL'" (`p5/android-native/p2-red-1.txt`). With the constant added and the private copies still in place, its second test named exactly the three screens (`p2-red-2.txt`). The updated per-screen pins failed 2 of 11 and 2 of 8 against the unchanged screens (`p2-red-3.txt`).
    - GREEN: the four classes pass (`p2-green.txt`; 21 tests by the result file's count, which the text log does not print).
    - `6d6bf94` — iOS `HelpSupportContactTests.testBothAppsOpenTheSameAddressAndLine` read Android's removed private literal, so at `23ba6e3` the iOS customer suite failed 1 of 1780. The test now reads `CleansiaWeb.kt` for `DOMAIN` and `CONTACT_EMAIL`, and Help's mailto link.
    - RED at `23ba6e3`: 3 tests, 1 failure (`HelpSupportContactTests.swift:42`) on 18.6. GREEN: 3 of 3 on 18.6 and on 16.4 (`p5/native-fixpass/logs/summary.txt`).
    - So `23ba6e3` on its own fails the iOS customer suite (the correctness review's N3; T-0832's record lists the six commits that do not build or pass on their own).
  - **3. Four wipe triggers.**
    - `9952094` — the ruling's wording, (i) to (v). It edits E9 in `consistency.md`, with its closed-roster enforcement line; the two SessionScopedCache passages in `patterns-mobile.md`; the S11 item in `.claude/agents/security.md`; and `docs/mobile-app/features.md:203`.
    - The commit cites Mike's "Finish the tasks from P2" for the charter edit, and names `features.md:203` as a deliberate addition. `agents/AUDIT-2026-10-07.md` stays as written.
    - Four passages outside the ruling's list still named two triggers after this commit (the follow-up correctness review's B1); `3bd7b2b` brought them to four (AC3, Review).
  - **4. The dark-read rule**, in one commit.
    - `7e51993` — in `patterns-mobile.md`, the T-0451 entry's parenthesis now says the roundtrip "keeps only a dynamic colour's light value" (`:439` at the head). The new entry follows it (`:444`): "Reading what a dynamic colour draws in dark mode: `StatusBarFade.rgb`, never `UIColor(Color)` (T-0829)".
    - `.swiftlint.yml` gains `dynamic_colour_dark_read` at severity error, with comments, doc comments and strings excluded (`:64-72`).
    - `DisputesListCardTests` reads through `StatusBarFade.rgb`, under `@testable import CleansiaCore` and `@MainActor`, with a self-check, `testTheReadSeesTheDarkSurface`.
    - The map-marker row is unchanged (`:1041` at the head).
    - The lint, over an export of the tree before the migration: one violation, `DisputesListCardTests.swift:86:9`, and nothing else (`p5/ios-native/evidence/swiftlint-rule-red-at-HEAD.txt`).
    - After the migration: 0 violations in 959 files (`evidence/swiftlint-final.txt`).
    - With the old read put back, in a scratch copy: one `dynamic_colour_dark_read` violation (`evidence/swiftlint-mutation.txt`).
    - A probe file: the rule flags a variable style, a literal `.dark`, stored traits, the `.init(userInterfaceStyle:)` shorthand and a read split across lines. It passes a literal `.light`, a colour argument with nested parentheses, a stored `UIColor`, a native `UIColor` and a comment (`evidence/swiftlint-rule-probe.txt`).
    - The self-check on iOS 16.4: with the old helper, 4 tests and 1 failure, the dark surface equal to the light one (`p5/ios-native/logs/customer-red-164-disputes.log`). With the new helper it passes, and the light 5.70 pin holds.
  - **5. The no-mail check.** Not run on either app (below).
  - **6. The first-read measurement, then its decision rule.**
    - The instrument: a scratch copy at `23ba6e3`, never committed (`p5/optimizer/logs/instrumentation.diff`). `StatusBarFade.rgb` got an `os_signpost` interval on its miss path, with hit, miss and flush counters. The bodies of `ProfileTab`, `ProfileHubContent` and `ProfileView` got a counter and `Self._printChanges()`, which tags what re-rendered each parent.
    - App-hosted XCTest probes ran on iOS 16.4, 18.6 and 26.3 simulators. No physical iPhone was available, so there was no Instruments run and no hitch test.
    - First reads per visit, hero and page together. Each visit loads the avatar, switches the theme once (except the two "no switch" visits), then scrolls the hero past the status bar and back five times.

      | Visit | 16.4 | 18.6 | 26.3 |
      |---|---|---|---|
      | partner hub, light, no switch | 11 | 4 | 4 |
      | partner hub, light → dark on the Theme screen | light 22 / dark 6 | 8 / 3 | 5 / 2 |
      | partner hub, dark → light on the Theme screen | dark 22 / light 6 | 8 / 3 | 5 / 2 |
      | partner hub, light → dark, set in place | light 13 / dark 6 | 5 / 2 | 5 / 2 |
      | customer Profile, dark, no switch | 3 | 2 | 2 |
      | customer Profile, dark → light on Appearance | dark 7 | 5 | 7 |
      | customer Profile, dark → light, set in place | dark 5 | 4 | 4 |
      | customer Profile, light → dark on Appearance | dark 2 | 2 | 2 |

    - The table was rebuilt from the result files (`p5/optimizer/results/run2-ax-off-164/`, `run4-base-186/`, `run4-base-263/`) with the measurement's own `p5/optimizer/probe/table.py` while this record was written.
    - Scrolling caused no parent re-render and no miss in any scenario.
    - Timings, simulator only. One warm first read had a median of 2.9 ms on 16.4, 3.7 ms on 18.6 and 2.9 ms on 26.3.
    - Main-thread time per forced update on 16.4, without and with the memo: partner hub 34.5–37.2 ms against 20.8–21.4 ms; customer Profile in dark mode 12.4 and 21.6 ms against 6.7 and 15.6 ms (`p5/optimizer/probe/ab_summary.py` over `p5/optimizer/results/ab-r*`).
    - The rule fires. On 16.4 the partner hub records 11 to 22 first reads in one scheme on every visit, at or above the ruling's eight. Without a device the miss count decides, as ruled.
    - `58f67cf` — the fix the ruling named. `BrandGradient.colors` returns a per-case table built once from the stops, so repeated accesses return equal `Color`s. No call site changes. The stops' doc comment says the colours are built once and compare equal.
    - `BrandGradientTests.testEveryDynamicStopIsTheSameColorOnEveryAccess` pins it. RED before the change: 8 tests, 10 failures on 16.4 and on 18.6, for blue, purple, cyan, orange and emerald, two stops each (`p5/native-fixpass/red/red-brandgradient-164.log`, `red-brandgradient-186.log`). GREEN after: 8 of 8 on both (`p5/native-fixpass/logs/summary.txt`).
    - With the same memo as a scratch prototype, every visit on 16.4 recorded 2 first reads per scheme (`p5/optimizer/results/run3-memo-164/`). Nobody re-measured the committed change.
  - `6672db1` — the CHANGELOG entry for item 1 (Gate 7).
  - Suites, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`):
    - Android: core 296, partner 839 and customer 1683 tests, 0 failures.
    - iOS: CleansiaCore 776/776, CleansiaPartner 1113/1113 and CleansiaCustomer 1780 (1779 passed, 1 skipped), on 18.6 and on 16.4.
    - SwiftFormat: 0 of 960 files to format. SwiftLint `--strict`: 0 violations in 959 files.
    - Checks: check-docs-refs, 377 references with 0 unresolved; check-catalog-claims, C1 0 · C2 0 · C3 0 with 157 advisories; the docs site builds with no dead links.
    - No native code changed after `6672db1`. The filing commit and the records change only `agents/backlog/`. `3bd7b2b` changes docs and catalog pages and one test comment, and `8c26004` changes the CHANGELOG. The master merge `753b98b` brings #326's workflow and Bicep changes and #327's backend code, which #327's exact-head CI ran on `b47e6ce`. No suite ran after `6672db1`, and none ran on the merged tree. The checks run for `3bd7b2b` are in its own 2026-10-13 entry below.
  - Gate 8.5, iOS 16.4: the suites above ran on 16.4, and the scratch harness rendered the Pending offers price there. No launch-and-navigate smoke ran.
  - **Ruled differently from Doing:**
    - Item 1: `109d76a` wrote a false "only" into the catalog row; `f8473c7` corrected it.
    - Item 2: `ContactEmailTest` also forbids "support@" anywhere else in the three modules' main sources, beyond the Doing's one pin on the value. `23ba6e3`'s message names it.
    - Item 2: the change broke an iOS test that reads Android source. The fix is its own commit, `6d6bf94`, because folding it into `23ba6e3` means rebasing reviewed commits.
    - Item 6: the parents' re-render source was tagged with `Self._printChanges()` beside the signposts, not on each signpost. The measurement also ran on 18.6 and 26.3.
  - Not run: the no-mail device check (AC5); Instruments timing, because there was no physical iPhone; nothing against DEV or PRO. T-0825's record has not been updated, as AC5 also asks.
- 2026-10-13 — AC5, the no-mail device check: the same three render attempts as T-0832's AC2, the first begun late on 2026-10-12. All three were set up for the owned guests against the local disposable render fixture. None touched DEV or PRO. Evidence: `render-p6/raw/`.
  - First attempt (`receipt.json`). It stopped at the Android build's own guard, after Gradle regenerated 7 API-client files from the head's specs. No guest was booted, no app was installed or opened, and Help was not tapped. Verdict: NOT OBSERVED.
  - Second attempt (`receipt-002.json`). Both partner apps were installed and launched.
    - iOS: the partner app opened signed in on its Dashboard, on iOS 18.6. Help was not tapped, and the Approval route was not reached. Verdict: NOT OBSERVED.
    - Android: the partner app rested on its splash retry screen, because the guest could not connect to `10.0.2.2:16002`. Neither screen was reached. Verdict: NOT OBSERVED.
    - On that guest the only activity answering a `mailto` intent is Gmail's, and it stayed enabled. No package was disabled or re-enabled.
  - Neither attempt recorded anything on T-0825.
  - Third attempt (`render-p6/raw/receipt-003.json` (sha256 `900967f1…`, verified by its own check), `prepare-manifest-003.json`). It reused the second attempt's products and fixed hosts; both partner apps opened signed in.
    - iOS, i02: PASS. The simulator has no Mail app; which branch of the Help action made the copy is not distinguishable (receipt-003). One tap on the dashboard's Help copied `support@cleansia.cz`, the "Address copied" notice showed in the screen dump, and no composer opened. The screenshot, taken about 0.3 s after the tap, came before the notice rendered. The Approval route was not reached. The pasteboard was emptied after the check; one empty text item may remain on the simulator.
    - Android, a02: NOT OBSERVED, waiting for an image with no mail handler or for Mike's word. Gmail is the only `mailto` handler on the owned emulator, and Help was not tapped.
    - **A process failure, contained.** The pass's instructions allowed it to disable Gmail on the owned emulator for this check. Doing 5 reserves exactly that step for Mike's word, and he had not given it, so the instruction was wrong. The disable was refused before it ran, and no app was disabled (`render-p6/raw/caller-intervention-003.md`; the receipt records the refusal and the modified command tool).
  - Recorded on T-0825, as AC5 asks.
- 2026-10-13 — `3bd7b2b` — AC3's remaining passages: `patterns-mobile.md`'s iOS parity row and push-token rule, `api-integration.md`'s forced sign-out section, `ios-app-architecture.md`'s push-token decision and `frontend.md`'s membership generation now name the four triggers S11 names, and five shorthand mentions say the clear runs on every wipe trigger. The frontend paragraph, the shorthand at `patterns-mobile.md:316` and the push-token section's "clear-on-logout" (`ios-app-architecture.md:739`) were found by this record's refuters after the follow-up review. Deliberate additions, in passages the same commit rewrote: the iOS parity row says the app containers register the caches they hold (no iOS repository registers itself), and the push-token decision says the registrar, not its store, is the `SessionScopedCache`. It also marks account deletion as the customer apps' in `docs/mobile-app/features.md` (the follow-up security review's N1) and corrects a test comment (the follow-up correctness review's N5). check-docs-refs, 377 references with 0 unresolved; check-catalog-claims, C1 0 · C2 0 · C3 0 with 157 advisories; SwiftFormat and SwiftLint `--strict` clean on the touched Swift file.
- 2026-10-13 — Not yet: the P6 pull request's exact-head CI and its merge.

## Review

- **Correctness review** of `fix/post-e-p6-native-followups` at `23ba6e3` against `6952149` (`p5/impl/review_native.json`): "## Verdict: CHANGES REQUESTED". "The code is correct on all three stacks and each fix has a test that catches its removal."
  - Its runs, on a `git archive` of `23ba6e3`: "New SwiftLint rule on master's tree" gave "exactly 1 hit, `DisputesListCardTests.swift:86:9`". With the old read put back the rule "fails; a read split across lines is caught, a literal `.light` read is not". CleansiaCustomer: "all T-0835 tests and `testTheReadSeesTheDarkSurface` pass on 16.4".
  - On the catalog: "every catalog edit was ratified by ruling 5 or is an inline descriptive edit it allows." "The cited lines check out (partner `AuthRepository.kt:136`/`:198`, customer `:217-222`, `Auth.swift:197`)."
  - Blocking items for this ticket, and what became of them:
    - Item 1, "**The iOS customer suite fails at HEAD, on both 18.6 and 16.4.**" Fixed in `6d6bf94`, as a separate commit.
    - Item 5, "**No changelog entry for changes cleaners will notice (Gate 7):**", among them "the iOS Pending offers price reads Kč". Fixed in `6672db1`.
    - Item 6: the currency-symbol row said "on iOS only the Pending offers price is pinned", which was false. Fixed in `f8473c7`.
    - Item 7: the docs edits were still uncommitted in the worktree. It found that "rulings 5(i)–(v) and the ruling 4 text are word for word", and asked to "Cite your "Finish the tasks from P2" for `security.md`, and name `features.md:203` as a deliberate addition." Committed in `9952094`, whose message does both.
  - Should fix, for this ticket: "**N6 (Gate 8.5, partial):**" the offer price had only a source pin and no 16.4 render check. The scratch render harness drew it on 16.4 and 18.6.
  - Its open items for this ticket: "AC5, the no-mail device check, was not run." The third device pass ran it: iOS PASS on Help, the Approval route not reached, Android NOT OBSERVED (AC5). On AC6 it found that the count "hits the ruling's threshold" and that the memo, its test and the comment change were owed. They landed in `58f67cf`.
  - It found the ticket unfiled: "T-0839 is not filed in the tree". `1791ec5` files it.
- **Security review** (`p5/impl/security_native.json`): it covered T-0835, the spec change and the generated clients, not this ticket. This ticket is not security-touching.
- **Correctness review of the later commits**, `6d6bf94` to `6672db1` (`p5/impl2/review_native_followups.md`): "**Verdict: CHANGES REQUESTED.**" "The three code commits are correct, and every catalog entry is routed properly."
  - "**B1 — T-0839 AC3 is not met**": four passages still named two wipe paths, among them `ios-app-architecture.md:772-773`, which this record had called a decision record. Fixed in `3bd7b2b`.
  - On `58f67cf`: "the change is correct on both 16.x and current iOS". On `6d6bf94`: "True", in its per-commit table. Its N5, the comment's "this app holds", is fixed in `3bd7b2b`.
  - Its N6: `ios-ci.yml:17-23` does not trigger on the Android files that two iOS tests read, so an Android-only PR can break them and merge green. Reported toward T-0849.
- **Security review of the later commits** (`p5/impl2/security_native_followups.md`): "**VERDICT: PASS-WITH-NOTES.**" For this ticket: "## 2. 734b89fde — the four session-wipe triggers (S11): PASS on both platforms". Its N1, account deletion not marked as the customer apps', is fixed in `3bd7b2b`.
- No review re-read `3bd7b2b`, `8c26004` or the records.
- **AC3** is met at the head: `3bd7b2b` brought the remaining passages to the four triggers, among them `frontend.md`'s membership generation, `patterns-mobile.md:316`'s shorthand and `ios-app-architecture.md:739`'s "clear-on-logout", which this record's refuters found; check-catalog-claims holds at C1 0 · C2 0 · C3 0. The heading at `docs/mobile-app/patterns.md:6` ("must be wiped on sign-out") is not counted, as the correctness review of the later commits ruled, because its body at `:11-12` names all four.
- **AC5** is not met. The iOS Help check passed in the third device pass; no pass reached the rejected-registration Approval route on either app; the Android half waits for an image or for Mike's word, recorded here and on T-0825.
- **AC6's reading.** No device was used, so the timings above are simulator figures and the decision rests on the miss counts, as ruled.
- **Catalog-edit routing** (the routing test in `agents/knowledge/conventions.md`):
  - `109d76a` and `f8473c7`, the currency-symbol row in `patterns-mobile.md`, edited inline. The row sits in a block marked "(descriptive — not a rule on call sites)", and the design ruling says no routing test fires. Code sweep at the head: `symbol: [a-zA-Z.?]*[cC]urrencyCode` over the three iOS `Sources` folders finds nothing. Every remaining `OrdersFormat.money` call takes the server's `currencySymbol` or `EarningsFormat.currencySymbol`.
  - `7e51993`, the `:430` parenthesis, the new dark-read entry and the lint rule. Ratified by the design ruling (item 5 (vi) to (ix)), with its routing: "Test 1 fires once: DisputesListCardTests.swift:86, driven with .dark at :58." That site is migrated in the same commit. "Test 2 fires", and "a SwiftLint custom_rule is iOS's T1-CI mechanism". "Test 3 does not fire". The baseline is zero: SwiftLint reports 0 violations at `6672db1`, and ios-ci runs `swiftlint lint --strict` (`ios-ci.yml:178`). check-catalog-claims gives one advisory on the entry's `StatusBarFadeScrollView.swift:319-342` citation, which never blocks; the ruling's wording was kept.
  - `9952094`, E9 in `consistency.md` and the two SessionScopedCache passages in `patterns-mobile.md`. Ratified by the design ruling (item 5 (i) to (iii)), with its routing. Test 1 does not fire: every sign-in that stores tokens wipes the caches first. Test 2 fires, because three becomes four, so it is priced with the closed-roster line. Test 3 does not fire.
  - Re-checked at the head: partner `AuthRepository.kt:136` and `:198`, customer `AuthRepository.kt:222` and iOS Core `Auth.swift:197` are the wipe calls. The roster's tests run in CI: `:partner-app:testDebugUnitTest` (`android-ci.yml:89`) and CleansiaCore (`ios-ci.yml:200`).
  - `.claude/agents/security.md` is a charter, not a catalog page. Its edit rests on Mike's "Finish the tasks from P2".
- **Gate 7:** the CHANGELOG entry under Fixed in `6672db1`, "a reserved job's price on *Pending offers* shows the koruna as *Kč*" (T-0839). The commit's message gives the rest no entry: the support-address constant is a refactor, the memo makes no visible change, and the test, lint and docs changes are excluded.
- **Reported, not fixed:**
  - Nineteen code comments still name two wipe paths. Android: `core/auth/SessionScopedCache.kt:15`, `core/auth/AuthAuthenticator.kt:38` and `:110-112`, partner `core/auth/UserProfileStore.kt:47` and `core/auth/EmployeeIdResolver.kt:24-25`, and customer `core/auth/SessionScopedModule.kt:24-25`, `core/auth/AuthModule.kt:200-201` and, each "Cleared on sign-out / account-delete", `core/disputes/DisputeRepository.kt:27`, `core/loyalty/LoyaltyRepository.kt:25`, `core/orders/OrderRepository.kt:28` and `core/referral/ReferralRepository.kt:26`. iOS, each "so sign-out / forced-401 wipes it": customer `SavedAddressRepository.swift:12-13`, `DisputeRepository.swift:9-10`, `MembershipRepository.swift:7-8`, `NotificationBadgeModel.swift:7-8`, `OrderRepository.swift:8-9`, `RecurringBookingRepository.swift:7-8` and `LoyaltyRepository.swift:7-8`, and partner `NotificationBadgeModel.swift:7-8`. Two test comments name the same two paths: customer iOS `HomeTabViewModelTests.swift:410-411` and partner Android `UserProfileStoreTest.kt:19-20`. Not on a ticket.
  - Fourteen code comments give sign-out or logout as when a cache is wiped, and name no other trigger. Android customer: `core/data/AddressRepository.kt:220`, `core/disputes/DisputeRepository.kt:209`, `core/loyalty/LoyaltyRepository.kt:132`, `core/orders/OrderRepository.kt:283` and `core/referral/ReferralRepository.kt:112`, each "called on sign-out", and `core/loyalty/LoyaltyRepository.kt:54-55`, "[loaded] only ever falls back to false on sign-out". Android core: `core/freshness/Staleness.kt:35`, "on logout". Android partner: `core/notifications/NotificationsModule.kt:49-50`, "runs on every sign-out", `core/network/NetworkModule.kt:59-60`, "needs flushing on sign-out", and `data/checklist/OrderChecklistRepository.kt:86-87`, "a sign-out is the only signal". iOS: Core `State/Staleness.swift:38-39`, "Sign-out, and any mutation"; customer `SavedAddressRepository.swift:22`, "wiped on sign-out like the rest", and `CustomerAppContainer.swift:181-182`, "a logout empties all of them (S11)"; partner `PartnerAppContainer.swift:142-143`, "on every sign-out". A comment that says what the sign-out path does is not counted, such as customer `core/auth/AuthRepository.kt:33` ("Sign-out wipes every [SessionScopedCache]") or partner `core/auth/EmployeeIdResolver.kt:71` ("the sign-out wipe cleared it"). Not on a ticket.
  - `docs/architecture/security-rules.md:840` (S11) and `agents/knowledge/consistency.md:271` say an iOS cache registers itself (`register(self)`). No iOS type does; the app containers register the caches they hold, which is why `3bd7b2b` corrected the catalog's parity row. Not on a ticket.
  - Four partner iOS views hold a `RemoteImageCache` of their own that nothing registers: `LockoutCard.swift:109`, `PhotosSection.swift:10`, `ProfileView.swift:12` and `RegistrationLockView.swift:9`. It is a `SessionScopedCache`, and its doc comment says a holder registers it (`RemoteImageCache.swift:16-18`). Whether an image outlives a session change depends on the view's lifetime, which no one checked. A code question for S11. Not on a ticket.
  - Both apps' `NotificationFeedRepository.kt` doc comments (customer `:18`, partner `:19`) list three triggers, "sign-out/forced-401/deletion". Both omit sign-in, and the partner app ends no session on a deletion request (`DeleteAccountViewModel.kt:24-27`). Not on a ticket.
  - `customer-app/…/features/rewards/RewardsTab.kt:1187` spells the domain out, `"https://cleansia.cz/r/$code"`, outside `CleansiaWeb`. iOS has `CleansiaWeb.referralLink(code:)`. Not on a ticket.
  - `agents/knowledge/patterns-mobile.md:214` cites `DashboardRepository.kt:243-252` for the wire-mapping rules, but those lines hold `clear()` and `getStats`. Pre-existing. Not on a ticket.
  - From the measurement, two costs the memo does not remove. Not on a ticket:
    - On iOS 16 the partner hub's body runs three times per update because of `@Environment(\.openURL)` (`ProfileHubContent.swift:17`). 334 of 481 hub re-renders in the 16.4 run came from that value alone.
    - The first `ImageRenderer` render in a process costs 16 to 307 ms on the simulator (`StatusBarFadeScrollView.swift:322`). It lands on the first screen with the fade, and needs a device trace before anyone acts.
  - This ticket's NOT doing items still hold at the head and are on no work ticket. `SessionManager.registerSessionScopedCache` and `clearSessionScopedCaches` have no callers (`SessionManager.swift:38`, `:42`). The partner `refreshPendingOffers` republishes with no session guard (`OrdersRepository.kt:263-269`). `DashboardRepository.invalidate()` has no caller (`:119`, `:236`).
  - `ios-ci.yml:17-23` triggers on none of the seven Android files the two Android-reading iOS tests (`HelpSupportContactTests.swift`, `WorkContractStringsTests.swift`) read: `HelpSupportScreen.kt`, `CleansiaWeb.kt` and the customer `strings.xml` in en, cs, sk, uk and ru, so an Android-only PR can break them and still merge green. T-0849 is the natural home; not on a ticket yet.
  - Already filed, so not repeated here: T-0845 (the customer Android sign-in wipe's missing test) and T-0846 (the possible iOS 16 text-field ink in dark mode).
