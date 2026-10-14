---
id: T-0848
title: Close the CI security findings SEC-CI-1 to SEC-CI-6 before production holds a secret — most of the work is in GitHub and Azure settings
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ci, docs]
security_touching: true
---

## Context

The following is the 2026-10-12 finding record. The current ruling and evidence are dated below.

These findings come from the CI security review of 2026-10-12. The review is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/security_ci.json`. Its evidence is under `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/security-ci-perms/`: `README.txt`, `github-api/env-prod-weu.json`, `github-api/envs.json`, `github-api/control-repos/` and `swa/`.

The review passed forks, `pull_request_target` and token use. Its two HIGH findings sit outside the YAML.

Production does not exist yet, so all six findings are latent. SEC-CI-1 and SEC-CI-2 must be closed before production holds a secret or a resource. Workflow lines are as of `207d2f319`.

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

- [x] **AC1** — An anonymous read of the environments API shows `prod-weu` with required reviewers and a `master`-only branch policy. `production` is gone, or confirmed empty.
- [ ] **AC2** — DEV and PRO each have their own Azure identity, scoped to their own resource group, and the runbook says so.
- [ ] **AC3** — The deploy runs the migrations bundle and the API packages only after checking they are the files the build made. A swapped artifact fails the run.
- [ ] **AC4** — The production deploy does not restore the shared NuGet cache. The SwiftFormat and SwiftLint downloads are checked against a checksum, and xcodegen and xcbeautify are pinned.
- [x] **AC5** — The repository's default workflow token is read-only.

## Work record

- 2026-10-12 — Filed from the CI security review of 2026-10-12, which filed no tickets itself. Reported, not absorbed (CLAUDE.md §2): Mike decides what becomes work. SEC-CI-1, SEC-CI-2 and SEC-CI-6 are his to act on.

### Owner ruling — 2026-10-13

Mike: **"Go with defaults (recommended)"**. Q9(a) in [decisions-2026-10-13.md](/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md):

> prod-weu: required reviewer MichaelChaban (user id 76599514), `prevent_self_review: false`, `can_admins_bypass: false`, deployment branch policy `master` only. Delete the empty `production` environment. Verify with an anonymous GET.

Q9(b/c):

> require actions pinned to full commit SHAs (`sha_pinning_required`).
> delete the 8 stale repository-level secrets: AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID, ACR_NAME, DB_CONNECTION_STRING, DB_CONNECTION_STRING_DEV, AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_DEV and AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_DEV.
> - KEEP AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV, which deploy-docs reads.
> - Re-verify at the time with a GET of the secrets list and a grep of `.github/workflows`. Every DEV job reads its own `dev-weu` copies. The only job that would fall back to the repository-level Azure trio is the PRO job execute-pro, which must get its own at go-live.

Q11 keeps actual Azure narrowing at owner go-live: **"narrow it at go-live. No action now."**
The rewritten runbook is target setup only; no Azure identity or role assignment was inspected or
changed. SEC-CI-2 / AC2 remain open for that owner work.

Q7 also authorizes the CI items:

> `execute-sql`'s two unused azure/login steps and their id-token grants;
> `persist-credentials: false` on every checkout;
> `deploy-dev.yml:60-61`'s input passed through env;
> `execute-sql`'s `PGSSLMODE=verify-full`, falling back to `require` if DEV refuses it, and recording why;

### P4 work — 2026-10-14; publication pending

The reviewed 30-path candidate is `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/candidate-source-001.json`. Its source manifest file
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/candidate-source-manifest-001.json` has actual file SHA-256
`595c06c5af936c80bf52138a78b4b74c64f9394aef5693adcf1016de7b846b01`.
The candidate report's `fe54d642…` hash is the canonical JSON payload without its trailing LF,
not the hash of the saved manifest file. `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/actual-base-rebind-001.json` confirms every candidate
preimage and frozen afterimage against actual base `a667f62b1c7157c5ca0104837dbe8e6c4dc45045`.
The subsequent native closure is the first P4 commit, `0a2b9e6c9f5a3b3c13c2b98b2957b9d332e9e173`;
the dedicated Nx correction follows as `8c2e8b461c04e786a79db174ad29828da0d29a3d`.

- **SEC-CI-1 / AC1 complete:** fresh after-GET and anonymous GET show the exact `prod-weu` reviewer,
  self-review and administrator-bypass values; a separate branch-policy GET shows only branch
  `master`. `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/settings-actual-001/prod-gate-receipt-001.json` has SHA-256
  `3dbf03a94af36737277474753629c7e039f65c08d41caef0d6711125173ddc50`.
  The unused `production` configuration was deleted after fresh checks found zero secrets, zero
  variables and zero current workflow readers. Its history contained one failed 2026-04-01 GitHub
  deployment (`production_environment: false`), not an empty history. After deletion its object's
  only changed field was `updated_at` and the statuses endpoint returned one `inactive` status;
  prior error evidence is retained off-tree, with continued server retention unverified.
  Authenticated and anonymous environment reads returned 404. Authoritative deletion receipt:
  `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/settings-actual-001/production-deletion-receipt-006.json`, SHA-256
  `23f9ded7b1e1b183179ca7e1cb23b790cbb5c78bb68ab1b747b42216d0d4f7b0`.
- **SEC-CI-2 / AC2 open:** one identity per stage and group-scoped grants are documented as target
  instructions. Actual DEV narrowing remains owner go-live under Q11; no Azure check ran.
- **SEC-CI-3 / AC3 source proof:** six upload artifact IDs become `build-dotnet` outputs; the EF
  consumer and five API consumers select the corresponding single ID. Source controls reject a
  wrong producer mapping, then restore GREEN. Actual download IDs, Linux bundle execution and
  migration/deploy outcomes await END DEV proof. The pinned download action falls back to all
  artifacts for a blank ID, warns for partially missing multiple IDs and warns on digest mismatch;
  the single nonempty missing-ID path fails. The current mappings do not establish a blanket
  fail-closed guarantee. AC3 stays unchecked.
- **SEC-CI-4/5 / AC4 source proof:** both-stage deploy NuGet and dead Nx cache steps are removed;
  SwiftFormat 0.60.1, SwiftLint 0.65.0, XcodeGen 2.46.0 and xcbeautify 3.2.1 have SHA-256 checks
  before extraction/installation. Authentic installations and one-byte archive tamper controls
  passed; each tamper exits 1 before the affected install. PRO coverage is source proof only,
  with no execution. AC4 stays unchecked pending publication and applicable final gates.
- **SEC-CI-6 / AC5 complete, read-only:** fresh `GET actions/permissions/workflow` returned
  `default_workflow_permissions: read` and `can_approve_pull_request_reviews: false`. It was
  already closed; no PUT ran. Receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/settings-actual-001/workflow-permissions-actual-001.json`
  has SHA-256 `a478bb51ea2a568fdac1910841fb633a4c57387b6e78a0068bf62266031ceff4`.
- All 21 checkout steps set `persist-credentials: false`; the two unused SQL Azure logins and
  two id-token grants are removed. DEV and PRO psql command tails inherited step-level
  `PGSSLMODE=verify-full` and the trusted-root path in local shim checks with no connection
  capability. Missing-root controls exit 1 before continuing. Actual DEV certificate-chain and
  hostname validation await END; no `require` fallback was taken and no PRO SQL ran.
- Q9(b/c) actual settings: `sha_pinning_required: true`; the eight authorized repository secrets
  were deleted sequentially after a fresh names/metadata GET and hashed workflow-reader check
  before each deletion, with fresh after-GETs. Only repository secret
  `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV` remains; all 32 `dev-weu` names/metadata are unchanged.
  No secret values were read. Aggregate receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/settings-actual-001/final-receipt-001.json`
  has SHA-256 `136e200da49afe76618eed62358e2388347bf611da8181c557b03035425d937f`.
  The eight exact names and individual before/after evidence are in the referenced secrets receipt.
- No changelog entry is owed: these are CI, tooling and operational documentation changes.
  No DEV dispatch/reset or PRO operation ran. END proof is authorized only after every phase
  has merged with green CI; it remains unperformed.

## Review

Pending.

### 2026-10-14 — source verdicts and combined candidate checks

The frozen source verdicts in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/review-001/verdict.json` and
`/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p4/security-001/verdict.json` are each **"APPROVED — SCOPED"**, with zero material findings.
They apply to the frozen 30-path candidate, with runtime/live-setting conditions; they are not merge
approval or approval of this later settings prose.

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
