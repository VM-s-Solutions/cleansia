---
id: T-0820
title: Bring the workflows and web tooling up to date without changing a gate
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-15
depends_on: [T-0812]
blocks: []
stories: []
adrs: []
layers: [ci, frontend, docs]
security_touching: false
---

## Context

CI and tooling findings that need no owner ruling, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). None of them changes what runs, what is required or what passes.

## Doing

- [WCD-03] Bump every action in the PR-checking workflows to its first Node-24 major, after reading that major's breaking changes for the inputs we pass. Deploy workflows are not touched.
- [WCD-04] `node-version: 22` in `ios-symbols-ci.yml`, `booking-policy-parity.yml` and `docs-ci.yml`.
- [WCD-05] Drop the `build-root-directory` input that setup-gradle ignores.
- [WCD-21/IOS-27] Workflow comments stop stating stale facts: Windows-only development, '10x' billing while the repo is public, workflow counts.
- [WCD-12] `--output-style=static` on the informational lint step.
- [WCD-15] `nx.json` lint inputs name the three imported config modules and `^default`, not two files that do not exist.
- [WCD-16] Delete the dead root `local-registry` target, `verdaccio` and its two overrides, and the stale `graph.json`.
- [WCD-17] `order-wizard`'s coverage directory stays inside the workspace.
- [WCD-09/WCD-10] Correct the `jest.polyfills.ts` comment, and say in one line which runs read the root `workerIdleMemoryLimit`.
- [WCD-28] Because the lockfile change makes every project affected, this PR's hosted Frontend CI shows every suite. Record its worker-exit warning count on T-0810.

## NOT doing

- Required checks, runners, image pins, caches, worker bounds, retention, lint becoming blocking (T-0813 and Mike).
- `deploy-pro.yml`, `deploy-azure.yml`, `deploy-dev.yml`, `deploy-docs.yml`.
- The Nx 24 conversion (T-0811).

## Acceptance criteria (done looks like)

- [x] **AC1** — Every PR-checking workflow runs its actions on Node 24 majors with no deprecation or ignored-input warning, and stays green on the exact head.
- [x] **AC2** — `npm ci`, every project's tests and lint, the three production builds and the e2e smokes pass locally on Node 22 / npm 10.
- [x] **AC3** — The worker-exit observation is recorded on T-0810.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by the CI and web-tooling lane, and completed by the review fix pass.
  - WCD-03: every action moves to its first Node-24 major.
    - checkout, setup-node, setup-java, setup-gradle, cache and setup-dotnet go to v5. upload-artifact goes to v6, because its v5 still runs Node 20.
    - For each action, the lane read the release notes of that major and its minors, and `action.yml` at v4, at the first Node-24 tag and at the floating major. Every input we pass keeps its meaning and its default.
    - setup-java v5's deprecation warnings cover `adopt*` distributions and the `jdkFile` input. We use neither.
    - A parsed-YAML comparison with HEAD shows these unchanged: job keys and names, triggers, paths, permissions, concurrency, cache keys and retention.
  - **gradle/actions stays on v5** (v5.0.2, MIT). v6 moved caching into a closed-source component, and using it means accepting Gradle's Terms of Use. That is Mike's licensing decision. The alternative is v6.1.0 or later with `cache-provider: basic`.
  - Done as listed: WCD-04, WCD-05, WCD-12, WCD-15, WCD-16, WCD-17, WCD-09 and WCD-10.
  - WCD-21/IOS-27 is closed in full. The fix pass handled the docs-ci case counts, `check-ios-symbols.test.mjs:14` and the ios-ci concurrency clause.
  - **WCD-15 changes what runs.** This corrects the ticket's "none of them changes what runs", as the reviewers asked.
    - An edit to only one ESLint module used to select 0, 0 and 5 lint projects. It now selects all 77, and 74 test projects.
    - That strengthens the required `build` and costs CI time. T-0813's runtime packet should take it into account.
  - WCD-16:
    - `npm uninstall verdaccio` alone keeps verdaccio, because it is an optional peer of `@nx/js`.
    - So the lane dropped verdaccio's 27 lockfile entries first, and npm 10.9.9 pruned the orphans.
    - Result: 242 entries removed and none added. No remaining entry changed version, resolved URL or integrity.
    - Read the lockfile diff with `--diff-algorithm=histogram`: 1 line added and 3,137 deleted, which is one line changed and 3,136 removed.
    - The verdaccio, `@verdaccio/local-storage-legacy` and globby audit findings drop out, and none is added. The absolute counts move with live registry data.
  - Guard duty: no guard for WCD-15 or WCD-17 (CLAUDE.md §4).
    - Lint is informational.
    - Nothing collects coverage.
    - T-0811's Nx output validation rejects output paths outside the workspace.
  - AC2, on Node 22.23.3 and npm 10.9.9:
    - `npm ci` exits 0;
    - tests: 74/74 projects, 408 suites and 5,038 tests, with no worker-exit warning;
    - lint: 77/77 projects, 0 errors and 1 known warning;
    - the three production builds pass;
    - the e2e smokes pass: 5, 1 and 1 tests;
    - the nine `agents/tools` self-tests pass.
- 2026-10-12 — Committed as `e08e479`, with `graph.json` staged as a deletion. AC1 and AC3 wait for this PR's hosted CI.
- 2026-10-12 — Shipped in [#322](https://github.com/VM-s-Solutions/cleansia/pull/322). Exact-head CI on `1136e16` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `6840882`. No deploy ran. Evidence: `raw/ci/p1-pr322-ci-merge-closure-001.json` (sha256 `d31e29ba…`) in the off-tree Wave F folder.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is FF-14–15.
- FF-14–15 correct the jsdom 26.1.0/Blob premise and describe the consistency checker as an on-demand check. No Blob polyfill, dependency, checker implementation or workflow is changed. P4's separate override-removal evidence on T-0849 remains history; the required-check table and passive T-0813 B are unchanged.
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

- The P6 follow-up for FF-14–15 jsdom and checker-description corrections was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

## Review

- **Correctness: approve-with-notes. Security: approve-with-notes.** Every note is either applied above, listed here, or recorded on T-0818 (the stale `backend-ci.yml:69-71` citations).
- **Reported, not fixed:**
  - All 20 `uses:` lines are on mutable major tags, and seven of the eight PR workflows have no `permissions:` block. SHA pinning and least privilege are a candidate ticket for Mike.
  - `deploy-pro.yml` and `deploy-docs.yml` still pin Node 20. They, `deploy-azure.yml` and `execute-sql.yml` still use v4 actions. The deploy workflows are under NOT doing here.
  - `main.bicep:312` runs the customer SSR site on `NODE|20-lts`, which is end of life. It is outside this ticket's Doing.
  - `patterns-frontend.md:482` says jsdom 20; it is 26.
  - `enforcement.md:79` says the consistency checker runs on the Windows dev box.
  - The `@module-federation/enhanced` override names a package that is in neither lockfile.
  - Four workflow comments still claim single-digit minutes. This is unverified against hosted durations.
- **AC1, on PR #322's exact head.** None of the 9 check runs carries an annotation reading "Node.js 20 is deprecated", "setup-java v4" or "Unexpected input". e2e-smoke's warning, "Retention days cannot be greater than the maximum allowed retention", predates this change: 2026-09-30 runs on the v4 actions show the same message.
- **AC3.** Recorded on T-0810. The hosted Frontend job ran all 74 test projects and printed the worker-exit warning once.
