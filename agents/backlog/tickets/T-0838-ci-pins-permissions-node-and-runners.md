---
id: T-0838
title: Harden the workflows — actions pinned to commits, least-privilege tokens, Node 24 actions and Node 22 in the deploys, the SSR on Node 22, and Ubuntu pinned to 24.04
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-15
depends_on: [T-0820]
blocks: [T-0849]
stories: []
adrs: []
layers: [ci, bicep, docs]
security_touching: true
---

## Context

T-0820's review reported three things for Mike: the mutable action tags, the missing `permissions:` blocks and the deploy workflows' Node 20.
- The CI ground truth of 2026-10-12 confirmed each one against `db2272a5d` and resolved every ref.
  - The ground truth itself is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_ci.json`.
  - The inventory is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/ci-inventory.json`.
  - Tag lists, `action.yml` at each SHA and the release notes are under `p5/ci-inventory/`.
- The CI security review of the same day checked the pins and wrote the permissions plan. It is in the same Wave F folder, `security_ci.json`, with its evidence in `p5/security-ci-perms/`.

**Mike, 2026-10-12:**
- **"Fix"** for the SHA pins, least privilege, the deploy workflows' v4 actions, the two Node 20 pins and `main.bicep`'s `NODE|20-lts`.
- **"Fix it"** for `ubuntu-latest` moving to Ubuntu 26. This also answers the pin question in T-0813's runtime packet.
- gradle/actions stays on v5.

At `207d2f319`:
- **No action is pinned.**
  - All 67 action `uses:` lines across the 13 workflows are on mutable tags, for example `android-ci.yml:45` `actions/checkout@v5`.
  - The two local reusable calls (`deploy-dev.yml:69`, `deploy-pro.yml:72`) are `./` paths.
  - `Azure/static-web-apps-deploy@v1` names both a tag and a branch. Actions runs the tag, `1a947af`.
- **Eight workflows have no `permissions:` block:** android-ci, backend-ci, booking-policy-parity, docs-ci, frontend-ci, ios-ci, ios-symbols-ci and deploy-docs.
  - `deploy-azure.yml` has job-level blocks only.
  - `deploy-dev.yml:40-42`, `deploy-pro.yml:36-38` and `execute-sql.yml:22-24` grant `id-token: write` at the top. So jobs with no Azure login inherit it: deploy-dev's `resolve` and execute-sql's `validate`.
  - `secret-scan.yml:19-20` already declares `contents: read`.
- **The deploy workflows run Node 20 action majors:**
  - checkout@v4 (12 uses), setup-node@v4 (3), setup-dotnet@v4, cache@v4 (2), upload-artifact@v4 (9), download-artifact@v4 (5) and azure/login@v2 (10);
  - Since 2026-06-16 the runners have run these node20 actions on Node 24 with a warning (DEV run 36763660261, and a deploy-docs job on 2026-08-25). Node 20 left the hosted runners on 2026-09-23, so nothing can opt back out.
- **Two Node 20 pins:** `deploy-pro.yml:61` and `deploy-docs.yml:18`. Node 20 reached end of life on 2026-04-30. Every other workflow pins 22.
- **The customer SSR runs on `NODE|20-lts`** (`deploy/bicep/main.bicep:312` → `modules/appService.bicep:80`, `:117`), but it is built on Node 22 (`deploy-azure.yml:217`).
- **25 jobs run on `ubuntu-latest`.** That label moves to Ubuntu 26.04 between 2026-10-19 and 2026-11-19 (actions/runner-images#14748), and the issue's stated mitigation is the `ubuntu-24.04` label.
- **No DEV deploy has run since 2026-09-30.** Ten later commits to the deploy workflows, `warm-site.sh` and the Bicep have never run on a hosted runner.

## Doing

1. **Pin every external action** to the commit it runs, with its version as a trailing comment (`@<sha> # vX.Y.Z`). The deploy workflows pin the Node 24 major from item 2; every other workflow pins what it runs today. The two local calls stay `./`.

   | Action | Pin |
   |---|---|
   | actions/checkout | `fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09` # v5.1.0 |
   | actions/setup-node | `a0853c24544627f65ddf259abe73b1d18a591444` # v5.0.0 |
   | actions/setup-dotnet | `26b0ec14cb23fa6904739307f278c14f94c95bf1` # v5.4.0 |
   | actions/setup-java | `b6effb05e454b25005698d916606bdc6ffcbf961` # v5.7.0 |
   | actions/cache | `caa296126883cff596d87d8935842f9db880ef25` # v5.1.0 |
   | actions/upload-artifact | `b7c566a772e6b6bfb58ed0dc250532a479d7789f` # v6.0.0 |
   | actions/download-artifact | `37930b1c2abaa49bbe596cd826c3c89aef350131` # v7.0.0 |
   | gradle/actions/setup-gradle | `0723195856401067f7a2779048b490ace7a47d7c` # v5.0.2 |
   | azure/login | `a641126d1b8aa4d1fa005f4f92df94a3a4c4c906` # v3.1.0 |
   | azure/webapps-deploy | `02a81bead70021f5284939794bcec79c271ab383` # v3.0.8 |
   | Azure/static-web-apps-deploy | `1a947af9992250f3bc2e68ad0754c0b0c11566c9` # v1 (the tag, not the branch head `4d27395`) |

2. **Move the deploy workflows onto Node 24 majors:**
   - checkout, setup-node, setup-dotnet and cache go from v4 to v5;
   - upload-artifact goes from v4 to v6 (its v5 still declares node20), and download-artifact from v4 to v7 (its v5 and v6 still declare node20). The pair moves together in `deploy-azure.yml`;
   - azure/login goes from v2 to v3.

   The ground truth checked each major's breaking changes against the inputs we pass, and none applies. webapps-deploy v3.0.8 already runs Node 24. static-web-apps-deploy is a Docker action, so it is only pinned.
3. **Set `node-version: 22`** at `deploy-pro.yml:61` and `deploy-docs.yml:18`.
4. **Run the SSR on `NODE|22-lts`** at `main.bicep:312`, and update `appService.bicep:21`'s description to match. This is the Node the bundle is built and tested on.
   - `NODE|24-lts` is also valid and is supported a year longer. But it would run the SSR on a Node it is neither built nor tested on.
   - The edit changes the provision fingerprint (`deploy-azure.yml:437-461`), so the next DEV deploy runs Bicep and restarts the SSR.
5. **Least privilege**, following the security review's plan:
   - **the seven PR workflows without a block:** a top-level `permissions: contents: read`;
   - **deploy-docs:** a top-level `contents: read`, and `repo_token` (`:40`) deleted. The action uses it only to comment on pull requests, and this workflow only runs on dispatch;
   - **deploy-dev:**
     - top level `contents: read`;
     - the `resolve` job `{}`;
     - the `deploy` job keeps `id-token: write` + `contents: read` (`:70-72`);
   - **deploy-pro:** top level `contents: read`, and the job blocks stay;
   - **execute-sql:** top level `contents: read`, and `id-token: write` + `contents: read` on `execute-dev` and `execute-pro` only;
   - **deploy-azure:**
     - a top-level `contents: read` as a backstop;
     - `deploy-customer-ssr` and `close-deploy-quiet-window` get `id-token: write` only;
     - `deploy-partner-spa` and `deploy-admin-spa` get `{}`;
     - the other job blocks stay.

   No check is renamed, because no `build` job gains `name:`, `strategy:`, `if:` or `uses:` (`docs/deployment/ci-cd.md:48-55`).
6. **Set `runs-on: ubuntu-24.04`** on all 25 Ubuntu jobs.
7. **Update every place that restates these facts:**
   - `ci-cd.md:129-131` and `:146-149` say no image is pinned (the end date there is 2026-11-19);
   - `ci-cd.md:317` holds the static-web-apps snippet, and `:453-461` the permissions and login snippet;
   - `deploy/AZURE-DEV-RUNBOOK.md:20` says Node 20, and `:138` says azure/login@v2;
   - `docs/architecture/overview.md:18` says Node.js 20;
   - the workflow comments that say `ubuntu-latest` (`android-ci.yml:54`, `backend-ci.yml:95`, `booking-policy-parity.yml:27`, `ios-symbols-ci.yml:10`), and `agents/tools/check-booking-policy-parity.mjs:26`.

   The ADRs and dated records that name azure/login@v2 stay as written. `docs/deployment/azure-setup.md:24` already says Node.js 22. Re-check the catalog's workflow line citations, such as `patterns-mobile.md:581` and `security-rules.md:650`. check-catalog-claims fails Docs CI only on a citation past the end of a file (`check-catalog-claims.mjs:806-811`).

**Validation:**
- The PR workflows run on this change's own head.
- `deploy-pro.yml` is never dispatched (CLAUDE.md). It gets a static review, plus a local run of its two legal-draft scripts on Node 22.
- execute-sql's DEV job is dispatched with the read-only `sql-scripts/check-db-health.sql`.
- A full DEV deploy is the only run that exercises download-artifact, webapps-deploy, the SPA deploys, the warm-up and the quiet window. That deploy and a deploy-docs dispatch are deployments, so each waits for Mike's explicit approval.

## NOT doing

- gradle/actions v6 (Mike: stay on v5). No move past the first Node 24 major either: no checkout v7, setup-node v7 and so on.
- These go to T-0849:
  - Dependabot's github-actions ecosystem for these pins;
  - static-web-apps-deploy's `skip_api_build` input and its tag/branch ambiguity;
  - Ubuntu 26.04;
  - the unpinned `dotnet-ef`.
- SEC-CI-1 to SEC-CI-6 (T-0848). This ticket's blocks close the workflow half of SEC-CI-6; the repository's default token setting stays with T-0848.
- Deleting execute-sql's two `azure/login` steps, which the security review prefers because no later step calls `az`. It would change what the PRO job runs. Reported for Mike.
- `persist-credentials: false` on checkouts, and moving the input expansion at `deploy-dev.yml:60-61` into an env var. Both are optional hygiene from the same review. Reported for Mike.
- ios-ci's `macos-latest`, the required checks and rulesets, and the other unpinned tools inside `run:` steps.

## Acceptance criteria (done looks like)

- [x] **AC1** — Every external `uses:` is a 40-character SHA with its version comment, matching the table. `grep -nE 'uses: [^.].*@v[0-9]' .github/workflows/*.yml` prints nothing.
- [x] **AC2** — Every workflow declares top-level permissions, and the job blocks are as listed. The names of the required `build` checks are unchanged.
- [x] **AC3** — No `node-version: 20`, `NODE|20-lts` or `ubuntu-latest` remains under `.github/workflows/` or `deploy/bicep/`. The listed docs and comments say what the files do.
- [x] **AC4** — The PR workflows pass on the exact head, with no "Node.js 20" annotation and no new warning.
- [ ] **AC5** — Validation is done or recorded:
  - deploy-pro has had its static review, and its two scripts pass on Node 22;
  - execute-sql's DEV job passes the read-only health check;
  - the DEV deploy and the deploy-docs run pass on Mike's approval, or are recorded here as owed.

## Work record

- 2026-10-12 — Filed from the CI ground truth and the CI security review of 2026-10-12. Mike: "Fix" and "Fix it" (2026-10-12); gradle/actions stays on v5.
- 2026-10-12 — Implemented on `fix/post-e-p7-ci-hardening`, one commit per item, except that item 7's comments rode with items 2 and 6:
  - `d006726` (item 1) pins every remote action;
  - `a8d6fd4` (item 2) moves the deploy workflows to the Node 24 majors;
  - `f79cfa2` (item 3) sets Node 22;
  - `c90e7c4` (item 5) applies least privilege;
  - `fce8eef` (item 4) runs the SSR on `NODE|22-lts`;
  - `1efe682` (item 6) pins every Ubuntu job to `ubuntu-24.04`;
  - `bb4b8c7` (item 7) updates the docs. The five `ubuntu-latest` comments went in with `1efe682`, and deploy-azure.yml's header comment with `a8d6fd4`;
  - after review, `9b7c6ab` drops deploy-docs' `repo_token`, and `bf383ed` corrects one docs sentence.
  - **Static validation; no workflow was dispatched and nothing was deployed.** A validator script in scratch (`p5/p7-ci-hardening/scripts/` and `p5/p7-ci-fixpass/` in the off-tree Wave F folder) passes 4,646 checks at the head, with 0 failures:
    - all 13 workflows parse;
    - the 67 remote `uses:` lines carry 11 distinct SHAs, each the peeled commit of the tag in its comment, from a fresh `git ls-remote` on 2026-10-12;
    - no input a step passes was removed or renamed across any major bump (each `action.yml` compared at both commits);
    - every job with steps holds `id-token: write` only if it runs azure/login, and `contents: read` only if it checks out. The two calls into deploy-azure, deploy-dev's and deploy-pro's `deploy`, hold both, as the ceiling for its jobs;
    - no `node-version: 20`, `NODE|20-lts` or `ubuntu-latest` remains (25 jobs on `ubuntu-24.04`, one on `macos-latest`), and the four `build` jobs are unchanged.
  - `bicep build deploy/bicep/main.bicep` and `build-params` for both stages exit 0. The ARM output differs only in the SSR runtime, the module description and the template hashes.
  - deploy-pro had a static review. On Node 22, `check-legal-drafts.test.mjs` passes 19/19, and the gate itself exits 1 by design, because all 35 seeded texts are drafts.
  - At the head, check-docs-refs finds 0 unresolved, and check-catalog-claims and booking-policy parity are OK. The docs build passed at `bb4b8c7`, and was not rerun after `bf383ed`'s one-word change; Docs CI on the PR head builds it.
  - azure/login is pinned to v3.1.0, the newest v3 and what `@v3` resolves to today, not the literal first Node 24 release, v3.0.0.
  - Before push, five commit messages were corrected by plumbing, with every tree unchanged:
    - `d006726`: the inputs in static-web-apps' warning, and why its comment is `# v1`;
    - `a8d6fd4`: where the Node 20 warning was observed;
    - `c90e7c4`: when the default token applies, which jobs the old grants reached, which step needs a token scope, which token a same-run artifact download uses, and its closing sentence;
    - `9b7c6ab`: that it ends only the explicit hand-off of the token, what the static-web-apps container is still given (the workspace's read-only token), and what the permissions commit said about `repo_token`;
    - the filing commit: which branches carry it, and where S8 cites T-0837.
- **Owed:**
  - **The execute-sql DEV health check.** It is read-only and not a deployment. This program did not run it, because it ran nothing against DEV.
  - **Waiting for Mike's explicit approval, because each is a deployment:**
    - a full DEV deploy, the only run that exercises deploy-dev's and deploy-azure's changes and the SSR's `NODE|22-lts`;
    - a deploy-docs dispatch, the only run of deploy-docs' changes.
  - deploy-pro's changes, deploy-azure's production-only steps and execute-sql's execute-pro job run on none of these before production. They have the static review only.

  No DEV deploy has run since 2026-09-30, so the first one also carries the ten deploy-pipeline commits made since then.
- 2026-10-13 — Shipped in [#326](https://github.com/VM-s-Solutions/cleansia/pull/326).
  - Exact-head CI on `3a0582b` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan.
  - Every job ran this PR's own definitions: pinned actions, least-privilege permissions and `ubuntu-24.04`.
  - Merged normally with the SHA guard as `52f1049`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge.
  - Evidence: `raw/ci/p7-pr326-ci-merge-closure-001.json` (sha256 `abec0bec…`) in the off-tree Wave F folder.
  - **AC4.**
    - No annotation names Node.js 20.
    - The `ubuntu-latest` migration notice is gone from every Ubuntu job; #323's run carried it.
    - No Ubuntu job gained an annotation.
    - The iOS job shows GitHub's macOS capacity notice and a different capped sample of the same compiler warnings. Both runs used Xcode 26.6 (17F113), and both logs carry the `nonisolated(unsafe)` warning 12 times.
  - T-0838 stays in progress for AC5: the execute-sql DEV check, a DEV deploy and a deploy-docs dispatch.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “You can reset db, but once we're done with everything and we can do a fresh deployment”; “Analyze and Fix those”; “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is FF-19–21; dated approval and END debt retained.
- FF-19–21 correct the current App Service tier/deployment parallelism, SPA/docs token names and Angular version descriptions. Existing workflow/CI closure is retained. Answer 1 already authorizes the single END reset, fresh DEV deployment and docs deployment after all phases merge with green master evidence. AC5 remains open for the read-only DEV health check and that procedure; approval is present and execution is still owed. No deploy or SQL workflow is dispatched by this documentation follow-up.
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

- The P6 follow-up for FF-19–21 deployment and Angular descriptions was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. AC5 and the END procedure remain open. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

### END actual execution and SSR blocker — 2026-10-15

The P6 publication subsection above is an earlier checkpoint. Its END-pending or unexecuted wording is historical and is superseded by this actual partial-execution record; original dated phase history and owner quotes remain unchanged.

The authorized DEV SQL pre-health, what-if and docs deployment passed (`38657842040`, `38658034721`, `38658881649`, each attempt 1). Reset and the sole current Initial application completed in cycle 1, but full deployment `38660355359` still failed: its three original API readiness failures recovered and their attempt-2 jobs passed; the remaining customer SSR Warm job timed out on both origins, and the later root/process diagnostics obtained no HTTP response. No seed-first exception or further mutation was admitted. Four seeds and post-seed final readiness remain unperformed. **AC5 remains unchecked/unmet; INDEX is blocked on Q-END-01** for redacted SSR startup/request and worker evidence. Earlier P6 and original phase “no deploy/reset” statements remain historical checkpoint facts. No PRO runtime, alert muting or complete RBAC-preview claim is added; the what-if `NestedDeploymentShortCircuited` roleAssignments diagnostic is retained.

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

#### Answered diagnostic question retained as history

## Q-END-01 — DEV customer SSR: first error and worker status after API recovery

**Raised by:** the final DEV procedure, 2026-10-15. **Who answers:** the owner, with the DEV
App Service diagnostics.
**Why it needs you:** reset cycle 1 passed. Deployment `38660355359`, attempt 2, ended with
15 successful jobs and one failed job; the three API jobs retried in that attempt passed. The
remaining failure is `Warm dev sites`, job `116199476676`, completed at **02:00:41 UTC on
2026-10-15**: both `https://web-cleansia-customer-weu-dev.azurewebsites.net/` and
`https://customer.dev.cleansia.cz/` returned no response in 30 attempts each (60 total,
HTTP `000`, curl exit `28`, zero-byte bodies). After attempt 2, the SSR HTTP diagnostics and
worker-process diagnostic each timed out after 20 seconds. The cause remains **unestablished**.
**Answer needed:** provide the first redacted DEV customer SSR startup or request error, together
with whether its worker was listening and its exit/restart status, for **00:49–02:02 UTC on
2026-10-15**, covering requests to both origins above. This evidence determines the next fix;
the recovered API warm failures are retained as history and are not the current blocker.
**Blocks:** T-0838 AC5, T-0844 Doing 6 / AC6, and T-0849 AC1. The four seed scripts have never
run, the published admin was not recreated, and no post-seed health or final readiness result is claimed.

The preceding question describes the earlier blocked checkpoint. Mike’s replies and the current obligations above supersede its open/never-run statements.

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

### PR #339 publication and read-only inspection blocker — 2026-10-15

The prior transport-publication-pending statements are dated checkpoints. [PR #339](https://github.com/VM-s-Solutions/cleansia/pull/339) merged `8403c48c63dceae066f09b64a0ff63dfd0ec183c` from reviewed head `476d0ac6f16f66e517bcd3b9022f80d8834eeae8`, retaining tree `d99a83ef61bdb2cb4f288a34f4d6204b24a2cbb3`. Exact-head PR CI passed six jobs/five workflows; exact-merge master CI passed four jobs (three repository push workflows plus one automatic Dependabot dynamic run). All were attempt 1/no reruns. PR retained 11 warning annotations and one verified Playwright archive; master retained 10 warnings plus one Dependabot Ubuntu-label notice, zero artifacts and no Frontend run/e2e skip. Both retained zero failure annotations. The approved final local seal records 8,746 source inputs unchanged and 17 actual commands: 16 successes plus one preserved Docs harness failure; prior qualified full application acceptance remains applicable.

One GET-only inspection dispatch initially had an unresolved identity; without reposting, GET reconciliation identified `38797931482` / attempt 1 on that master. All six jobs failed with partial JSON; six ZIPs/JSON reports were retained with digest/CRC checks. The comparator blocks all six apps as `BLOCKED_PENDING_OR_UNVERIFIED`: history/active archive/pointer stability remain unverified and no upload is justified. A minimal diagnostic source follow-up is required before another state assessment; no Azure cause, upload retry, recovery or final readiness is established. [T-0636](T-0636-deployment-startup.md) records the actual request outcomes, local/CI qualifications and immutable evidence pins.

The applicable END criterion remains unmet, INDEX remains `in_progress` and MS-2 stays Open. Original reset/docs/deployment attempts and successful bootstrap `38702440461`, fixtures `38702856219` and admin `38703413453` remain history; successful seeds are not replayed. Failed promo `38703639106` still awaits its sole corrected-script retry after the runtime dependency is resolved; final DB health and 13 HTTP observations are unperformed. No second reset, docs redispatch, storage cleanup, PRO or graphify action is added. `MustChangePassword=false`/no authenticated console exercise and the existing provider-warning/no-muting-proof limits remain retained.

## Review

- **Correctness: changes requested, then fixed.**
  - B1: deploy-docs still passed `repo_token`. `9b7c6ab` drops it.
  - B2: `ci-cd.md` said no Azure credential is stored. `bf383ed` says no Azure client secret.
  - The message corrections above.
  - Everything else passed on the reviewer's own runs, including 67 of 67 pins re-resolved.
- **Security: pass**, on `bb4b8c7`'s tree, before the two review fixes; it had itself recommended one of them, dropping `repo_token`. Every pin is a commit on an upstream tag. No permission is broader than master or the plan. No secret is newly exposed, and no trigger changed.
- **Reported, not fixed:**
  - Nothing in CI guards the pins, the permissions or the runner label. T-0849 carries Dependabot only.
  - The living docs' workflow line citations were already off at base, and this branch shifts them by three lines (for example `security-rules.md:408` and `:650`, which cite `backend-ci.yml:69-71` and `:69-74`, the NuGet cache step at base).
  - Pre-existing docs drift:
    - `deploy/AZURE-DEV-RUNBOOK.md:7` names the agents as authors, against CLAUDE.md;
    - `ci-cd.md` still describes the B1 plan tier and sequential deploys;
    - its Required Secrets table names the static-web-apps tokens with a stage suffix that `deploy-azure.yml` does not read;
    - `azure-setup.md` says B1, and `overview.md` says Angular 19.
  - execute-sql's two azure/login steps sign in to Azure, but no later step calls `az`. Deleting them is listed under NOT doing for Mike.
  - deploy-docs still hands the run's token, now read-only, to checkout and setup-node through their defaults. Checkout leaves it in the workspace's `.git/config`, which the static-web-apps container mounts. `persist-credentials: false` is under NOT doing for Mike.
  - SEC-CI-1 to SEC-CI-5, and SEC-CI-6's repository default-token setting, are T-0848. This branch's permission blocks close SEC-CI-6's workflow half.
