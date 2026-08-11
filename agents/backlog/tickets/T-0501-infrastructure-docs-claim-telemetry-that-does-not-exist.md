---
id: T-0501
title: The infrastructure docs claim all five APIs send telemetry; none of them do
status: done
size: S
owner: docs
created: 2026-08-02
updated: 2026-08-05
depends_on: [T-0500]
blocks: []
stories: []
adrs: [0015]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Azure cost investigation (2026-08-02).** A side finding: *"the infrastructure docs claim
all APIs send telemetry (false)."*

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`

**There is no Application Insights exporter in any of the five API hosts.** A grep for
`ApplicationInsights`, `AddAzureMonitor` and `UseAzureMonitor` across `Cleansia.Config` and every
`Cleansia.Web*` project returns **zero hits**. The connection string is provisioned by Bicep, injected
as an app setting, and **read by nothing**.

**Why a docs ticket is worth filing rather than shrugging at:** this is the same failure class that
cost this backlog real time twice in sprint-14 alone —

- `CLAUDE.md:84-91` documented **six** Nx commands, **none of which run** (T-0462). The sprint doc's
  own note on it: *"a developer who does not stop to investigate could reasonably conclude the build
  is broken rather than the docs — and then start 'fixing' a build that was never broken."*
- Three `*-client-formatter.sh` scripts lacked `set -e` and **always exited 0**, so a green run proved
  nothing (fixed in `d6969fef`).

**This instance is worse than a stale command, because it is load-bearing for an incident.** An
engineer during an outage reads "all APIs send telemetry to Application Insights", goes to the
workspace, finds nothing, and concludes **the APIs are down** — when in fact they were never
instrumented. The document does not just fail to help; it points at a wrong diagnosis at the exact
moment that costs the most.

**And it interacts with the money:** T-0499 is reducing App Insights spend on the Functions host.
Someone reading these docs would reasonably believe the five APIs are also contributing to that bill
and go looking for savings that are not there.

## Acceptance criteria

- [ ] **AC1 — every false telemetry claim is FOUND, not just the one that was reported.** Grep the
      documentation tree (`docs/`, `deploy/*.md`, `agents/architecture/`) for claims about
      Application Insights, telemetry, monitoring and observability on the API hosts. Produce the
      list with file:line **before** editing anything. Evidence: the list.
- [ ] **AC2 — each claim is corrected to the verified state, and the correction is specific.** Not
      "telemetry may be limited" — the true statement is: *the five API hosts have no Application
      Insights exporter; the connection string is provisioned and unread; the Functions host is the
      only App Insights producer.* Evidence: the diff.
- [ ] **AC3 — the Bicep's inert connection string is ANNOTATED where it is provisioned**, so the next
      reader of `main.bicep` does not re-derive this. One comment line, at the setting. **This is the
      same annotation T-0500 AC4 requires — coordinate so it is written once, not twice.** Evidence:
      the diff, or a note that T-0500 already did it.
- [ ] **AC4 — the doc states what error tracking DOES exist**, per **T-0500**'s ruling. If the answer
      is "none on dev", the document says so plainly rather than omitting it. **A document that is
      merely no longer wrong is a wasted edit; it should be right.** Evidence: the paragraph.
- [ ] **AC5 — `CLAUDE.md` is NOT edited by this ticket.** It is owner-gated and a shared-file-lane
      entry (`process/shared-file-lanes.md`). **If a correction is needed there, it is proposed as
      text in `## Review`** for the owner to apply — exactly as T-0462 does for its three
      corrections. Evidence: the proposed text, or "no `CLAUDE.md` change needed".
- [ ] **AC6 — no claim in the corrected text is asserted without having been checked.** Every
      statement about what a host does or does not do carries a file:line or a command. **A docs
      ticket correcting an unverified claim with another unverified claim is the same defect.**
      Evidence: the citations inline or in `## Review`.
- [ ] **AC7 (Gate 0.5 leg 3)** — state which documents were **not** reviewed and why.

## Out of scope

- **Adding an exporter.** That is **T-0500** AC2 option (b). This ticket documents reality; it does
  not change it. **Hence `depends_on: [T-0500]`** — writing "the APIs send no telemetry" the week
  before a ticket makes them send telemetry produces a second wrong document.
- **`CLAUDE.md`** — AC5.
- **The Functions host's telemetry configuration** — **T-0499**.
- **`docs/` build/deploy.** If the VitePress site needs rebuilding, that is `manual_steps: docs-build`
  and it is flagged, not run.

## Implementation notes

**No panel — one-line "no-decision" note:** correcting a factually false statement to a verified one
introduces no behaviour and no decision. **The only decision (does dev get error tracking) belongs to
T-0500**, and this ticket consumes its answer.

**Sequencing:** strictly after T-0500's ruling. It is cheap and it should ride in the same PR as
T-0500 if the timing allows — one reader, one correction, one commit.

**Read first:** `deploy/AZURE-DEV-RUNBOOK.md`, `deploy/AZURE-PROD-POSTURE.md`,
`docs/architecture/*`, `agents/architecture/decisions/azure-deployment.md`, **ADR-0015**.

## Status log
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **PM-verified
  first-hand:** zero `ApplicationInsights` / `AddAzureMonitor` / `UseAzureMonitor` references across
  `Cleansia.Config` and all five `Cleansia.Web*` projects. Filed as a real ticket rather than a
  shrug because this backlog has already paid twice for documentation that described a system that
  did not exist (T-0462's six non-running Nx commands; the always-exit-0 formatter scripts), and this
  instance points an incident responder at a wrong diagnosis. **`depends_on: [T-0500]`** so the
  correction is written once against the final posture.

## Review

### ⚠️ The ticket's own ground truth was right in conclusion, wrong in reasoning

The PM's grep covered `Cleansia.Config` and every `Cleansia.Web*` project. **The Azure Monitor
exporter lives in neither** — it is in `Cleansia.ServiceDefaults`, which the grep did not include:

```
src/Cleansia.ServiceDefaults/Extensions.cs:160:    builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
```

So "zero hits" was a scoping artefact. **The conclusion survives and is actually stronger**, because
the exporter is not merely unconfigured — it is on a code path nothing calls:

- `Extensions.cs` declares **two** `AddServiceDefaults` overloads: `IHostApplicationBuilder` (`:20`)
  and `IServiceCollection` (`:38`).
- `UseAzureMonitor` (`:160`) sits in `AddOpenTelemetryExporters` (`:143`), called only by
  `ConfigureOpenTelemetry` (`:114`), called only by the **`IHostApplicationBuilder`** overload (`:22`).
- A repo-wide grep for `AddServiceDefaults` across `src/` returns exactly **three** hits: the two
  definitions and **one** call site — `Cleansia.Config/Abstractions/CleansiaStartupBase.cs:138`, which
  takes the `IServiceCollection` overload.
- All five APIs reach it: `Cleansia.Web.{Partner,Admin,Customer,Mobile.Partner,Mobile.Customer}/Program.cs:17`
  → `UseStartup<Startup>()`, and all five `Startup` classes derive from `CleansiaStartupBase`
  (`Startup.cs:8-9` in each).

**This matters for T-0500 AC2 option (b):** adding the exporter is not a config value. It is a
one-line call-site change (or an overload merge) plus a redeploy. `T-0500` should be told this before
it prices option (b) — "the code path already exists" is true only in the sense that the *code* exists.

The same scoping caveat applies to **T-0500's Half 1** ("A grep … returns zero hits … nothing reads
it"). "Nothing reads it" is correct; "there is no exporter in the tree" is not.

### AC1 — every false telemetry claim, with file:line (produced before editing)

| file:line | Claim | Verdict |
|---|---|---|
| `docs/architecture/infrastructure.md:268` | "All APIs and Functions send telemetry to Application Insights for monitoring, logging, and alerting." | **False.** Functions only. |
| `docs/architecture/infrastructure.md:282` | "All APIs use structured logging via `ILogger` which flows to Application Insights" | **False.** No exporter, and no App Service diagnostic setting either. |
| `docs/architecture/infrastructure.md:292-301` | KQL tip querying the `requests` table to find 5xx | **Misleading.** `requests` holds no API rows. |
| `docs/architecture/infrastructure.md:270-278` | "Key Metrics Monitored" — five thresholds | **Wrong numbers.** Latency/5xx are real but are *platform* metrics at different thresholds; "Function execution failures > 3 in 15 minutes" matches no provisioned alert (`alerts.bicep:52` is 25 dev / 10 prod, component-wide). |
| `docs/architecture/infrastructure.md:255` | Sentry "across all APIs and Functions" | **Half false.** `UseSentryMonitoring` is an `IWebHostBuilder` extension; the Functions host is a `HostBuilder` and never calls it. |
| `docs/architecture/infrastructure.md:257-263` | `builder.WebHost.UseSentry(...)` sample with `options.Environment` | **Not the shipped code.** Real shape is `UseSentryMonitoring()` with the empty-DSN guard; `options.Environment` is never set. |
| `docs/architecture/infrastructure.md:56` | Secrets table: `Sentry--Dsn` → "All APIs, Functions" | **Wrong consumer list**, and omits that it is empty. |
| `docs/deployment/environment-config.md:147` | "Development uses an empty DSN (disabled). Production configures a 20% trace sample rate." | **Misleading.** `Environment`/`TracesSampleRate` are not bound at all; the rate is hard-coded. Implies prod is enabled — prod has never deployed. |
| `docs/deployment/environment-config.md:200` | Production table: "Sentry \| Enabled with 20% trace rate" | **Aspirational.** |
| `docs/deployment/azure-setup.md:125` | PRO cost table "Monitoring (Sentry) ~$26" | Forecast for a subscription that is not active; **and neither cost table lists App Insights / Log Analytics at all**, which is the thing that actually bills (T-0499). |
| `docs/architecture/frontend.md:16` | "Sentry 10.40 \| Error tracking" | **Misleading.** `Sentry.init` is guarded on `environment.sentryDsn` (`cleansia-admin.app/src/main.ts:4`, `cleansia-partner.app/src/main.ts:4`) and all nine committed environment files set `sentryDsn: ''`. The customer app has no Sentry init at all. |
| `docs/architecture/infrastructure.md:27` | Resource inventory row "Application Insights \| Basic \| Basic" | True as provisioning, reads as coverage. |

**Checked and found TRUE** (no edit): `docs/architecture/infrastructure.md:149` (poison-queue alerting
— matches `queueAlerts.bicep`); `docs/architecture/push-notifications.md:35` and `:53-54` (the FCM
dispatch path is the *Functions* host, which is a real App Insights producer);
`docs/mobile-app/overview.md:140` ("Sentry stays dormant (no-op init)" — already honest).

### AC2 — corrections made

- **`docs/architecture/infrastructure.md`** — the `### Sentry` + `## Application Insights` sections are
  replaced by a `## Observability` section: a `::: danger` box stating that no error tracker receives
  anything from the APIs, a per-host "Who sends what" table, a "Why the APIs send nothing" subsection
  carrying the two-overload call-path evidence, "What you can actually see today", "Reading API logs",
  and a corrected Sentry subsection quoting the real `UseSentryMonitoring` body. Also: a warning under
  the resource-inventory row (`:27`) and a corrected `Sentry--Dsn` secrets row (`:56`).
- **`docs/deployment/environment-config.md`** — the Sentry block keeps the real JSON but gains a
  warning that only `Dsn` is bound and that it is empty everywhere; the Production table row is
  qualified.
- **`docs/architecture/frontend.md`** — tech-stack row marked dormant; a warning after the environment
  properties block naming the guard and the empty values.
- **`docs/deployment/azure-setup.md`** — a warning naming the two gaps in the cost tables.
- **`CHANGELOG.md`** — one `Fixed` entry, operator-voiced.

### AC3 — Bicep annotation NOT written here, and deliberately so

**T-0500 has not run** (`status: draft`, empty `## Review`, `Q-OBS-01` still open in
`questions/open.md:70`). `main.bicep` is a shared-file lane assigned **T-0500 → T-0502**
(`status/sprint-15.md:260`), and T-0500 AC4 may *remove* the setting rather than annotate it. Writing
a comment now either collides with that lane or annotates a line that gets deleted.

**Proposed text for whoever runs T-0500 AC4**, at `deploy/bicep/main.bicep:469` (and the same at
`:665` for the SSR):

```bicep
  // INERT: nothing reads this. The five APIs take the IServiceCollection AddServiceDefaults overload
  // (CleansiaStartupBase.cs:138), which registers no Azure Monitor exporter — UseAzureMonitor sits on
  // the IHostApplicationBuilder overload (ServiceDefaults/Extensions.cs:160), which has no caller.
  // The SSR host (:665) has no App Insights client at all. Only functionApp.bicep:102 is live.
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
```

### AC4 — what error tracking DOES exist

Documented in *What you can actually see today*. T-0500's ruling does not exist yet, so the docs
describe the **current** posture and nothing else. The honest answer to *"how would I see an error
today?"*:

- **You would be emailed** if a site crosses 25 HTTP 5xx in 15 minutes, or averages > 2 s response
  time, or the Functions health probe drops, or Postgres fails connections / saturates CPU / fills
  storage, or anything lands in a poison queue. All of these read **platform** metrics, so they work
  without app instrumentation, and `weu.dev.bicepparam:45` sets `alertEmail` so they are live on DEV.
- **You would not learn what failed.** No stack trace, exception or request record leaves the API
  process. A single 500 on DEV does not even reach the alert threshold.
- **The only remaining path** is attaching to the live log stream (`az webapp log tail`) and
  reproducing — nothing retrospective, because no App Service diagnostic setting ships stdout to Log
  Analytics (the only `diagnosticSettings` in the repo is `queueAlerts.bicep:51`, for the queue).

> **If T-0500 later rules (a) or (b), the `## Observability` section must be revised in the same PR.**
> It is written as a statement of the present, not a permanent claim.

### AC5 — `CLAUDE.md`

**No change needed.** A case-insensitive grep for `insights|telemetry|observability|sentry|monitor`
over `CLAUDE.md` returns no matches — it makes no observability claim. Root `README.md` likewise
returns none. Neither file was edited.

### AC6 — citations

Every statement in the edited text carries a `file:line` that was opened and read in this session. The
call-path chain (`Program.cs:17` → `Startup.cs:8-9` → `CleansiaStartupBase.cs:138` →
`Extensions.cs:38-76`), the alert inventory (`alerts.bicep` line numbers), the gating
(`main.bicep:793`, `weu.dev.bicepparam:45`), the injection sites (`main.bicep:469`, `:665`,
`functionApp.bicep:102`) and the Functions registration (`Functions/Program.cs:29-30`, `host.json:3-10`)
were each read directly, not inferred.

### AC7 — not reviewed, and why

- **`agents/architecture/`** — no such directory exists in the tree (the ticket's *Read first* names
  `agents/architecture/decisions/azure-deployment.md`; it is absent). Architecture records live in
  `agents/backlog/adr/`.
- **`agents/backlog/adr/0015-*.md:208`** — reviewed, **not edited.** It states *"All 5 APIs + SSR +
  Functions emit telemetry (D3/observability)"*, which is false today. Left alone deliberately: an ADR
  records the decision as made, and the charter's split is that the architecture doc carries current
  state. **Flagged for the architect** — if D3 was accepted as built, the ADR needs a correction note.
- **`docs/architecture/push-notifications.md:57-58`** — reviewed, **not edited.** It claims a `traces`
  row survives because `host.json` excludes `Exception` from sampling. Sampling exclusion is per
  telemetry *type*, and `ILogger.LogError` produces `Trace`, not `Exception` — so that row **is**
  subject to the 5 items/second cap. Left to **T-0499**, which owns Functions sampling and is likely to
  touch this exact sentence.
- **`src/Directory.Packages.props:97-101`** — **out of scope (`src/`), reported not edited.** The
  comment on the `Azure.Monitor.OpenTelemetry.AspNetCore` package reads *"exports the API hosts' OTel
  traces/metrics/logs to Application Insights"*. It does not. Same for the in-code comment at
  `ServiceDefaults/Extensions.cs:153-156` (*"the Bicep sets … on every API host"* — true, but it frames
  the block as live when its overload is uncalled). Both belong to whoever runs T-0500.
- **`docs/node_modules/**`** — third-party, excluded by `srcExclude` in `.vitepress/config.ts:6-9`.
- **The live DEV environment** — not checked. Everything above is repo-verified only; confirming the
  deployed app-setting values is **T-0500 AC1** and requires portal access.

### Manual step

`manual_steps: docs-build` — the VitePress build was **not run** (no shell available to this agent;
also charter-excluded). Five files changed under `docs/`. Anchors introduced:
`#observability` and `#what-you-can-actually-see-today` on `/architecture/infrastructure`, both
matching headings added in the same edit.
