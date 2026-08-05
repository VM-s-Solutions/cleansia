---
id: T-0499
title: One file, five values — Functions host.json polls 14 queue listeners every 5s and defeats its own sampling (€35–42/month)
status: in_review
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0015, 0029]
layers: [architect, backend]
security_touching: false
manual_steps: [azure-deploy]
sprint: 15
---

## Context

**Source: the Azure cost investigation (2026-08-02).** The headline it produced: the €49 bill is not
what anyone assumed. **Alerts cost €0.63, not €50.** The request-logging theory was **refuted**. The
driver is `host.json`.

### Ground truth — PM-verified first-hand at `master` `0e4ede1b`

`src/Cleansia.Functions/host.json` — **the entire file is 26 lines** and every cost driver is in it:

```json
"logging": {
  "applicationInsights": {
    "samplingSettings": { "isEnabled": true, "excludedTypes": "Request;Exception" }
  },
  "logLevel": { "default": "Information", "Host.Results": "Information",
                "Function": "Information", "Host.Aggregator": "Trace" }
},
"extensions": {
  "queues": { "maxPollingInterval": "00:00:05", "visibilityTimeout": "00:00:30",
              "batchSize": 1, "maxDequeueCount": 5, "newBatchThreshold": 0,
              "messageEncoding": "base64" }
}
```

**The five values, and what each one does:**

| Value | Effect |
|---|---|
| `"maxPollingInterval": "00:00:05"` | **The default is 60 seconds.** Every queue listener wakes 12× more often than it would out of the box. The investigation counted **14 listeners → ~7.3M polls/month**, each an Application Insights dependency record. |
| `"excludedTypes": "Request;Exception"` | Sampling **is** enabled — and then Requests are **excluded from it**. The highest-volume telemetry type is the one sampling is told not to touch. |
| `"Host.Aggregator": "Trace"` | Trace level on the aggregator, the noisiest host category. |
| `"logLevel.default": "Information"` | Information on everything by default. |
| `"newBatchThreshold": 0` with `"batchSize": 1` | Forces a fresh poll per message rather than batching. |

**So sampling is configured to be on and configured not to engage.** That is the shape of the whole
finding: nothing here is broken, everything here was set deliberately for an early-development
feedback loop, and nobody revisited it once there were 14 listeners.

**The investigation's estimate: €35–42/month, from one file.** RELAYED — the *values* are
PM-confirmed, the *euro figure* is not.

### Two things this ticket must NOT do, both from the investigation

- **Retention cuts save €0.** The workspace is already under the free-tier floor. Do not "also"
  reduce retention — it is motion without saving.
- **All €49 is a dev sandbox with no users. Prod has never been deployed.** So this is not a
  production cost problem; it is paying real money to watch an idle system. That framing matters for
  AC2: the right polling interval for a dev sandbox is not the right one for prod.

## Acceptance criteria

- [ ] **AC1 — the cost attribution is RE-DERIVED from Azure, not inherited.** Run the cost/usage query
      that attributes the bill by resource and by telemetry type, and paste the result. **If the
      driver is not what the investigation says, stop and re-file.** *(The specific queries are on the
      owner-decision list; if the developer has no portal access, the owner runs them and this AC is
      discharged with their output.)* Evidence: the query and its result.
- [ ] **AC2 — `maxPollingInterval` is set with a stated latency budget, per environment.** 60s is the
      platform default; anything lower is a **latency purchase** and must name what it buys. What is
      the worst acceptable delay between a message landing and its function running, **for dev** and
      **for prod**? If the two answers differ, the file must be able to express that — and if it
      cannot, say so and state which one wins. **Note `AutoCancelStaleRecurringOrders` and
      `SendRecurringOrderReminders` are schedule-driven, not latency-sensitive**; check whether any
      listener genuinely needs sub-minute pickup before paying for all 14. Evidence: the budget, the
      value, and the per-listener check.
- [ ] **AC3 — sampling is made to actually engage.** `excludedTypes: "Request;Exception"` is
      revisited. **Keep `Exception` excluded** — losing exceptions to sampling is how an outage
      becomes invisible, and this platform is about to have *less* error tracking than anyone thought
      (**T-0500**). Requests are the volume. State the new setting and the expected reduction.
      Evidence: the diff plus the stated expectation.
- [ ] **AC4 — log levels are lowered with a stated floor.** `Host.Aggregator: Trace` and
      `logLevel.default: Information` both come down. **State what is still visible afterwards** —
      specifically, can you still tell that a function ran, and that it failed? A cost fix that
      blinds the only observability the platform has is a bad trade. Evidence: the new levels plus
      the visibility statement.
- [ ] **AC5 — the saving is MEASURED after the change, not asserted.** Re-run AC1's query at least 7
      days after the change and record before/after. **This AC is deliberately allowed to stay open
      past merge** — record it as an owed measurement on the ticket rather than closing on a
      prediction. Evidence: the two figures.
- [ ] **AC6 — no functional behaviour changes.** All 14 listeners still fire. The three Functions
      smoke suites (`MaterializeRecurringBookingsHandlerSmokeTests`,
      `SendRecurringOrderRemindersHandlerSmokeTests`, `AutoCancelStaleRecurringOrdersHandlerSmokeTests`,
      plus `SendMembershipLifecycleNotificationsHandlerSmokeTests` — PM-listed from
      `src/Cleansia.Tests/Functions/`) stay green. Evidence: the run.
- [ ] **AC7 — retention is explicitly NOT touched, and the reason is written down.** So the next
      person to look at this bill does not spend a day on it. Evidence: the sentence in `## Review`.
- [ ] **AC8 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**. **Leg 1:** the evidence here is a config change and
      a billing measurement — say so under leg 3; there is no mutation for a JSON value.

## Out of scope

- **App Insights on the five APIs.** They have **no exporter at all** — PM-verified, and it is
  **T-0501**'s decision. This ticket touches the **Functions** host only.
- **Sentry** — **T-0500**.
- **Retention.** AC7.
- **Cold start / Always On / deploy restarts** — **T-0503**, **T-0504**. Different investigation,
  different files, €0 each.
- **Anything in the Azure portal.** No agent changes cloud configuration. This ticket changes a file
  in the repo that a deploy applies.

## Implementation notes

**A short architect panel, and it is proportionate rather than ceremonial:** AC2's latency budget and
AC4's visibility floor are genuine trade-offs (money vs latency, money vs observability) and the wrong
answer is silently invisible. Author + 2 challengers + lead. **The challenge to expect:** *"just set
everything to the defaults"* — the counter is AC2's per-listener check, since one genuinely
latency-sensitive listener would make a blanket 60s wrong.

**Read first:** `deploy/AZURE-DEV-RUNBOOK.md`, `deploy/bicep/modules/functionApp.bicep`, **ADR-0015**,
and `src/Cleansia.Functions/Functions/` for the listener inventory (AC2 needs the real count — the
investigation says 14).

**This is the highest euro-per-line-changed item in the entire backlog: one file, five values.**

## Status log
- 2026-08-05 — **in_review (backend).** **All five values were already changed** by `2012b014` and are
  pinned by `HostJsonTelemetryCostTests`; this pass re-verified them, corrected one premise
  (`newBatchThreshold`/`batchSize` are throughput, not idle-cost, settings), and **derived the listener
  count from the triggers instead of trusting it** — 14, confirmed. That count surfaced a real drift:
  `storage.bicep` provisioned **12 queues for 14 listeners**, missing `live-activity-dispatch` and its poison
  companion despite its own comment claiming it mirrors `QueueNames.cs`. Fixed and pinned.
  **`manual_steps: [azure-deploy]`** added — a new token; the Bicep queue addition needs a `Deploy to DEV`
  to apply (nothing depends on it: both producer and listener create the queue at runtime). AC1 and AC5
  remain **owner steps** and AC5 is an **owed measurement** — the change has been live since 2026-08-02, so
  the ≥7-day window is already open.
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **The five values were
  PM-verified first-hand** — `host.json` was read in full and every claim in the investigation about
  its contents is accurate, including the sampling-configured-not-to-engage shape. The **€35–42
  figure is RELAYED** and AC1 re-derives it. Two of the investigation's negative findings are written
  into the ticket as guardrails (retention saves €0; the alert theory is dead) so nobody re-derives
  them at cost.

## Review

### Gate 0 — the five values were ALREADY CHANGED. This ticket is mostly a re-verification.

`2012b014` (2026-08-02 17:31, on `master`) rewrote `host.json`. At `c15e295e` the file is:

| Ticket's finding | At HEAD | AC |
|---|---|---|
| `maxPollingInterval: 00:00:05` | **`00:00:30`** (`host.json:19`) | AC2 done |
| `excludedTypes: "Request;Exception"` | **`"Exception"`** + `maxTelemetryItemsPerSecond: 5` (`:7-8`) | AC3 done |
| `Host.Aggregator: "Trace"` | **absent** — inherits the default (`:11-15`) | AC4 done |
| `logLevel.default: "Information"` | **`"Warning"`** (`:12`) | AC4 done |
| `newBatchThreshold: 0` + `batchSize: 1` | **unchanged** — see below | AC2, deliberately |

The reasoning already lives in `Cleansia.Tests/Functions/HostJsonTelemetryCostTests.cs` (four tests, one per
value), which is the right home: `host.json` admits no comments.

**One premise in the ticket's table is wrong and should not be re-derived at cost.** `newBatchThreshold: 0`
with `batchSize: 1` is listed as a cost driver — *"forces a fresh poll per message rather than batching"*.
That is a **throughput** setting, not an idle-cost one. The bill described here is an **idle** system's:
polls against **empty** queues, which retrieve nothing and are unaffected by batch size. Changing it would
alter concurrency (today: strictly one message at a time) for no telemetry saving. Left alone, on purpose.

### AC1 / AC5 — OWNER STEPS, not dischargeable here

I have no Azure portal or `az` access. The cost attribution and the ≥7-day before/after both need the
portal. **AC5 is an owed measurement and is the more valuable of the two**, because the change has already
been live since 2026-08-02 — so the "after" window is open now, and the "before" is whatever the July
invoice says. The **€35–42/month figure remains RELAYED and unverified by me.**

### AC2 — the latency budget, per environment and per listener

**The real count is 14, and it is now derived rather than asserted.** `QueueListenerInventoryTests`
(new) reflects over every `[QueueTrigger]` in `Cleansia.Functions/Functions/`: **7 live + 7 poison = 14**.
The investigation's number is correct.

**Per-listener check — nothing here is sub-minute-critical:**

- **7 of the 14 are poison companions.** They dead-letter and are irrelevant to alerting latency: the alert
  fires on the storage `PutMessage` log row, not on the consumer
  (`deploy/bicep/modules/queueAlerts.bicep:70-101`, `evaluationFrequency`-driven). **The briefing's concern
  that a slower poison consumer blunts the alert does not apply** — the alert would fire at the same time if
  the poison consumers did not exist. Their pickup speed only affects how fast forensics land.
- **The two a user can perceive are `notifications-dispatch` (push) and `live-activity-dispatch` (iOS lock
  screen).** Both are produced through the **outbox**, not enqueued directly —
  `LiveActivityProducer.cs:35-49` calls `pendingDispatch.Enqueue`. So this ceiling is the *second* of three
  legs: drainer tick ≤10 s + this backoff ≤30 s + handler ≈ **~40 s worst case**, matching the figure
  `patterns-backend.md` already publishes. Typical is well under, since the ceiling is only reached by a
  queue that has been idle.
- The schedule-driven work (`AutoCancelStaleRecurringOrders`, `SendRecurringOrderReminders`, and 11 more) is
  on **timer** triggers and never touches a queue listener.

**Budget: ~40 s to a lock screen on a fully idle system is accepted for dev.** A Live Activity that updates
40 s after the cleaner taps *On the way* is the one place this is arguable, and it is **named for the owner
rather than silently bought back** — halving the interval re-buys the telemetry cost this ticket exists to
remove.

**Per environment: the file CANNOT express it, and here is the shape that can.** `extensions.queues` is
host-global — there is no per-queue override — and one `host.json` ships to every environment. The
override mechanism is the app setting
`AzureFunctionsJobHost__extensions__queues__maxPollingInterval`, which **nothing sets**. Since dev is the
only environment ever deployed, **the single value is dev's, and dev wins by default.** A prod that wants
faster pickup buys it with that app setting on the Functions app, not by editing this file. All of this is
now written into `HostJsonTelemetryCostTests.QueuePollingIntervalIsThirtySeconds`, where the rest of the
reasoning lives.

### AC7 — retention is NOT touched, and why

**Retention was not changed and must not be.** The Log Analytics workspace is under the free-tier retention
floor, so lowering it saves **€0** — it is motion that looks like a fix and produces nothing, while costing
the forensic window on a platform whose error tracking is already thinner than assumed. The next person
looking at this invoice should skip retention entirely and look at ingestion volume.

### AC6 — no functional change

All 14 listeners still fire — the trigger set is untouched, and `QueueListenerInventoryTests` now asserts
every trigger names a declared queue or its poison companion. The four Functions smoke suites are green
inside the full unit run.

### A drift found while counting the listeners, and fixed

**`deploy/bicep/modules/storage.bicep` provisioned 12 queues for 14 listeners.** Its own comment
(`:52-55`) says the array mirrors `QueueNames.cs`, and it had drifted by one: `live-activity-dispatch` and
its poison companion — required by ADR-0029, present in `QueueNames.cs`, present in two triggers — were
never in the template.

Nothing was broken, which is exactly why it survived: both the producer
(`AzureStorageQueueClient.cs:17`, `CreateIfNotExistsAsync`) and the Functions listener create a missing
queue on first use. The only symptom is two queues unmanaged by the template that claims to own them.

Fixed by adding the name (one line; ARM queue creation is idempotent, so it is a no-op against the queues
that already exist at runtime) and pinned by
`QueueListenerInventoryTests.EveryDeclaredQueueIsProvisionedByTheStorageTemplate`, **mutation-proved** by
removing it again → red.

**`manual_step: azure-deploy`** — the Bicep change needs a `Deploy to DEV` run to take effect. Nothing
depends on it (the queues exist), so it can ride the next deploy.

### AC8 (Gate 0.5)

Baselines re-measured locally at `c15e295e` before any edit: **3129 / 144 / 135**, all exit 0 (the ticket's
2295 / 108 / 75 are stale). After: **3146 / 144 / 135**.

**Leg 1 / mutation, under leg 3 as the AC asks:** there is no mutation for a JSON value, and no local
measurement of a billing outcome. The four `host.json` value assertions were already shipped and are
already the guard. What was *added* here is falsifiable and was mutation-proved — the queue-provisioning
pin (see above). **The euro saving is asserted by nobody in this review; AC5 owes it.**
