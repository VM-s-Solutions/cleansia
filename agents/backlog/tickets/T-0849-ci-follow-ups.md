---
id: T-0849
title: CI follow-ups — move to Ubuntu 26.04 deliberately, let Dependabot update the action pins, settle static-web-apps-deploy's ref and stray input, pin dotnet-ef, delete the dead partner Dockerfile, and bring CLAUDE.md's Nx version current
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-14
depends_on: [T-0838]
blocks: []
stories: []
adrs: []
layers: [ci, backend, docs]
security_touching: true
---

## Context

The following is the 2026-10-12 finding record. The current ruling and evidence are dated below.

These are surprises in the CI ground truth of 2026-10-12 that no ruling covered. The ground truth is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_ci.json`. Its inventory is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/ci-inventory.json`. Lines are as of `207d2f319`.

1. **Ubuntu 26.04.** T-0838 pins `ubuntu-24.04` ahead of the 2026-10-19 migration. Moving later is a choice to make with evidence. These image differences touch our jobs:
   - Docker goes from 28.0.4 to 29.4.2. That affects Testcontainers.PostgreSql 4.10.0 (`src/Directory.Packages.props:122`);
   - psql goes from 16.15 to 18.6. `deploy-azure.yml:889` uses the image's psql; execute-sql installs its own from apt (`:129-132`, `:187-190`);
   - Android cmdline-tools go from 12.0 to 20.0. `android-ci.yml:53-69` relies on the preinstalled SDK and its accepted licences;
   - jq goes from 1.7 to 1.8.1 (`deploy-azure.yml:342`, `:1354`).
2. **Nothing will update the pins.** `.github/dependabot.yml:26` and `:81` cover npm only.
3. **static-web-apps-deploy's `v1` is both a tag and a branch.**
   - Actions runs the tag (`1a947af`). Deploy-docs check run 97938729832 carries the annotation "Unexpected input(s) 'skip_api_build'", and the valid inputs it lists are exactly the tag's.
   - So `deploy-docs.yml:44` passes an input the action ignores.
   - The branch head (`4d27395`) declares that input, and others besides.
   - The deploy logic itself lives in the image `mcr.microsoft.com/appsvc/staticappsclient:stable`, which no pin freezes. The security review accepts that.
4. **`dotnet-ef` is installed unpinned.**
   - `deploy-azure.yml:179-181` installs it with no `--version`, while EF Core is 10.0.3 (`src/Directory.Packages.props:11`, `:13`, `:20`). There is no `dotnet-tools.json`.
   - It builds the migrations bundle that the deploy runs with the database admin connection (SEC-CI-3, SEC-CI-5).
5. **A dead Dockerfile.**
   - `src/Cleansia.Web.Partner/Dockerfile:15-24` builds `Cleansia.Web/Cleansia.Web.csproj`, and no `src/Cleansia.Web` exists.
   - No workflow, Bicep file or script references this Dockerfile. Only Visual Studio's hooks do: `<DockerDefaultTargetOS>` (`Cleansia.Web.Partner.csproj:8`) and the "Docker" launch profile (`Properties/launchSettings.json:12-13`).
6. **CLAUDE.md's quick reference is mostly current already.**
   - The ground truth quoted "Angular 19, Nx 21", but that line is not in the tree.
   - `CLAUDE.md:100` has said "Angular 20.3, Nx 23.1, NgRx 20.1, PrimeNG 20.4" since `00583a452` (2026-10-09), at `db2272a5d` and on `origin/master`. The old line survives only in a local checkout that is behind `master`.
   - The one drift left: Nx is 23.2.1 (`src/Cleansia.App/package.json:92`, `:122`).

## Doing (proposed, for Mike)

1. **Ubuntu 26.04.** A branch moves the PR workflows to `ubuntu-26.04`, and they must all pass on its head. The deploy workflows follow, on an approved DEV deploy. Until then, 24.04 stays.
2. **Dependabot.** Add `package-ecosystem: github-actions`, `directory: "/"` to `dependabot.yml`, so it proposes updates to the pins and their version comments. Add an `ignore` rule for gradle/actions major versions (Mike: stay on v5).
3. **The stray input.** Delete `skip_api_build: true` at `deploy-docs.yml:44`. The action ignores the input today, so deleting it changes nothing but the warning. Moving to the branch head would change which inputs the action declares, so that is not proposed.
4. **`dotnet-ef`.** Add `--version 10.0.3` to the install, or use one tool manifest shared with T-0842's CI step.
5. **The Dockerfile.** Delete it and its two Visual Studio hooks.
6. **CLAUDE.md.** Change `Nx 23.1` to `Nx 23.2` at `CLAUDE.md:100`. CLAUDE.md is Mike's file, so this waits for his word.

## NOT doing

- ios-ci's `macos-latest`, gradle/actions v6, and newer action majors.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Before any switch, the PR workflows pass on `ubuntu-26.04` on one exact head, and the deploy workflows pass on an approved DEV deploy. Otherwise 24.04 stays, with the reason recorded here.
- [x] **AC2** — Dependabot's config validates and covers the pinned actions. It never proposes gradle/actions v6.
- [ ] **AC3** — deploy-docs carries no "Unexpected input" annotation. This needs a deploy-docs dispatch, which is a deployment, so it waits for Mike's approval.
- [x] **AC4** — `dotnet-ef` installs at the EF Core version.
- [x] **AC5** — No file refers to a `Cleansia.Web` project or to the deleted Dockerfile, and the backend builds.
- [x] **AC6** — On Mike's word, CLAUDE.md's quick reference matches `package.json`.

## Work record

- 2026-10-12 — Filed from the surprises in the CI ground truth of 2026-10-12. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

### Owner ruling — 2026-10-13

Mike: **"Go with defaults (recommended)"**. The request to fix T-0840–T-0851 is
**"Analyze and Fix those"**, saved in [decisions-2026-10-13.md](/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md). Q7 includes:

> the dead Docker hooks in the Customer, Mobile.Customer and Mobile.Partner csproj files and the Admin launch profile;
> ios-ci triggers on the 7 Android files two iOS tests read;
> the dead `@module-federation/enhanced` override;
> type-checking the app spec suites, only if they type-check clean, or are cleaned in the same PR (otherwise file it with the measured error count);

### Execution reading — 2026-10-14, recorded before any P5 merge

Ubuntu 26.04 remains its own **P5 PR**. The deploy half uses the decision record's **merge-then-prove**
reading: exact-head PR-workflow CI on 26.04 comes before P5 merge; deploy-workflow preflights and
the approved DEV/docs execution then prove the merged configuration at END, after all phases merge.
The deploy preflights do not run from P4. The 26.04 deploy runtime proof remains open until those
END receipts exist. If it fails, record the failure and resolve the runner change; do not mark AC1
complete on PR CI alone. P4 leaves every runner label at 24.04 and changes no macOS label.
This reading is written before P5 implementation/merge and does not claim either has happened.

### P4 work — 2026-10-14; publication pending

The reviewed 30-path candidate is `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/candidate-source-001.json`. Its source manifest file
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/candidate-source-manifest-001.json` has actual file SHA-256
`595c06c5af936c80bf52138a78b4b74c64f9394aef5693adcf1016de7b846b01`.
The candidate report's `fe54d642…` hash is the canonical JSON payload without its trailing LF,
not the hash of the saved manifest file. `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/actual-base-rebind-001.json` confirms every candidate
preimage and frozen afterimage against actual base `a667f62b1c7157c5ca0104837dbe8e6c4dc45045`.
The subsequent native closure is the first P4 commit, `0a2b9e6c9f5a3b3c13c2b98b2957b9d332e9e173`;
the dedicated Nx correction follows as `8c2e8b461c04e786a79db174ad29828da0d29a3d`.

- **Item 1 / AC1 pending:** Ubuntu 26.04 is reserved for P5 and the END deploy gate under the
  reading above. No P4 runner change or deploy ran.
- **Item 2 / AC2 source complete:** the full Dependabot schema validates; a monthly GitHub
  Actions entry covers the action pins in one minor/patch group. Its deliberate ignore rule
  excludes **every GitHub Actions major**, broader than the original Gradle-only wording, so it
  cannot propose gradle/actions v6. Both original npm entries remain identical. A normal scheduled
  Dependabot run is still future evidence; none was manufactured.
- **Item 3 / AC3 source complete, runtime pending:** unsupported `skip_api_build` was removed
  from the unchanged SHA-pinned tag-based SWA action. Only a later END `deploy-docs` dispatch can
  establish absence of the annotation, so AC3 remains unchecked. The mutable `stable` Docker
  image is an accepted residual and was not pinned by removing the input.
- **Item 4 / AC4 local complete:** the workflow installs `dotnet-ef --version 10.0.3`, matching
  EF Core. An isolated actual 10.0.3 install/version and the exact bundle project/startup/Release
  flags produced a **Mach-O arm64** bundle without executing it. No self-contained or target-runtime
  flags were used. `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/ef-bundle-preflight-001/summary.json` retains both initial failures
  (missing project assets, then a build failure), the startup Release build (32 warnings, zero
  errors) and the third successful bundle attempt. This is local tool/compile proof; Linux
  packaging and execution remain END workflow proof.
- **Item 5 / AC5 local complete:** the dead Partner Dockerfile, src-root `.dockerignore`, four
  Docker properties, two Docker launch profiles and sole central/reference Containers.Tools
  entries are removed. Active build/launch inputs no longer reference the nonexistent `Cleansia.Web`
  project or deleted Partner Dockerfile. The live root/Functions Docker contexts and all non-Docker XML/JSON/BOM
  bytes are preserved. The combined Release rebuild succeeds with zero errors.
- **Item 6 / AC6 complete on its own commit:** `8c2e8b461c04e786a79db174ad29828da0d29a3d`
  changes exactly the CLAUDE.md quick-reference line from `Nx 23.1` to `Nx 23.2`; package.json's
  actual Nx version is 23.2.1. Receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/nx-only-commit-001.json` records the separate commit,
  its exact afterimage and parent native-closure commit. It is local and unpushed; phase publication
  remains pending. Raw-head docs checks passed in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/nx-only-head-001/final.json`.
- Q7 source changes: seven exact Android test-reader paths trigger iOS CI on PRs and master
  pushes; the dead module-federation override is removed with byte-identical lockfile proof;
  `typecheck:specs` checks all **73** actual app specs sequentially (25/17/31) and is a blocking CI
  step. Injected type errors in each app exit 2 and exact restoration is GREEN; a no-op stub
  would wrongly accept the same error. No application behavior changed.
- No changelog entry is owed: CI, tooling, dead development hooks and documentation only.
  No new migration or client/spec regeneration was needed by these P4 changes. Isolated iOS
  generation used the unchanged specs to prove the pinned tool/package layout; those outputs
  were not source edits. DEV reset, health checks, docs deployment and fresh deploy stay at END.

### 2026-10-14 — completion follow-up; publication pending

The 34-path source/settings-record change is committed locally as `a87c0be5aca745d8bfa980080269631a1138873e`, after the native
closure `0a2b9e6` and dedicated Nx commit `8c2e8b4`. `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-and-records-commit-001.json` records
all exact afterimages. The source-to-suite binding in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-commit-sdk-rebind-001.json`
(SHA-256 `b7a869975d6991f67cd119acfbe0d89edd2edfd638086132f4d66e06df68e059`) compares **8258 committed Git blobs and modes**:
**8242 match** the measured combined source, with **16 declared documentation/record deltas** and
all modes exact. **Every compiled/runtime input is identical** to the tested source. The Windows
Gradle wrapper's working CRLF is qualified separately; its committed raw blob matches the measured
source. This is source identity proof, not a claim that the suites were re-executed from the Git head.

Items 2–6 are now committed with the source change; **AC2/AC4/AC5/AC6 remain checked**. Nx remains
its dedicated earlier commit. **AC1 remains unchecked** for the separate Ubuntu26 P5 and END
deploy gates under the already-dated merge-then-prove reading. **AC3 remains unchecked** until
the END docs dispatch proves absence of the SWA annotation. No Ubuntu26 implementation, hosted
CI, merge or deployment is claimed by these local receipts.

## Review

Pending.

### 2026-10-14 — source verdicts and combined candidate checks

The frozen source verdicts in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/review-001/verdict.json` and
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/security-001/verdict.json` are each **"APPROVED — SCOPED"**, with zero material findings.
They cover the 30-path source candidate; actual-setting prose, records, final-head CI and merge
remain separate gates.

- Combined candidate verification: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/full-backend-frontend-001/final.json` records
  `dotnet restore Cleansia.Api.sln --force --disable-parallel`, Release `-t:Rebuild` (220 warnings,
  zero errors), then unit → real-Postgres integration → host tests, each exit 0:
  **8731 / 975 / 509 passed**, zero failed or skipped. The test commands use `-c Release --no-build`
  and `xUnit.parallelizeTestCollections=false` in that order.
- Frontend: `npm run typecheck:test` (8 self-tests), `npm run typecheck:specs` (all 73 app specs),
  `nx run-many -t test --all --parallel=3 --ci --maxWorkers=1 --skip-nx-cache`, all three
  `build:cleansia-*` production builds, and all three e2e commands exit 0. Jest: **5041 tests / 408
  suites in 71 nonempty projects**, with **3 empty projects** retained in the 74-target inventory.
  Playwright: **5 customer / 1 partner / 1 admin** smokes, with network-stubbed APIs. Advisory lint
  exits 0 across 77 executed targets. An initial outside-workspace lint selector failed and was
  corrected to the actual workspace `package.json` path; the failure is retained, with no source fix.
- Mobile interim proof: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/full-mobile-001/interim-handoff.json` records forced Android
  compile/unit tasks, **2873 passed** (core 298, partner 881, customer 1694), zero failed or skipped;
  iOS 18.6 Core **776 passed** and Partner **1133 passed**. Customer is **NOT STARTED** because the
  free-space admission failed. **This is not a full mobile PASS.** Actual P4 compiled/runtime input
  rebinding and Customer/final preservation proof remain pending.
- These are prospective combined candidate receipts, not hosted exact-head CI or merge evidence.
  Final documentation checks, exact-head CI, publication and the approved END DEV/docs procedure
  remain pending. No SQL, reset, DEV deployment or PRO operation ran in these P4 checks.

### 2026-10-14 — completed local suites bound to the committed source

The earlier Customer **NOT STARTED** paragraph is retained as the true interim capture.
Customer resumed only after a fresh free-space measurement cleared its **2,680,205,312-byte**
admission threshold. The later complete mobile receipt is `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/full-mobile-001/final.json`
(SHA-256 `3b306e02d2fcd6d911636a12835f3d86f4aac98e86ccbf100b707015ef7788b1`), with packet `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/full-mobile-001/final-packet.json`
(SHA-256 `a5d0e7f0e70e8849cfabedced6de94b3b1d63f39427c97676dceede098585da1`). Its exact result is
**`PASS_WITH_UNCHANGED_VOICEOVER_RUNTIME_SKIP`**: Android forced compile then unit tasks pass,
**2873 tests** (298 core / 881 partner / 1694 customer), zero errors, failures or skips; iOS **18.6**
Core **776**, Partner **1133** and Customer **1779** pass, with **one unchanged Customer skip** and
zero failures. The skipped method is
`ProfileEditChipTests/testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated()`; no VoiceOver
retry or spoken-announcement proof is claimed. Xcode 26.3 used Simulator SDK 26.2, arm64, initially
absent DerivedData and within-run shared DerivedData, with copied SPM downloads. All **8258** source
hashes/modes, **68** native source inputs, **479** generated files and historical result/product pins
remain unchanged; the final product manifests retain **20,590 Android** and **2,219 iOS** files.
The simulator lease is released and no SDK command remains active. P4 added no iOS 16.4, device,
provider, mail, D9, APK or R8 acceptance run; retained P3 proof is not described as a P4 re-test.

The full backend/frontend receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/full-backend-frontend-001/final.json` is now bound to
the actual source commit by the Git-blob/mode receipt above: Release rebuild, zero errors;
unit → real-Postgres integration → host **8731 / 975 / 509 passed**, zero failed/skipped; all **73**
app specs and guard self-tests, **5041 Jest tests / 408 suites** across **71 nonempty + 3 empty**
projects, three production builds and **7 network-stubbed** smokes (5/1/1) pass. Advisory lint passes
across 77 executed targets. Full local mobile completion is qualified above by its one unchanged
runtime skip. None of these receipts is hosted CI or live DEV acceptance.

The raw source commit's docs-reference check, catalog-claims check and VitePress build each exit 0
in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-head-001/final.json` (SHA-256 `300c773489054361b3178dd1f23efc59f13a3fef09b4405c68840ab9c59b3a8d`), with all tracked
input hashes/modes unchanged. The later sealed receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-head-extra-001/final.json` (SHA-256 `9585ee67f11adcf4afd6fef245632dbc447101ecb889f5f723faa77b4c6fdbcc`)
confirms **all eleven local commands PASS** on that same raw head, with all **8258** tracked
input hashes/modes unchanged: the three docs gates plus eight Node checks/self-tests
(available-status parity, booking-policy parity and iOS symbols with their self-tests, plus
the docs-reference and catalog-claims self-tests), each exit 0. Receipt/log hashes are bound.
This is local command proof, not hosted CI.


The preceding draft's factual assessment is **"CLEAN — SCOPED FACTUAL REFUTATION"** in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/actual-records-refuter001/verdict.json`, with zero material findings. The consolidated
source commit message003 is **"PASS — SCOPED SOURCE, DRAFT APPLICATION AND MESSAGE003 VERIFICATION"**
in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-message-verifier001/verdict.json`; its checked message SHA-256 is
`5227fde5a595cb929460ce5568412f016c99d68434b35b1c3c4e6bf413985494`. Those verdicts cover their stated prior source,
message and draft scopes; this completion follow-up still needs its own factual check. No pushed
history was rewritten.

The unchanged EF 10.0.3 Mac arm64 compile receipt above still proves install/version and local
bundle compilation only, with both initial failures retained and no bundle invocation. Actual
Linux packaging/execution, artifact-ID transfers, DEV certificate/hostname validation and the
SWA annotation result remain END proof. The final record-only commit still needs its own exact
Git-blob binding and actual-head gates before push.
