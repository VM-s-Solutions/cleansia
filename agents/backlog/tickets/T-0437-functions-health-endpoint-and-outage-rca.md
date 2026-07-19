---
id: T-0437
title: "Functions host /api/health endpoint + HealthCheckStatus alert + startup-graph guard (dev outage RCA)"
status: done
size: M
owner: backend
created: 2026-07-19
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
priority: high
manual_steps:
  - "Owner redeploys the Functions host (merge + Deploy to DEV dispatch) — the health endpoint + healthCheckPath + alert reach the dev host only on the next deploy. This is ALSO what recovers the current 503 if it is a stuck deploy/platform state."
source: "T-0320 dev-smoke finding — func-cleansia-weu-dev returns persistent 503; owner asked for a fix + a health check"
---

> The 2026-07-18 dev-smoke found `func-cleansia-weu-dev` returning a persistent HTTP 503 "Application
> Error" (all 5 API hosts + SSR healthy). The owner reported notifications stopped being sent that day
> and that there are no logs. This ticket = the root-cause analysis + the observability the owner asked
> for (a health check that surfaces the next break).

## Root-cause analysis (static — no az / no container logs available)

The committed Functions **startup DI graph is clean** — verified, not assumed:

- Every startup-crash vector was inspected and is safe: the eager DB connection in
  `AddDbContextBindings.TryEagerlyReloadTypeCatalog` is `try/catch`-swallowed; the
  `NpgsqlTypeCatalogInitializer` hosted service swallows all exceptions in `StartAsync`; no config uses
  `ValidateOnStart`/`[Required]`; all `IXConfig` singletons bind lazily; `host.json`/csproj were
  **untouched by #126** (`git show 89791d42`); every queue trigger uses a literal name + the shared
  `QueueStorageConnectionString` connection (unchanged shape).
- New guard test `FunctionsHostStartupGuardTests` composes the SAME graph `Program.cs` builds with the
  deployed app-settings shape (connection strings present, every secret empty) and resolves all 27
  `Cleansia.Functions.Core` handlers + the hosted services + the health check in a scope. **It passes** —
  the committed code, deployed, starts cleanly.

Conclusion: the 503 is **environmental**, not a code regression — a failed/half-applied deploy or Azure
platform state from the 2026-07-18 rollout (a bad zip/container swap, or the migrate step leaving the app
crash-looping). No repo change fixes a stuck Azure host; the fix is a redeploy (the MANUAL_STEP). What a
repo change CAN do — and this ticket does — is (a) make the next occurrence LOUD instead of silent, and
(b) guard against a FUTURE code cause of the same symptom.

## What shipped

1. **`GET /api/health`** (`HealthFunction` HTTP shell → `FunctionsHealthCheck` in `Cleansia.Config/Health`,
   anonymous) — probes the database (`CanConnectAsync`) + queue storage (`QueueServiceClient.GetProperties`),
   returns 200 when both pass, 503 with the failing probe named (S6: probe detail is the exception TYPE
   only, never a message that could carry a connection string). Added the `Microsoft.Azure.Functions.Worker.Extensions.Http`
   package (the host had no HTTP surface before).
2. **`healthCheckPath: '/api/health'`** on the Functions app (functionApp.bicep) — App Service actively
   pings it and **auto-recycles an unhealthy instance** (self-healing for host-up-dependency-down), and it
   emits the `HealthCheckStatus` metric.
3. **`alert-functions-health-*`** metric alert (alerts.bicep) on `HealthCheckStatus < 100`, wired to the
   existing action group — the owner is emailed when the host is unhealthy. On for dev AND prod (this
   outage was dev; the owner wants to know).
4. **`FunctionsHostStartupGuardTests`** — the permanent regression guard: a future change that adds a
   required dependency a handler can't satisfy, or forgets a registration, trips CI instead of a silent
   prod 503.

## Owner steps
- **Redeploy the Functions host** (merge this branch → Deploy to DEV dispatch). This delivers the health
  endpoint/alert AND is the recovery action for the current 503.
- The two failure modes are now both covered post-deploy: worker process down → health endpoint
  unreachable → App Service health monitor trips; worker up but DB/queue down → 503 from the endpoint →
  metric alert.

## Status log
- 2026-07-19 — RCA + health endpoint + healthCheckPath + alert + startup guard shipped; backend suite
  2016/2016; all Bicep compiles (validator 0.45.15). Delivered on `feature/payroll-invoice-paid-notify`.
  Awaiting the owner's redeploy for the fix/observability to reach dev.
- 2026-07-19 (review corrective) — the guard test initially reflection-registered handlers into its OWN
  container, so it caught an unsatisfiable ctor dependency but NOT a handler omitted from Program.cs
  (the reflection re-registered it). Fixed: extracted the handler+background-service list into a shared
  `FunctionsProcessingRegistration.AddFunctionsProcessing()` called by BOTH Program.cs and the test — the
  test now resolves the REAL registration set, so a handler added but not registered fails CI. Adversarial
  review otherwise clean (audit/PII + secret-wiring dimensions: zero findings). 2016/2016.
