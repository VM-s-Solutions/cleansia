# Local orchestration

One command brings up Postgres, the storage emulator, the migrator, all five APIs and the Functions
host:

```bash
cd src
dotnet run --project Cleansia.AppHost
```

Everything below is a decision the AppHost encodes. Each of them was a bug first.

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

## Blob containers are declared, not created on demand {#blob-containers}

A fresh Azurite volume starts with **no** blob containers. Queues are created on every send, so they
self-heal — but the blob read and list paths never create one, so the data-retention sweep and the PDF
jobs failed on first run.

The AppHost declares the containers so the emulator creates them at startup:
`generated-receipts`, `generated-invoices`, `user-files`, `employee-documents`, `order-photos`,
`dispute-evidence`. The names mirror the production Bicep.

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
and **starts anyway**: a customer can book while the legal page serves the previous version. Pinned by
`BootDatabaseIoTests`.

## Why there are two customer hosts {#two-customer-hosts}

The Customer **Web** host (5003) issues HttpOnly cookies. Native clients cannot read those, so the
Customer **Mobile** host (5004) mirrors the partner-mobile shape instead: body-token JWT, no cookies,
no CSRF.

Both issue tokens for the same audience, so it is one user pool reached two ways — not two accounts.
