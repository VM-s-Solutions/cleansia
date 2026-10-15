---
id: T-0845
title: Android customer app — one injectable seam for the Stripe key, view-model guards on card-only actions, and a pin on the sign-in wipe
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-15
depends_on: [T-0835]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: true
---

## Context

This comes from the 2026-10-12 native design ruling: open questions 1 and 2 of its item on T-0835, and open question 1 of its catalog item. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`.

Keyless builds are debug-only: a release build refuses a blank key (`customer-app/build.gradle.kts:252-262`). Lines are as of `207d2f319`.

- **The key is read inline in six places, with no seam.**
  - `BuildConfig.STRIPE_PUBLISHABLE_KEY` is read at `CleansiaApp.kt:58`, `ConfirmStep.kt:115`, `OrderDetailScreen.kt:582`, `SubscribePlusScreen.kt:100`, `CreateRecurringViewModel.kt:731` and `CreateRecurringScreen.kt:1285`.
  - T-0835's ruling adds a seventh read: `cardAvailable` in `CreateRecurringViewModel`.
  - `BuildConfig` is fixed at compile time, and android-ci builds without a key. So CI never runs the keyed branch of any of these reads.
- **The card-only view models refuse nothing.** In a keyless build, only hidden buttons keep them unreachable:
  - `OrderDetailViewModel.confirmRecurring` (`:521-573`). Here the server spends the customer's credit before the payment sheet opens (`:556-560`).
  - `MembershipViewModel.startSubscribe` (`:150-174`).

  iOS refuses all three in its view models: `OrderDetailViewModel.swift:444`, `MembershipViewModel.swift:95`, and Add card after T-0835.
- **The customer sign-in wipe has no test.**
  - `core/auth/AuthRepository.kt:217-222` clears every session cache before it saves the new tokens.
  - `AuthRepositoryTest` pins only the logout wipe (`:551-622`).
  - The partner app pins its sign-in wipe (`AuthRepositorySignInWipeTest`), and T-0839's E9 line names the customer wipe as outside its roster.

**If not built:**
- CI keeps testing only the keyless branch;
- the unguarded calls stay reachable only through a hidden button, in a debug build;
- a regression of the customer sign-in wipe (S11) fails no test.

## Doing — approved 2026-10-13

1. **One injectable "card payment available" value.** It is read once from `BuildConfig`, provided through Hilt, and used by all seven reads, so a test can set it either way.
2. **Guards.** `confirmRecurring` for a card occurrence and `startSubscribe` return before any server call when card is unavailable, as iOS does.
3. **A customer sign-in wipe test.** It checks that the caches are cleared before the tokens are saved, in that order. E9's roster gains it.

Each test goes RED then GREEN, and the full Android suites pass.

## NOT doing

- iOS, release builds, server behaviour, and T-0835's own changes.

## Acceptance criteria (done looks like)

- [x] **AC1** — No production file reads `BuildConfig.STRIPE_PUBLISHABLE_KEY` outside the seam, and a test runs a keyed branch on CI.
- [x] **AC2** — With card unavailable, `confirmRecurring` **for a card occurrence** and `startSubscribe` make no server call; the existing cash path remains allowed. The tests go RED then GREEN.
- [x] **AC3** — The customer sign-in wipe test fails when the wipe is removed, or moved after the token save.
- [x] **AC4** — The full Android suites pass.

## Work record

- 2026-10-12 — Filed from the native design ruling's open questions on T-0835 and on the wipe-trigger wording. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "Analyze and Fix those" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. Its verifier correction qualifies AC2 **for a card occurrence** and requires a separate applicable security assessment. No Add-card entry is introduced.
- Evidence root for this dated follow-up: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Android evidence ids resolve through `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/records-android-preparation-002/evidence-hash-manifest.json` (SHA256 `6b433bd7c839ac2a2e12a777a6070914ac23f785c746ec8a3ee9e467e12eb21f`), whose `evidence_ids` maps each Android id below to its exact path and SHA256. The preparation draft is retained unchanged; the actual assessment verdicts are quoted below. Base `31c10f5caac57ed17dfde0b6e07a8007bb9c310f` contains the preceding phase's first closure commit. These native source overlays are local, with commit, exact-head hosted CI and guarded merge still pending.
- Exact 26-file customer/Core `green-005` source manifest SHA256 `d235d55c62c48c306e05314cf7caa97b9a370825804c02a49d32d09b9e15d591`. `PaymentModule.kt:20` is the only production BuildConfig key reader, providing immutable `StripeConfig` with `publishableKey.isNotBlank()` availability. The application initializes Stripe only when available; ConfirmStep, OrderDetail, Membership and CreateRecurring read the injected value. Baseline had eight reads at seven logical sites in six files. Release key validation and contract/provider configuration are unchanged.
- `OrderDetailViewModel.kt:529–535` guards a keyless **CARD occurrence** before confirm/payment-intent requests; cash still confirms. `MembershipViewModel.kt:154–164` returns `SubscribeOutcome.Failed` before phase one when keyless. Keyed and keyless-cash positive call-count controls pass. The Failed comment now includes silent guarded/transport failures. Actual Android Payments exposes load/remove and receivable Checkout links; it has **no Add-card action**, so no Add-card guard or test is claimed.
- Actual customer `core/auth/AuthRepository.kt` is unchanged, SHA256 `35d43f935157bf6db387cd91e2bf9ca47fa38b87a5f80bd346319b400d88e73c`. Its actual login, confirmEmail and Google-auth routes call `handleAuthBody`; confirmed usable token conversion precedes cache clear at :222, then token save at :224. The three `AuthRepositoryTest` methods `login_clearsEverySuppliedCacheBeforeSavingFreshTokens`, `confirmEmail_clearsEverySuppliedCacheBeforeSavingFreshTokens`, `googleAuth_clearsEverySuppliedCacheBeforeSavingFreshTokens` assert exactly `[clear-A, clear-B, save]`. Scratch removal and after-save relocation each compile then fail all three; unchanged source passes. This adds coverage of the existing implementation; complete provider-roster equality is not claimed.
- `AC-COMMANDS`: qualified compiled baseline 220 tests/12 assertion failures; binding baseline 11/2. Final forced Core/customer compile 47/47 tasks and tests 61/61 tasks, full 298+1694 = 1992/0/0/0. Same assertion bytes/identities are retained where baseline/mutation-compatible, with only required fixture constructor wiring adapted. The first green004 full run executed 1992 with two failures in legacy direct-BuildConfig source expectations (`MembershipCurrencyBindingTest`, `OrderDetailRecurringGateTest`). Their old source, manifest, raw XML and receipt remain in `AC-FIRST-BINDINGS`; availability expectations followed the seam, all unrelated assertions stayed, and the amended tests also produced compiled baseline 11/2 RED before final GREEN. First compile/preview/import failures are nonproof and remain retained.
- Independent `AC-REVIEW` actually reports **APPROVED**: own deletion/default controls 178/12 expected failures and after-save 3/3 expected failures, byte/mode-exact restore then 1992/0/0/0 rebuilt. Mandatory separate `AC-S11` actually reports **PASS**: own targeted 249, lifecycle 31 and restored controls 26 all pass; independent negative copy 26/5 expected failures. Eighteen rejected/unusable response controls across the three actual entries perform no clear/save/token-clear. No whole cache-roster equality or full S11 closure is claimed.
- `A-COMBINED` is the actual combined Android receipt, SHA256 `79723d63889bad7b0b7b7aaf83cc7c79a2f8e07191486229aa5e1b8a07f9ecb8`: forced compile 56/56 tasks, full Core 298 → Partner 881 → Customer 1694, **2873 passed, zero failed/error/skipped**, and 96/96 test tasks executed. Both Debug APK builds executed 69/69 tasks each; partner release R8 executed 55/55. All five commands exited 0. The 41 Android overlays match frozen manifest SHA256 `c6066079399fbff4f9a620f40d785ef7bbe5a6c4a2236d249c4339bd2fa9b2b9`; these are measured combined results, not an expected total or a device result.
- AC1–AC4 are locally met. T-0839 records M5. No DTO/endpoint/spec re-dump was required; ordinary ignored generation ran from committed specs. The actual sign-in-test names above are the customer addition owed to E9's roster; that catalog edit remains in the later docs phase, outside this native records grant. The Android partner device receipt did not install/exercise the customer APK. No payment-provider or customer-device proof is inferred, and no new such criterion is imposed. CHANGELOG records only the keyless guard behavior. Final record/doc gates and commit/CI/merge remain pending.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.


### Native publication closure — 2026-10-14

- 2026-10-14 — Native follow-up shipped in [#333](https://github.com/VM-s-Solutions/cleansia/pull/333). Exact-head CI on `e6039a9acca40e22047a2bf4ba56a4c76a1abf63` passed all 8 check runs across 7 triggered workflows on their first attempt, with no rerun. Merged with the head SHA guard as `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` at `2026-10-14T12:55:03Z`. Exact merge-head master CI on `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` passed all 6 check runs across 6 triggered workflows on their first attempt, with no skipped job or rerun. Evidence: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-native-pr333-ci-merge-closure-001.json` (SHA256 `ca11540ebb8c221b5b6abe1af2160e107afd801ce360adba66c7275a0692e179`) and `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/master-ci-evidence-receipt-001.json` (SHA256 `1005f55da28ab299d259aea4772f2253ee42eff213314291e8d215ce4c9abde8`).
- AC1–AC4 are complete within the recorded command and applicable security scope. No customer-device, PaymentSheet or provider proof is added. The later E9 catalog roster update remains in PR 6; no whole S11 closure is claimed.
- The source/full-suite and actual committed-head gate binding is recorded in T-0839’s native publication closure below its Work record.
- The captured deployment metadata queries beginning at the merge timestamp reported zero runs for deploy-dev, deploy-pro, deploy-docs and execute-sql. The authorized wave-end DEV reset, fresh deployment and docs deployment remain due. Earlier dated pending statements record their capture time.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “Analyze and Fix those”; “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is E9 named customer sign-in ordering roster; no production roster-equality test added.
- E9 now explicitly names customer Android `AuthRepositoryTest` beside partner Android `AuthRepositorySignInWipeTest` and Core `SignInWipeTests`. The exact customer methods are `login_clearsEverySuppliedCacheBeforeSavingFreshTokens`, `confirmEmail_clearsEverySuppliedCacheBeforeSavingFreshTokens` and `googleAuth_clearsEverySuppliedCacheBeforeSavingFreshTokens`. They use supplied test caches and pin clear-before-save ordering; they do not prove the production multibinding membership. This discharges only the named E9 catalog-roster debt previously left for PR6 in the current INDEX row.
- The supplemental clause received `APPROVED_SCOPED`, with zero material findings, in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/e9-roster-security-review001/verdict.json`; its seal SHA256 is `afa79389b33b9320da7872daa95b0b57ff8e27abd121b2fe3fc8e0c73ec771f3`. That is documentation-clause/source and retained-evidence review, not a new runtime or whole-S11 result. `SessionScopedModuleTest` per Android app and iOS `SessionScopedCacheRegistryTest` remain specified/not built; E9 remains advisory. The #333 command/security closure stays done, with no customer-device, PaymentSheet or provider acceptance added.
- Source: `51a66fe807400ce9dc628e29936333aa0cf43507`, parent `dccf996787ed52e14b7a8e0305bebab3fa3ca0ea`, after the first P5 closure on P6. The 106 approved paths match the frozen afterimages and Git modes; the other 8,152 tracked paths are unchanged. Application receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/source-commit001.json` (SHA256 `02ccb3119f4c30a6cd8d51b42a4ef641ada01cac06e3d533180fe3d5f5fa542d`). This is source binding, not a new SDK, device, provider, CI or deployment result.

#### Actual P6 source-head local gates — 2026-10-14

- On source `51a66fe807400ce9dc628e29936333aa0cf43507`, tree `c6e7ac4fee9ba1a9334ac4cae7a23a784c4e19a4`, the 11 documentation/checker commands all exited 0: documentation references and catalog checks with their self-tests, the VitePress build, and the iOS-symbols, booking-policy and Available-status scans with their self-tests. These source-head results are separate from the first P5 closure-head checks. Receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/source-head-docs001/final.json` (SHA256 `2ea68d9f1b6811e6c8a9f25b60a5bc01ab6ed3dae5d93e690b8b4b25e7f2b5e7`).
- The 25 backend/frontend commands all exited 0. The fresh Release Rebuild recorded 220 warnings and 0 errors; backend unit/integration/host suites passed 8,731/975/509 with zero failed or skipped cases. The actual frontend delta contains one comment-only spec; affected lint selected nine projects and exited 0. The separate 77-project full lint was informational, also exit 0. The typecheck guard passed 8 self-tests and the three app spec programs included 73 specs; frontend Jest passed 5,041 tests in 408 suites across 74 targets (71 nonempty, three empty). All three production builds ran without Nx cache hits. Seven localhost network-stubbed Playwright smokes passed (customer/partner/admin 5/1/1). Receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/full-backend-frontend001/final.json` (SHA256 `33ae2a4702469d6b184726849dcc0cecf53543c1278370233871f1539c315173`); sealed packet SHA256 `1fcae90b147df798eefda7738386a5580061379125c6406caf54928d75559c71`.
- Exact argv/cwd, exits, raw receipt/log hashes, complete 8,258-row source snapshot bindings and retained product-map bindings are recorded in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/actual-gate-records-preparation003/actual-gate-facts.json` and the original sealed packets. These are local macOS/Docker results; hosted Node20/Ubuntu26, provider behavior and live DEV acceptance remain separate. The retained `gradlew.bat` working bytes use CRLF and its raw Git blob uses LF; each identity is qualified, with no generic normalization claim. Actual mobile local results follow below. No device, hosted-CI, merge or deployment PASS is inferred from these docs/backend/frontend receipts.

#### Actual P6 mobile local gates — 2026-10-14

- The combined 30-command full mobile receipt is `PASS` at source `51a66fe807400ce9dc628e29936333aa0cf43507`. Android's fresh compile invocation executed all 56 actionable tasks and the JVM unit invocation all 96; all three modules compiled with `--rerun-tasks --no-build-cache`. The full unfiltered JVM suites passed 2,873 cases (core/partner/customer 298/881/1,694), zero failed/errors/skipped, in 309 retained XML files. Fresh iOS 18.6 build/test results are Core 776 Passed, Partner 1,133 Passed and Customer 1,779 Passed plus one Skipped. The exact known skip is `ProfileEditChipTests/testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated()` because it requires the simulator accessibility server; it proves no VoiceOver announcement.
- The original run completed 24 commands and stopped before Customer at `2026-10-14T22:41:20.123952+00:00` on the unchanged 2.5 GB capacity guard. The stopped receipt is retained unchanged (SHA256 `c3b3862d8c6733f26bd54fd19a66091ad99eb25aee7f531369c175d16f3513cf`). A separately reviewed and admitted Customer-only continuation supplied six further commands, using this same run's fresh DerivedData and private cloned package cache. Android/Core/Partner suites were not rerun, and no previous-phase Products were used. This is one capacity pause and continuation, not a failed-test retry.
- The local mobile gate ran both iOS API-client generations through `./scripts/generate-api-clients.sh`, XcodeGen in both app directories, SwiftFormat lint before strict SwiftLint, and the three build/test schemes. Android generation tasks also executed within the fresh compile/unit invocations. The complete source proofs retain all 8,258 tracked working-byte/Git-blob/mode identities, with the known working-CRLF/raw-Git-LF `gradlew.bat` qualification. No contract/model/migration change is added. Full mobile final: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/full-mobile001/final.json` (SHA256 `cb493f835abdc65afaffec7dc3c94070c83818a6144cbfc0e8808ccca7a6e701`); sealed packet SHA256 `4e969d0ac98da4446f8dec65eb41c8ac372fbb1f0d217eb6fca0b6987deb38a7`. Exact commands, raw maps, source/product bindings and the stop/continuation remain in the retained receipts and `actual-gate-records-preparation003/actual-gate-facts.json`.
- These are local command results. There is no new iOS-floor, device, provider, PaymentSheet, R8 or VoiceOver acceptance. Hosted CI, publication and END execution remain separate. All local source suites are now recorded; actual publication closure is still owed.

### P6 publication closure — 2026-10-15

- The P6 follow-up for named customer sign-in ordering roster documentation was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. This closes the named-roster documentation debt; production roster equality remains specified and unbuilt. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

## Review

### Native follow-up assessment — 2026-10-14

- Frozen customer/Core assessment `AC-REVIEW`: **APPROVED**, command only, exact 26 owned paths. Separate mandatory applicable assessment `AC-S11`: **PASS**, command only. Independent mutations/restoration, rejected-response controls, keyed/cash controls and actual unchanged wipe-before-save are qualified above. Actual combined Android full suites meet AC4. Publication and the later E9 catalog roster update remain separate; no PaymentSheet/provider or full S11 approval is claimed.
