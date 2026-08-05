---
id: T-0500
title: The only live environment has no error tracking at all — Sentry's DSN is empty and there is no App Insights exporter
status: in_review
size: S
owner: architect
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0015]
layers: [architect, backend]
security_touching: false
manual_steps: [azure-deploy]
sprint: 15
---

## Context

**Source: the Azure cost investigation (2026-08-02),** which flagged *"Sentry may be silently
disabled, which would mean the APIs have no error tracking whatsoever."*

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`. Both halves are TRUE, and the
### investigation's framing needs one correction.

**Half 1 — there is no Application Insights exporter on any of the five APIs. Confirmed.**
A grep for `ApplicationInsights` / `AddAzureMonitor` / `UseAzureMonitor` across `Cleansia.Config` and
every `Cleansia.Web*` project returns **zero hits**. The connection string is plumbed through Bicep
and is **inert** — nothing reads it. The investigation is right.

**Half 2 — Sentry is disabled, and it is NOT silent. This is the correction.**

- All five hosts call it: `Cleansia.Web.Partner/Program.cs:16`,
  `Cleansia.Web.Admin/Program.cs:16`, `Cleansia.Web.Customer/Program.cs:16`,
  `Cleansia.Web.Mobile.Customer/Program.cs:16`, `Cleansia.Web.Mobile.Partner/Program.cs:16`.
- The implementation (`Cleansia.ServiceDefaults/Extensions.cs:85-113`) is **correct and deliberate**:
  an absent or blank DSN leaves Sentry uninitialized, with a doc comment explaining that the SDK
  rejects an empty DSN and would otherwise **fail startup**. That is a guard, not an accident.
- **Every committed `appsettings*.json` carries `"Dsn": ""`** — 10 files, all empty.
- The DSN is supplied at deploy from a GitHub secret: `deploy-azure.yml:433` `SENTRY_DSN:
  ${{ secrets.SENTRY_DSN }}` → `:481` `set_secret "Sentry--Dsn" "$SENTRY_DSN"` → Key Vault →
  `main.bicep:467` / `:730` `Sentry__Dsn: kvRef(...)`.
- **And `deploy/AZURE-DEV-RUNBOOK.md:239` says it outright:** *"leave EMPTY for dev (Sentry off); real
  DSN in prod."* `:520` repeats it.

**So Sentry is off on dev BY DESIGN, documented, in a runbook.** Not silent, not a misconfiguration.

### Why this is still a real ticket, and arguably an urgent one

**Because of a fact from the same investigation: prod has never been deployed.** So the "real DSN in
prod" that the design defers to **does not exist yet**, and **DEV is the only live environment** — the
one the owner's iPhone runs against, the one that will be demoed.

Put the two halves together and the honest statement is:

> **The only running instance of this platform has no error tracking of any kind.** No App Insights
> exporter (nothing reads the connection string). No Sentry (DSN empty by documented design). An
> unhandled 500 on DEV right now is visible to nobody unless a human is watching a log stream.

That is not a cost problem and not a bug. It is a **posture that was correct when dev was a scratch
environment and is no longer correct now that dev is the demo environment.** The decision to revisit
it is an architect + owner call, which is why this is `architect`-owned and why AC1 is a question
rather than a fix.

**And it sharpens T-0499:** that ticket lowers Functions log levels to save money. **Lowering
observability on a platform that has none is a worse trade than it looks**, which is why T-0499 AC3
keeps `Exception` excluded from sampling and AC4 demands a stated visibility floor. Recorded on both.

## Acceptance criteria

- [ ] **AC1 — the state is CONFIRMED against the running DEV environment, not just the repo.** Check
      the deployed app settings: is `Sentry__Dsn` empty on the live dev sites? **And is
      `secrets.SENTRY_DSN` populated in GitHub at all?** *(The second is owner-only — it is on the
      owner-decision list.)* Evidence: the app-setting value (redacted to present/absent) plus the
      owner's answer on the secret.
- [ ] **AC2 — the DECISION is made and recorded: does DEV get error tracking?** Three options, each
      priced: **(a)** populate `SENTRY_DSN` and turn Sentry on for dev (Sentry has a free tier; the
      code path already exists and is tested-by-construction — **this is a secret, not a build**);
      **(b)** add a real App Insights exporter to the five APIs — but note **T-0499 exists because
      App Insights telemetry is what is costing money**, so this option *increases* the bill;
      **(c)** accept no error tracking on dev until prod exists, and write that down as a decision
      with a date. Evidence: the ruling with the why-nots.
- [ ] **AC3 — if (a): the DSN's blast radius is stated.** With `TracesSampleRate = 0.2` and
      `AutoSessionTracking = true` (`Extensions.cs:105-107`), turning Sentry on has its own volume and
      its own free-tier ceiling. **And `SendDefaultPii = false` is already set (`:103`) — confirm it
      stays**, because sprint-14's **T-0457** established that this platform writes caller email,
      name, phone and birth date to Information-level logs on all five hosts, and an error tracker
      that ships log context would carry that to a third party. **T-0457 should land first if (a) is
      chosen.** Evidence: the stated volume plus the PII check.
- [ ] **AC4 — the inert App Insights connection string is resolved either way.** If the ruling is not
      (b), then the connection string plumbed through Bicep into five apps is **dead config** that
      reads as working instrumentation to anyone who looks. Either remove it or annotate it in
      `main.bicep`. **This is the exact defect T-0501 documents from the other direction.** Evidence:
      the diff or the annotation.
- [ ] **AC5 — the runbook's dev/prod sentence is updated to match the ruling.**
      `AZURE-DEV-RUNBOOK.md:239` and `:520` currently encode option (c) implicitly. Whatever is
      decided, those two lines say it explicitly, with the reason. Evidence: the diff.
- [ ] **AC6 — no secret value is ever written into the repo, a ticket, or a log.** The owner sets
      GitHub secrets; no agent handles a DSN. Evidence: `git diff` contains no DSN-shaped string.
- [ ] **AC7 (Gate 0.5 leg 3)** — state plainly what was checked in the **repo** versus what was
      checked against the **live environment**, and which claims are the owner's rather than measured.

## Out of scope

- **The Functions host's telemetry cost** — **T-0499**. Related and sequenced against this
  (see AC2 option (b)), but a different file.
- **Setting any GitHub secret or Azure app setting.** Owner-only.
- **Building a new observability stack.** The three options are: use what exists, add the exporter
  the Bicep already assumes, or accept the gap. Nothing new is designed here.
- **The docs correction about telemetry** — **T-0501**, which fixes the *documentation* claiming the
  APIs send telemetry. This ticket decides whether they should.

## Implementation notes

**Architect panel, short.** AC2 is a genuine three-way trade-off and one of the options (b) makes the
bill T-0499 is fixing **worse** — so the two tickets should be ruled together or in a stated order.
The `architect` owns it; the **owner ratifies**, because "we run a demo with no error tracking" is a
risk acceptance, not an engineering default.

**This ticket should run near the front of the sprint regardless of its size.** Not because it is
expensive — it is `S` and option (a) is a secret paste — but because **every other ticket in this
sprint that ships to DEV ships into an environment where its failures are invisible.** It changes what
"green on DEV" means.

**Read first:** `Cleansia.ServiceDefaults/Extensions.cs:80-115`, `deploy/AZURE-DEV-RUNBOOK.md:230-300`
and `:510-530`, `deploy/bicep/main.bicep:426-470`, `.github/workflows/deploy-azure.yml:425-490`, and
sprint-14's **T-0457**.

## Status log
- 2026-08-05 — **in_review (backend).** Option **(b) chosen and shipped**, and it turned out to cost far
  less than AC2 priced it at: the App Insights exporter was **already written** and needed no new
  dependency, no new resource and no new secret — only to be reachable from the overload the hosts call.
  The five APIs now export logs/exceptions/requests/metrics to the component that has been provisioned and
  empty since day one. **Option (a) is RECOMMENDED but not taken** — it is the owner's secret to set, and
  the recommendation is *complementary*, not redundant (App Insights is the record; Sentry is the pager).
  **(c) is rejected.** `manual_step: azure-deploy` — the change takes effect on the next `Deploy to DEV`,
  not on merge. AC1's live-environment half and the 7-day volume measurement stay **owner steps**.
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **All of it PM-verified
  first-hand, and the investigation's framing corrected on one point:** Sentry is *not* "silently
  disabled" — the empty-DSN guard is deliberate, documented at `Extensions.cs:87-90`, and
  `AZURE-DEV-RUNBOOK.md:239` explicitly says *"leave EMPTY for dev (Sentry off); real DSN in prod."*
  **The conclusion survives the correction and gets worse:** prod has never been deployed, so the
  "prod" that was supposed to have Sentry does not exist, and **DEV — the demo environment — has no
  error tracking from either source.** Filed `architect`-owned because the fix is a posture decision,
  not a defect.

## Review

### Gate 0 — every premise re-verified first-hand, and the ticket's Half 1 is WRONG in a way that made the fix cheap

The ticket says *"there is no Application Insights exporter on any of the five APIs"* and cites a grep
returning zero hits. **That grep was scoped to `Cleansia.Config` and the `Cleansia.Web*` projects; the
exporter is in `Cleansia.ServiceDefaults`.** T-0501 caught this on 2026-08-05 (`8f0ac88c`) and it is the
finding this whole ticket turns on. Re-confirmed here, both halves, at HEAD:

| Claim | How I checked | Verdict |
|---|---|---|
| App Insights is a real provisioned resource | `main.bicep:291` → `modules/appInsights.bicep` (workspace-backed component + Log Analytics) | true |
| Its connection string reaches seven hosts | `main.bicep:469` (the 5 APIs via `apiBaseSettings`), `:665` (SSR), `:731` (Functions) | true |
| The exporter exists | `ServiceDefaults/Extensions.cs` `UseAzureMonitor`, added by `183543e6` (2026-07-02) | true |
| Nothing calls the overload it hangs off | `grep -rn "AddServiceDefaults" --include="*.cs" src/` → **one** hit outside the file itself: `CleansiaStartupBase.cs:138`, the `IServiceCollection` overload | true |
| The Functions host is the one real producer | `Cleansia.Functions/Program.cs:29-30` — the AI **worker SDK**, not OpenTelemetry, and it never calls `AddServiceDefaults` | true |

The commit that added the exporter says *"Now every API host ships its OTel pipeline to App Insights."*
It shipped nothing — from `183543e6` (2026-07-02) through every deploy since, a bit over a month — and no
build, test or alert noticed. That is the shape worth
carrying out of this ticket, and it is why the tests below are what they are.

### AC2 / the design question — why the overloads diverged, and why calling the other one is NOT the fix

**They diverged for a real reason, and it is not the one that would let you swap them.**

`AddServiceDefaults(IHostApplicationBuilder)` is the **stock .NET Aspire ServiceDefaults template**
(`IsAspireSharedProject=true`). It is dead: nothing in the solution calls it. The
`(IServiceCollection, IConfiguration, IHostEnvironment)` overload was hand-written because the five APIs
use the **Startup-class pattern** — `Program.cs:17` `UseStartup<Startup>()` → `Startup : CleansiaStartupBase`
→ `ConfigureServices(IServiceCollection)`. **There is no `IHostApplicationBuilder` at that point in the
lifecycle**, so "just call the other overload" is not a design choice that was passed over; it does not
type-check. The exporter was simply added to the template copy nobody runs.

I checked what else the builder overload does, because switching wholesale would have carried it:

| Only on `IHostApplicationBuilder` | Only on `IServiceCollection` |
|---|---|
| `builder.Logging.AddOpenTelemetry(...)` (formatted message + scopes) | **`.AddSentry()` on the tracing pipeline** — the `Sentry.OpenTelemetry` span processor that `UseSentryMonitoring`'s `options.UseOpenTelemetry()` pairs with |

So switching overloads would have **dropped the Sentry OTel bridge** — silently, and invisibly today
because the DSN is empty, surfacing only on the day somebody set one. Confirms the brief's instinct:
**extract, do not switch.**

The fix is one private method both overloads call — `AddTelemetryExporters(IServiceCollection,
IConfiguration)` in `Extensions.cs`. Nine lines moved; no behaviour added to either overload beyond the
one that was missing. **`builder.Logging.AddOpenTelemetry(...)` was deliberately NOT copied across**, and
that is a measured decision, not an omission: I probed the distro's registrations directly
(`services.AddOpenTelemetry().UseAzureMonitor(...)` on a bare `ServiceCollection`) and
**`UseAzureMonitor` registers the OpenTelemetry `ILoggerProvider` itself** — bare collection: 0
`ILoggerProvider` descriptors; after `UseAzureMonitor`: 1. Copying the line would have been a second,
redundant registration of the log pipeline. `TheExporterBringsTheLogPipelineAndNotOnlyTraces` pins that
contract with the dependency so a distro upgrade that dropped it is a red test rather than a silent loss
of the only signal worth having.

**The remaining divergence is reported, not fixed:** the builder overload still lacks `.AddSentry()`. It
is harmless while the overload is dead, and unifying it would add a Sentry span processor to a
hypothetical future host that may never call `UseSentryMonitoring` — an architect call about the Aspire
template shape, not a bug fix. Same reason the dead overload was **kept** rather than deleted: deleting
public API from a shared project is not this ticket's call. `TheHostApplicationBuilderOverloadReachesTheSameExporter`
now makes the two impossible to diverge on the exporter again.

### What was turned on, and what was NOT

| | Status |
|---|---|
| Partner / Admin / Customer / Partner-Mobile / Customer-Mobile APIs | **on** — logs, exceptions, requests, metrics |
| Azure Functions | already on, **untouched** — it uses the AI worker SDK and never calls `AddServiceDefaults`, so T-0499's `host.json` tuning is unaffected |
| Customer SSR (Node) | **NOT turned on.** It is a Node host with no telemetry package and no auto-instrumentation agent. Turning it on is either an `applicationinsights` npm dependency in `apps/cleansia.app/server.ts` (frontend lane) or an `ApplicationInsightsAgent_EXTENSION_VERSION` app setting (owner posture). Its connection string is now **annotated as dead config** in `main.bicep` rather than left to read as working instrumentation |
| Sentry | **NOT turned on**, and nothing about it changed. Recommendation below |

### AC3 — sampling and cost: the trade-off I picked

**Posture: distro defaults. No SDK sampling (`SamplingRatio` 1.0), Live Metrics on, no per-route
filtering, no new config knob.** Deliberately the *opposite* of the Functions posture T-0499 chose, and
the reasons are the reasons the two hosts differ:

- **Sampling protects against traffic and DEV has none.** Real DEV traffic is the owner's phone and a
  demo. Dropping 80% of a handful of requests makes the one request that failed the one most likely
  discarded — blindness bought for a rounding error. Functions samples because 14 listeners poll
  continuously whether anyone is using the system (T-0499 `QueueListenerInventoryTests`).
- **Logs are self-limiting, and that is the whole cost argument.** No `ASPNETCORE_ENVIRONMENT` is set on
  any host in `deploy/` (grepped: workflow, `appService.bicep`, `functionApp.bicep`, both Dockerfiles),
  so every deployed host runs as **`Production`** and binds `appsettings.Production.json` —
  `Logging:LogLevel:Default = Warning`, `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` also
  `Warning`. An idle healthy host therefore emits almost no log telemetry; volume appears when something
  is wrong, which is when you want to pay for it.
- **I added no knob on purpose.** A config-bound `SamplingRatio` that nothing sets would be exactly the
  defect AC4 is about — inert config that reads as a control. The prod lever already exists and is
  already parameterized: `appInsights.bicep` `samplingPercentage` (dev 100, prod 50) and `dailyQuotaGb`
  (dev 1 GB, prod 5 GB). **Compounding warning now in the docs:** an SDK `SamplingRatio` of 0.2 on top of
  the component's prod 50% keeps one trace in ten.
- **The cap is a breaker, not a budget** — when it trips, ingestion stops until the next UTC day,
  *including exceptions*. So blowing 1 GB/day on routine telemetry blinds the exact signal this ticket
  buys. That is the residual risk, and it is why AC5-style measurement is owed (below).

**What I chose NOT to do, with the measurement that decided it.** Health probes are the dominant DEV
request volume — App Service polls `/health` on six sites, and `/health` runs the readiness checks, which
include an Azure Blob `ExistsAsync` (`ReadinessHealthChecks.cs:41-65`), so each probe emits a request span
**and** an HttpClient dependency span. The obvious fix is an
`AspNetCoreTraceInstrumentationOptions.Filter` dropping `/health`. **I built it and measured it, and it is
wrong:** with the filter in place, the server span is dropped but the HttpClient dependency span underneath
is still exported, now parented to a span that was never sent. It trades one noisy record for one orphaned
record. The correct instrument is a sampler (or not making a storage round trip on every probe) and that
is a cost ticket, not this one. Recorded in the docs so nobody re-derives it at cost.

### AC4 — the inert connection string is resolved, in both directions

- **Five APIs:** no longer inert — it is now the switch that arms the exporter. `main.bicep` says so at
  the setting, including the warning that deleting it silently disables API error tracking.
- **SSR:** still inert, and now **labelled** `DEAD CONFIG on this host, deliberately kept`, with what it
  would take to change that.
- **`alerts.bicep` corrected.** Its comment and its alert `description` claimed the `exceptions/count`
  alert covered *"all five APIs, the SSR, and the Functions host in a single signal"*. It covered Functions
  alone. It now covers the five APIs + Functions — genuinely, after the deploy — and the comment says
  plainly that the SSR was never in it and why a claim like that goes stale.

### AC5 — the runbook

`SENTRY_DSN` on both the dev (`:241`) and prod (`:522`) tables now states the ruling rather than encoding
option (c) implicitly. A **new smoke-test item** was added to §9: a KQL query that expects a row per API
host, with the failure mode spelled out, because "the exporter did not register" is exactly the failure
that produced no symptom across every deploy since the exporter was written.

### AC6 — no secret in the diff

`git diff` over `deploy/`, `docs/` and `src/Cleansia.ServiceDefaults/` plus the new test file, scanned for
DSN / instrumentation-key / ingestion-endpoint shapes: **one hit**, the all-zeros placeholder
`InstrumentationKey=00000000-…-000000000000;IngestionEndpoint=https://example.invalid/` in the test
fixture. No real value is handled anywhere in this change; the App Insights connection string is a Bicep
module output that never appears in the repository.

### The Sentry recommendation — complementary, keep it, and here is the owner's step

**Not redundant, not dead weight.** The two answer different questions and Sentry answers the one nothing
else does. Every App Insights alert in `alerts.bicep` is a **volume** threshold — 5xx `> 25 in 15 min`,
exceptions `> 25 in 15 min` — so on DEV a single 500 moves nothing and nobody is emailed. App Insights
*records* it; that is a real and large improvement over recording nothing, but it still requires a human to
go and look. Sentry's unit is the **issue**: first-seen, deduplicated, grouped, regression-after-release,
notified on occurrence **one**.

*App Insights is the record. Sentry is the pager.*

**Owner steps, in order, if you want it:**

1. Create a Sentry project (free tier: 5k errors/month) and set `SENTRY_DSN` in the `dev-weu` GitHub
   Environment. CI writes `Sentry--Dsn` to Key Vault on the next deploy. **No code change** — the empty-DSN
   guard at `Extensions.cs` already handles both states and is why an unset secret has never broken a boot.
2. Know the volume before you set it: `TracesSampleRate = 0.2` also sends 20% of **transactions**, and
   `AutoSessionTracking = true` sends sessions. Those, not the errors, are what consume a free tier.
3. **`SendDefaultPii = false` is already set and must stay.** With the deployed log level at `Warning`,
   `RequestLoggingMiddleware`'s Information-level records — the ones T-0457 established carry caller email,
   name, phone and birth date — never reach any provider, so they cannot become Sentry breadcrumbs either.
   **Lowering the deployed log level and enabling Sentry are individually fine and jointly not.** T-0457
   still owns that PII; this ticket does not depend on it, because at `Warning` the exposure does not exist
   for either tracker.

Two smaller Sentry defects **reported, not fixed** (both inert while the DSN is empty, both outside this
change's blast radius): `appsettings.Production.json` hardcodes `Sentry:Environment = "production"`, so DEV
errors would arrive tagged as production; and `TracesSampleRate`/`SendDefaultPii` are fixed in code rather
than bound from configuration, so the sample rate cannot be tuned per environment without a redeploy.

### What an operator will actually see afterwards, concretely

After a `Deploy to DEV` (this is `manual_step: azure-deploy`; merging changes nothing):

- **`exceptions`** — every unhandled exception from any of the five APIs, with its stack trace. The chain
  is real and I traced it: an exception escaping a handler is caught by `ExceptionHandlerMiddleware`
  (registered at `CleansiaStartupBase.cs:181`), logged at **Error** under `Microsoft.AspNetCore.…`
  (which clears the `Warning` filter) **with the exception object attached**, and the Azure Monitor log
  exporter maps a log record carrying an exception into the `exceptions` table.
- **`traces`** — every `LogWarning`/`LogError` the application writes, correlated to the failing request by
  `operation_Id`, with `cloud_RoleName` distinguishing the five APIs from the Functions host.
- **`requests`** — per-route status, duration and result code; **`dependencies`** — outbound HTTP
  (Stripe, SendGrid, Mapbox, Blob).
- **Live Metrics** — a real-time failure feed during a demo, at no ingestion cost.
- **The `exceptions/count` metric alert starts being able to fire for an API**, which it never could
  before. It still needs 26 in 15 minutes.
- **Not** the container's raw stdout: `appService.bicep` still configures no diagnostic settings, so a
  crash *before* the OTel pipeline starts is still only visible on `az webapp log tail`. Said so in the
  docs rather than letting the new coverage imply otherwise.

`docs/architecture/infrastructure.md` was updated with all of it — the per-host table now reads yes/yes/
yes/yes/yes/no/yes, "Why the APIs send nothing" became "How the APIs reach App Insights", the
platform-metrics table keeps its (still true) rows and its `Server exceptions` row is corrected, and a new
**Volume and cost** section carries the sampling reasoning including the measured health-probe finding.
The doc states explicitly that everything takes effect on the next deploy, not on merge.

**Three other pages linked into that section and each carried a claim my change falsifies.** Leaving them
would have re-created exactly the defect T-0501 fixed, so all three were corrected in the same pass:

- `docs/architecture/frontend.md` — *"a browser exception is reported to nothing. The same is true
  server-side"*. The second half is now false; the gap is browser-only.
- `docs/deployment/azure-setup.md` — *"the Functions host is the only telemetry producer … the five APIs
  and the SSR host send nothing"*, in the **cost** section. That is the claim most likely to mislead the
  owner about the bill, so it now names the change, keeps the SSR exception, and says plainly that the
  daily cap is a breaker rather than a budget.
- `docs/deployment/environment-config.md` — *"Sentry is not collecting anywhere right now"* is **still
  true** and stays; what was corrected is the implication that this means no server-side error tracking,
  plus a stale `AZURE-DEV-RUNBOOK.md:239` line citation into a line I moved.

VitePress `npm run build` after all of it: **exit 0** (it fails on dead links, so the new
`#volume-and-cost` anchor is validated too).

### AC1 / AC7 (Gate 0.5 leg 3) — repo vs live vs relayed

- **Measured by me, in the repo:** every row of the Gate 0 table; that no `ASPNETCORE_ENVIRONMENT` is set
  anywhere in `deploy/`, the workflow or the Dockerfiles; that `appsettings.Production.json` ships in the
  publish output; that `UseAzureMonitor` registers the log provider; that the health-probe filter orphans
  the dependency span; the three test suites; the VitePress build.
- **Relayed, NOT measured:** that `Sentry--Dsn` is unset in the dev Key Vault — **the owner's answer**, per
  the brief. I have no Azure access (`az` is not installed on this machine), so the live half of AC1 and
  the whole of any cost measurement are **owner steps**. I did not check a deployed app setting, a Key
  Vault entry, or a GitHub secret, and nothing here should be read as if I had.
- **Predicted, not observed:** that telemetry will appear in `appi-cleansia-weu-dev`. The registration is
  pinned by tests; that the exporter then reaches Azure over the network is what the new §9 smoke item is
  for. **Nobody should mark this ticket done on my say-so — mark it done on that KQL query returning rows.**
- **Owed measurement (mirrors T-0499 AC5):** ≥7 days after the deploy, run
  `Usage | where TimeGenerated > ago(7d) | summarize sum(Quantity) by DataType` on the workspace. If
  `AppMetrics` dominates, the lever is the metric export interval, not sampling. I am asserting no euro
  figure here.

### Catalog-edit routing — searched, and NOT edited inline

There is a candidate entry: *"a registration on a code path nothing calls is invisible to every test of
that registration — pin the CALL SITE, not the method."* I applied the `conventions.md` §"Who ratifies a
catalog edit" test rather than writing it.

- **Test 2 search (the floor):** `agents/knowledge/*.md` + `roles/*.md` for `call site`, `unreachable`,
  `dead code`, `never called`, `overload`, `reachab`, `wired`. It returned a governing sentence:
  `patterns-backend.md:1177` — *"Dead code that asserts a safety net is the same defect at class scope …
  a resident class is read as a live guarantee."* That governs my subject (code that exists, is called by
  nothing, and is read as a live guarantee) at a more general level; my entry would sit inside its scope.
  **Both readings recorded** as the section requires: read narrowly it is about *classes* and a *safety
  net*, and an exporter is neither, which would make mine a first statement; read as the catalog's general
  rule about resident-but-uncalled code, it reaches the exporter directly. I do not think that
  disagreement should be settled by me.
- **Test 1 fires anyway, which decides it.** The entry would oblige call sites — anyone registering
  services in a shared project would owe a call-site test — and I ran no sweep establishing a zero
  baseline, because there plainly is not one (`Cleansia.Config` and `ServiceDefaults` carry other
  registrations with no such pin). A constraining entry with a non-zero baseline needs a deviation entry
  and a canonicalization ticket, neither of which I can file for myself.

**→ Routed to the Architect. No file under `agents/knowledge/` was touched.**

### Verification

**Master moved under this ticket, so the before/after needs two anchors rather than one.** Baselines were
measured locally before any edit at HEAD **`6a901ed0`**; three commits then landed on `master` mid-ticket
(`f837e0ec` docs, `0580cb4e` the device-token redaction fix, `29d02cb0` a test-container migration), which
is why the final unit figure is not baseline + 5.

| Suite | Baseline @ `6a901ed0` | Repo @ `29d02cb0`, mine excluded | Final, mine included | Exit |
|---|---|---|---|---|
| `Cleansia.Tests` | **3146** | **3174** | **3179** | 0 / 0 |
| `Cleansia.IntegrationTests` | **144** | — | **144** | 0 / 0 |
| `Cleansia.HostTests` | **135** | — | **135** | 0 / 0 |

The middle column is measured, not inferred: `--filter "FullyQualifiedName!~AppInsightsExporterWiringTests"`
→ **3174**, and the same filter with `~` → **5**. **3174 + 5 = 3179**, so all five of mine ran and nothing
else moved.

**Build executed, and the earlier evidence for that was weak — replaced.** My first post-change build
reported `0 Warning(s)` in 7–23 s, which I initially read as proof of a real compile; it is not, because an
incremental build still prints each project's `-> …dll` line without recompiling. The honest evidence is a
forced full compile: `dotnet build Cleansia.Api.sln --no-incremental -v m` → **`0 Error(s)`, 226
Warning(s), `Time Elapsed 00:00:26.37`, exit 0**. Every one of those 226 is a pre-existing analyzer warning
in other files (`xUnit2029`, `CS8600`, `CS9113`, …); **grepping the build log for my two files returns no
warning at all**. VitePress `npm run build`: exit **0**.

**What is pinned, and the proof it is not a test that passes regardless.**
`src/Cleansia.Tests/Configuration/AppInsightsExporterWiringTests.cs`, five tests, each asserting a
different leg of the chain — because a test that merely called the extension method would have been green
throughout the outage:

1. `TheOverloadTheApiHostsUseReachesTheExporter` — the app setting name is parsed **out of `main.bicep`**
   (the `RepoPath` walk-to-`.sln` idiom `QueueListenerInventoryTests` established), fed as the config key,
   and followed to `IOptions<AzureMonitorOptions>.Value.ConnectionString`. So the key the code reads and
   the key the deployment sets are asserted to be the same key, across trees.
2. `TheHostApplicationBuilderOverloadReachesTheSameExporter` — anti-drift.
3. `WithoutAConnectionStringNoExporterIsRegistered` — no `IConfigureOptions<AzureMonitorOptions>`
   descriptor at all when unconfigured, so a laptop and the test hosts stay off the wire.
4. `TheExporterBringsTheLogPipelineAndNotOnlyTraces` — an `ILoggerProvider` appears iff the exporter does.
5. `TheStartupEveryApiHostRunsReachesTheExporter` — runs the **real `CleansiaStartupBase.ConfigureServices`**
   through a host-shaped subclass. This is the leg the defect actually lived in.

**Mutation 1 — delete the call from the `IServiceCollection` overload.** RED: 3 failed, 2 passed. The two
that stayed green were the absence test and — *the point* — `TheHostApplicationBuilderOverloadReachesTheSameExporter`.
**That is a live demonstration of the original bug: a green test on the overload nobody runs.**
Restored byte-exact → 5/5 green.

**Mutation 2 — rename the config key the code reads** (`APPLICATIONINSIGHTS_` → `APPINSIGHTS_`). RED: 4
failed, 1 passed (the absence test, correctly). Proves the cross-tree key pin bites rather than decorating.
Restored byte-exact → 5/5 green.


### Shared-file contamination REPORTED, not touched — and the false red it produced

Two backend files outside my lane appeared modified mid-ticket (21:25/21:26, after my 21:17 baseline):
`src/Cleansia.Web.Partner/Middleware/RequestLoggingMiddleware.cs` (adding `deviceToken` to
`SensitiveFieldRegex`) and `src/Cleansia.Tests/Logging/RedactionUnmaskedFreeTextGuardTests.cs` (adding
`Platform` to the allow-list). **Per `agents/process/shared-file-lanes.md` I ran no
`git restore`/`git checkout`/`git reset` on anything, at any point.** That lane has since committed them
as `0580cb4e`, so the working tree is clean of them; the only uncommitted files are mine.

**It cost one test run, and that is worth recording rather than hiding.** My first post-change `HostTests`
pass reported **44 failed** — all with the same message,
`Can't find '…/Cleansia.HostTests/bin/Debug/net10.0/Cleansia.Web.Partner.deps.json'`, thrown from
`WebApplicationFactory.EnsureDepsFile()` **before any application code ran**. A concurrent `dotnet test`
from the other lane was rewriting the shared output directory mid-run (25 `dotnet` processes at the time;
the file was present again seconds later). It is a build-artifact race, not a behavioural failure — and
independently my change cannot reach that suite: `HostTests` configures no connection string, which is
exactly the condition `WithoutAConnectionStringNoExporterIsRegistered` pins, so no exporter registers
there at all. Re-run serially once the tree was quiet: **135 passed, 0 failed, exit 0**, twice.

Two process notes for whoever hits this next, since both cost me time:

- **A serialization wait keyed on `ps | grep 'dotnet test'` does not converge** in a repo with several
  live lanes; mine waited indefinitely and had to be killed. Build-then-run immediately, and re-run on the
  `deps.json` signature rather than trying to schedule around other agents.
- **`0 Warning(s)` from `dotnet build` is not evidence a compile happened.** An incremental build still
  prints each project's `-> …dll` line. Use `--no-incremental` when the claim matters.
