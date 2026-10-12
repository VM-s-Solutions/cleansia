---
id: T-0848
title: Close the CI security findings SEC-CI-1 to SEC-CI-6 before production holds a secret — most of the work is in GitHub and Azure settings
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ci, docs]
security_touching: true
---

## Context

These findings come from the CI security review of 2026-10-12. The review is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/security_ci.json`. Its evidence is under `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/security-ci-perms/`: `README.txt`, `github-api/env-prod-weu.json`, `github-api/envs.json`, `github-api/control-repos/` and `swa/`.

The review passed forks, `pull_request_target` and token use. Its two HIGH findings sit outside the YAML.

Production does not exist yet, so all six findings are latent. SEC-CI-1 and SEC-CI-2 must be closed before production holds a secret or a resource. Workflow lines are as of `76e1638a3`.

| Finding | Severity | Who acts |
|---|---|---|
| SEC-CI-1: `prod-weu` has no approval gate | HIGH | Mike: repository settings |
| SEC-CI-2: one Azure identity for DEV and PRO | HIGH | Mike: Azure. The runbook is repo work |
| SEC-CI-3: deploy artifacts are not tied to their builder | MEDIUM | repo: `deploy-azure.yml` |
| SEC-CI-4: the deploy restores a shared NuGet cache | MEDIUM | repo: `deploy-azure.yml` |
| SEC-CI-5: tool downloads are not checked | LOW | repo: `ios-ci.yml` (the `dotnet-ef` pin is in T-0849) |
| SEC-CI-6: the repository's default token | LOW | Mike: repository settings (T-0838 closes the workflow half) |

**SEC-CI-1: `prod-weu` has no approval gate.**
- **Evidence.**
  - An anonymous `GET /repos/VM-s-Solutions/cleansia/environments` on 2026-10-12 returned `protection_rules: []` and `deployment_branch_policy: null` for `prod-weu`.
  - Nothing has changed since the environment was created (2026-09-07T21:26:37Z).
  - `dev-weu` and an unused `production` environment look the same.
  - The same call shows `required_reviewers` for three other public repos, so the empty list is real, not hidden.
- **What it contradicts:** `deploy-pro.yml:9-12`, `deploy-azure.yml:24-26`, `docs/deployment/ci-cd.md:165`, `:358`, `:384`, and the runbook's go-live steps (`deploy/AZURE-DEV-RUNBOOK.md:184-186`, `:567-568`, `:582-587`).
- **How it can be used.** Anyone with write access, including the owner token that agents use, has two routes:
  - start the production pipeline with no approval pause: `gh workflow run deploy-pro.yml -f mode=deploy`;
  - push a branch whose workflow names `prod-weu`, and dispatch it from there. That releases every `prod-weu` secret and a production login.
- **execute-sql's PRO job is exposed the same way.** Its only gate is the typed "execute" (`execute-sql.yml:31-35`), and its fixture guards (`:52-93`) can be edited out on a branch.
- **Fix (Mike, Settings → Environments → `prod-weu`):**
  - add required reviewers;
  - limit deployment branches to `master`;
  - consider turning off admin bypass;
  - delete `production`, or confirm it holds nothing;
  - confirm that `prod-weu` holds no secret today.
- **Limit of the fix.** A required reviewer whose token agents also hold can approve through the API. Closing that needs an agent token that cannot approve deployments, which is Mike's decision.

**SEC-CI-2: one Azure identity serves DEV and PRO.** This is judged from the runbook; the real role assignments were not checked.
- **What the runbook sets up.**
  - It creates one app, `cleansia-github-oidc` (`AZURE-DEV-RUNBOOK.md:143`), and links it to `environment:dev-weu` (`:149-155`).
  - It grants that app Contributor and User Access Administrator on the whole subscription (`:160-163`).
  - Go-live step P2 links the same app to `prod-weu` (`:589-597`).
  - Step P3 puts production's resource group in the same subscription (`:600-603`).
  - `:612` sets production's `AZURE_CLIENT_ID` to the same app.
- `deploy-azure.yml:607-615` already uses those rights to grant itself Key Vault Secrets Officer.
- **How it can be used.**
  - `dev-weu` is open by design (`:183`). So a DEV deploy, or any branch workflow naming `dev-weu`, gets a token that can assign Owner anywhere in the subscription.
  - Once production lives there, that reaches every production secret and the production database.
  - It also gets round SEC-CI-1's gate, even once that gate is fixed.
- **Fix (Mike, in Azure; the runbook edits are repo work):**
  - one app per stage, each linked only to its own environment;
  - rights scoped to that stage's resource group. That is enough: `main.bicep:17` targets a resource group, and provider registration is already manual (`deploy-azure.yml:361`);
  - ideally, a separate subscription for production.

  Anything that touches a production resource is Mike's alone (CLAUDE.md).

**SEC-CI-3: the deploy runs files that nothing ties to their builder.**
- **The chain.**
  - The EF migrations bundle is uploaded at `deploy-azure.yml:194-199`, downloaded by name at `:726-730` and run at `:877-878`. It runs holding the database admin connection string and the Azure login (`:754`, `:824`).
  - `build-angular` runs at the same time (`:278`) and executes `npm ci` install scripts (`:221-223`). Code there can delete and re-upload any artifact in the run by name.
  - The API packages (`:145-173`, deployed at `:981-994`) are exposed the same way.
- **Result:** one malicious npm release would get SEC-CI-2's Azure session.
- **Fix:**
  - each upload's `artifact-id`, or a sha256, becomes a build-job output;
  - the consumer downloads by `artifact-ids:` (download-artifact v4.3.0 and v7.0.0 both support it), or checks `sha256sum -c` before `chmod +x`;
  - either way, the deploy fails closed if the file was swapped.

**SEC-CI-4: the deploy restores a NuGet cache that any master run can write.**
- `deploy-azure.yml:97-103` restores `~/.nuget/packages`, falling back to the newest `nuget-Linux-` cache.
- `backend-ci.yml:69-75` writes the same path, with the same key shape.
- Code in any master-push job can plant that key first. NuGet trusts its global packages folder, so the APIs and the migrations bundle are built from whatever is there.
- Not affected: npm's cache is integrity-checked by `npm ci`, and the Nx cache step (`:228-234`) does nothing at all.
- **Fix:** add `if: inputs.env != 'prod'` to the NuGet cache step, or drop the step, since deploys are manual. The dead Nx cache step can go with it.

**SEC-CI-5: some tool downloads are not checked.**
- SwiftFormat (`ios-ci.yml:113-114`) and SwiftLint (`:132-133`) are checked only for their version (`:167`, `:174`). openapi-generator (`:88-91`) and gitleaks (`secret-scan.yml:43-45`) are checked by sha256.
- `brew install xcodegen xcbeautify` (`:75`) is unpinned.
- None of these steps has access to secrets.
- `dotnet-ef`, unpinned at `deploy-azure.yml:179-181`, gets its pin in T-0849.
- **Fix:** sha256 checks for the two downloads, and pinned versions of xcodegen and xcbeautify.

**SEC-CI-6: the default token.**
- Eight workflows have no `permissions:` block, so they get the repository's default token. That setting cannot be read without admin rights.
- If it is "write", every master push hands a write token to npm, Gradle, NuGet and brew code, and checkout saves it in `.git/config`.
- T-0838's blocks make every workflow read-only regardless.
- **Fix (Mike, Settings → Actions → General → Workflow permissions):** set the default to read-only.

## Doing (proposed, for Mike)

- **Mike, in settings:** SEC-CI-1 and SEC-CI-6, and SEC-CI-2's identity split in Azure.
- **In the repo, after T-0838,** which edits the same files:
  - the runbook's P1 to P3 rewritten for one identity per stage;
  - SEC-CI-3's artifact check;
  - SEC-CI-4's cache condition, and removal of the dead Nx step;
  - SEC-CI-5's checksums and pins.
- Only a full DEV deploy exercises the `deploy-azure.yml` changes, and that needs Mike's explicit approval.

## NOT doing

- Nothing runs against production (CLAUDE.md).
- The real Azure role assignments are not read; SEC-CI-2 is judged from the runbook.
- The permissions blocks and action pins (T-0838), and Dependabot and the `dotnet-ef` pin (T-0849).

## Acceptance criteria (done looks like)

- [ ] **AC1** — An anonymous read of the environments API shows `prod-weu` with required reviewers and a `master`-only branch policy. `production` is gone, or confirmed empty.
- [ ] **AC2** — DEV and PRO each have their own Azure identity, scoped to their own resource group, and the runbook says so.
- [ ] **AC3** — The deploy runs the migrations bundle and the API packages only after checking they are the files the build made. A swapped artifact fails the run.
- [ ] **AC4** — The production deploy does not restore the shared NuGet cache. The SwiftFormat and SwiftLint downloads are checked against a checksum, and xcodegen and xcbeautify are pinned.
- [ ] **AC5** — The repository's default workflow token is read-only.

## Work record

- 2026-10-12 — Filed from the CI security review of 2026-10-12, which filed no tickets itself. Reported, not absorbed (CLAUDE.md §2): Mike decides what becomes work. SEC-CI-1, SEC-CI-2 and SEC-CI-6 are his to act on.

## Review

Pending.
