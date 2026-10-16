---
id: T-0849
title: CI follow-ups — move to Ubuntu 26.04 deliberately, let Dependabot update the action pins, settle static-web-apps-deploy's ref and stray input, pin dotnet-ef, delete the dead partner Dockerfile, and bring CLAUDE.md's Nx version current
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-15
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
- [x] **AC3** — deploy-docs carries no "Unexpected input" annotation. This needs a deploy-docs dispatch, which is a deployment, so it waits for Mike's approval.
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

### 2026-10-14 — P4 shipped; remaining runtime gates

P4 shipped in [#335](https://github.com/VM-s-Solutions/cleansia/pull/335), merged at
**2026-10-14T16:11:33Z** as `252c5e50502539ca0d562ca2cf7ae1b083e5dfbd` using the exact-head SHA guard on
`6343b1908a8ca30f20e6bbae1713b47085a1c89f`. The merge tree equals the PR head tree, `0358f5df38a9a139e7ed80ab2ed8ea6fff3ca673`.
The PR contains **42 changed paths and four commits**: native closure first (`0a2b9e6`), the
dedicated Nx correction (`8c2e8b4`), source/settings records (`a87c0be`) and the four-record
completion follow-up (`6343b19`). The earlier capture-time records are preserved, including
their then-pending publication and interim evidence where present.

Exact-head PR CI passed **9/9 checks across eight triggered workflows**, all at attempt 1,
with **zero re-runs**: Backend, Frontend (build and e2e-smoke), Android, iOS, Docs, iOS Symbols,
booking-policy parity and Secret scan. The actual closure is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-ci-hardening-pr335-ci-merge-closure-001.json`
(SHA-256 `5fc756948b54492e3f781c9f8012d9884dc5255d481ffba906945c1bc0e386ce`). Its same-day
evidence summary contains **18 entries: nine annotation responses and nine job logs**; this is
not a count of annotation messages. The one saved `playwright-report` artifact, id **11904984846**,
has SHA-256 `9c8c321c224c19f4a42ef44ad1a3174cd75d6c28c3240ae3d5b44172b802360b`;
the download receipt is `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/ci-artifact-receipt-001.json` (SHA-256 `2b70ad120f172ae9c98bd91d523b9de87979bb61e8ca7a58ab7e894737aa6c93`).

One PR check-runs GET returned HTTP 500 during polling. That was a transport failure,
not a CI job failure or re-run; the saved exact-head CI attempts and zero re-runs above remain.

Master push CI on `252c5e50502539ca0d562ca2cf7ae1b083e5dfbd` completed on **2026-10-14**: **eight repository workflows**, **nine repository check rows** comprising **eight successful executed jobs** and **e2e-smoke skipped** by its push guard. Every repository workflow ran at attempt 1, with **zero re-runs**. The raw **12-check** inventory also contains **three successful automatic extras**, counted separately: **two dynamic Dependabot update jobs** and **`.github/dependabot.yml` configuration validation**. Both dynamic runs were attempt 1; configuration validation has no Actions job log.

Actual master receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/master-ci-evidence-receipt-001.json` (SHA-256 `3f5316b1d8b69e12bb5971bab4a8651f850eaa2bf355a99e7ef2c7a304b5d43b`). Its same-day evidence summary has **21 entries: 12 annotation collections, eight executed CI logs and one skip entry**. The supplemental receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/master-automatic-and-artifacts001.json` (SHA-256 `fb7195bf318a95ad7fa7c9ffbe45af5272999a74871cfa10815d099d811c82c4`) saves **two additional automatic-job logs and run metadata**. All **ten artifact-list GETs** for the eight repository and two dynamic runs returned **zero master artifacts**. The PR's one saved artifact is separate. These captures were saved on **2026-10-14 UTC**.

No phase DEV reset, deployment, docs dispatch or SQL runtime proof is supplied by P4; no PRO
operation ran. The four bounded workflow-dispatch GETs saved in the closure each returned zero
runs created since **2026-10-14T16:11:33Z** for `deploy-dev.yml`, `deploy-pro.yml`, `deploy-docs.yml`
and `execute-sql.yml`; `deploy-azure.yml` is workflow-call only. Those reads describe their
capture window, not later activity. The approved one-reset/fresh-DEV/docs END procedure remains
due after every phase merges green. Decisions 10 and 11 still leave the separate token decision
and actual Azure identity narrowing to owner go-live.

Items 2–6 have shipped, with **AC2/AC4/AC5/AC6 still checked**. The deliberate Dependabot
ignore remains broader than the initial Gradle-only wording: **all GitHub Actions major updates**
are ignored. The saved schema/source proof remains. Master's two automatic Dependabot update
jobs are separately recorded above as dynamic runs; the earlier not-yet-scheduled sentence is
preserved as its capture-time qualification. No scheduled run was manufactured.
**AC1 stays unchecked** for the separate Ubuntu 26.04 P5
exact-head and END deploy gates under the dated merge-then-prove reading. P4 retains Ubuntu 24.04
and the existing macOS label. **AC3 stays unchecked** until the END docs dispatch supplies the
actual SWA annotation result; the mutable SWA `stable` image remains an accepted residual.

The exact dotnet-ef 10.0.3 install/version and Mach-O arm64 bundle compilation remain command-only
proof, with both initial failures preserved and no bundle invocation. Linux packaging/execution,
artifact-ID transfers and DEV TLS remain END evidence. No tracked DTO, client/spec, model or
migration regeneration occurred in P4; ignored mobile generation and XcodeGen used unchanged
specs. The Initial id remains `20261013160545`, with its final DEV reset debt still open. No
changelog entry is owed for this CI/tooling and record-only phase.

### 2026-10-14 — P5 source and local suites complete; publication and runtime gates pending

The separate Ubuntu 26.04 source is committed locally as `2751ffb7d44976d27f7b0a83f5083ab3adfc7f9e`,
after the P4 closure-first commit `65f2aa2a1ac52038420747855b458d8c5fdebdfc`. The source receipt
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/source-commit001.json`
(SHA-256 `661781bbb9414576720bcddd9d6b19d24640f3da59ec55a11ec1ac39cfc502c9`) binds exactly **14 changed paths**:
**25 Linux runner declarations in 12 workflow files**, **five related comments** including the
parity checker comment, and the CI runner documentation. All other **8244 Git blobs/modes** are
unchanged from the closure-first parent. The macOS workflow, action pins, runtime flags and
workflow logic are unchanged; no application behavior or client, spec, model or migration changed.
No changelog entry is owed for this CI and record-only phase.

The frozen source verdict is **"APPROVED — SCOPED"**, with zero material findings, in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/candidate-review001/verdict.json`.
The later source/message factual assessment is **"CLEAN — BOUNDED SOURCE AND MESSAGE PRECOMMIT REFUTATION"**,
**1021 checks passed**, zero material findings, in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/source-precommit-refuter001/verdict.json`.
Both cover their stated source/message scopes; these P5 records and publication evidence remain
separate gates. YAML parsing, runner inventory, source equality and actionlint are **command-only**
proof. Actionlint 1.7.12's built-in inventory rejects the 25 Ubuntu26 labels; it exits 0 with the
saved off-tree label configuration, with ShellCheck/Pyflakes disabled. No repository actionlint
configuration changed, and the allowance does not prove hosted compatibility.

The separate Gate 3 source assessment is **"APPROVED"**, qualified as
**"SCOPED P5 T-0849 GATE 3 SOURCE PASS"**, with zero material findings, in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/ubuntu-security-review001/verdict.json`
(SHA-256 `922f2bf482f3e6279a7a52f704f8f2990998b52bc5f3116d2c42d1e63fa316f9`). It covers the exact
committed 14-path source change and preserves the existing token/cloud grants and workflow
security boundaries; it is not hosted/runtime or Azure identity approval.

Fresh backend → frontend local checks on the frozen source passed **all 23 recorded steps** in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/full-backend-frontend001/finalization-continuation001/final.json`
(SHA-256 `bfc03be8cfc63fed84a0d9f68180e95b6333dbd76c872573cba46881d7696bb5`); its
`sealed-packet.json` has SHA-256 `41851fe45547c3a6b764276404811fe775abd9fe8cf3f6bed3f6f0fbdc2e22f6`.
Release rebuild has **220 warnings, zero errors**, followed by unit → real-Postgres
integration → host tests: **8731 / 975 / 509 passed**, zero failures or skips. Frontend guard
self-tests (**8**), type-checking all **73** app specs, **5041 Jest tests / 408 suites** across
**71 nonempty + 3 empty projects**, three production builds and **7 localhost, network-stubbed
smokes** (5 customer / 1 partner / 1 admin) pass. The actual P5 affected-lint selection is **zero
projects / no coverage**, because the 14 source changes are outside the Nx workspace; separate
full informational lint passes across **77 executed targets**. The first finalization attempt's
parser rejected a wrapped success marker; the continuation corrected only whitespace parsing,
preserved the initial failure and re-ran **zero SDK steps**.

The later source binding
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/source-commit-sdk-rebind001.json`
(SHA-256 `f14d0ebf03928cdde0ccc2317e687b3f5db31a24e9594a5a8209b5113464d4b8`) compares
**8258 committed Git blobs/modes**: **8254 identical** to the measured source, four declared
P4 closure-record deltas and all modes exact. **Every compiled/runtime input is identical.**
This is source identity proof; the backend/frontend suites were not re-executed from the Git head.
The actual source head's docs-reference check, catalog-claims check and VitePress build each exit 0 in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/source-head001/final.json`
(SHA-256 `106cc56228081508f08ecc9fe0486412648d8fb159ad02e81fbffd7c74dbc64f`), with all
**8258 tracked input hashes/modes unchanged**.

The actual P5 mobile receipt
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/full-mobile001/final.json`
(SHA-256 `1ddc7db5667ae4f80383ce8224bd82865f300d01e513f38c9ea19959f2f4f410`), with sealed packet SHA-256
`7ad47152cc166766012d7d99ac2ed6a2c18cf4824180b55014bebe1ac2d05aa5`, records **`PASS_WITH_UNCHANGED_VOICEOVER_RUNTIME_SKIP`**.
Fresh, forced Android compile/unit execution passes **2873 tests in 309 XML files**
(298 core / 881 partner / 1694 customer), zero failures, errors or skips. Compile executes
**56/56 actionable tasks** and units **96/96**, with no `FROM-CACHE` rows. The named lifecycle,
diagnostic and empty-Java task outcomes are separately qualified; the first helper rejected a
successful compile's lifecycle `UP-TO-DATE` rows. That parser finding was independently confirmed
with **44 passing controls**. The narrow off-tree helper correction reparsed the saved successful
compile, preserved the original failure and re-ran **zero SDK compile steps**; the later unit
execution is actual P5 evidence. Its parser-history seal is
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/android-parser-finding-verifier001/sealed-packet.json`
(SHA-256 `48fd5127e6ed2a117f11df93c72942f7ab2ce535b889a4258f6ece016ce8d589`).

iOS **18.6**, arm64, executes all three unfiltered `test-without-building` schemes:
Core **776**, Partner **1133**, Customer **1779 passed**, zero failures and **one unchanged
Customer VoiceOver runtime skip**, `ProfileEditChipTests/testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated()`.
No VoiceOver retry or spoken-announcement proof is claimed. These are new P5 executions using a
private APFS copy of exactly **2219 source-bound P4 compiled-product files**, with distinct inodes
and no hardlinks. **There is no fresh P5 iOS compilation or P3 binary reuse.** P3 supplies only
portable, unchanged, unfiltered execution metadata. Tool versions, arm64 Simulator SDK **26.2**
ABI under Xcode **26.3**, compile source and generated inputs match the P4 products.
Preparation ran SwiftFormat `--lint` before SwiftLint `--strict`, the local-config guard,
`./scripts/generate-api-clients.sh` and `xcodegen generate` in both app directories from unchanged
specs. The **473 ignored client files plus six XcodeGen files** match P4 compilation inputs;
these commands produced no tracked client/spec edits.
Reused binaries retain P4 `#filePath` roots: the saved audit confirms equal **7508 `src` files/modes**,
keeps the old source readable, and finds no source-backed runtime reads of the changed 14 paths.
That audit is **static source/binary proof**, not runtime file-access instrumentation:
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/full-mobile001/runtime-reader001/sealed-packet.json`
(SHA-256 `761c9f65fc1b44ff91c9807508ad03b7c99df9c67eba9ef6ac06381d9a6f3563`).
The final mobile receipt must be read with these inherited-compilation and runtime-reader limits.
P5 adds no iOS 16.4, device, provider/mail/D9, APK or R8 acceptance proof.

Mobile admission followed two guarded off-tree capacity cleanups, recorded in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/mobile-capacity-review001/revision002/application-receipt.json`
(SHA-256 `d0054259bbe6d1d252ce882932920e758857ff1fbf0b7b2038b469b9195a8985`) and
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/mobile-capacity-options002/revision002/application-receipt.json`
(SHA-256 `aff3a50f2d71d10fc1d1cf1d26cab7be106eb4829c5cd035938d3774ae2c7ab0`). They remove
**39746 generated leaf files**, with preservation checks passing and zero intersection with
the original **80021 preservation pins**. The summed observed free-space increase is
**2673881088 bytes**; it is an observation, not exclusive APFS reclaim attributed to deletion.
The final mobile packet confirms all **8258 source hashes/modes**, **68 native inputs**, **7508
runtime-read `src` inputs**, **479 generated files** and **80021 historical preservation pins**
remain exact. New Android **20590** compiled-product files and the copied iOS **2219** files are
pinned, the simulator lease is released and no mobile SDK command remains active. These SDK
executions are off-tree; the actual Git checkout itself was not compiled. Earlier failures
and captures remain preserved.

No Ubuntu26 hosted job, P5 PR publication, merge or END deploy/SQL/docs runtime result is supplied
by these local receipts. Expected source-triggered hosted proof is **eight Linux PR checks across
seven workflows**, then **seven executed Linux master jobs plus the guarded e2e-smoke skip**
(eight repository check rows), with actual runner-image logs. The approved END procedure must
prove deploy/SQL/docs execution and expanded matrix legs after all phases merge. The two PRO-only
declarations (`deploy-pro`'s `legal-texts`, `execute-sql`'s `execute-pro`) remain source-only and
unexecuted under the PRO ban. **AC1 remains unchecked** until its PR and END runtime gates are
proved under the dated merge-then-prove reading; **AC3 remains unchecked** until the END docs
dispatch supplies the actual SWA annotation result. Existing checked AC2/AC4/AC5/AC6 and owner
wording are preserved. No reset, deployment, SQL operation or tracked client/migration regeneration is
claimed by this P5 phase; the Initial `20261013160545` reset debt stays at END.

The final record commit still requires its own compiled/runtime-input rebinding and **13
actual final-head local commands** before push. Those gates are pending at this records capture;
the three source-head docs results above are the completed command evidence currently supplied.
Hosted PR/master receipts and END runtime results remain future evidence.

### 2026-10-14 — P5 Ubuntu 26.04 merge and master closure

P5 [#336](https://github.com/VM-s-Solutions/cleansia/pull/336) merged at
`2026-10-14T20:11:54Z` as `2118ad22736d91e2026967f5a19503b5e4019afd` from exact head `44392c870177ab63467448b9a841141d7da8db45`.
All **eight PR checks across seven workflows** passed on their first attempts, with **zero
re-runs**. Captured logs show **Ubuntu 26.04 in all eight executed Linux PR jobs**. The tested
head and merge have the same Git tree. Exact merge, checks and bounded no-phase-dispatch evidence
are saved in `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-ubuntu26-pr336-ci-merge-closure-001.json` (SHA-256 `f0904ef9d7abad2601a6a88b898823dfbbe5393ddc3857538aeab3470424a4fa`).

Master then passed **seven executed repository push jobs across seven workflows**, each on
attempt 1, with the single **Frontend e2e-smoke guarded push skip**, zero re-runs, and
**0 automatic extra checks** separately preserved. The master receipt
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/master-ci-evidence-receipt001.json` (SHA-256
`35207b649e1ab10cce4a1c5dd67051e4befd588ef2cf021d05026ecbc755aece`) records the completed checks and
**seven actual Ubuntu 26.04 repository job images**; its hashes bind the poll/evidence summaries
and all seven executed job logs. The separate raw-collection supplement
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/master-capture-bindings-supplement001.json` (SHA-256
`9a58b8437700af6e7e77c2116d2bcb7b8c1fc7a0af8f18b98c3a20833d2173df`) binds the exact paths and digests of all **eight raw annotation
collections** and **seven artifact-list responses**. Captures were saved on the UTC day of
execution under the one-day retention rule.

The PR API annotation comparison initially stopped on one CS8604 warning replacing a different
sampled warning. That original **`NEW_ANNOTATIONS_STOP`** receipt is preserved. The separately
verified equivalence receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/exact-annotation-equivalence-author001/annotation-equivalence001.json` (SHA-256 `673508f197285ce6b74c78305c4678bf8c2b012ea0b7e6493eb010b5327e0041`) qualifies
only the exact `BlobContainerClientFactory.cs:38:40` API substitution: both complete compiler
logs contain the same **440 warning occurrences / 220 unique diagnostics**, SDK **10.0.402**,
runtime **10.0.13**, and identical **7508 source blob/mode rows**. The prior log already contains
that CS8604. This is an inherited compiler diagnostic; no zero-warning claim or general warning
waiver is made. The exact gate and all eight fresh check identities/counts preceded the guarded
merge. The hosted compiler versions are distinct from the recorded local SDK 10.0.301.

The final head's **13 local command gates** and full local suite receipts above remain bound by
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p5/final-record-sdk-rebind001.json` (SHA-256
`b24f57bd6e33915c4477c17c089338eb491b7d3bbde4885af3ae42a31563fb46`). P5's iOS qualification remains unchanged:
unfiltered iOS 18.6 tests used independently copied P4 products; **no fresh P5 iOS compilation**,
and Customer retained **one VoiceOver runtime skip**. These source/runtime bindings are not an
additional suite execution. The PR's Playwright artifact was saved with its exact ID and digest.

Mike's dated 2026-10-13 approval — “You can reset db, but once we're done with everything and we
can do a fresh deployment” — and “Go with defaults (recommended)” continue to govern END.
**AC1/AC3 remain open**: the deploy/SQL/docs declarations and expanded matrix legs still need the
approved final DEV procedure after P6 merges with green master CI. The two PRO-only declarations
remain unexecuted. This phase dispatched no deployment or SQL workflow and performed no reset
or tracked regeneration. The single Initial remains `20261013160545`; its DEV reset debt is END.
This P5 closure is prepared to be the **first commit of P6**.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “Analyze and Fix those”; “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is FF-13 docs-table cross-reference; P5/END source/runtime closure remains separately owned.
- FF-13 separates request logging from local orchestration in CLAUDE.md's truth table, recorded on T-0818 and cross-recorded here because CLAUDE.md was also an AC6 destination. The dedicated Nx version line stays byte-identical. P4 items 2–6 and the actual P5 #336/source/CI/merge/master evidence above remain.
- The actual P5 closure is the first P6 commit, `dccf996787ed52e14b7a8e0305bebab3fa3ca0ea`, parent `2118ad22736d91e2026967f5a19503b5e4019afd`; this addition preserves its T-0849 record and sole INDEX notes. Receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/p5-closure-first-commit001.json` (SHA256 `6cfca7460d620e4c378a723f9008b19f30e1fcacc93d154bbefd34a87ac67dc4`). The closure head's three documentation commands passed as recorded in `p6/p5-closure-head001/final.json` (SHA256 `1627152ab1db10b09dcb6c8858a9bf692780d86a7bcd377bb64017ab53bfadae`); those are closure-head results, not P6 source-head results.
- AC1/AC3 and the merge-then-prove reading remain open for the final authorized DEV/SQL/docs procedure. The P5 inherited-warning qualification, P4-product iOS test qualification and VoiceOver skip remain. The two PRO-only declarations stay unexecuted.
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

- The P6 follow-up for FF-13 CLAUDE table cross-reference was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. The first P5 closure and merge-then-prove history are preserved; AC1/AC3 and END runtime remain open. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

### END actual execution and SSR blocker — 2026-10-15

The P6 publication subsection above is an earlier checkpoint. Its END-pending or unexecuted wording is historical and is superseded by this actual partial-execution record; original dated phase history and owner quotes remain unchanged.

The dated merge-then-prove history is retained. P5's actual hosted Ubuntu26 CI passed, and END what-if plus the real DEV build/provision/migration ran on the approved definitions; this is actual runner execution, but it does not complete **AC1** because the full DEV deployment ended with failed customer SSR readiness after the three web API retry jobs passed. **AC1 stays unchecked; INDEX is blocked on Q-END-01** for the missing SSR runtime/worker evidence. No revert or source correction is inferred from the unknown cause.

**AC3 is now checked:** the single authorized docs deployment `38658881649` / attempt 1 passed VitePress and SWA upload on the pinned final master, with zero stored annotations and no Unexpected-input/Node20/integrity-warning matches. An authenticated docs-reader session was not tested. Runtime attempt-1 compiler/Buffer warnings and attempt-2 failure/warning/notice evidence remain preserved; this is no zero-warning full-deploy claim. The four seeds, post-seed health and final host/SSR probes were never completed.

Shared END actuals and qualified evidence pins: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation002/end-outcome-facts.json` (SHA256 `8c49656e9f9dfecd936cf0386846fb958cbb64d0eb55f6dc8b67e965de0c3695`).


### Owner reply and resumed DEV procedure — 2026-10-15

This is the checkpoint after the earlier END/SSR blocker above. Q-END-01 was answered and removed from the open queue; it does not require another owner diagnostic before the already-authorized work continues. The cause of the earlier process outage remains unestablished.

Mike’s replies, verbatim, 2026-10-15:

> All of the services are healthy. It happens all the time that after deployment it takes some time to start up the service and if fails in the beginning. I want you to investigate this issue and make the start up almost immediate without issues

> There is no seed data which is expected on backend (language,currency, so on)

Fresh sequential HTTP checks at 07:51 UTC passed all five API `/health` endpoints with exact `Healthy` bodies and both customer SSR origins with the server-render marker, before any of the four seeds. This establishes recovery before seeding, not a missing-seed cause. The original deployment `38660355359` recovered as attempt 3 after rerunning only its failed Warm job: all 16 job conclusions are successful, with 14 carried executions and only Warm `116312011258` and Close `116312189668` freshly executed. Earlier attempt-1/2 failures remain retained. The close job still reports `MissingSubscriptionRegistration`; shortening/muting is not proved.

On unchanged master `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`, DEV-only SQL scripts then ran one at a time after fresh master and all three workflow-dispatch queue inventories:

- `prod-bootstrap.sql`: run `38702440461`, attempt 1, committed at 07:59:52.9004135 UTC.
- `insert_seed_data.sql`: run `38702856219`, attempt 1, committed at 08:03:19.5675480 UTC.
- `insert_local_dev_admin.sql`: run `38703413453`, attempt 1, actual `INSERT 0 1` at 08:08:09.1632115 UTC. Its source leaves `MustChangePassword=false`; no forced password-change or authenticated console exercise is claimed.
- `seed/insert_email_template_translations_promo_code.sql`: run `38703639106`, attempt 1, failed at 08:09:49.4004421 UTC because `UpdatedOn` is `timestamptz` while its derived VALUES column is inferred as text. The single rejected INSERT wrote no prefix. Both nullable timestamp columns need explicit typing, with a real PostgreSQL regression before correction.

The first three scripts must not be replayed. The current END obligation is a reviewed source correction on the startup branch, retrying only that failed seed after merge, then `check-db-health.sql` and final five-API/two-SSR readiness. No further reset, storage emptying, PRO operation or docs dispatch is added. Existing Initial `20261013160545_Initial` remains unchanged. Startup reliability work revisits existing T-0636 separately; no earlier outage cause or speed improvement is claimed yet.

Actual receipts are retained under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/startup-2026-10-15/resume-end001/`. Independent attempt-3/bootstrap/fixtures/admin reviews and the confirmed promo failure are under `startup-2026-10-15/resume-end-review001/`; the promo finding SHA256 is `35d4974400e501cfe0d02b8468c249f631feea19356cdb93eca0ba2668e84db3`. Final seeded health and the applicable acceptance criteria remain unchecked.

### Startup correction and remaining END completion — 2026-10-15

The dated END and resumed-procedure sections above remain the record of their capture times. Q-END-01 is answered; the original deployment `38660355359` recovered on Warm-only attempt 3, with 14 carried and two fresh successful jobs. Its earlier failures remain retained. The three successful seeds `38702440461`, `38702856219` and `38703413453` are not replayed. The promo seed's failed run `38703639106` remains a failed first attempt.

The promo script's minimal source correction now passed its exact-script real-PostgreSQL regression, including all 60 translations across five languages and 12 keys, with unchanged complete rows after a repeat. Only the two first-tuple nullable timestamp casts changed. The Initial migration, schema and generated contracts remain unchanged. Startup implementation, scoped reviews and completed local gates are recorded on [T-0636](T-0636-deployment-startup.md); they do not substitute for the following END actuals.

- Resulting-master startup publication and DEV rollout: outstanding at this checkpoint; no startup publication or resulting-master startup rollout has occurred.
- Retry of only the failed promo script, from the merged correction: outstanding at this checkpoint; retry only failed run `38703639106` from the merged correction.
- Read-only `check-db-health.sql` and final exact Healthy responses from all five APIs plus both server-rendered SSR origins: outstanding at this checkpoint; earlier warm responses and approximate statistics do not establish final seeded readiness.
- Acceptance disposition after those results: unmet at this checkpoint. Until the required live results are recorded, the applicable END criterion remains unchecked and INDEX remains `in_progress`.

There is one database reset in this END procedure. No second reset, replay of the first three seeds, storage emptying, further docs deployment or PRO operation is added. The published admin's existing `MustChangePassword=false` and absence of an authenticated console exercise remain qualified. The attempt-3 quiet-window close warning `MissingSubscriptionRegistration` remains retained; a successful job conclusion is not proof of mail suppression.

### PR #338 publication and failed startup rollout — 2026-10-15

The earlier publication-pending checkpoint is superseded by these dated actuals. [PR #338](https://github.com/VM-s-Solutions/cleansia/pull/338) merged as `c54d3d64e777d5042572231b1ce2db0990e67306`, SHA-guarded on reviewed head `3a480403e7e48a8d6f8369b5a343f32211a877aa` and base `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. The merge retained reviewed tree `12206772eab4070624c197d5bd9af2a0f56facba`. PR CI passed all six jobs across five triggered workflows, attempt 1, no reruns, retaining 11 warnings (10 Backend, one e2e artifact-retention) and zero failure annotations. Exact-merge master CI completed four executed jobs successfully across four workflows; Frontend `e2e-smoke` was the fifth job view and was skipped on push. All were attempt 1 with no reruns; ten Backend warning annotations and zero failure annotations remain. The skip supplies no executed test pass.

The resulting-master rollout `38766841247` / attempt 1 failed with nine successful, six failed and one skipped job; 30 annotations retain 18 warnings and 12 failures. All six SCM package attempts have **UNVERIFIED** committed outcomes and no verified final package JSON or structured activation timings. `Warm dev sites` was skipped. The qualified step count is 18 PRO-only skips plus five DEV API warm-step skips, not 23 PRO-only steps. No new migrations were applied; the bundle reported the database already up to date. Nine retained CRC/digest-checked producer archives total 358,521,552 bytes, with six artifact-ID consumer bindings and three qualified name bindings; no live package verification is inferred.

The stopped observer's 924 samples (588 healthy, 336 five-second curl-28/HTTP-zero timeouts; first round healthy, last round timed out) are dated samples, not continuous or final readiness proof. A separately confirmed owned loopback early-401/403 large-request `BrokenPipeError` reproducer establishes diagnostic ambiguity only, not an Azure authentication cause or October incident cause. Transport follow-up is being prepared separately; no corrected deployment/new PR or executed read-only diagnosis is claimed. Full actual qualifications and immutable evidence digests are on [T-0636](T-0636-deployment-startup.md).

No promo retry or final seeded DB/HTTP acceptance followed the failed dependency. The three successful seeds `38702440461`/`38702856219`/`38703413453` must not be replayed; only failed promo `38703639106` remains a future retry. Original deployment attempts 1–3, earlier failures and the single reset stay historical. No additional reset, docs dispatch, storage emptying or PRO work is added. The applicable END criterion remains unmet, INDEX remains `in_progress` and MS-2 remains Open.

### Fixed DEV diagnostic workflow source — 2026-10-15

The transport follow-up adds manual master-only `diagnose-dev.yml` for the six fixed DEV apps under existing OIDC/concurrency and sanitized one-day artifacts. Both Backend CI filters include it; the existing CI step selects the module whose 50 local contracts passed. [T-0636](T-0636-deployment-startup.md) records qualified fresh application actuals and remaining final operations/source binding/publication/live acceptance at the dated checkpoint. This supplies no diagnostic dispatch or hosted runtime proof; AC1/END and MS-2 remain open.

### PR #339 publication and read-only inspection blocker — 2026-10-15

The prior transport-publication-pending statements are dated checkpoints. [PR #339](https://github.com/VM-s-Solutions/cleansia/pull/339) merged `8403c48c63dceae066f09b64a0ff63dfd0ec183c` from reviewed head `476d0ac6f16f66e517bcd3b9022f80d8834eeae8`, retaining tree `d99a83ef61bdb2cb4f288a34f4d6204b24a2cbb3`. Exact-head PR CI passed six jobs/five workflows; exact-merge master CI passed four jobs (three repository push workflows plus one automatic Dependabot dynamic run). All were attempt 1/no reruns. PR retained 11 warning annotations and one verified Playwright archive; master retained 10 warnings plus one Dependabot Ubuntu-label notice, zero artifacts and no Frontend run/e2e skip. Both retained zero failure annotations. The approved final local seal records 8,746 source inputs unchanged and 17 actual commands: 16 successes plus one preserved Docs harness failure; prior qualified full application acceptance remains applicable.

One GET-only inspection dispatch initially had an unresolved identity; without reposting, GET reconciliation identified `38797931482` / attempt 1 on that master. All six jobs failed with partial JSON; six ZIPs/JSON reports were retained with digest/CRC checks. The comparator blocks all six apps as `BLOCKED_PENDING_OR_UNVERIFIED`: history/active archive/pointer stability remain unverified and no upload is justified. A minimal diagnostic source follow-up is required before another state assessment; no Azure cause, upload retry, recovery or final readiness is established. [T-0636](T-0636-deployment-startup.md) records the actual request outcomes, local/CI qualifications and immutable evidence pins.

The applicable END criterion remains unmet, INDEX remains `in_progress` and MS-2 stays Open. Original reset/docs/deployment attempts and successful bootstrap `38702440461`, fixtures `38702856219` and admin `38703413453` remain history; successful seeds are not replayed. Failed promo `38703639106` still awaits its sole corrected-script retry after the runtime dependency is resolved; final DB health and 13 HTTP observations are unperformed. No second reset, docs redispatch, storage cleanup, PRO or graphify action is added. `MustChangePassword=false`/no authenticated console exercise and the existing provider-warning/no-muting-proof limits remain retained.

### PR #340 closure and unresolved runtime identity — 2026-10-15 UTC

[PR #340](https://github.com/VM-s-Solutions/cleansia/pull/340) merged `70878a3545da4a6fbc8b541a20916d7d53c86be7` from reviewed head `b6335299314ffc4111ca5c4fda25f35e8c4ebfce`, retaining tree `cf4dcb96901186f269eeeafcdd2b0a6989b896dc`. Exact-head PR CI passed six jobs/five workflows; exact-merge master CI passed three jobs/three repository push workflows, with no Frontend master job or extra run. All were attempt 1/no reruns. The final local seal bound nine actual commands (seven successes, one explicit consistency non-run and one preserved interrupted Python attempt), the later full 59-test Python pass and 8,746 inputs with qualified prior application results; no SDK replay is implied.

The reviewed read-only inspection `38814387736` supported one scoped recovery. Recovery `38815343537` / attempt 1 failed (nine successful jobs, six Location-guard package failures, one warm skip); HTTP 202 remains source-inferred, with no preserved raw status/header or final verified SCM JSON. Subsequent GET-reconciled singleton inspection `38818409957` / attempt 1 passed all six collection jobs: each new UUID was terminal, all selected package ZIP hashes/byte counts matched the new intents, but all six SCM `wwwroot` entry hashes differed from the new entries and equalled the older SCM hashes. Active worker/mount identity and readiness remain unresolved; neither another upload nor a seed follows from green collection. [T-0636](T-0636-deployment-startup.md) records exact local/CI/live qualifications and immutable pins. Recovery/reconciliation were 2026-10-16 in Europe/Prague, dated here in UTC.

The applicable END criterion remains unmet, INDEX remains `in_progress` and MS-2 remains Open. The original reset/docs and successful three seeds stay historical; only the failed promo retry, final DB/13-HTTP acceptance and final closure remain outstanding after the runtime dependency. No successful seed replay, reset, docs redispatch, storage cleanup, PRO or graphify action is added.

### Linux package source and later promo completion checkpoint — 2026-10-15 UTC

This source checkpoint is 2026-10-16 in Europe/Prague; preceding unresolved/promo-pending statements remain history. [T-0636](T-0636-deployment-startup.md#linux-selected-package-verification-source-checkpoint--2026-10-15-utc) records the exact source, review and actual-evidence pins. Linux selected-package archive identity plus separate public readiness is the accepted bounded gate; all six SCM entry mismatches and failed recovery `38815343537` remain intact, with no executing-worker/cold-start/continuous-uptime claim. Later public health samples passed 6/6, not the real SSR/final 13-HTTP gate.

Promo-only retry `38820436701` / attempt 1 completed on master `70878a3545da4a6fbc8b541a20916d7d53c86be7`: two successful jobs/one PRO skip, zero annotations/artifacts; DEV reported `INSERT 0 60` and script success. Sixty is affected rows, not a table total; no literal BEGIN/COMMIT receipt is claimed. No original successful seed/reset/docs work was repeated. C3 `f5e04cbb210afd7b9053ffb34840b98ff4bf3112` is local/unpushed and exactly three reviewed source changes are locally uncommitted. Full 66-test/local/docs/head gates, actual source binding/publication/CI, resulting-master deployment and final DB/13-HTTP acceptance/closure remain pending. The applicable END criterion remains unchecked, INDEX remains `in_progress` and MS-2 remains Open.

### PR #341 closure and qualified selected-package reconciliation — 2026-10-16 UTC

[PR #341](https://github.com/VM-s-Solutions/cleansia/pull/341) merged `2b83eff633a195c35b4fac332839ff47942638c1` from C4 `4b42cfb4bd543f08b5fa4aa7f782b8226eb594ad`, retaining tree `d8855b6ff21815d605435bed30e2a3d99e5a71a7`. Local proof retains eight actual rows/seven successes/one consistency non-run and full Python 66; PR CI passed six jobs/five workflows and master CI three push jobs/three workflows, all attempt 1/zero reruns. PR/master retain 11/10 warning annotations, cache/zero-affected-Jest/fixture browser/app-carry qualifications and no invented master Frontend run. [T-0636](T-0636-deployment-startup.md#pr-341-closure-selected-packages-verified-rolloutreadiness-still-failed--2026-10-16-utc) records exact evidence and limits.

DEV `38830931038` / attempt 1 failed with 14 successes, one Customer Mobile post-acknowledgment deployment-list GET TimeoutError (60.519 seconds/no final JSON) and one global warm skip. Six new intents/five selected-package final JSONs and nine fresh artifact lineages are retained. Its stopped observer recorded 672 samples/533 healthy/139 timeouts, last round 0/6; no real SSR render/final acceptance follows. Read-only singleton `38833690130` / attempt 1 passed six collection jobs; the independent 897-check/109-pin comparison verifies all six current selected ZIP hashes and byte counts against their own UUID intents, including Customer Mobile. All six SCM backing-entry mismatches remain separate namespace diagnostics, not executing-worker proof. The failed rollout stays failed; neither readiness nor reupload is justified by green collection. No causal source defect is confirmed; bounded hosted-runtime/plan-metrics evidence is prepared off-tree only.

Promo-only retry `38820436701` is complete (`INSERT 0 60`, affected rows/no literal COMMIT receipt) and is not repeated. Fresh final DB health/13 anonymous HTTP acceptance remain unperformed. The applicable criterion/checklist remains unchanged and unmet, INDEX remains `in_progress` and MS-2 remains Open. Original successful reset/docs/bootstrap/fixtures/admin/promo are not replayed; all prior history and qualifications remain.

### Bounded runtime evidence source checkpoint — PRELAUNCH 2026-10-16 UTC

[T-0636](T-0636-deployment-startup.md#bounded-runtime-evidence-source-checkpoint--prelaunch-2026-10-16-utc) records the one-YAML evidence candidate and scoped correctness/security/command-mock QA. Full66/docs/head/app-input carry proof, publication/CI and hosted runtime execution remain pending at this dated prelaunch checkpoint. No causal fix or live readiness is claimed; prior rollout/inspection/promo facts, all criteria, `in_progress` and MS-2 Open remain unchanged.

### PR #342 closure and bounded runtime observations — 2026-10-16 UTC

[T-0636](T-0636-deployment-startup.md#pr-342-closure-and-bounded-runtime-observations--2026-10-16-utc) records PR #342/M3 CI, qualified local/application evidence and read-only diagnostic `38868360114`/1. Selected packages still match; public status/timing fields are NULL and plan samples establish no initiating cause. The failed rollout and prior history remain. Promo is complete/no replay; final DB/13 HTTP remain unperformed. The applicable criteria/checklists, INDEX `in_progress` and MS-2 Open stay unchanged; B3 remains a pending proposal under the cost ban.

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

### 2026-10-14 — actual final head and publication evidence

The full local suites remain bound to source commit `a87c0be5aca745d8bfa980080269631a1138873e` by
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/source-commit-sdk-rebind-001.json` (SHA-256 `b7a869975d6991f67cd119acfbe0d89edd2edfd638086132f4d66e06df68e059`): **8258 committed blobs/modes**, **8242 identical**
to the measured combined source and **16 declared documentation/record deltas**; every compiled
or runtime input is identical. This is source identity proof; the suites were not re-executed
from that Git head. Android's forced compile/unit result remains **2873 passed**; iOS **18.6**
Core **776**, Partner **1133**, Customer **1779** passed with **one unchanged Customer VoiceOver
runtime skip**. No spoken-announcement proof, additional iOS 16.4, device, provider, mail, D9,
APK or R8 acceptance run is added by P4; the qualified native-phase proof remains preserved.

The final follow-up `6343b1908a8ca30f20e6bbae1713b47085a1c89f` changed exactly the four ticket/INDEX afterimages in
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/final-records-commit-001.json` (SHA-256 `fb77a8b0d2c64a2dbd9cb15ac0cf8e2d36037f4e182935efbc9479bd31454bc2`). The other **8254 Git blobs/modes** are unchanged from
the source commit. Its actual-head receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/final-record-head-001/final.json` (SHA-256 `059a9d52b642df16168c65acacd8b20a382b8516d3a6119daac1f87cf3692410`)
records **13 commands, each exit 0**: docs references, catalog claims, VitePress build, eight
Node checks/self-tests, actionlint with ShellCheck/Pyflakes disabled, and the redacted secret-history
scan. All **8258 tracked hashes/modes** stayed unchanged. These command gates precede and are
distinct from the hosted PR result above.

The earlier frozen source verdicts remain **"APPROVED — SCOPED"** for their candidate source
scope; the actual-settings assessment remains **"PASS — actual P4 GitHub settings match the
authorized decisions; no material security finding in this scope."** for the saved actual-settings
outcomes. Neither is a blanket runtime or Azure assessment. The final record/message factual
assessment, `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/final-records-refuter001/verdict.json` (SHA-256 `954e80e7276c0a3e20153423d1488bd65dcca19b3699e03237748fde2f90e606`), is
**"CLEAN — SCOPED FACTUAL REFUTATION"**, **800 checks passed**, zero material findings.
The subsequent assessment `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/publication-refuter001/verdict.json` (SHA-256 `30874657fc725baa17c7b3260e095af5ccd131f6c54776c1968ef159b1ed37e8`) is
**"CLEAN — BOUNDED PREPUBLICATION FACTUAL REFUTATION"**, **721 checks passed**, zero material
findings; it rechecked the publication change and bindings while retaining the prior 800-control
round, whose controls were not re-run. Publication receipt
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/publication-001/published-receipt.json` (SHA-256 `a93461b72e25fe48378d2013d5166cf495903f203c277b700ddf8c0a96d3242a`) binds the published PR body SHA-256
`9399d0d24f01e3b3fac42db8cb20727f15746bb01234c822186b7d5a4f0c364d`.

Command-only checks remain qualified: seven trigger inputs, Docker-reference/BOM controls,
app-spec type-error/stub controls, lockfile equality, dependency schemas, and the isolated Mac
dotnet-ef install/compile. The passed PR jobs do not close Ubuntu 26.04 deploy runtime or SWA
annotation acceptance.
