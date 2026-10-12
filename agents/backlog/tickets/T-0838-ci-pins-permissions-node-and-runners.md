---
id: T-0838
title: Harden the workflows — actions pinned to commits, least-privilege tokens, Node 24 actions and Node 22 in the deploys, the SSR on Node 22, and Ubuntu pinned to 24.04
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0820]
blocks: [T-0849]
stories: []
adrs: []
layers: [ci, bicep, docs]
security_touching: true
---

## Context

T-0820's review reported three things for Mike: the mutable action tags, the missing `permissions:` blocks and the deploy workflows' Node 20.
- The CI ground truth of 2026-10-12 confirmed each one against `b405e9cc5` and resolved every ref.
  - The ground truth itself is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_ci.json`.
  - The inventory is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/ci-inventory.json`.
  - Tag lists, `action.yml` at each SHA and the release notes are under `p5/ci-inventory/`.
- The CI security review of the same day checked the pins and wrote the permissions plan. It is in the same Wave F folder, `security_ci.json`, with its evidence in `p5/security-ci-perms/`.

**Mike, 2026-10-12:**
- **"Fix"** for the SHA pins, least privilege, the deploy workflows' v4 actions, the two Node 20 pins and `main.bicep`'s `NODE|20-lts`.
- **"Fix it"** for `ubuntu-latest` moving to Ubuntu 26. This also answers the pin question in T-0813's runtime packet.
- gradle/actions stays on v5.

At `76e1638a3`:
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
