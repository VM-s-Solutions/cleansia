---
id: T-0499
title: One file, five values — Functions host.json polls 14 queue listeners every 5s and defeats its own sampling (€35–42/month)
status: draft
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: [0015]
layers: [architect, backend]
security_touching: false
manual_steps: []
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
- 2026-08-02 — **draft (created by pm from the Azure cost investigation).** **The five values were
  PM-verified first-hand** — `host.json` was read in full and every claim in the investigation about
  its contents is accurate, including the sampling-configured-not-to-engage shape. The **€35–42
  figure is RELAYED** and AC1 re-derives it. Two of the investigation's negative findings are written
  into the ticket as guardrails (retention saves €0; the alert theory is dead) so nobody re-derives
  them at cost.

## Review
