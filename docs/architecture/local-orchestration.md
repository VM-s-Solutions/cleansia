# Local orchestration

One command brings up Postgres, the storage emulator, the migrator, all five APIs and the Functions
host:

```bash
cd src
dotnet run --project Cleansia.AppHost
```

It needs a Docker engine for Postgres and the storage emulator; on a Mac that is Colima
([below](#colima)). Everything else below is a decision the AppHost encodes. Each of them was a bug
first.

## On a Mac the container engine is Colima {#colima}

On a Mac, Docker runs through **Colima**, a Lima VM with the Docker engine inside, not Docker Desktop
(set up 2026-10-05). The AppHost's containers, and the Testcontainers Postgres that
`Cleansia.IntegrationTests` and `Cleansia.HostTests` start, both run on it, so those two suites run on
the Mac and not only in CI.

```bash
brew install colima docker
colima start      # once per boot; `colima status` shows the engine and its socket
```

`colima start` creates the `colima` Docker context and makes it current (`docker context ls` marks it),
so the `docker` CLI, and Aspire through it, find the engine with nothing else set. The default profile
is 2 CPUs, 2 GiB of memory and a 100 GiB disk, on Apple's Virtualization.framework with virtiofs
mounts. Testcontainers is pointed at it by two variables in the shell that runs the tests:

```bash
export DOCKER_HOST="unix://$HOME/.colima/default/docker.sock"
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
cd src
dotnet test Cleansia.IntegrationTests/Cleansia.IntegrationTests.csproj -c Release
dotnet test Cleansia.HostTests/Cleansia.HostTests.csproj -c Release
```

The first is the socket on the Mac. The second is the socket's path *inside* the VM, which
Testcontainers mounts into its clean-up container; the Mac's path does not exist in there.

`Cleansia.HostTests` boots several hundred hosts, and each one watches its appsettings files. On macOS a
watcher is an FSEvents stream, and after a few hundred the system refuses new ones; a refused watcher
reports a change at once, the configuration re-registers synchronously, and the test host dies of a
stack overflow (a run stopped at test 289 of 428). The suite therefore switches to polling watchers before
its first host starts (`PollingFileWatchers`, a module initializer setting
`DOTNET_USE_POLLING_FILE_WATCHER`), so it runs to the end on a Mac with nothing set by hand. Linux CI
uses inotify and never hit it.

**Regenerating the web clients on a Mac** needs the APIs up, so this engine, and two more things since
2026-10-05. The three `nswag-*.json` documents pin `"runtime": "Net100"`, the .NET the repo builds on:
NSwag 14.7.1 refuses a document whose runtime differs from its own process's, and the `Net80` they
pinned could not run where only .NET 10 is installed. And the client formatters rename with GNU sed's
`\L` and `\U`, which macOS's own sed writes as a literal `L` or `U` while still exiting 0, mangling
every name it renames; on macOS the formatters call `gsed` (`brew install gnu-sed`) and refuse with a
message when it is missing. With the five APIs up, `npm run generate-clients` then regenerates all three
clients byte-identical to the committed ones.

## Ports are pinned {#azurite-ports}

The Azurite emulator runs on the **standard** ports — 10000 blob, 10001 queue, 10002 table — and that
pinning is load-bearing rather than tidy.

`appsettings.json` and `local.settings.json` fall back to `UseDevelopmentStorage=true`, which is
hard-coded to those ports inside the Azure SDK. Without the pin, Aspire assigns random ports, and the
**producer** (the web hosts) and the **consumer** (the Functions queue triggers) end up talking to
different Azurite instances. A message goes in, nothing comes out, and the queue function never fires.

That was the recurring *"queue function not triggered"* bug, and it presents as silence rather than as
an error, which is what made it expensive.

The API ports are pinned for the same class of reason — the dev-server proxies and the mobile clients
target fixed numbers:

| Host | Port |
|---|---|
| Partner API | 5000 |
| Admin API | 5001 |
| Partner Mobile API | 5002 |
| Customer API | 5003 |
| Customer Mobile API | 5004 |

**On a Mac, port 5000 is also AirPlay Receiver's.** From macOS 12 the system's AirPlay Receiver
(`ControlCenter`) listens on 5000, and on 7000, on every interface, so `lsof -i :5000` names it even
with nothing of ours running. It does not stop the Partner API: the AppHost binds the API to
`localhost`, and a listener on the loopback address takes the loopback's connections from one on every
interface, so `http://localhost:5000` reaches the API while it runs (checked on macOS 15.7 with a .NET
listener on `127.0.0.1:5000` and `[::1]:5000`; the partner client regenerated through it). While the
Partner API is **not** running, `localhost:5000` answers `403 Forbidden` with `Server: AirTunes/…`. That
is not an authentication failure: it is the sign the API is down, and it is what the partner web's dev
proxy, the partner NSwag run and `curl` all get. A tool that binds every interface on 5000 fails while
AirPlay Receiver is on; it is switched off under System Settings → General → AirDrop & Handoff.

## Blob containers are declared, not created on demand {#blob-containers}

A fresh Azurite volume starts with **no** blob containers. Queues are created on every send, so they
self-heal — but the blob read and list paths never create one, so the data-retention sweep and the PDF
jobs failed on first run.

The AppHost declares the containers so the emulator creates them at startup:
`generated-receipts`, `generated-invoices`, `user-files`, `employee-documents`, `order-photos`,
`dispute-evidence`. The names mirror the production Bicep. The eighth container, `company-archives`
(the archived-company bundle, [ADR-0064](/decisions/adr-0064)), is not in the AppHost list: it has no
read-before-write path — the archive build's first act is a streaming write, and every blob write creates
its container if it is missing — so it appears the first time a company is archived.

## The development seed, and why its administrator is a separate file {#development-seed}

A local Development boot that finds an empty `Languages` table runs three scripts from the repo-root
`sql-scripts/`, in order (`CleansiaStartupBase.DevelopmentSeedScripts`): `prod-bootstrap.sql` — the
reference data production gets as well: `generate_ulid()`, the `cleansia-cz` tenant, the languages,
the countries, CZK and the Czech market — then `insert_seed_data.sql` — the DEV fixtures: the
catalogue with its prices and pay rates, Plus plans, promo codes and the company record — then
`insert_local_dev_admin.sql`, which creates the local administrator whose password the README
publishes. The split exists so that the known-password account reaches a shared database only on
purpose: neither shared script creates a user at all, and `execute-sql.yml` refuses
`insert_local_dev_admin.sql` for PRO, comparing the file name so a relative path cannot walk around
it. The shared DEV database may run it (owner ruling 2026-09-30, until an app registration gates the
DEV apps), and like every run it inserts only into a database with no users. Named administrators are
promoted with `set-admin-role.sql` after they register, and `fix-deactivate-local-dev-admin.sql`
retires the published account once one exists; the README has the steps. `execute-sql.yml` refuses
that script for PRO too, from the same list of DEV-only file names
([CI/CD](/deployment/ci-cd#workflows-overview)).

## The Functions host's local settings are each developer's own {#functions-local-settings}

`src/Cleansia.Functions/local.settings.json` holds a database password, so each developer's copy is
meant to be their own. `.gitignore` lists it, but **the file is still tracked**: an ignore rule does not
untrack a file already committed, and it has not yet been removed from the index
(`git rm --cached src/Cleansia.Functions/local.settings.json`). Until it is, git still sees every edit
to it — do not commit your values into it. `local.settings.example.json` beside it has the same keys,
with the Postgres password replaced by `YOUR_LOCAL_POSTGRES_PASSWORD`; copy it and put in your own
values. Committed secrets are what the CI secret scan looks for →
[CI/CD — secret scan](/deployment/ci-cd#secret-scan).

## The Postgres password is fixed, not generated {#postgres-password}

The container is **persistent**, so its password is baked in when it is first created and is never
updated on later starts. A per-run generated password would drift from the baked-in one and fail
authentication with `28P01`.

It comes from user-secrets or the environment (`Parameters:postgres-password`).

## The migrator is a one-shot executable, and everything waits for it {#migrator}

The migrator is the **only** startup actor allowed to touch the schema, and every API waits for its
**completion** — exit 0 — rather than merely for Postgres being healthy.

Waiting on the database alone let the hosts' background jobs (the outbox drainer, the fiscal sweep)
race the in-process migration and crash on missing tables. A failed migration now keeps every
dependent stopped instead of letting it run against a half-migrated schema.

It is deliberately an **executable** resource rather than a project resource. Under Visual Studio,
project resources launch through the IDE's run-session service, and VS refuses this console project —
*"run session could not be started"*. Executables are spawned by Aspire's own orchestrator, so the same
graph works under F5 and `dotnet run` alike. The AppHost keeps a project reference (with
`IsAspireProjectResource=false`) purely so the migrator is compiled before the AppHost starts.

## The legal texts are seeded by every host, after the type catalog {#legal-seed}

The migrator owns the **schema**; the legal *texts* are **rows**, and they are written by every host
that binds the database — the five APIs and the Functions host alike — at every start, in every
environment. `LegalDocumentSeedHostedService` reads the markdown files embedded in
`Cleansia.Infra.Database` (`Seed/Legal/{audience}/{type}/{ISO3|any}/{yyyy-MM-dd}/{xx}.md`) and upserts
them into `LegalDocuments` / `LegalDocumentTexts`: a new folder is a new version, a changed file under
a date already in force is **refused with a warning** (what a customer accepted must read the same
forever), and a second host booting at the same moment finds the rows already there. That is why a
new version of the terms is *a seed file plus a deploy* and nothing else — there is no admin authoring.
→ [ADR-0063](/decisions/adr-0063)

Its place in the hosted-service order is load-bearing, like the type-catalog initializer's. Hosted
services start **sequentially**, and `NpgsqlTypeCatalogInitializer` awaits a retry loop that can span
about two minutes while a migration is in flight; the seed is registered **after** it, so it waits
behind the migration instead of spending its own five bounded retries against a half-built schema.
It is a plain `IHostedService`, awaited inline — not a `BackgroundService` — because being awaited is
what turns its position into a wait rather than a race. A host that still cannot seed logs at Error
and **starts anyway**: a customer can book while the legal page serves the previous version — as long
as *some* version of every text is in the table. Since [ADR-0068](/decisions/adr-0068) the contract
for work is one of those texts, and a booking with **no** version of it in force is refused by
`OrderFactory`; that is why the hosted seeder is not the only writer on a first local boot. Every
hosted service starts **before** the Development migrate-and-seed pipeline in `CleansiaStartupBase`
runs, so on a fresh Development database the seeder's five attempts all fail against no
`LegalDocuments` table and the host would come up unable to book until restarted. `CleansiaStartupBase`
therefore runs `LegalDocumentSeeder.SeedAsync` once more after the Development migration and the
data seed, idempotently (a no-op on a database the hosted service already seeded). Non-Development
deploys migrate in CI/CD before any host starts, so there the hosted service finds the table and that
branch is never entered. Pinned by `BootDatabaseIoTests`.

## Why there are two customer hosts {#two-customer-hosts}

The Customer **Web** host (5003) issues HttpOnly cookies. Native clients cannot read those, so the
Customer **Mobile** host (5004) mirrors the partner-mobile shape instead: body-token JWT, no cookies,
no CSRF.

Both issue tokens for the same audience, so it is one user pool reached two ways — not two accounts.
