---
id: T-0503
title: Boot cost — a blocking DB call with a 15s timeout runs during DI registration, and the EF model is built on the first request
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
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
- 2026-08-02 — **draft (created by pm from the cold-start / deploy investigation).** **Both findings
  marked RELAYED, not PM-verified** — AC1 re-establishes each, and a partial close is permitted.
  Filed separately from **T-0502** because the two halves are different layers with different
  reviewers and different failure modes, and because the infrastructure half needs a deploy to take
  effect while this half is testable locally.

## Review
