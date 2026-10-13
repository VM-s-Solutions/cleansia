# CI/CD Pipeline

Cleansia uses GitHub Actions. **The workflows below check a pull request** — one per stack, the docs,
the dependency-free Node checks whose drift spans trees no single stack job can see, and the secret
scan — and the rest deploy or run operational jobs. **The only status required to merge is `build`**:
[what it covers](#required-checks).

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

`build` is the only status check required to merge into `master`, and this section says what it
covers (owner ruling 2026-10-11, T-0813).

The repository rulesets *master* and *Pull request rule* protect `master`, and there is no classic
branch protection. They apply only to `master`, so a pull request into another branch (a stacked one)
has no required check. Ruleset *master* requires every review thread to be resolved, no approval, and
the status check **`build`**, strict: the branch must also be up to date with `master`. The check is
pinned to no app, so a `build` from any source counts.

**`build` is a job name, not a workflow.** Every check called `build` on the head must report, and
these jobs report one:

- **Frontend** (`frontend-ci.yml`) and **Android** (`android-ci.yml`), on every pull request;
- **Backend** (`backend-ci.yml`), when the pull request touches `src/**` outside the web, Android and
  iOS trees, `sql-scripts/**` or the workflow;
- **iOS** (`ios-ci.yml`), when it touches `src/cleansia_ios/**`, `src/cleansia_android/openapi/**` or
  the workflow.

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
  reaches that state on its own (T-0813).

**Advisory checks** show red on a pull request when they fail, and do not block the merge: Docs CI
(*References and build*), Secret scan (*No new secret in the history*) and Frontend CI's *e2e-smoke*
on every pull request; iOS Symbols CI (*Symbols resolve and switches stay exhaustive*) and Booking
policy parity (*Every surface quotes the policy it actually enforces*) when their paths change.

**The gate changes when a workflow does.** A required check is matched by its name alone, not by its
workflow, matrix or event:

- a `name:`, a matrix, or a move into a reusable workflow renames a job's check, and that workflow
  leaves the gate without any error;
- any other check called `build` on the head joins the gate, from any workflow or app;
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

So a secret committed anywhere in a pull request fails it, even if a later commit deletes it again —
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
| `ios-ci` | PR to any branch that touches `src/cleansia_ios/**`, `src/cleansia_android/openapi/**` or the workflow | SwiftFormat, SwiftLint, then build and test the three schemes |
| `docs-ci` | PR to any branch | Docs pointers and written claims resolve; the docs site builds |
| `ios-symbols-ci` | PR to any branch that touches `src/cleansia_ios/**`, the checker, its self-test or the workflow | `check-ios-symbols.mjs` on Linux |
| `booking-policy-parity` | PR to any branch that touches a file the checker reads, the checker, its self-test or the workflow | `check-booking-policy-parity.mjs` |
| `secret-scan` | PR to any branch, push to `master` | gitleaks — no new secret in the history |
| `deploy-dev` | **Manual (`workflow_dispatch`)** | Deploy everything to DEV |
| `deploy-pro` | Manual (`workflow_dispatch`) | Deploy everything to PRO |
| `deploy-azure` | Called by `deploy-dev` and `deploy-pro` (`workflow_call`) | The shared build, provision, migrate and deploy pipeline |
| `deploy-docs` | Manual (`workflow_dispatch`) | Build the docs site and deploy it to DEV's Static Web App |
| `execute-sql` | Manual | Run ad-hoc SQL scripts. On PRO it refuses a development fixture by resolved path (`insert_seed_data.sql`, anything under `seed/` or outside `sql-scripts/`), and every DEV-only script by file name, from one list in its step *Refuse DEV-only scripts against production*: `insert_local_dev_admin.sql`, `fix-company-contact-placeholders.sql`, `fix-plus-trial-14-days.sql` and `fix-deactivate-local-dev-admin.sql`. A relative path cannot walk around a name, each refusal says where production does the same thing instead, and a new DEV-only script is one more row of the list (one list since 2026-10-04; the last two were not refused before). The three fixes correct what a DEV database got from a development fixture, which production is never built from. DEV runs all of them, `insert_local_dev_admin.sql` included (owner ruling 2026-09-30, until an app registration gates the DEV apps). It opens no network window, so it cannot reach the production database once that is private ([below](#production-window)) |

As of 2026-10-11, every file in `.github/workflows/` has a row. The workflows that check a pull
request also run on pushes to `master`, path-scoped except for the secret scan.

## Runners and images {#runners}

Every Ubuntu job runs on `ubuntu-24.04`, and `ios-ci.yml` runs on `macos-latest`. The Ubuntu jobs pin
their Ubuntu release and the macOS job does not pin its macOS one; none uses a self-hosted or larger
runner. A new workflow either joins one of these two labels or says why not.

- **Which image a label resolved to** is printed only in the job log's *Runner Image* group (`Image:`
  and `Version:`). The repository keeps logs for one day, so read it within 24 hours. The jobs API
  returns only the requested label.
- **iOS CI picks its tools at run time.** The newest installed Xcode (`ls -d /Applications/Xcode*.app |
  sort -V | tail -1`), and the first available iPhone simulator.
- **Observed on 2026-10-11**, in runs 38098170687 and 38098170722 at `64dfed7`:
  - `ubuntu-latest` resolved to ubuntu-24.04, version 20261004.327.1;
  - `macos-latest` resolved to macos-26-arm64, version 20260907.0351.1, with Xcode 26.6 (17F113) and an iPhone 17 Pro simulator.

  A maintainer's Mac ran Xcode 26.3 that day, with the same SwiftFormat, SwiftLint, xcodegen,
  xcbeautify and openapi-generator versions as CI. Only SwiftFormat, SwiftLint and openapi-generator
  are pinned: CI installs xcodegen and xcbeautify from Homebrew, unpinned, so their versions follow
  Homebrew's.
- **Ubuntu 26.** GitHub moves the `ubuntu-latest` label to Ubuntu 26.04 in a rollout that begins
  2026-10-19 and completes by 2026-11-19 ([actions/runner-images#14748](https://github.com/actions/runner-images/issues/14748)).
  Every Ubuntu job names `ubuntu-24.04` instead (owner ruling 2026-10-12), the image it already ran on,
  so the rollout changes nothing here. Moving to 26.04 is a deliberate change of the label: that image
  brings Docker 29 under backend-ci's Testcontainers, a PostgreSQL 18 client for the migrate job's
  `psql`, Android command-line tools 20 and jq 1.8.
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
`OrderVisibility`. Both run with `xUnit.parallelizeTestCollections=false`.

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

Publishes **five** API projects as separate artifacts (`deploy-azure.yml:120-136`):

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

Creates and runs an EF Core migrations bundle:

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

Each API deploys sequentially to Azure App Service:

```bash
az webapps-deploy --app-name api-cleansia-{service}-dev
az webapp stop --name api-cleansia-{service}-dev --resource-group rg-cleansia-dev
az webapp start --name api-cleansia-{service}-dev --resource-group rg-cleansia-dev
```

::: tip Sequential Deployment
APIs deploy one at a time to avoid overloading the B1 App Service Plan. Each deployment includes a stop/start to force container refresh.
:::

#### 5. Azure Functions

Built as a Docker image, pushed to ACR, and deployed:

```bash
az acr build --registry $ACR_NAME \
  --image cleansia-functions:$GITHUB_SHA \
  --file src/Cleansia.Functions/Dockerfile .

az functionapp config container set \
  --name func-cleansia-dev \
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
| Environment | (default) | `prod-weu` (GitHub Environment) |
| Angular config | `staging` | `production` |
| Resource group | `rg-cleansia-dev` | `rg-cleansia-pro` |
| App names | `*-dev` | `*-pro` |
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

Every job that signs in to Azure uses federated identity (OIDC), so no Azure client secret is stored.
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
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_DEV` | Partner SPA deploy token (DEV) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_DEV` | Admin SPA deploy token (DEV) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER_PRO` | Partner SPA deploy token (PRO) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN_PRO` | Admin SPA deploy token (PRO) |
