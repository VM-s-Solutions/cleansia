---
id: T-0805
title: Reduce initial web bytes and repeated validation reads, repair Swift warnings
size: L
owner: pm
created: 2026-10-08
updated: 2026-10-09
depends_on: [T-0803]
blocks: []
stories: []
adrs: []
layers: [backend, frontend, ios, docs]
security_touching: true
---

## Context

Mike explicitly selected Wave B on 2026-10-08 after the remaining-roadmap review.
The approved estimate is 3–5 engineering days, including comparable measurements,
focused regression and relevant full verification. This is one approved wave and one
branch/PR, with one commit per independent cause. INDEX.md alone records ticket status.
These are measured implementation reductions and documentation corrections with preserved
behavior; no new product rule, architecture decision or general abstraction is introduced.

Latest master at entry is `ed1e1b8fe8c6963758063def22dbc424446d0b9d` (PRs #311 and #312
merged). All seven causes were confirmed in the entry tree. The immutable original audit is
`agents/AUDIT-2026-10-07.md` in the primary checkout; implementation evidence is retained
outside the tree at `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`.

## Acceptance criteria

- [x] **AC1 / W-06** — Partner/admin login uses responsive encodings of the original
  mascot with unchanged visible artwork, layout and behavior. Comparable browser
  measurements record transferred bytes and LCP; replacement pixels and responsive
  selection are inspected. The different-glow existing WebP is not a substitute.
- [x] **AC2 / W-02** — Chart.js code is loaded with the partner dashboard instead of the
  application shell. Existing dashboard charts render and behave correctly; production
  bundle/metafile and browser waterfall evidence distinguishes initial and lazy bytes.
- [x] **AC3 / W-03** — Partner/admin configure one animation provider strategy, with
  existing dialogs, overlays and route behavior preserved. Initial bundle evidence and
  relevant existing regressions verify the result.
- [x] **AC4 / W-08** — The admin notifications page loads through the existing lazy child
  component pattern, with thin route metadata eager and launcher links, route shape
  and permissions retained. Route navigation and
  production artifact/waterfall checks verify the behavior and loading boundary.
- [x] **AC5 / B004** — Each recurring-update validation run reads the owned template
  once, preserving validation ordering, failed ownership/null results, retired held
  selections and the separate handler-owned current/tracked read. Comparable local
  evidence records SQL count and duration; regression assertions protect semantics.
- [x] **AC6 / MOB-REL-01** — Repair the three verified Swift concurrency warning causes
  in Staleness, customer OrderEventBus and partner Splash with the smallest existing
  isolation pattern. Preserve splash behavior, event delivery and cancellation; the
  same compiler settings/build method record before/after warnings. No Swift-language
  migration, app-startup optimization or new abstraction is implied.
- [x] **AC7 / D-01** — Reconcile the audit's operating-documentation drift with current
  implementation and owner rulings. Align framework versions, documented tenant/auth
  behavior and superseded operating instructions without changing runtime policy.
- [x] **AC8** — Evidence uses comparable before/after methods with at least five
  observations where measured in the audit. Scratch helpers, builds, raw data and exact
  command records remain outside the maintained tree. Gains and unchanged values are
  reported separately; no latency gain is inferred from byte/read reductions alone.
- [x] **AC9** — Relevant full backend/web/iOS verification, repo checkers, docs build,
  graph refresh and independent source/security/measurement review pass. Latest master
  is merged before final verification; exact-head CI is green. The PR gives before/after
  results. Mike explicitly authorized merge after all applicable local/review and
  exact-head CI gates pass, followed by Waves C and D separately.

## Out of scope

Waves C–F, broader Board/preview queries, logging/compression, digest/cleanup batching,
native request parallelism/cache work, Android dead-code cleanup, unranked audit
candidates, API/schema changes, new features, visual/design polish, new maintained
framework/type/harness/CI gate, CI policy or billing changes, DEV/PRO/deployment/reset,
providers/APNs/TestFlight/distribution, physical-device claims and owner settings.
Preserve the intentional native splash hold and Xcode string catalogs. Do not edit
the immutable audit or its phone copy.

## Implementation notes

Reuse existing providers, route patterns and FluentValidation request context. Scope
Swift changes to the existing isolation model, preserving all event and UI semantics.
Do not hand-edit generated clients. No contract change or client regeneration is
expected. Coordinate heavyweight builds and timing windows to avoid contention.

Carry the three B010 merge-closure Markdown records into this approved branch; this
does not add a second implementation PR for the completed Board fix.

## Work record

- 2026-10-08 19:25 UTC — Mike selected Wave B. Fresh origin/master verified at `ed1e1b8`;
  reused the attached managed worktree on `codex/wave-b-loading-reads`. Ground-truth,
  baseline preservation and independent implementation lanes begin within this scope.
- 2026-10-09 — All seven approved causes committed separately; final native
  cause `cf55783f`. Fresh fetch004/merge003 at 00:05:58 UTC leaves master `ed1e1b8`
  unchanged. Root backend 9,758 passes, web 4,475 passes/65 lint projects, nine repo
  checkers/eight self-test files, VitePress build and bounded AST/source review pass.
  Root independently executes the clean full native schemes: 3,545 passes/one existing
  VoiceOver-dependent skip/zero failures. Earlier author results remain supplemental
  history. Root also executes all 19 touched-code iOS 16.4 tests without
  failure/skip. Both lints/device builds and dual-architecture compiler proof pass.
  Target Swift warning causes/lines reduce 3/14 to 0/0; overall warning debt remains.
- Floor Gate 8.5 passes its explicit tests-covered leg: Staleness 6, Splash state/
  cancellation 10 and real event-bus/refetch/order-model 3. No view/push/navigation
  changes. Default 1.8-second hold body is unchanged; injected-hold tests do not
  measure its duration. Ordinary Partner onboarding and Customer sign-in appear
  behind notification modals; desktop permission-blocked clear/interactive frames
  remain an auxiliary unverified limit. Both exact PIDs stayed alive; scoped console
  queries and owned cleanup pass. Customer made one catalogue GET denied locally
  with 503; no backend/provider delivery or whole-machine egress claim.
- Local report and exact command snapshots are in `agents/WAVE-B-2026-10-08.md`.
  AC9 remains unchecked for final record checks and forthcoming PR/exact-head CI at this
  publication cutoff. Final CI/merge closure will be recorded with the next wave;
  no future successful check or merge is implied here.

## Review

Source, security, measurement and completed local stack/floor gates accepted by
independent reviewers and root execution. Root's actual clean full native run closes
the previously missing Gate 8 execution artifact; its own commands and counts follow. Gate 0.5: B004 named read-once validation regression
is actually RED before (nine passes/six expected failures) and GREEN after (15 passes),
with 151 related passes; native compiler evidence is actual dual-architecture output
binding rather than a behavior mutation, and artifact/visual byte evidence is qualified.
Gate 6.5: `A_Validation_Reads_The_Owned_Template_Once_With_The_Caller_Token` requires
valid production validation plus a single actual loader call; a no-op/null loader
does not satisfy it. Existing guards, auth/tenant/ownership, cancellation and held
retired-selection semantics remain. No new maintained framework/type/harness/gate.

Formal bounded receipts include backend final-floor/gates `ea8a8f7a`, Gate 8.5
applicability `fcfb87e8`, ordinary-floor closure `6997a2c1`, root full backend
`c476929a`, root web `58c1dc38`, final web measurements `31ccff59` and source checklist
`1f0ff237`; full paths/hashes and observation limits remain in the report/scratchpad.
Existing warning/advisory debt and one native skip remain visible. Exact-head hosted
CI and PR delivery are pending separately; Mike's conditional merge authorization
is already present.

### Own commands and counts

Root executed the current combined-tree verification. The reviewer/PM native requirement is met by these clean root builds and full suites; earlier author-executed schemes and generic-device builds remain supplemental history. Core starts with an absent owned DerivedData directory; three serial schemes populate the same fresh tree, then consume their pinned .xctestrun without rebuilding on owned iOS 18.6 `06BFFC4B-51D7-438C-A6FC-DB4E3A2B6F48`. Existing SourcePackages checkouts are reused with resolved-version/update flags. No fresh whole-cache integrity or speed claim follows.

| Root suite | Build wrapper seconds / exit | Full wrapper seconds / exit | Passed / failed / skipped / total | Build errors / warnings / analyzer warnings |
|---|---:|---:|---:|---:|
| CleansiaCore | 28.131322 / 0 | 18.618246 / 0 | 763 / 0 / 0 / 763 | 0 / 14 / 0 |
| CleansiaPartner | 68.365767 / 0 | 18.278912 / 0 | 1,061 / 0 / 0 / 1,061 | 0 / 8 / 0 |
| CleansiaCustomer | 103.544792 / 0 | 28.428274 / 0 | 1,721 / 0 / 1 / 1,722 | 0 / 24 / 0 |

**Actual total: 3,545 passed, zero failed and one existing conditional AX skip out of 3,546 tests.** The skipped case is `ProfileEditChipTests.testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated`. Fresh full-build warnings (14 Core / 8 Partner / 24 Customer) differ from the repaired production comparison of three target causes / 14 emitted lines → zero / zero. Backend independently rehashed/parsed actual root xcresults, fresh changed-source SwiftCompile records, current three sources, 453 SDK paths, two specs, six catalogs and app inspections08/09. Review SHA-256 `2d6640bd17cd801e6958ec227858e167ba252c67b2f853cde75170ec3a2ddc1f`; its reader does not execute native builds/tests. Core is unhosted.

Later independent lint/lifecycle closure SHA-256 `976caa8412e7636d1f8cadb4be35ee9bb0ad7152f407ef0f3e30e09c9d24ddf5` confirms closing source pins and exact own cleanup. Root SwiftFormat 0.60.1: 0/946 files require formatting, 12 skipped; root SwiftLint 0.65.0: zero violations in 945 files. Both exit 0. Real app inspections08/09 pass. Exactly owned guest shutdown/delete exit 0 (5.121961 / 5.100970 wrapper seconds); the delete receipt records its exact device directory absent. No global cleanup is claimed.

| Other actual root gate | Wrapper seconds / exit | Outcome |
|---|---:|---|
| root-b-backend-release-build-001 | 25.062039 / 0 | Release build: zero errors; 219 warning diagnostics match baseline. |
| root-b-backend-full-verification-001 | 563.354992 / 0 | Unit 8,479, integration 845, host 434: 9,758 passed; zero failures/skips. |
| root-b-web-full-verification-002 | 316.187773 / 0 | 4,475 passed, zero failures/skips; 341 suites, 62 Jest projects, 65 lint projects; eight typecheck-guard cases, app typecheck and three production builds pass. |
| root-b-repo-full-verification-001 | 20.064959 / 0 | Nine checkers and eight self-test files pass; eight self-tests, zero failures/skips. |
| root-b-docs-build-final-001 | 15.060510 / 0 | VitePress build passes; existing large-chunk warning retained. |

<details>
<summary>Exact root parent argv, UTC and cwd</summary>

All six native parent commands use cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`. The exact recorded parent argv below retains source/script/attempt inputs. Other cwd values are shown explicitly. Root app inspection and owned lifecycle argv, actual xcode/guard child argv, producer source/product/result pins and raw logs remain in the final command/producer annexes; these references are not extra executions. Wrapper duration includes setup/guards and is not isolated app timing.

**root-b-ios-core-clean-build-001** — UTC `2026-10-09T00:39:06.281571+00:00`; exit 0; wrapper `28.131322458` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "build", "--scheme", "CleansiaCore", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-ios-core-clean-full-001** — UTC `2026-10-09T00:40:57.313839+00:00`; exit 0; wrapper `18.618245750` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "test", "--scheme", "CleansiaCore", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-ios-partner-clean-build-001** — UTC `2026-10-09T00:41:54.347128+00:00`; exit 0; wrapper `68.365766625` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "build", "--scheme", "CleansiaPartner", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-ios-partner-clean-full-001** — UTC `2026-10-09T00:43:18.430346+00:00`; exit 0; wrapper `18.278911959` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "test", "--scheme", "CleansiaPartner", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-ios-customer-clean-build-001** — UTC `2026-10-09T00:44:32.477588+00:00`; exit 0; wrapper `103.544792250` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "build", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-ios-customer-clean-full-001** — UTC `2026-10-09T00:46:42.697501+00:00`; exit 0; wrapper `28.428274167` seconds.

```json
["python3", "-E", "tools/ios_b_root_full_tests.py", "test", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-root-latest-simulator-01.json", "--attempt", "02", "--preflight-inspection-attempt", "07", "--new-product-inspection-attempt", "08"]
```

**root-b-backend-release-build-001** — UTC `2026-10-08T21:25:26.316695+00:00`; exit 0; wrapper `25.062039417` seconds. Cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/source-after/src`.

```json
["python3", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/mobile_guarded_exec.py", "--start-free-gib", "12", "--reserve-gib", "8", "--deadline-s", "1800", "--terminate-grace-s", "10", "--", "dotnet", "build", "Cleansia.Api.sln", "-c", "Release", "--no-incremental"]
```

**root-b-backend-full-verification-001** — UTC `2026-10-08T21:26:01.237927+00:00`; exit 0; wrapper `563.354991500` seconds.

```json
["python3", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/backend-full-verification.py", "--id", "final-001", "--timeout-seconds", "1800"]
```

**root-b-web-full-verification-002** — UTC `2026-10-08T22:04:03.121451+00:00`; exit 0; wrapper `316.187772542` seconds. Cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia`.

```json
["python3", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/root-web-verification.py", "--id", "final-002"]
```

**root-b-repo-full-verification-001** — UTC `2026-10-08T22:45:28.151616+00:00`; exit 0; wrapper `20.064958500` seconds. Cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia`.

```json
["/usr/bin/env", "-u", "NX_HEAD", "-u", "NX_BASE", "PATH=/Users/michael/.codex/scratchpads/cleansia-audit-2026-10-07/tools/web-tooling/node_modules/node/bin:/opt/homebrew/bin:/usr/bin:/bin", "NPM_CONFIG_OFFLINE=true", "NPM_CONFIG_UPDATE_NOTIFIER=false", "NX_DAEMON=false", "NX_NO_CLOUD=true", "python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/mobile_guarded_exec.py", "--start-free-gib", "12", "--reserve-gib", "8", "--deadline-s", "1800", "--terminate-grace-s", "10", "--", "python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/verify-repo.py", "--id", "final-001", "--skip-docs"]
```

**root-b-docs-build-final-001** — UTC `2026-10-08T22:14:02.973765+00:00`; exit 0; wrapper `15.060510292` seconds. Cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia/docs`.

```json
["python3", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/mobile_guarded_exec.py", "--start-free-gib", "12", "--reserve-gib", "8", "--deadline-s", "1200", "--terminate-grace-s", "10", "--", "/Users/michael/.codex/scratchpads/cleansia-audit-2026-10-07/tools/web-tooling/node_modules/node/bin/node", "node_modules/vitepress/bin/vitepress.js", "build"]
```

**root-b-ios-swiftformat-lint-final-001** — UTC `2026-10-09T00:47:39.585910+00:00`; exit 0; wrapper `5.059359875` seconds. Cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia/src/cleansia_ios`.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/mobile_guarded_exec.py", "--start-free-gib", "12", "--reserve-gib", "8", "--deadline-s", "1800", "--terminate-grace-s", "10", "--", "/opt/homebrew/bin/swiftformat", "--lint", "."]
```

**root-b-ios-swiftlint-lint-final-001** — UTC `2026-10-09T00:48:32.055422+00:00`; exit 0; wrapper `5.063630000` seconds. Cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia/src/cleansia_ios`.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/mobile_guarded_exec.py", "--start-free-gib", "12", "--reserve-gib", "8", "--deadline-s", "1800", "--terminate-grace-s", "10", "--", "/opt/homebrew/bin/swiftlint", "lint", "--strict"]
```

</details>

Web source acceptance is attributed to root/mobile/backend, rather than its author. The web author independently reviewed backend/native/docs. Existing tenant/auth/private guards and route permissions remain source-qualified; no blanket runtime-security certificate is asserted. Gate8.5 closes this no-UI/push/navigation isolation scope through 19 actual covered tests at 16.4. Auxiliary ordinary-launch screenshots remain modal-obstructed; Customer makes one owned-loopback read receiving 503 without a real backend/database. The conditional AX skip and warning debt remain.

Report/ticket publication checks and exact-head CI remain pending at this cutoff. Mike has already authorized merging B after those checks pass, then C/D separately.


### 2026-10-09 quote-readiness correction

The first B head `6607aba7…` failed the original booking promo currency assertion. The only additional maintained source is the existing test helper, final SHA `47eafa5a…`: subscribe before update/debounce, wait for the exact quoted state within two seconds, retain cancellation and original currency/business assertions. Production booking/quote/promo/currency behavior is unchanged. Intermediate file-length lint failure remains; final SwiftFormat and strict SwiftLint passed without a waiver.

| Root-owned completed evidence | Actual result and qualification |
| --- | --- |
| Matched BEFORE20 | Every suite 1,721 passes / zero failures / one existing AX skip; full failed suites 0/20, quote failed suites 0/20, five callers 100 passes. |
| Matched AFTER20 | 34,419 passes / one unrelated profile failure / 20 AX skips across 20 suites; full failed suites 1/20, quote failed suites 0/20, five callers 100 passes. Observation 16 and failed controllers remain; no local failure-rate gain is established. |
| Fresh root functional group | Core 763 + Partner 1,061 + separate Customer02 1,721 = 3,545 passes / zero failures / one existing AX skip. Customer02 is outside the rate cohort. Independent native review `92f64309…`; rate review `e8e89504…`. |
| Actual compiler/runtime | Xcode 26.3 (17C529), observed simulator iOS 26.3.1 (23D8133), current fixture04. Observed fresh BFTC build errors zero, warnings 14 / 8 / 24; these are not a matched fresh BEFORE warning-count comparison. Original source04 targeted 3-cause / 14-line → zero warning proof remains separate. |
| Currency mutation and fresh restoration | Original assertion line 220 RED: 0 passes / 1 failure / 0 skips, Xcode 65, no readiness failure. Exact Codes source restored; fresh restored BFTC/inspection and named GREEN: 1 / 0 / 0. Independent actual review `1518618e…`. Actual restored parent cwd differs from the frozen plan; V2 reconciliation preserves that difference. |
| Scoped evidence preservation | Verified lossless original/retired BEFORE Products archives keep six original modules and 12 historical Customer files in place; all 24 proof hashes match. Only BEFORE01 result bundle archived; plans 02–10 unused. Retired cache discard preserves all 36 finite hashes. No allocated-space or speed claim; native 12/8 guards unchanged. |
| Fresh repository checks | Nine existing checkers and eight self-tests pass; self-tests 8 / 0 / 0. Receipt `c92a4dbc…`, root wrapper 0 / 15.768486 s. Existing catalog/consistency advisories remain; unchanged docs retain prior build, not a newly repeated build. |
| Floor, final lifecycle and command tail | COMPLETED: floor31 passes / zero failures / zero skips on actual16.4; eight owned lifecycle stages exit zero and exact two directories absent (producer observation); tail04:47:35.822623 UTC captures375 parent rows /409 announcements, separately qualified. Peers `05929b92…` / `07fbf5d7…`; tail `86bdf5c0…`. |
| Corrected head / exact 38 paths / fresh CI | PENDING delivery. Actual current 38-path roster retained; new-head seven workflows/eight jobs/36 critical steps must pass independently. Existing successes do not cover the future head. |

The unchanged historical backend/web/repository/docs proof and actual own commands remain in the existing Review/report prefix. This correction adds the following actual root parent invocations; nested Xcode/guard/inspection commands belong in the final append-only command tail, with observed metadata only. These literal arrays are execution records, not planned or inferred commands.

<details><summary>Actual root parent commands for fresh native and mutation/restoration</summary>

`root-b-ci-quote-after-cleansiacore-build-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:24:03.782613+00:00`, exit 0, wrapper 28.048570 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "build", "--scheme", "CleansiaCore", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01"]
```

`root-b-ci-quote-after-cleansiacore-full-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:24:44.448347+00:00`, exit 0, wrapper 16.512464 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaCore", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01", "--build-attempt", "01", "--new-product-inspection-attempt", "11"]
```

`root-b-ci-quote-after-cleansiapartner-build-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:25:07.839355+00:00`, exit 0, wrapper 67.483121 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "build", "--scheme", "CleansiaPartner", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01"]
```

`root-b-ci-quote-after-cleansiapartner-full-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:26:40.261253+00:00`, exit 0, wrapper 17.627266 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaPartner", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01", "--build-attempt", "01", "--new-product-inspection-attempt", "11"]
```

`root-b-ci-quote-after-cleansiacustomer-build-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:27:06.449841+00:00`, exit 0, wrapper 98.391839 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "build", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01"]
```

`root-b-ci-quote-after-customer-final-full-02`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T02:51:36.642696+00:00`, exit 0, wrapper 29.231058 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "02", "--build-attempt", "01", "--new-product-inspection-attempt", "11"]
```

`root-b-ci-quote-mutant-customer-named-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T03:33:05.085924+00:00`, exit 0, wrapper 34.042001 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01", "--arm", "mutant", "--selection", "named", "--new-product-inspection-attempt", "11"]
```

`root-b-ci-quote-restored-customer-named-01`: cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/source/src/cleansia_ios`, UTC `2026-10-09T04:04:05.932625+00:00`, exit 0, wrapper 18.468671 s.

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-latest-simulator-01.json", "--attempt", "01", "--arm", "restored", "--selection", "named", "--new-product-inspection-attempt", "11"]
```

</details>

Actual direct root repository parent argv: `python3 -E /Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/verify-repo.py --id ci-quote-final-001 --skip-docs` (cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia`); exact absolute argv/UTC/log hashes are retained by `root-b-repo-ci-quote-final-001` and the final command tail. No native wrapper/guard change is implied by these pure source checks.

All original failures, raw summaries/logs and source preimages remain. The current 38-path delivery roster includes historical Board merge-closure records; it is not a claim that all 38 files are new implementation changes. Mike authorized merge when B's actual local/review/exact-head CI gates close, then C/D sequentially; that authorization remains present. C/D implementation remains held; this correction records actual local closure while future exact-head CI remains pending.

Actual root floor parent argv (cwd `/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08`, UTC `2026-10-09T04:45:19.792576+00:00`, exit0, wrapper 18.600373s):

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/ios_b_ci_quote_after_tests.py", "test", "--scheme", "CleansiaCustomer", "--owner-receipt", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/raw/ios/uniqueowned-ci-quote-floor-simulator-01.json", "--attempt", "01", "--build-attempt", "01", "--selection", "class", "--runtime", "floor", "--new-product-inspection-attempt", "11"]
```

Actual root repository parent argv (cwd `/Users/michael/.codex/worktrees/wave-a-correctness/cleansia`, UTC `2026-10-09T04:38:46.148216+00:00`, exit0, wrapper 15.768486s):

```json
["python3", "-E", "/Users/michael/.codex/scratchpads/cleansia-wave-b-2026-10-08/tools/verify-repo.py", "--id", "ci-quote-final-001", "--skip-docs"]
```

All nine new native diagnostic/full parent arrays and the direct repository command are actual recorded invocations; exact nested Xcode/guard/guest operations and observed metadata remain in the full report tail. Producer announcements do not establish child execution. The current graph peer binds 12 finite sources and 169 saved structural nodes (`cc724c2f…`). Completed mutant cache discard retains all 36 finite hashes (`e6ce8ed2…`); restored cache untouched. Future publication checks/CI/merge have a later actual delivery cutoff.


### 2026-10-09 final CI and actual merge closure

**AC9 is complete.** Earlier pending statements above describe their original
publication cutoffs and remain unchanged. Corrected head `8e161c96087e3b2a949a6cf5500606b402d58f5f`
at base `ed1e1b8fe8c6963758063def22dbc424446d0b9d` passed all seven observed workflows,
eight jobs and 36 critical named steps. Hosted iOS selected Xcode 26.6 (17F113)
and recorded 3,545 passes, zero failures and one skip; hosted OS/runtime remain
unestablished. Frontend/Backend/iOS workflow walls exceeded the 15-minute target
at 1,420/1,218/1,167 s while succeeding. Shared strict `build` policy and optional
skipped uploads remain qualified; all prior failures and local limits are retained.

[PR #313](https://github.com/VM-s-Solutions/cleansia/pull/313) actually merged
2026-10-09 08:08:21 UTC by normal SHA-guarded REST merge as
`18a2e5ac8e118be1c527443dc8f6cc48ee30ce6c`, with parents the base and corrected head.
Merge tree `464b77865ce115255d1dac467a10afa5decafdc8` equals the verified head tree;
the postmerge recorded worktree was clean and fresh master bound to that merge.
The [Wave B report](../../WAVE-B-2026-10-08.md#2026-10-09-final-hosted-ci-and-actual-merge-closure)
records exact CI, independent review, actual publication/merge receipt pins and
one verbatim delivery annex through ledger entry 1,155. Exporter completion
(B row 1,156) and C scratch bootstrap (B row 1,157) remain in B's ledger; later
C-owned actions use C's ledger. The actual C cross-ledger reference
`raw/b-c-cross-ledger-transition-001.json`, SHA-256
`ae0ed0aef4a69c7e79fcc7a00f40fb2225d3c9679ed4e90564078d0baf9101e1`, links those completed
B rows and C entry rows without repeating execution or extending the B annex cutoff.
These three B closure records are carried on the next approved C branch; they do not
change the merged B head. INDEX.md is the sole status record.
