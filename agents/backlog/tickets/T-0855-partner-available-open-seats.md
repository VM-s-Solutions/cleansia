---
id: T-0855
title: Use available-seat filters on both partner phone boards
size: S
owner: pm
created: 2026-10-13
updated: 2026-10-14
depends_on: [T-0854]
blocks: []
stories: []
adrs: ["0037"]
layers: [android, ios, docs]
security_touching: true
---

# Use available-seat filters on both partner phone boards

## Context

ADR-0037 §D9 records the 2026-08-03 Available-tab switch (Q-AVAIL-01), but the current phone query shapes retain isUnassigned. Decision 4 explicitly schedules that existing ruling with mike-5. Both boards must show a free second seat and an eligible cover seat while excluding the current cleaner. The P2 contact report maps the backend rule but does not freshly verify this native implementation.

## Owner decision — 2026-10-13

Mike, answer 5: "I don't think that it needs to be offerable".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This implements default decision 4, shipping ADR-0037 D9 with the slot-end change. The older ruling is cited, not re-quoted from memory.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/contact-verifier-before.md`

## Doing

- Ground-truth both partner Available query builders at the native phase head. Replace isUnassigned with hasAvailableSpots plus excludeEmployeeId on Android and iOS.
- Preserve existing statuses, paging, sorting and account/hold/currency/contract restrictions; use the existing client parameters.
- Pin each encoded wire request and exercise a half-crewed job, cover seat and current-employee exclusion under the slot-end backend rule. Record fulfillment of ADR-0037 D9 without changing its historical body.

## NOT doing

- No backend endpoint/rule/DTO change or generated client/spec regeneration.
- No new filters, dashboard behavior, translation or crew/cover lifecycle redesign.
- No backend implementation in PR 2 for this phone switch.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — Each Available request sends hasAvailableSpots and the signed-in employee exclusion, without isUnassigned. Wire tests demonstrate the old request shape RED and the new shape GREEN.
- [x] **AC2** — Both apps show an eligible free second seat and cover seat, exclude an already-assigned current cleaner, and preserve no offer at/after slot end. Record command/device evidence honestly and distinguish their scope.
- [ ] **AC3** — Both platforms full relevant suites and parity/symbol gates pass on the actual head; reviewer/security checks preserve current-account and hold restrictions; ADR-D9 fulfillment and owner ruling are recorded.

## Dependencies and execution

Phase: **PR 3**. Dependencies: T-0854. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing partner Orders list query/wire tests on both native platforms.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "I don't think that it needs to be offerable" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. Decision 4 explicitly says: "Both partner apps' Available tab sends `hasAvailableSpots` plus `excludeEmployeeId` instead of `isUnassigned`." ADR-0037 D9 is fulfilled locally with a dated head note; its historical body is unchanged. T-0854's merged backend slot-end proof remains the command authority at exact end equality.
- Evidence root for this dated follow-up: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Android evidence ids resolve through `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/records-android-preparation-002/evidence-hash-manifest.json` (SHA256 `6b433bd7c839ac2a2e12a777a6070914ac23f785c746ec8a3ee9e467e12eb21f`), whose `evidence_ids` maps each Android id below to its exact path and SHA256. The preparation draft is retained unchanged; the actual assessment verdicts are quoted below. Base `31c10f5caac57ed17dfde0b6e07a8007bb9c310f` contains the preceding phase's first closure commit. These native source overlays are local, with commit, exact-head hosted CI and guarded merge still pending.
- Both query builders resolve current employee first, fail closed on absent/unusable identity without a paged call, and forward the existing `hasAvailableSpots=true` plus `excludeEmployeeId`. Available omits `isUnassigned` and positive EmployeeId; Active/History retain positive current-employee scope without availability/exclusion terms. Existing statuses 0/2/3/4, paging/sort and restrictions remain. No backend/DTO/spec/client contract changes.
- Android canonical compiled `AvailableOrdersWireTest` 6 cases/5 AssertionError failures, then final and independent 6/6 through resolver/repository/generated Retrofit/socket. Existing Active/History control remains green; independent view-model 11/11 also passes. The prior timeout/uncaught run is retained and excluded as behavioral RED. iOS original baseline wire/query/current-ID RED methods are named in the 23-entry final matrix, then same ids pass on both floors; missing/null/blank/whitespace identity cases are real refusal controls, not scenario counts relabelled as extra methods.
- Android actual final native ordinary request `A-NATIVE-WIRE` and terminal `A-DEVICE` **PASS** on producer-bound APK: available seats plus exclusion `01AP0000000000000000000001`, statuses 0/2/3/4, offset 0/limit 50, no unassigned/positive employee/date floor. Default sort is estimatedCleanerPay descending; Soonest first selects cleaningDateTime ascending for matrix observations. CZE is joined fixture/account/HTTP evidence, not a captured native response body.
- `runtime-preparation/ios-runtime-acceptance-final-001.json` (SHA256 `40501b1b8af9dc2ef62bba9c33b57d8a358b8f99e801e01abe4befcb557c7628`) seals the final two-floor runtime receipts and their product/source bindings. It records PASS within the stated device scope, not a full ticket or whole-wave approval. iOS `runtime-preparation/ios-native-wire-001.json` qualifies **14 actual GetPaged requests, HTTP 200**, correct availability/exclusion/status/paging query joined to authenticated middleware user `01AE0000000000000000000001` and employee `01AP0000000000000000000001`. Per-floor attribution comes from UI/command intervals; no server device header or native response-body mirror is claimed.
- Native matrix **PASS within the first 50 loaded Available rows plus independent SQL/HTTP qualification**, on Android and iOS16.4/18.6: CONTROL, HALF, COVER and STARTED each listed and opened; CURRENT, FULL and EXPIRED each returned zero exact search matches. iOS receipts: `runtime-preparation/ios-final-d9-composite-004-164.json` and `ios-final-d9-008-186.json`. The incomplete 16.4 negative-search driver attempt lost focus when filtering rebuilt rows; retained failed attempt is nonproof, then the fresh refocused negative check supplied three zeros. No exhaustive page crawl is claimed.
- The reviewed seven-row PostgreSQL16 matrix under Initial `20261013160545_Initial` has four eligible rows and three excluded rows. Actual root HTTP qualification had **16 responses, all 200**. STARTED was observed before its `2026-10-14T13:15:00Z` raw slot end; expired absence plus merged T-0854 SQL/HTTP/unit equivalence qualifies the end rule. No new instantaneous exact-slot-end device gate is inferred. Base fixture 001 failed/rolled back; reviewed minimal correction002 retained 16 CZ/2 SK/2 DE cleaners and actual apply passed. Local `runtime-preparation/p3-d9-qualification-004.json` records terminal seven-row readback unchanged at 11:21 UTC; its exit-zero wrapper is `local/p3-d9-qualification-004.json`. Two owned hosts stopped and owned PostgreSQL was removed at 11:33; final runtime packet binds both receipts.
- COVER is an occupied cover model: detail caption **"2 cleaners needed · No spots left"**, with a visible enabled Take control on each platform. Record this exact observation; **no Take/material mutation** was performed, no caption/crew fix absorbed, and no payment/provider action claimed. CZE remains fixture/HTTP-bound. Those limits apply to AC2.
- `A-COMBINED` is the actual combined Android receipt, SHA256 `79723d63889bad7b0b7b7aaf83cc7c79a2f8e07191486229aa5e1b8a07f9ecb8`: forced compile 56/56 tasks, full Core 298 → Partner 881 → Customer 1694, **2873 passed, zero failed/error/skipped**, and 96/96 test tasks executed. Both Debug APK builds executed 69/69 tasks each; partner release R8 executed 55/55. All five commands exited 0. The 41 Android overlays match frozen manifest SHA256 `c6066079399fbff4f9a620f40d785ef7bbe5a6c4a2236d249c4339bd2fa9b2b9`; these are measured combined results, not an expected total or a device result.
- `ios/developer-final-packet-001.json` (SHA256 `470115f5e483f4af7f835d3b66221bbae638315771d4296f791eab51b448c031`) binds all 27 iOS overlays to `ios/developer-final-source-001-source-manifest.json` (SHA256 `1cc89799ea912d3d650ce8fc33c2a2a96aca8b308d813d261c6610ece2ba09cb`). On **each** of 16.4 and 18.6: Core 776 passed; Partner 1133 passed; Customer 1780 reported, 1779 passed and one unchanged VoiceOver guard skipped; no failures. Focused Partner 133/133 passed on each floor. All nine final attempts are retained, including the second Customer 16.4 run: the accessibility preference was already 1, reapplying 1 still left `UIAccessibility.isVoiceOverRunning` false, and readback restored the same 1. There is no spoken VoiceOver proof. The actual Dashboard source split is 24 main-file methods plus 10 barrier-file methods, all 34 passing on both floors; the older 25+9 preparation split is stale. The final RED/GREEN matrix has 23 entries with the same method ids passing on both floors. Its 19 original failed methods include an invalid assumed default-phone-grouping assertion; the epoch attempt's three failures include the still-unlabelled Menu and a wrong radius fixture expectation, both explicitly qualified. Harness errors are not relabelled as behavior RED.
- AC1/AC2 are locally met within the recorded command/device scopes. Final native iOS approval is sealed below; AC3 remains open for applicable final parity/symbol/docs/publication checks. Separate saved Android assessments preserve caller/session/hold restrictions; no full S11 closure is asserted. The native phase is uncommitted/unmerged at this point, and the wave-end DEV reset/deploy is outside this ticket's execution.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.

- 2026-10-14, local gate follow-up — T-0839 records all 11 final docs/parity/symbol commands passing and the source commit byte/mode rebind to all 8260 measured inputs. AC3 stays open until the final committed-head documentation checks and native publication are recorded; the prior pending statements above describe their earlier capture time.

## Review

### Native follow-up assessment — 2026-10-14

- Frozen Android `AP-REVIEW` and separate applicable `AP-S11` actual verdicts: **APPROVED**, limited to command/artifact scope; actual combined Android command/device board-read evidence is separately recorded. iOS prior `APPROVED_SCOPED_LOGIC` ran 104 copied-product tests, final retained-input rebind 29 PASS/0 failed. Final native iOS approval is sealed below; remaining AC3 final gates are pending. No authorization, hold, currency, contract or provider restriction is relaxed; no Take or full S11 approval is inferred.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
