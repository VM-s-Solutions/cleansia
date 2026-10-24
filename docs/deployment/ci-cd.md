# CI/CD Pipeline

Cleansia uses GitHub Actions. **The workflows below check a pull request** — one per stack, the docs,
the dependency-free Node checks whose drift spans trees no single stack job can see, and the secret
scan — and the rest deploy or run operational jobs. **The required statuses are `build`,
`References and build` and `No new secret in the history`**: [what they cover](#required-checks).

::: info Source Files
**Runs on a PR**

| Workflow | Guards |
|---|---|
| `backend-ci.yml` | the .NET solution — unit, integration (Testcontainers) and host tests. Also runs on pushes to `master`, because direct-to-master commits used to bypass it entirely. Scoped to `src/**` **and `sql-scripts/**`** |
| `frontend-ci.yml` | the Nx workspace — lint, test, build across the three apps |
| `android-ci.yml` | the Gradle multi-module build |
| `ios-ci.yml` | SwiftFormat, then SwiftLint, then three test schemes |
| `docs-ci.yml` | both halves of the reference contract: two checkers with their own self-tests blocking first, then `vitepress build` with `ignoreDeadLinks: false` |
| `ios-symbols-ci.yml` | the compiler-free half of the iOS gate — `check-ios-symbols.mjs` on Linux |
| `booking-policy-parity.yml` | every client's stated booking figures against `BookingPolicy`, and the legal seed's currency placeholders |
| `secret-scan.yml` | gitleaks over every commit reachable from the head — [below](#secret-scan) |

**Deploy and operational**

- `deploy-dev.yml`, `deploy-pro.yml`, `deploy-azure.yml`, `deploy-docs.yml`
- `execute-sql.yml` — the manual, environment-gated SQL runner
:::

## What a pull request needs to merge {#required-checks}

Ruleset **6739260**, applied and read back on **2026-10-14** under the owner's 2026-10-13 ruling
(T-0813), requires **`build`**, Docs CI's **`References and build`** and Secret scan's
**`No new secret in the history`**. Only **`build`** is pinned to GitHub Actions
(`integration_id: 15368`); the other two contexts have no integration pin.

The repository rulesets *master* and *Pull request rule* protect `master`, and there is no classic
branch protection. They apply only to `master`, so a pull request into another branch (a stacked one)
has no required check. Ruleset *master* requires every review thread to be resolved, no approval, and
the three status checks above, strict: the branch must also be up to date with `master`. The
2026-10-14 update preserved strict checking, team 11814740's `always` bypass and
`require_extra_approval_for_unattributed_changes: true`, along with all other rules.

**`build` is a job name, not a workflow.** Every check called `build` on the head must report, and
these jobs report one:

- **Frontend** (`frontend-ci.yml`) and **Android** (`android-ci.yml`), on every pull request;
- **Backend** (`backend-ci.yml`), when the pull request touches `src/**` outside the web, Android and
  iOS trees, `sql-scripts/**` or the workflow;
- **iOS** (`ios-ci.yml`), when it touches `src/cleansia_ios/**`, `src/cleansia_android/openapi/**`,
  either of the two Android Kotlin sources or five locale XML files read by its cross-tree tests
  (listed below), or the workflow.

The list is every job keyed `build` with no `name:`, `strategy:`, `if:` or `uses:`, and its
membership test has two halves. `grep -nE '^  build:$' .github/workflows/*.yml` lists the jobs keyed
`build`, but a `build` job that gains a `name:`, `strategy:`, `if:` or `uses:` leaves the gate while
the grep still lists it. This check prints nothing while none of the four jobs has one:

```bash
for f in android backend frontend ios; do awk '/^  build:$/{f=1;next} f&&/^    steps:/{exit} f&&/^    (name|strategy|if|uses):/{print FILENAME": "NR}' .github/workflows/$f-ci.yml; done
```

Frontend's lint step is `continue-on-error`, so lint never fails its `build`.

- **A running `build` holds the merge.** Observed 2026-10-11 on PR #320: it stayed blocked while
  Backend's `build` ran, with Android's and Frontend's already green, and was mergeable at the next
  reading, 45 s after Backend finished.
- **A failed `build` beside a green one has not been observed.** Whether a red `build` still blocks
  when another `build` on the same head finishes green after it stays unknown until a pull request
  reaches that state on its own (T-0813). Its passive observation baseline is now the ruleset
  updated at `2026-10-14T15:34:25.285+02:00`, with these three contexts and only `build` integration-pinned.
  No push, re-run or pull request may be created to manufacture the qualifying state.

**Advisory checks** remain Frontend CI's *e2e-smoke*
on every pull request; iOS Symbols CI (*Symbols resolve and switches stay exhaustive*) and Booking
policy parity (*Every surface quotes the policy it actually enforces*) when their paths change.

**The gate changes when a workflow does.** A required check matches its context name, with a
GitHub Actions integration restriction for `build`, rather than its workflow, matrix or event:

- a `name:`, a matrix, or a move into a reusable workflow renames a job's check, and that workflow
  leaves the gate without any error;
- any other GitHub Actions check called `build` on the head joins the gate, from any workflow;
- renaming every `build` job leaves each pull request waiting for a check that never arrives, until
  someone bypasses the rule;
- a workflow that fails before its job starts reports no `build`, so the others can pass the gate
  without it (inferred, not observed).

Team *VM* can bypass both rulesets. That happened once in the month to 2026-10-11: PR #295 merged with
no `build` reported.

## Secret scan (`secret-scan.yml`) {#secret-scan}

Runs on every pull request, whatever its base branch, and on every push to `master`. It installs the
gitleaks **8.30.1** CLI, pinned by version and SHA-256 (the GitHub Action needs a paid licence on an
organisation repository), checks out the full history and runs:

```bash
./gitleaks git --config .gitleaks.toml --redact --no-banner --verbose --log-opts="HEAD" .
```

So a secret committed anywhere in a pull request fails the Secret scan check, even if a later commit deletes it again —
deleting a file does not un-publish it. `--redact` keeps a finding out of the job log.

| File | What it holds |
|---|---|
| `.gitleaks.toml` | gitleaks' default rules, plus two allow-lists that apply to the **generic API key rule only**: the test-code paths (the published test JWT key, sandbox-shaped Stripe ids) and dotted translation keys. The Stripe, SendGrid and AWS rules still fire everywhere |
| `.gitleaksignore` | the baseline — the fingerprints of what is already in history, in commented groups (two old SendGrid keys, an old Nx Cloud token, the published test JWT key in early host settings, false positives). Only a **new** finding fails |

A finding that is a real secret is revoked at its provider first; baselining it only stops the build
failing, it does not make the secret safe.

## Workflows Overview

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `backend-ci` | PR to any branch that touches `src/**` outside the web, Android and iOS trees, `sql-scripts/**` or the workflow | Build + test .NET solution |
| `frontend-ci` | PR to any branch | Build Angular apps |
| `android-ci` | PR to any branch | Compile and unit-test `:core`, `:partner-app` and `:customer-app` |
| `ios-ci` | PR to any branch that touches `src/cleansia_ios/**`, `src/cleansia_android/openapi/**`, its seven Android test inputs listed below, or the workflow | SwiftFormat, SwiftLint, then build and test the three schemes |
| `docs-ci` | PR to any branch | Docs pointers and written claims resolve; the docs site builds |
| `ios-symbols-ci` | PR to any branch that touches `src/cleansia_ios/**`, the checker, its self-test or the workflow | `check-ios-symbols.mjs` on Linux |
| `booking-policy-parity` | PR to any branch that touches a file the checker reads, the checker, its self-test or the workflow | `check-booking-policy-parity.mjs` |
| `secret-scan` | PR to any branch, push to `master` | gitleaks — no new secret in the history |
| `deploy-dev` | **Manual (`workflow_dispatch`)** | Deploy everything to DEV |
| `deploy-pro` | Manual (`workflow_dispatch`) | Deploy everything to PRO |
| `deploy-azure` | Called by `deploy-dev` and `deploy-pro` (`workflow_call`) | The shared build, provision, migrate and deploy pipeline |
| `deploy-docs` | Manual (`workflow_dispatch`) | Build the docs site and deploy it to DEV's Static Web App |
| `execute-sql` | Manual | Run ad-hoc SQL scripts. On PRO it refuses a development fixture by resolved path (`insert_seed_data.sql`, anything under `seed/` or outside `sql-scripts/`), and every DEV-only script by file name, from one list in its step *Refuse DEV-only scripts against production*: `insert_local_dev_admin.sql`, `fix-company-contact-placeholders.sql`, `fix-plus-trial-14-days.sql` and `fix-deactivate-local-dev-admin.sql`. A relative path cannot walk around a name, each refusal says where production does the same thing instead, and a new DEV-only script is one more row of the list (one list since 2026-10-04; the last two were not refused before). The three fixes correct what a DEV database got from a development fixture, which production is never built from. DEV runs all of them, `insert_local_dev_admin.sql` included (owner ruling 2026-09-30, until an app registration gates the DEV apps). It opens no network window, so it cannot reach the production database once that is private ([below](#production-window)) |

iOS CI also watches the exact Android inputs its tests read on pull requests and master pushes:

- `src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/profile/HelpSupportScreen.kt`
- `src/cleansia_android/core/src/main/java/cz/cleansia/core/config/CleansiaWeb.kt`
- `src/cleansia_android/customer-app/src/main/res/values/strings.xml`
- `src/cleansia_android/customer-app/src/main/res/values-cs/strings.xml`
- `src/cleansia_android/customer-app/src/main/res/values-sk/strings.xml`
- `src/cleansia_android/customer-app/src/main/res/values-uk/strings.xml`
- `src/cleansia_android/customer-app/src/main/res/values-ru/strings.xml`

As of 2026-10-11, every file in `.github/workflows/` has a row. The workflows that check a pull
request also run on pushes to `master`, path-scoped except for the secret scan.

## Runners and images {#runners}

All 25 Linux `runs-on` declarations request `ubuntu-26.04`. `ios-ci.yml` requests `macos-26` for its
required `build` job and `xcode-27` for its `xcode-27-readiness` job. Every job names an exact image
rather than a `-latest` label, and none uses a self-hosted or larger runner. A new workflow either joins
one of these labels or says why not.

- **Which image a label resolved to** is printed only in the job log's *Runner Image* group (`Image:`
  and `Version:`). The repository keeps logs for one day, so read it within 24 hours. The jobs API
  returns only the requested label.
- **iOS CI pins its Xcode.** `build` selects the Xcode whose `xcodebuild -version` reports the version in
  `src/cleansia_ios/.xcode-version`, 26.3, the Xcode that archives the TestFlight builds, and fails with
  an `::error::` naming the installed ones when the image has none. It matches the reported version, not
  the app's name, because an image may name an app by its release stage. `macos-26` ships it as
  `/Applications/Xcode_26.3.app` (17C529), beside 26.0.1 to 26.6, the image default; `macos-latest` moves
  to a new macOS and its Xcode set on GitHub's schedule, which is why `build` no longer uses it. The
  simulator is still the first available iPhone. The release lanes select the same Xcode, so moving the
  pin moves both. → [Optimizer-only crashes](/mobile-app/overview#optimizer-only-crashes)
- **The optimised passes take longer than the Debug ones they follow.** Measured on 2026-10-24 on the archiving
  Mac (Apple M4), in the job's order and one DerivedData: the partner suite took 67 s in Debug and 137 s
  optimised, the customer suite 118 s and 614 s, the latter with the machine shared by other builds
  (246 s in a quieter run). GitHub's runners are slower; the first runs show the real cost against the
  job's 120-minute ceiling.
- **`xcode-27` is a preview image** ([actions/runner-images#14404](https://github.com/actions/runner-images/issues/14404)):
  macOS 27, Xcode 27.0, 27.1 and a 27.2 beta, one iOS 27.0 simulator runtime, and no iPhone Duo device.
  `xcode-27-readiness` selects Xcode 27.1 the same way and generates, builds and runs the three Debug
  suites, as an early signal of what Swift 6.4 and the iOS 27.1 SDK break. It has `continue-on-error` and
  is not keyed `build`, so it never blocks a merge. It goes when `.xcode-version` moves to 27.1, which needs
  an archiving Mac on macOS Tahoe 26.6 or later, and `build` moves to an image that ships it.
- **Observed on 2026-10-11**, in runs 38098170687 and 38098170722 at `64dfed7`:
  - `ubuntu-latest` resolved to ubuntu-24.04, version 20261004.327.1;
  - `macos-latest` resolved to macos-26-arm64, version 20260907.0351.1, with Xcode 26.6 (17F113) and an iPhone 17 Pro simulator.

  A maintainer's Mac ran Xcode 26.3 that day, the Xcode that archives the TestFlight builds, which is the
  gap the pin closes; it had the same SwiftFormat, SwiftLint, xcodegen, xcbeautify and
  openapi-generator versions as CI. CI now pins XcodeGen **2.46.0** and xcbeautify
  **3.2.1**, alongside SwiftFormat **0.60.1**, SwiftLint **0.65.0** and openapi-generator **7.10.0**.
  All five downloads are SHA-256 checked before extraction or installation; the four macOS tools
  resolve from `$RUNNER_TEMP/pinned-tools`, with XcodeGen's sibling setting presets preserved.
- **Ubuntu 26.** `ubuntu-26.04` became generally available on 2026-09-17
  ([actions/runner-images#14747](https://github.com/actions/runner-images/issues/14747)). The explicit
  release label follows the owner's 2026-10-13 ruling (T-0849), independently of the `ubuntu-latest`
  rollout from 2026-10-19 to 2026-11-19
  ([actions/runner-images#14748](https://github.com/actions/runner-images/issues/14748)). The published
  26.04 image includes Docker 29, a PostgreSQL 18 client, Android command-line tools 20 and jq 1.8.
  The locked Playwright **1.62.1** supports Ubuntu 26.04 and its Chromium dependencies; actual browser
  installation and execution must still be proved by PR CI. The job log establishes the image assigned.
- **Runner proof.** All **eight Linux PR jobs** must pass on one exact head before merge. Master runs
  **seven Linux jobs** and intentionally skips the PR-only `e2e-smoke`. Deploy/SQL runtime proof
  follows in the approved end-of-wave DEV procedure, after all phase PRs merge; this is T-0849's
  recorded **merge-then-prove** reading. Its deploy half remains open until those receipts exist,
  including the expanded API matrix legs and actual runner-image logs. The two PRO-only declarations
  (`deploy-pro`'s `legal-texts` and `execute-sql`'s `execute-pro`) change in source only and remain
  unexecuted under the PRO ban. A runner-caused failure needs an observed scoped fix or revert,
  with the failure recorded; PR CI alone does not close the deploy half.
- **Required checks** are not a runner setting: [what a pull request needs to merge](#required-checks).
  → [What validates iOS, and where](/mobile-app/overview#ios-validation-tiers)

## Branch Strategy

```
feature/* ──PR──> master ──manual──> DEV
                    |
                    └──manual──> PRO
```

- **Feature branches** -- all development work
- **`master`** -- integration branch. **It does NOT auto-deploy.** The push trigger was removed on
  owner request 2026-07-17; a DEV deploy is a deliberate button press, like prod. `deploy-dev.yml`
  offers a `what-if` mode that previews the Bicep change without mutating anything.
- **PRO deployment** -- manual, gated by **required reviewers on the `prod-weu` GitHub Environment**.
  That protection is UI configuration rather than YAML — see the PROD section of
  `deploy/AZURE-DEV-RUNBOOK.md`.

## Backend CI (`backend-ci.yml`)

Runs on a pull request that touches `src/**` outside the web, Android and iOS trees, `sql-scripts/**`
or the workflow itself. Every step uses `working-directory: ./src` — the solution lives at
`src/Cleansia.Api.sln`, not the repo root.

```yaml
steps:
  - Setup .NET 10.x
  - Cache ~/.nuget/packages   # keyed on Directory.Packages.props + all csprojs
  - dotnet restore Cleansia.Api.sln
  - dotnet build Cleansia.Api.sln --configuration Release --no-restore
  # Three suites, single-threaded, fast-first, each run as
  #   dotnet test <project> --configuration Release --no-build --verbosity normal
  #     -- xUnit.parallelizeTestCollections=false
  - dotnet test Cleansia.Tests/Cleansia.Tests.csproj                        # unit
  - dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj  # Testcontainers Postgres
  - dotnet test Cleansia.HostTests/Cleansia.HostTests.csproj                # authz/isolation
```

::: tip Why the container-backed suites exist
`Cleansia.IntegrationTests` and `Cleansia.HostTests` spin a real PostgreSQL via Testcontainers.
They are what catch the multi-tenant / FK / migration / webhook bugs the SQLite-and-mocks unit tests
structurally cannot — for example the SQL-vs-C# equivalence pins on `OrderAvailability` and
`OrderVisibility`. All three suites — unit, integration and host — run with
`xUnit.parallelizeTestCollections=false`.

Migration setup keeps EF's `PendingModelChangesWarning` guard enabled. The existing
`SeededDatabaseHasNoOrphanTenantRowsTests` also migrates a fresh database through an unsuppressed
context before applying the seed, so relational model drift fails CI. A token-only snapshot change
that alters no relational schema is outside this guard's scope and needs no DDL. The migrator's
`MigrateAsync` path is unsuppressed too, but startup skips that path when there are no pending
migration ids; startup alone does not check relational drift on every boot.
:::

## Deploy to DEV (`deploy-dev.yml`)

Run by hand (`workflow_dispatch`); a push to `master` deploys nothing. It is a thin caller — the
pipeline itself lives in the reusable `deploy-azure.yml`, which `deploy-pro.yml` also calls. Nine
deployable components.

### Pipeline Stages

```
build-dotnet ──┬──> provision (Bicep) ──> migrate-database ──> deploy-partner-api
               │                                          ──> deploy-admin-api
               │                                          ──> deploy-customer-api
               │                                          ──> deploy-partner-mobile-api
               │                                          ──> deploy-customer-mobile-api
               │
               └──> build-and-deploy-functions

build-angular ─────> deploy-customer-ssr
              ─────> deploy-partner-spa
              ─────> deploy-admin-spa
```

### Job Details

#### 1. Build .NET APIs

The five **Publish … API** steps in `build-dotnet` publish separate API artifacts:

| Artifact | Project |
|----------|---------|
| `partner-api` | `Cleansia.Web.Partner/Cleansia.Web.Partner.csproj` |
| `admin-api` | `Cleansia.Web.Admin/Cleansia.Web.Admin.csproj` |
| `customer-api` | `Cleansia.Web.Customer/Cleansia.Web.Customer.csproj` |
| `partner-mobile-api` | `Cleansia.Web.Mobile.Partner/Cleansia.Web.Mobile.Partner.csproj` |
| `customer-mobile-api` | `Cleansia.Web.Mobile.Customer/Cleansia.Web.Mobile.Customer.csproj` |

#### 2. Build Angular Apps

Builds three Angular apps using Nx:

| Artifact | Nx Project | Configuration |
|----------|-----------|---------------|
| `customer-app` | `cleansia.app` | `staging` (SSR) |
| `partner-app` | `cleansia-partner.app` | `staging` |
| `admin-app` | `cleansia-admin.app` | `staging` |

The Customer app includes SSR with a generated `package.json` for Node.js startup.

#### 3. Database Migration

`build-dotnet` installs **dotnet-ef 10.0.3** and builds the EF Core migrations bundle in its
**Build EF migrations bundle** step, only in deploy mode. Its upload step exposes the `ef-bundle`
artifact ID as a job output. `migrate-database` downloads that single ID before running the bundle;
the five API consumers likewise select their producer's artifact ID through the host matrix.
The commands run in different jobs:

```bash
dotnet ef migrations bundle \
  --project Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project Cleansia.Web.Partner/Cleansia.Web.Partner.csproj \
  --configuration Release \
  --output ./efbundle

# Connection string read from Key Vault at run time (ConnectionStrings--cleansia-db) —
# the same secret the runtime hosts resolve, so a rotation touches one place.
DB_CONNECTION_STRING="$(az keyvault secret show \
  --vault-name kv-cleansia-<region>-<env> \
  --name ConnectionStrings--cleansia-db --query value -o tsv)"
./efbundle --connection "$DB_CONNECTION_STRING"
```

::: warning
Migrations run **before** any API deploys to ensure the database schema is ready.
:::

#### 4-7. API Deployments

The API matrix deploys to Azure App Service with at most **two** parallel jobs on DEV and **five** on PRO:

The `deploy-api` matrix in `deploy-azure.yml` names `partner`, `admin`, `customer`, `partner-mobile`
and `customer-mobile`. Its **Deploy to Azure App Service** step targets
`api-cleansia-<host>-<region>-<env>`. DEV deploys directly to the live site and runs **Warm the site**;
PRO deploys to `staging`, runs **Warm the staging slot**, then **Swap staging into production**. Those
slot steps run only when `inputs.env == 'prod'`.

::: tip Deployment capacity
The configured plans are **B2 for DEV** and **S1 for PRO** (`weu.dev.bicepparam` / `weu.prod.bicepparam`).
The matrix’s `max-parallel` is 2 for DEV and 5 for PRO.
:::

#### 5. Azure Functions

Built as a Docker image, pushed to ACR, and deployed:

```bash
az acr build --registry $ACR_NAME \
  --image cleansia-functions:$GITHUB_SHA \
  --file src/Cleansia.Functions/Dockerfile .

az functionapp config container set \
  --name func-cleansia-{region}-{env} \
  --image "$ACR_NAME.azurecr.io/cleansia-functions:$GITHUB_SHA"
```

The Functions build context must be the repository root: `Cleansia.Core.AppServices` embeds HTML
from the root-level `email-templates/` directory. The Dockerfile copies both `src/` and those
templates, and the project fails its build if a required template is missing. A `src/`-only context
previously produced an image without templates, causing email rendering to fail before sending.

The request API accepting an email means it was queued, not delivered. After deploying a delivery
fix, messages that have already exhausted the five queue attempts remain in `DeadLetters` and
need controlled replay; submitting the public promo form again does not resend them.

#### 6. Customer SSR

Deployed as a Node.js app with startup command:

```bash
az webapp config set --startup-file "node server/server.mjs"
```

#### 7-8. SPAs

Partner and Admin apps deploy to Azure Static Web Apps:

```yaml
- uses: Azure/static-web-apps-deploy@1a947af9992250f3bc2e68ad0754c0b0c11566c9 # v1
  with:
    azure_static_web_apps_api_token: ${{ secrets.TOKEN }}
    action: upload
    app_location: ./partner-app
    skip_app_build: true
```

The action's repository has a tag and a branch that are both called `v1`. `@v1` ran the tag, so the pin
is the tag's commit, not the branch head, whose action declares a different set of inputs.

### Warming is one job, after every deploy {#warm-dev-sites}

`warm-dev-sites` runs once all five APIs and the SSR app have deployed, and proves each answers before
the run is called green. A site that never answers is still a failed deploy — the job reports **every**
unreachable site rather than stopping at the first.

**It used to be a step inside each deploy job, and that made two of them fail every time.** Six sites
share a single **B2** plan — 2 vCPU, 3.5 GB, five .NET APIs plus the Node SSR app, all `alwaysOn` — and
they deploy in parallel, so they all cold-start at once. Whichever finishes deploying last warms
straight into the worst of that contention.

That is why it was always the same two. `partner-mobile` and `customer-mobile` are last in the
`apiHosts` array, so they deploy last and start last. They failed together on 2026-08-12 and again on
2026-08-15 while `partner-api` warmed in **one second** — and both were serving normally within the
hour. Nothing was wrong with them except *when* they were asked.

::: warning If this starts failing again, the answer is the plan
Do not raise the retry budget and do not re-serialise the deploys. The parallel fan-out is
[ADR-0015](/decisions/adr-0015) D5/B2 and the B2 SKU is its D2 owner cost override — a genuine
trade, deliberately made. A warm failure here means six cold starts no longer fit in two cores, and
the honest response is the SKU or fewer always-on sites, not a longer wait.
:::

**Prod is untouched.** There, each job warms its own **staging slot** and only then swaps it, because a
slot must be proven healthy before it takes traffic. That ordering cannot be moved to the end.

## Deploy to PRO (`deploy-pro.yml`)

Same pipeline as DEV with these differences:

| Aspect | DEV | PRO |
|--------|-----|-----|
| Trigger | Manual (`workflow_dispatch`) | Manual (`workflow_dispatch`) |
| Approval | None | **Required reviewers on the `prod-weu` Environment** |
| Default mode | `deploy` or `what-if`, chosen at dispatch | `what-if` — the no-thought path is the non-mutating preview |
| Concurrency | Never cancel: a second dispatch waits for the running one | Never cancel |
| Environment | `dev-weu` (GitHub Environment) | `prod-weu` (GitHub Environment) |
| Angular config | `staging` | `production` |
| Resource group | `rg-cleansia-<region>-dev` | `rg-cleansia-<region>-prod` |
| App names | `*-<region>-dev` | `*-<region>-prod` |
| Legal-text gate | none | the `legal-texts` job runs first, in both modes — below |

**A production deploy is refused while a legal text in force, or one still to come, is a draft.**
`deploy-pro.yml`'s first job, `legal-texts`, runs `agents/tools/check-legal-drafts.test.mjs` (its
self-test) and then `check-legal-drafts.mjs`, and the deploy job `needs:` it, so neither `what-if` nor
`deploy` provisions anything until it passes. The checker walks every audience, document type and
market under `src/Cleansia.Infra.Database/Seed/Legal` and reads, in each folder, the version in force
today (the newest `yyyy-MM-dd` on or before today, UTC) **and every version dated after today** — the
same deploy seeds a future version, and it comes into force on its date with no deploy in between. It
fails if any of their language files carries the draft banner — a line starting `> Návrh —` (cs, sk),
`> Draft —` (en), `> Черновик —` (ru) or `> Чернетка —` (uk). It also fails on an empty scan: no seed
tree, nothing in force anywhere, or a version in force with no text (an upcoming folder with no text
does not fail). A market with nothing in force yet is only noted, provided its future versions carry no
banner. The gate is on the production deploy only; pull requests and the DEV deploy do not run it,
because every text seeded today is a draft.

::: warning Production Safety
Two things guard prod, and neither is the typed confirmation this page used to describe — that gate was
**replaced** by GitHub Environment protection. Every job touching prod runs in the `prod-weu`
Environment, whose **required reviewers must approve the run before any prod secret is released**; and
the dispatch mode defaults to `what-if`, so a run started without thinking previews rather than
mutates.
:::

### The private database and vault, and the migration's login {#production-window}

Production's database and Key Vault take no public traffic
([Azure setup — production posture](/deployment/azure-setup#production-posture)), and a GitHub-hosted
runner is outside their VNet. Both jobs read the stage's posture from the param file Bicep deploys, so
on DEV none of these steps runs.

- **`provision`** opens the vault to the runner's IP alone for the secret push, then closes it.
- **`migrate-database`** turns the database's public access on, admits the runner's IP with a per-run
  firewall rule, migrates, removes the rule and turns public access off again.
- **Both closes run even on failure or cancellation**, and the job fails if the database or the vault
  does not report `Disabled` afterwards. The manual close is in `deploy/AZURE-PROD-POSTURE.md` §6.
- **The migration signs in as the administrator; the hosts do not (E-4).** On production the migrate
  job builds the administrator's connection string from `POSTGRES_ADMIN_PASSWORD`, over
  `Ssl Mode=VerifyFull`, and never reads the vault; after the migration it runs
  `deploy/db/grant-app-login.sql`, which gives the `cleansia_app` login row reads and writes and
  nothing else and sets its password from `POSTGRES_APP_PASSWORD`. The provision job writes that
  login's connection string to `ConnectionStrings--cleansia-db`, and refuses a deploy before anything
  is provisioned when `POSTGRES_APP_PASSWORD` is missing or is not at least 24 letters and digits. DEV
  still migrates with the vault's connection string, as *3. Database Migration* above shows.

`execute-sql.yml` opens no window, so an administrator's `psql` against production goes through the
same window by hand (`deploy/AZURE-PROD-POSTURE.md` §6), never while a `Deploy to PRO` run is in
flight.

### The first production deploy {#first-production-deploy}

The owner's steps, in order, are `deploy/AZURE-DEV-RUNBOOK.md` §11: the P0 console-access checklist,
the `prod-weu` Environment with required reviewers, the OIDC federation, the resource group, the
secrets, a `what-if` and then a `deploy`, and the two Static Web App tokens with a second deploy and
the smoke test. Every production value is the owner's, and an agent never runs anything against
production. The Stripe secrets are the **live** keys of the operating company's own Stripe account
(decision 49) — [Environment configuration — Stripe](/deployment/environment-config#stripe).

Two steps follow the deploy, both runbook step P7.

**The reference-data bootstrap (E-9).** A migrated production database holds no reference data at
all — no operating company, language, country, currency or market — so nobody can register or book
until `sql-scripts/prod-bootstrap.sql` has run. The owner runs it once, as the administrator, through
the database window. It is one transaction and idempotent, so a second run changes nothing: the
operating company, the languages, every country with Czechia serviced, the Czech service cities, CZK,
the Czech market and its operator, its invoice configuration, the cleaner document requirements (the
insurance certificate among them, optional since 2026-10-04), the e-mail texts, the four loyalty tiers
and the Czech size ladder. No users, orders, promo codes, catalogue, prices, pay rates, Plus plans or
company record: those come from the launch values sheet (decision 77), typed into the admin console. A
Development boot runs the
same file before the DEV fixtures in `insert_seed_data.sql`, and `ProductionBootstrapScriptTests`
runs it twice on an emptied migrated database and then books on it.

**The first administrator** comes after the bootstrap. Register on the customer site with the address
that will administer, confirm the e-mail, then run `sql-scripts/set-admin-role.sql` with that address,
as the administrator, through the database window. The script sets the profile and the role and
nothing else: it neither checks the administrators' 12-character minimum nor sets
`MustChangePassword`, so **register with a password of at least 12 characters**. Then invite that
person's Microsoft identity to the admin console with `admin_console`, MFA on
([Admin app — access to the deployed console](/admin-app/overview#access-to-the-deployed-console)).
Every later administrator is created in the console →
[Security rules — the production perimeter](/architecture/security-rules#production-perimeter)

## Authentication

The 2026-10-14 GitHub settings readback confirms `sha_pinning_required: true`. The default workflow
token was already `read`, with `can_approve_pull_request_reviews: false`; no write was needed for
that setting (SEC-CI-6). Checkout steps set `persist-credentials: false`.

`prod-weu` now requires reviewer **MichaelChaban** (user id **76599514**), permits self-review
(`prevent_self_review: false`), disables administrator bypass (`can_admins_bypass: false`) and has
one custom deployment branch policy: branch **`master`**. Fresh authenticated and anonymous reads
confirmed the gate; the branch-policy read confirmed the exact branch. The unused `production`
configuration was deleted after fresh checks found zero secrets, zero variables and no current
workflow readers. Its history was not empty: one failed GitHub deployment from **2026-04-01** was
present before deletion. After deletion that object's only changed field was `updated_at`, and the
statuses endpoint returned one `inactive` status. The prior error status is preserved off-tree;
its continued server retention was not established. No PRO operation ran.

Every job that signs in to Azure uses federated identity (OIDC), so no Azure client secret is stored.
The target setup uses a distinct OIDC app per stage, federated only to that stage's environment and
granted at its resource-group scope; actual DEV identity narrowing remains deferred to owner go-live.
Each workflow's token is read-only (`contents: read`) at the top level, and `id-token: write` is
granted per job: to the jobs that sign in, and to the `deploy` job of `deploy-dev.yml` and
`deploy-pro.yml`, whose grant caps what any job in `deploy-azure.yml` may request. A job that signs in
and checks out the repository declares:

```yaml
permissions:
  id-token: write
  contents: read

- uses: azure/login@a641126d1b8aa4d1fa005f4f92df94a3a4c4c906 # v3.1.0
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

## Required Secrets

Deploy jobs read their stage's GitHub Environment secrets: DEV uses `dev-weu`, and PRO requires its
own `prod-weu` values at owner go-live. The only repository-level secret retained after the
2026-10-14 cleanup is `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV`, read by `deploy-docs.yml`.
The eight obsolete repository copies were deleted sequentially after fresh name-list and workflow
reader checks. All 32 `dev-weu` secret names and metadata remained unchanged; no secret values were read.

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | Service principal client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription |
| `ACR_NAME` | Azure Container Registry name |
| `DB_CONNECTION_STRING_DEV` | DEV database connection string — read by `execute-sql.yml` |
| `DB_CONNECTION_STRING_PRO` | PRO database connection string — read by `execute-sql.yml` only, which cannot reach the private production database |
| `POSTGRES_ADMIN_PASSWORD` | the database administrator's password: the Bicep parameter, and on production the migration's sign-in |
| `POSTGRES_APP_PASSWORD` | **production only** — the `cleansia_app` login's password, at least 24 letters and digits ([above](#production-window)) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER` | Partner SPA deploy token, scoped to the target GitHub Environment |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN` | Admin SPA deploy token, scoped to the target GitHub Environment |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DOCS_DEV` | Docs deploy token, repository-scoped and read by `deploy-docs.yml` |
