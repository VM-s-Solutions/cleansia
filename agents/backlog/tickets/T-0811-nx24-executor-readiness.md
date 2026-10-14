---
id: T-0811
title: Wave E C04 — Nx 24 readiness: replace the deprecated Jest and ESLint executors through the supported generators
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
---

## Context

Nx 24 removes the `@nx/jest:jest` and `@nx/eslint:lint` executors, and Nx 23.2.1's schemas already mark both deprecated. The roadmap asks to "prepare Nx project/workspace registration only through existing supported configuration and commands; preserve project boundaries". Checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`).

**Inventory.** The resolved graph has 78 projects:
- 74 test targets use `@nx/jest:jest`, and 73 lint targets use `@nx/eslint:lint`.
- `@nx/jest/plugin` was never registered. `@nx/eslint/plugin` is registered, but the 73 explicit lint targets override it.
- The supported path is installed: `@nx/jest:convert-to-inferred`, `@nx/eslint:convert-to-inferred`, `@nx/workspace:infer-targets`.

**Dry run.** It ran in memory, and nothing was written to the tree. It keeps every project, tag, edge and target name, every custom input including the C# policy hashes, `passWithNoTests` and the cache settings. It is **not drop-in**:
1. `order-wizard`'s coverage directory points outside the workspace (`jest.config.ts:5`), so its converted output fails Nx's output validation.
2. The registration guard (`check-nx-project-registration.mjs`, NX-7) goes from 0 to 66 false violations.
3. The generator registers `@nx/jest/plugin` with a 74-glob include, so a future lib would silently get no test target.
4. A dead lint `targetDefault` is left behind.

**Nothing forces it today.** Nx 24 is unpublished (npm latest is 23.3.0), and Nx is pinned exactly.

## Doing

- The conversion through the supported generators, with the four corrections and the guard, self-test and docs updates.
- Proof:
  - before/after `nx graph --file` equivalence;
  - full 74-project test and 73-project lint result sets, before and after, equal;
  - no deprecation warnings;
  - exact-head CI. An `nx.json` change marks every project affected, so that run is the full suite.

## NOT doing

- No conversion tonight (2026-10-11).
- No CI switch to `-c ci` or coverage collection; lint stays non-blocking; no fix for the lint cache-input gaps; no Nx upgrade; no change to the stale root verdaccio target.

## Acceptance criteria

- [x] **AC1** — A grep of `project.json` and `nx.json` for `@nx/jest:jest` / `@nx/eslint:lint` returns 0.
- [x] **AC2** — The graph diff shows only the expected deltas.
- [x] **AC3** — The test and lint result sets are equal.
- [x] **AC4** — The guard and its self-test are green.
- [x] **AC5** — Exact-head Frontend CI is green.

## Work record

- 2026-10-11 — Filed from the Wave E ground truth; scheduled after Wave F (Mike, 2026-10-11).
- 2026-10-12 — Converted locally on `fix/post-e-p4-nx-inferred-targets`, alongside Wave F's W01 work (T-0830, PR #324), in the order the program plan of 2026-10-11 set: W01's costed decision, then T-0811. The branch is pushed only after #324 merged. Wave F itself has not closed, because T-0830 awaits Mike's letter, so whether this PR merges now or waits is Mike's call under his ruling "After wave F". Node 22.23.3, npm 10.9.9 and Nx 23.2.1, with the daemon and Nx Cloud off.
  - The SHAs here are the branch's final ones. Before push, commit messages were corrected and two trees changed:
    - `93adc83`'s T-0830 closure line now cites closure `-002` and its deploy check;
    - `e98fa44` carries two comment fixes.

    Every later commit carries both changes, and no other tree changed. The rewrite kept each commit's original dates, so `93adc83`, dated 12:34, cites closure `-002`, which was written at 13:37.
  - `3ff8f8c`: the generator's output, unedited: `nx g @nx/workspace:infer-targets --plugins=@nx/jest --plugins=@nx/eslint --no-interactive`. The review re-ran it on a fresh master copy and reproduced the commit's `src/Cleansia.App` byte for byte.
  - `f30c532`, correction 4: the dead `@nx/eslint:lint` target default goes.
  - `0200b99`, correction 3: `@nx/jest/plugin` is registered with no `include`, not the generator's 74 globs, so a new lib's jest config gets a test target. Checked in a scratch copy, not committed: a lib copied from `libs/shared/utils`, and a lib from `nx g @nx/angular:library` with its explicit targets deleted, each get one (76 projects with a test target), and the copy runs 5 suites and 46 tests. With the 74 globs neither does: "Cannot find configuration for task".
  - `89d4b6d`, correction 2: the registration guard counts the test target the plugin infers (AC4).
  - `ffcb2f1`: `docs/architecture/frontend.md` (Testing) and `agents/knowledge/patterns-frontend.md` say the plugins infer the targets and their default inputs.
  - Correction 1 needed no change: T-0820 had already moved order-wizard's coverage directory inside the workspace on master. Nx's output validation passes for all 166 targets with outputs. In a scratch copy, `-c ci` wrote order-wizard's coverage to `coverage/libs/cleansia-customer-features/order-wizard`, and a cache replay restored it.
  - **AC1.** Across the 78 `project.json` files and `nx.json`: `@nx/jest:jest` 75 → 0 and `@nx/eslint:lint` 74 → 0 (74 and 73 targets, plus one target default each).
  - **AC2.** `nx graph --file` and `nx show project --json` for all 78 projects, before and after.
    - Unchanged: the 78 projects, their tags and implicit dependencies, the 411 edges, and the target names on every project. Every target other than test and lint is byte-identical. All 74 test and 77 lint targets keep `cache: true` and no `dependsOn`, and all 74 test targets keep `passWithNoTests`. Every custom input is byte-identical (bookingPolicy and the four project-level C# runtime hashes, the scss and client globs), and no input is lost.
    - Changed, as expected:
      - test runs `jest` and lint runs `eslint .`, through `nx:run-commands` from the project root. Jest finds there the config the executor's `jestConfig` option named, so `jestConfig` and `tsConfig` go. The plugin sets `TS_NODE_COMPILER_OPTIONS` so that ts-node loads the TypeScript jest config;
      - the inferred inputs add each tool's packages as `externalDependencies`, so the cache key gets stricter. They also add inputs that `default` already covers (`src/test-setup.ts`, and partner-services' `jest.global-setup.js`), and a `dependentTasksOutputFiles` entry that adds nothing, because no test target has `dependsOn`;
      - `-c ci`'s `codeCoverage` becomes the CLI's `coverage`;
      - descriptive `metadata` is added. Test outputs resolve to the same paths, and lint gains an `{options.outputFile}` output that resolves to nothing unless `--outputFile` is passed.

      `after/ac2-compare.txt` counts every delta kind.
    - Corrections 3 and 4 change nothing resolved: the graph file is byte-identical (`9cfada1c…`) after the generator alone, after each correction, and at `ffcb2f1`. Nothing under `src/Cleansia.App` changed after that.
  - **AC3.** Full runs on both sides: `run-many -t test --all --parallel=3 --ci --maxWorkers=1 --output-style=static --skip-nx-cache` for test, and `run-many -t lint --all --parallel=3 --output-style=static --skip-nx-cache` for lint.
    - Test: 74 projects, 408 of 408 suites and 5,038 of 5,038 tests, with 3 projects that have no tests, on both sides. There are 0 per-project differences, and a second run after the change was equal too.
    - Lint: 77 projects, 0 errors, and the same 1 warning on both sides. There are 0 per-project differences.
    - Files: run the executor's way and the inferred way, ESLint reads the same 1,613 files with identical messages, and `jest --listTests` selects the same 408 spec files.
  - **AC4.**
    - RED: the old guard on the converted tree reports 66 NX-7 violations, all false. The new self-test against the old guard fails 28 of 59 scenarios.
    - GREEN: the self-test passes 59 of 59 (60 of 60 after the fix pass). The guard on the tree reports 74 jest configs and 74 test targets (74 inferred), with 0 violations.
    - Teeth: an exit-0 stub turns 54 of the 59 scenarios red (55 of 60 after the fix pass), and seven targeted weakenings each turn at least one red.
  - Deprecation warnings naming the two executors: 147 before (74 test, 73 lint), and 0 after.
  - Timing, which no AC covers: the first run after the change took 208 s against 106 s before, and a re-run took 89 s. That is consistent with Jest's transform cache going cold after the config change, but the cache itself was not measured. AC5's hosted run will show the CI figure.
  - Evidence: `p4-nx.noindex/` in the off-tree Wave F folder (`MANIFEST.sha256` `a40e3e10…`, 246 artefacts).
- 2026-10-12 — Review fix pass:
  - `6b36264`: catalog-edit routing (ADR-0033). Tests 1 to 3 do not fire on this branch's edits to `patterns-frontend.md`. Test 4, the price of a law, fires on the NX-6/7/8 sentence the branch edited, which still read "`T1-CI`, same repo-root workflow". Ratified with that amendment: the sentence now says on demand only, `T2-ADVISORY`, as the registration entry above it already did, and so do the guard's and self-test's headers (comment-only).
  - `e98fa44`: NX-7 checks a `test` target's options only where a target runs, declared or inferred. Since `89d4b6d`, a project with no jest config whose `test` entry names no runner but carries an option not on disk was reported by both NX-7 and NX-8, so the catalog's "disjoint by construction" was no longer true. A new self-test scenario pins it: red with the condition removed, green with it, 60 of 60. The self-test header's stub count now says 55 of 60, and the guard's comment says which rule owns a project with no target. A declared or inferred target with an option not on disk is still NX-7, checked by hand in a scratch copy (`p4-nx.noindex/disjoint/`).
  - `cced2d2`: `docs/architecture/frontend.md` says a library made with `nx g @nx/angular:library` does not get the inferred targets. In Nx 23.2.1 that generator still writes both targets on the deprecated executors. They replace the inferred ones. With the `@nx/jest:jest` and `@nx/eslint:lint` target defaults gone (the generator removed the first, correction 4 the second), such a test target runs with no cache, inputs, `passWithNoTests` or `ci` configuration, and such a lint target with no cache or inputs. Checked in a scratch copy (`p4-nx.noindex/fixpass/`). The sentence is conditional, so it does not recommend the generator.
  - Re-checked at the final SHAs (`p4-nx.noindex/fixpass-final/`):
    - the self-test passes 59 of 59 at `6b36264`, and 60 of 60 at `e98fa44` and `cced2d2`; the guard reports 0 at each. Logs such as `selftest-5900a446f.txt` and `guard-8c69e8e0e.txt` carry the SHAs these two commits had before their last reword, at the same trees;
    - an exit-0 stub turns 55 of 60 red, and the mutant without the new condition fails only the new scenario;
    - both Docs self-tests, check-docs-refs and check-catalog-claims pass.

    The fix pass's evidence is in `FIXPASS-MANIFEST.sha256` (`070ace7b…`, 40 files).
- 2026-10-12 — Shipped in [#325](https://github.com/VM-s-Solutions/cleansia/pull/325). Mike: "You can merge" (2026-10-12), which settles the schedule question in the Review below. Exact-head CI on `be776dc` passed all 5 check runs with no re-run: Frontend build and e2e-smoke, Android, Docs and Secret scan. Backend, iOS, iOS Symbols and booking-policy parity did not run, because their path filters match none of the changed files. Merged normally with the SHA guard as `6952149`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge. Evidence: `raw/ci/p4-pr325-ci-merge-closure-001.json` (sha256 `3fe9783f…`) and `raw/ci/p4-pr325-ci-001.json` (`4b858697…`) in the off-tree Wave F folder.
  - **AC5.** The hosted Frontend job ran lint on 77 projects and test on 74. Its 71 Jest summaries add up to 408 suites and 5,038 tests passed, with none failed, and the log names neither deprecated executor.
  - The hosted test step printed Jest's worker-exit warning 9 times, in nine projects, against once on #322's full run, and took 1,296 s against 954 s. That is one run each, and the three production builds, which this change does not touch, were also 30–40% slower on this runner. So neither difference is attributed to the conversion. T-0810 records it.
  - The first review's git-archive copies under `p4-nx.noindex/review/` lost their `node_modules` on 2026-10-12 to free disk; its outputs in `review/out/` are kept.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is FF-01–08.
- FF-01–08 correct the current generated-client references, Nx tiers and checker scope, the frontend test-step pointer, checker headers and the conventions example. The supported executor conversion and #325 evidence above remain the record of that implementation; these descriptions and comments change no generator, workflow or project behavior.
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

## Review

- **Correctness, read-only, of `3ff8f8c` to `ffcb2f1`, with its own runs in `git archive` copies: approve-with-notes.** Nothing blocks, and none of AC1 to AC4 was refuted.
  - Its own graph and resolved-target captures match the saved before and after files byte for byte, and all 246 manifest entries verify. It also reproduced the generator commit.
  - Samples, equal on both sides:
    - test on 13 projects and lint on 15, apps and e2e included;
    - per-test results for 139 spec files and 1,685 test cases;
    - ESLint JSON for all 77 projects.
  - A failing spec and a `debugger;` still fail the converted targets.
  - The guard goes red on a real violation (the 74-glob include plus a new lib) and green once the include is removed.
  - The three production builds, `typecheck`, check-consistency, check-catalog-claims, check-docs-refs, the Docs self-tests and the VitePress build all pass.
  - Its notes, and what became of them:
    - the schedule question: open for Mike. #324 merged first, but Wave F has not closed, so the merge waits for his word;
    - the false enforcement tier, and generated libs: the fix pass above;
    - notes 3, 5 and 6: reported below;
    - notes 7 and 8 needed nothing: the generator's `"// targets"` key in dashboard's `project.json` is harmless, and the review's scratch stays on disk.
- **Catalog-edit routing (ADR-0033):** tests 1 to 3 do not fire, and the sweep is recorded. The 8 `project.json` files with a `test` entry each list `{workspaceRoot}/jest.polyfills.ts`, and `nx.json` registers `@nx/jest/plugin` with no include or exclude over 74 jest configs, none of which calls `getJestProjectsAsync()`. On test 2, the candidate sentences at master include:
  - `patterns-frontend.md:488-489`: the polyfill input is declared on the jest target in `nx.json` `targetDefaults` → `@nx/jest:jest`;
  - `:493-496`: the membership test, under which a project-level `inputs` list replaces `targetDefaults`, so it must repeat the entry. Its finder grep, `grep -l '"inputs"' libs/*/*/project.json`, becomes `grep -l '"test"' apps/*/project.json libs/*/*/project.json`;
  - `:1187-1188`: a project-level `inputs` list replaces the `targetDefaults` one, so a workspace-wide input added to `nx.json` alone misses every project with its own list;
  - `:1113-1114`: NX-7, `jest.config.*` ⇔ a `test` target.

  `:1161-1163`, `:1223-1224` and `:1244-1245` restate the first ones.

  Reading A: the edit replaces them, so test 2 fires. Reading B, adopted: they described a mechanism this ticket removed, the obligation they carried (a project-level `inputs` list repeats the workspace-wide entries) is kept, and NX-7 is widened, not narrowed. Test 4 fires on the NX-6/7/8 sentence, so the edits are ratified with the amendment in `6b36264`. No ADR is needed, because `enforcement.md`'s gate table already decides the tier.
- **Fix pass review:** approve-with-notes, with nothing blocking, on the uncommitted edits behind `6b36264` and `cced2d2`. It re-ran check-catalog-claims (OK, 0 violations, the same 156 advisories as before the edits), check-docs-refs (0 unresolved), the guard and its self-test, both Docs self-tests and the VitePress build. It also checked every new sentence against the tree and the installed Nx source. Its "disjoint" note was this branch's own debt and became `e98fa44`. Its two wording notes were applied after it finished, and the rest are reported below.
  - `e98fa44` and the reworded sentences came after that review. They were checked by the re-runs in the fix-pass entry above, and by the records check below, which re-derived the guard's disjointness and the mutant result.
  - The VitePress build was not re-run on the reworded `frontend.md`; the PR's Docs CI runs it.
- **Records check before push** (read-only), in two rounds:
  - **Round one**, four lenses (the record against the evidence, the commit messages, the reported items and the P3 closure, and the PR body): 34 findings, several found by more than one lens. They included:
    - the schedule claim above;
    - review coverage that was overstated;
    - a stale stub count in the self-test header;
    - five commit messages;
    - the P3 closure's evidence, superseded by `raw/ci/p3-pr324-ci-merge-closure-002.json`.
  - **Round two**, two lenses over everything the fixes changed: 13 findings, again with overlap. They included:
    - `e98fa44`'s message citing a pre-rewrite SHA;
    - this record's account of which trees changed;
    - three PR-body sentences that carried round-one errors.
  - All were fixed before push. Six commit messages changed in all: the five round one named, and `e98fa44`'s. That one was reworded to match its folded comment fix, then to cite `89d4b6d`.
- **Reported, not fixed:**
  - **Line citations the generator's reformat displaced.**
    - `apps/cleansia.app/project.json:26`: `tsConfig` is now at :23. It is cited at `docs/decisions/adr-0031.md:109`, `:126`, `:485` and `:550`, and at `agents/architecture/decisions/generated-client-contract.md:148`.
    - `libs/shared/models/project.json:14-19`: the test inputs are now :10-24, with the client glob at :15. It is cited at `adr-0042.md:217`, `:956` and `:1031`, and at `generated-client-contract.md:315`.
    - Already stale before this change: `adr-0042.md:632` and `:958`.
    - The route is ADR-0031's dated-erratum lane, which also decides which of these are historical receipts that stay as written. check-catalog-claims still passes, because C3 only checks that a cited line exists.
  - ADR and decision text still describes the `@nx/jest:jest` inputs as written: `adr-0037.md:940` and `:1577`, `adr-0042.md:1213`, and `order-availability.md:518`. Their reasoning still holds.
  - **Where a workspace-wide test input now goes.** An `inputs` list in `nx.json`'s `test` target default replaces every inferred list unless it uses the `"..."` spread, and the 8 projects with their own test inputs must repeat it. That would be a catalog addition, and it was not written.
  - **More stale tiers and citations in `patterns-frontend.md`, all predating this change:**
    - `:1364-1365` still calls NX-8 `T1-CI`, against the corrected `:1118-1122`;
    - `:824-826` and `:863` call `check-module-boundaries.mjs` `T1-CI`, which `enforcement.md` lists as on demand only;
    - `:707` cites `frontend-ci.yml:85-87` for `nx affected -t test`, which is now at `:97-98`.
  - **Over-claims left in the NX-6/7/8 sentence, also older than this change:**
    - NX-8 says "a registered project", but the guard reads registered libs plus the three rostered apps. The three `*-e2e` apps hold TypeScript and have no `test` target.
    - "Each instance is a one-token fix" does not hold for NX-8, whose message asks for three scaffolded files.
  - Nothing mechanical checks that a project-level test `inputs` list repeats `jest.polyfills.ts`; only the grep a reader runs does. Today all 8 lists comply.
  - **Stale lines in the guard's header:**
    - `:19-20` cites `frontend-ci.yml`'s lint step as `continue-on-error: true` at `:73`, which is now `:83`;
    - `:21-22` sets the guard against `check-consistency.mjs` for running in no workflow. Since `6b36264` says no workflow runs the guard either, the contrast no longer holds.
  - `check-catalog-claims.mjs:15-16` and `:18`, another checker's header, describe a workflow of its own (`catalog-claims.yml`). `docs-ci.yml` runs the checker (`:87`) and its self-test (`:72`).
  - `conventions.md:242` cites `patterns-frontend.md:462-465` as the model T2-ADVISORY token. Those lines have since moved.
  - **What else a library made with `nx g @nx/angular:library` brings, checked in a scratch copy:**
    - the `tsconfig.base.json` alias as `./libs/…`, which the guard's alias witness skips (`startsWith("libs/")`), so NX-3 cannot fire for it;
    - a `jest.config.cts`, which nx.json's `production` input does not exclude (`jest.config.[jt]s`);
    - an `@cleansia.app/<name>` import path;
    - inferred lint inputs without `eslint.module-boundaries.config.mjs`.

    The Nx warnings suggest `convert-to-inferred` after generating; only deleting the targets was checked.
  - No git ref ever held `nx-project-registration.yml`, so its 2026-08-11 deletion rests on the records alone.
  - The guard applies `include` and `exclude` with `path.matchesGlob`, which has no minimatch `dot: true`. It differs from Nx only for a project under a dot-directory, and there is none today.
  - Two self-test gaps: dropping the `@nx/jest/plugin` name check, and a leading-negation include that starts from nothing, each survive the self-test. The guard itself matches Nx on both shapes.
  - The 73 converted `project.json` files carry the same lint `inputs` residual, so a future lint-inputs change touches 73 files. Lint cache inputs are on the NOT doing list.
