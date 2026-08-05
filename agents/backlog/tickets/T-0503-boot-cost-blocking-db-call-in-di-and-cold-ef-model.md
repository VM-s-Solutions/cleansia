---
id: T-0503
title: Boot cost — a blocking DB call with a 15s timeout runs during DI registration, and the EF model is built on the first request
status: in_review
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the cold-start / deploy investigation (2026-08-02).** Two app-side boot costs, both **€0**
to fix, both independent of the infrastructure half (**T-0502**).

**Status: RELAYED from the investigation, NOT re-verified by the PM.** AC1 re-establishes both before
either is changed.

### The two findings

**(a) A blocking database call, with a 15-second timeout, inside DI registration.**
DI registration runs on the startup path *before* the host can serve anything. A synchronous DB call
there means:
- **every cold start pays the round trip**, and on an unloaded B2 that is on top of the runtime's own
  start;
- if the database is slow or unreachable, **startup blocks for up to 15 seconds and then proceeds
  with whatever the failure path does** — so a transient DB blip becomes a 15-second outage on every
  instance that happens to be starting;
- **it runs five times, once per API host**, and again for the Functions host.

**The generalizable rule, and it is worth stating because it is the reusable part:** *DI registration
composes the object graph; it must not perform I/O.* Anything needing data at startup belongs in an
`IHostedService` / `StartupTask` that the health check can gate on — which makes the readiness
explicit instead of hidden inside a `services.Add…` call.

**(b) The EF model is compiled lazily, so the first request after every cold start pays for it.**
EF Core builds the model — entity configurations, relationships, query filters — on first use. This
codebase has a **large** model with **global query filters for multi-tenancy** on many entities, so
that build is not trivial. Every cold start hands the first user a request that includes it.

The fix is a **warm-up at boot** (touch the model / force `DbContext.Model` once from a background
startup task), which moves the cost off the first user's request. It composes with **T-0502**: Always
On stops the unload; this stops the first request after an unavoidable start (a deploy, a scale
event, a platform restart) from being the slow one.

## Acceptance criteria

- [ ] **AC1 — both findings are RE-ESTABLISHED at file:line before either is changed.** For (a): the
      registration, the call, the 15s timeout, and **which hosts execute it**. For (b): confirm the
      model is not already warmed somewhere. **If either does not reproduce, say so — a partial close
      is a valid outcome.** Evidence: the file:line trace for each.
- [ ] **AC2 — the blocking call is MOVED, not deleted.** It exists for a reason and AC1 must state
      what depends on its result. Move it to a startup task / hosted service / lazy accessor so the
      **object graph composes without I/O**. Evidence: the diff plus the named consumer.
- [ ] **AC3 — the failure behaviour is now EXPLICIT and is stated.** Today a DB blip costs 15
      seconds and then something happens that nobody has written down. After: does the host fail
      readiness, serve degraded, or retry? **State it, and make the health endpoint reflect it** —
      there is a health endpoint on this platform already (`T-0437` shipped one for Functions).
      Evidence: the stated behaviour plus the health-check wiring.
- [ ] **AC4 — the timeout is justified or changed.** 15 seconds is a long time to block a process
      start. If it survives the move, say why that number. Evidence: the sentence.
- [ ] **AC5 — the EF model is warmed at boot, off the request path.** Background/startup, **not
      blocking readiness** unless AC3's ruling says it should — warming a model synchronously at boot
      converts a slow first request into a slow start, which is the same cost in a different place.
      Evidence: the diff plus the reasoning for blocking vs background.
- [ ] **AC6 — both improvements are MEASURED, separately.** Time-to-first-successful-response on a
      cold host: baseline, after (a), after (b). **Separately, because if one of them is worth 200ms
      and the other 4 seconds, the next person needs to know which.** Evidence: the three timings ×3
      samples.
- [ ] **AC7 — all five API hosts get the fix, or the ones that do not are named.** `Cleansia.Web.Partner`,
      `Cleansia.Web.Admin`, `Cleansia.Web.Customer`, `Cleansia.Web.Mobile.Customer`,
      `Cleansia.Web.Mobile.Partner` — plus `Cleansia.Functions`. **Sprint-14's lesson on this exact
      shape:** the five copies of `RequestLoggingMiddleware.cs` are one logical change where
      *"four-of-five is a hole"*, and their line offsets are **not uniform**. If the registration is
      shared (`Cleansia.Config`), one edit covers all — **say which it is.** Evidence: the host list
      with a per-host verdict.
- [ ] **AC8 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** For (a), an assertion
      that DI registration performs no I/O (e.g. building the service provider against a
      guaranteed-unreachable connection string completes fast and does not throw). Prove it fails
      today. Evidence: the red run, then green.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**. `HostTests` is the suite most likely to move here —
      it boots hosts.

## Out of scope

- **Always On, the warm probe, the deploy double-restart** — **T-0502**. Different layer, different
  files, no dependency in either direction. **They compose: T-0502 keeps the host up; this makes it
  come up faster.**
- **Rewriting DI registration generally.** Only the I/O comes out.
- **EF compiled models / AOT.** A bigger change with its own trade-offs. If AC6 shows the model build
  dominates, **name it as a follow-up** rather than starting it.
- **Changing the database, the connection pool or the plan SKU.**

## Implementation notes

**No panel needed for the mechanics** — moving I/O out of DI registration is a well-established
correction, not a design decision. **But AC3 IS a decision** (what a host does when its startup data
is unavailable) and it is written as a forced ruling inside the ticket. **If the answer turns out to
be "the host cannot function at all without this data", stop and escalate** — that is a readiness/
availability design question and it deserves the architect.

**Gate 6.5 applies** — startup/DI is spine. `routing.md` rule 7: behavioural non-stub plus an
end-to-end test driving the real path.

**Read first:** `Cleansia.Config/` (the shared DI registration — AC7 turns on whether this is one
file or five), `Cleansia.ServiceDefaults/Extensions.cs`, each host's `Program.cs`, and
`Cleansia.Infra.Database`'s DbContext + entity configurations for the model size.

**Shared-file lane:** if the fix lands in `Cleansia.Config`, that is a file every host depends on —
check for other sprint-15 writers before starting.

## Status log
- 2026-08-05 — **in_review (backend).** **Finding (a) did not reproduce** — `2012b014` had already made the
  blocking probe opt-in and only the Functions worker opts in, so AC2/AC4 became verification + a guard
  rather than a move. **Finding (b) did reproduce and is the shipped work:** an EF model warm-up, measured at
  **1.7–3.0 s** off the first request. A test asserting the warm-up "does not block startup" was written,
  **proved fake** (`BackgroundService` on .NET 10 never runs `ExecuteAsync` inline), and deleted along with
  the `Task.Yield()` whose stated purpose it disproved. AC6 partially open (no cold-host timing without a
  deploy); AC3's Kestrel-ordering claim is reasoned, not run.
- 2026-08-02 — **draft (created by pm from the cold-start / deploy investigation).** **Both findings
  marked RELAYED, not PM-verified** — AC1 re-establishes each, and a partial close is permitted.
  Filed separately from **T-0502** because the two halves are different layers with different
  reviewers and different failure modes, and because the infrastructure half needs a deploy to take
  effect while this half is testable locally.

## Review

### Gate 0 — finding (a) DOES NOT REPRODUCE. It was fixed before this ticket was picked up.

`2012b014` (*"feat: the owner's remark list … cost and cold start"*, 2026-08-02 17:31, on `master`) already
moved the blocking call. At `c15e295e`:

- `Cleansia.Config/Database/DbContextBindingExtensions.cs:22-26` — `AddDbContextBindings(…, bool
  eagerlyReloadNpgsqlTypeCatalog = false)`. The blocking probe is `TryEagerlyReloadTypeCatalog` (`:69-82`),
  reached only from `:56-59` behind that flag.
- `Cleansia.Config/CoreExtensions.cs:26-37` — same flag, same default, forwarded.
- **The only caller that passes `true` is `Cleansia.Functions/Program.cs:36-37`.** The five API hosts call
  `AddCoreBindings(configuration, env)` with two arguments —
  `Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:26`, `Web.Admin/…:26`, `Web.Customer/…:28`,
  `Web.Mobile.Partner/…:28`, `Web.Mobile.Customer/…:31`.

So the ticket's *"it runs five times, once per API host, and again for the Functions host"* is **false at
HEAD**: it runs once, on the Functions worker, deliberately. **AC2 is already discharged; nothing was moved
by this ticket.** What was missing is a guard — the opt-in is an optional argument, so a flipped default or
one host passing `true` would have been invisible. That guard is AC8 below.

### AC1 — finding (b) DOES reproduce, and is the real work

No warm-up existed anywhere: no reader of `DbContext.Model` outside a request, no `IHostedService` touching
it. Confirmed by search across `Cleansia.Config`, `Cleansia.Infra.Database`, all five hosts,
`Cleansia.Functions` and `Cleansia.ServiceDefaults`. The model is 68 entity types (68 files in
`Infra.Database/EntityConfigurations/`, 64 `DbSet`s) with tenant query filters applied in
`CleansiaDbContext.OnModelCreating:115-127`.

### AC5 — the warm-up

`src/Cleansia.Config/Database/EfModelWarmupService.cs`, registered in
`DbContextBindingExtensions.AddDbContextBindings` — **one edit, all six hosts** (AC7: the registration is
shared, so there is no five-copy problem here; contrast `RequestLoggingMiddleware`).

Background, not blocking readiness, as AC5 requires. Three properties, in the doc-comment and pinned:

1. **No I/O.** `DbContext.Model` is metadata only — it runs to completion with Postgres down, which is what
   keeps it off the readiness path.
2. **Registered BEFORE `NpgsqlTypeCatalogInitializer`, and that order is load-bearing.** Hosted services
   start sequentially and the initializer awaits a retry loop of up to ~2 minutes
   (`NpgsqlTypeCatalogInitializer.cs:27-77`) while another host migrates. Behind it the warm-up would not
   begin until exactly the slow boot it exists to help was over.
3. **Best-effort.** On failure the first request builds the model as it does today. That is why *this* one
   may be fire-and-forget where the type-catalog reload beside it may not: the reload's result is a
   correctness precondition, this one's is an optimisation.

### AC6 — MEASURED, separately

**(a) is worth ~0 ms at HEAD** — it was already fixed, and on the API hosts the probe no longer runs at all.
Measured composition cost of the whole API graph with an unreachable database: **105 / 123 / 129 ms** — no
connect attempt, so no timeout term. With the opt-in on, the same composition blocks for the Npgsql connect
(15 s default; the ticket's number is correct).

**(b) is worth 1.7–3.0 s.** First `DbContext.Model` access, three runs in fresh processes, 68 entity types:
**2796 / 3011 / 1742 ms**. Second access in the same process: **13 / 31 / 4 ms**. So the warm-up moves
~2 s off the first request after every cold start.

**Both figures are Debug, macOS/arm64, in a unit-test process — not the deployed B2.** They establish the
order of magnitude and the ratio, not the deployed number. Time-to-first-successful-response on a cold host
**was not measured**: it needs a deploy, and no runtime signal reaches me (see the observability note
below). That AC is **partially open** and honestly so.

**Follow-up named rather than started, per "Out of scope":** at ~2 s, the model build now dominates boot,
so an EF **compiled model** (`dotnet ef dbcontext optimize`) is the next lever. Not started — it is a
build-step + regeneration-discipline change with its own trade-offs.

### AC3 — the failure behaviour, stated

With Postgres unreachable at boot, at HEAD plus this change, an API host:

1. **composes and starts** — no I/O in registration (pinned);
2. **warms the model anyway** — metadata only, no connection (pinned);
3. **retries the type-catalog reload in the background** for ~2 min, then logs one Warning and gives up
   (`NpgsqlTypeCatalogInitializer.cs:50-54`) — it does not fail the host;
4. **fails readiness while the database is down.** `/health` runs `AddDbContextCheck<CleansiaDbContext>`
   (`Cleansia.Config/Services/ReadinessHealthChecks.cs:26-28`) and reports **Unhealthy** → non-200;
   `/alive` keeps only the `self` check (`Cleansia.ServiceDefaults/Extensions.cs:177-185`). App Service
   polls `/health` (`appService.bicep:37` — `healthCheckPath` defaults to `/health`), so the instance is
   routed around and recycled rather than serving empty screens.

So: **serve-and-fail-readiness, with the DB probe on the request path where it belongs.** No new wiring was
needed — the health split already existed; this AC is a statement, and the statement is now true because the
boot path no longer has an opinion about database reachability.

*Unverified:* that Kestrel is listening during step 3 rests on `GenericWebHostService` being registered by
`ConfigureWebHostDefaults` (`Web.Partner/Program.cs:12-18`) ahead of `Startup.ConfigureServices`, i.e. on
hosted-service registration order. That is framework semantics I reasoned about, **not something I ran**.

### AC4 — the 15 s timeout: JUSTIFIED, not changed

It is Npgsql's default connect timeout and nobody chose it. It survives because:

- it now applies to **one** host, not six, on a path that is already best-effort and swallowing;
- shortening it means either putting a shorter `Timeout` on the Functions worker's shared data source —
  changing every runtime connect on that host, blast radius beyond the boot probe — or bounding the wait and
  proceeding, which **reinstates the exact race the probe exists to close** (an isolated-worker trigger
  firing before `IHostedService` start completes, `DbContextBindingExtensions.cs:43-55`);
- with no error tracking on the Functions host, a connect-timeout change is a runtime behaviour change I
  could not observe. Given the choice between an unverifiable improvement and a verified scope reduction, I
  took the scope reduction and left the number.

### AC8 — the guard, red against the pre-fix shape

`src/Cleansia.Tests/Startup/BootDatabaseIoTests.cs`. The probe is a loopback listener that **accepts and
immediately closes**, so a connection attempt is *counted* rather than timed — the assertions are about
whether a socket opened at all, not about how long anything took, so they do not flake on a loaded box.

| Test | Pins |
|---|---|
| `ComposingAnApiHostGraphOpensNoDatabaseConnection` (×5, driven through each host's real `AddServices`) | AC7 + AC8 |
| `EveryApiHostIsCovered` | the host list is complete — four-of-five is a hole |
| `ComposingTheFunctionsWorkerGraphDoesOpenOne` | non-vacuity: the probe *can* observe a connect |
| `ModelWarmUpBuildsTheModelWithoutOpeningAConnection` | AC5 property 1 |
| `WarmUpIsABackgroundServiceRegisteredAheadOfTheRetryingInitializer` | AC5 property 2 |
| `TheBuiltModelIsSharedAcrossScopes` | the premise the warm-up rests on |

**Mutation-proved, three of them:**

- default `eagerlyReloadNpgsqlTypeCatalog` → `true`: **5 failed** (all five host cases).
- registration order swapped: **red** — *"warm-up at 1, initializer at 0"*.
- `CanConnect()` added to the warm-up: **red** — *"Expected: 0, Actual: 2"*.

### A test I wrote, proved fake, and deleted — the framework already guarantees it

I first wrote `ModelWarmUpDoesNotBlockHostStartup`, gating the warm-up behind a `ManualResetEventSlim` and
asserting `StartAsync` returns anyway. It passed. It also passed with `await Task.Yield()` deleted, and
**with `Thread.Sleep(3000)` at the top of `ExecuteAsync`**.

Cause, established directly: on **.NET 10.0.9**, `BackgroundService.StartAsync` does not run `ExecuteAsync`
inline. Measured — `StartAsync` returned in **0 ms**, caller thread **11**, `ExecuteAsync` thread **17**.

Consequences, both taken:

- **The test was deleted, not weakened.** "The warm-up does not block startup" is unfalsifiable for any
  `BackgroundService` on this runtime; keeping it would have been a green tick for a property nothing here
  provides.
- **`await Task.Yield()` was removed from the warm-up.** Its stated purpose was false, and a line whose
  comment claims a guarantee it does not provide is worse than its absence. What replaced it is the
  assertion that the type stays a `BackgroundService` — because that *is* where the guarantee lives, and
  converting it to a plain `IHostedService` would silently make boot 2 s slower with nothing to catch it.

### Gate 0.5 — suites

Baselines re-measured locally at `c15e295e` before any edit: **3129 / 144 / 135**, all exit 0 — the ticket's
header figures (2295 / 108 / 75) are stale. After: **3146 / 144 / 135** (+17 unit: 10 here, 4 in
`DeployWarmProbeCoverageTests`, 3 in `QueueListenerInventoryTests`). `HostTests` did **not** move, which is
worth noting since the ticket predicted it would — the hosts it boots now also run the warm-up, and it is
invisible to them by design.

### Observability — one correction to the briefing, scoped to what I checked

The OpenTelemetry half of the briefing is confirmed: `ConfigureOpenTelemetry()` → `AddOpenTelemetryExporters`
is reachable only from the `IHostApplicationBuilder` overload (`ServiceDefaults/Extensions.cs:20-22`), and
the hosts call the `IServiceCollection` overload (`CleansiaStartupBase.cs:138` → `Extensions.cs:38`). Dead
path.

**But Sentry is separately wired and is not on that path.** All five API hosts call `UseSentryMonitoring()`
(each `Program.cs:16` → `Extensions.cs:85-111`), and `Sentry__Dsn` is provisioned from Key Vault in
`main.bicep:467`. Whether exceptions are actually captured therefore turns on **one owner-owned fact I
cannot see: is the `Sentry--Dsn` secret populated in the dev Key Vault?** If yes, the five APIs do report;
if it is blank the SDK is deliberately left uninitialised (`Extensions.cs:88-96`). The **Functions host has
no Sentry wiring at all** (`Cleansia.Functions/Program.cs`) — it has only Application Insights. I verified
the wiring, not the secret.
