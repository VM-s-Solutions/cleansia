---
id: T-0427
title: "iOS — Live Activity for an in-progress clean (Wolt/Uber-style lock-screen + Dynamic Island live status)"
status: proposed
size: L
owner: architect
created: 2026-07-16
updated: 2026-07-16
depends_on: [T-0403, T-0404]
blocks: []
stories: []
adrs: []
layers: [architect, ios, backend]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: owner remark ("real-time background notification while the cleaner is cleaning, like Wolt/Uber") + remarks-sweep (wf_064232d3)
---

> Owner wants a live, background status while the cleaner works — lock screen + Dynamic Island — like
> Wolt/Uber delivery tracking. **This is a real feature: a second APNs push channel + a new extension target,
> not a toggle.** Needs an ADR before build.

## Scope (ActivityKit)
An order going `InProgress` (event exists) starts a **Live Activity** showing live status (assigned → on the
way → in progress → done), updated in real time and ended on completion.

**Phase A — client:**
- Lift `LiveProgressLogic.swift` into `CleansiaCore` as the shared `ContentState` model.
- Add a **Widget Extension** target to `CleansiaCustomer/project.yml` with `ActivityAttributes` + lock-screen
  and Dynamic Island views; add `NSSupportsLiveActivities`; bump the deployment target to **16.1**.
- Start the Activity on `order.in_progress` (or on `on_the_way`), register its **ActivityKit push token**,
  and end it on completion.

**Phase B — backend (the hard part):**
- A **separate APNs Live-Activity push channel** — ActivityKit updates are APNs pushes to the *activity*
  push token, a distinct payload/topic (`<bundle>.push-type.liveactivity`) from the FCM alert path landed in
  T-0403/T-0404. Decide: send directly via APNs (new APNs client on the Functions host) vs. through FCM's
  APNs bridge (FCM does not natively target Live Activity tokens — likely a direct-APNs path is required).
- Persist the per-order activity push token (new registration endpoint, analogous to Device/Register).
- Drive updates from the same order-status transitions that fan out the alert pushes.

## Decisions for the ADR
- Direct-APNs vs FCM for the live-activity channel (feasibility of FCM targeting activity tokens).
- Which events drive start/update/end; the ContentState shape and the ≤ frequency/battery budget.
- Token lifecycle (per-order, ended/cleaned up on completion; no-PII on the wire).
- Android parity path (Android has no Live Activity equivalent — foreground service / ongoing notification is
  the analogue; scope separately).

## Acceptance criteria
- [ ] **AC0 (ADR)** — architect records the push-channel choice + the ContentState/event contract.
- [ ] **AC1** — starting a clean shows a Live Activity on the lock screen + Dynamic Island; it updates on each
  status change and ends on completion.
- [ ] **AC2** — backend sends the ActivityKit updates reliably from the order-status transitions; token
  registered + cleaned up; no PII on the wire.
- [ ] **AC3** — `dotnet test` + iOS build green; deployment-target bump doesn't break the 16.0 floor policy
  (16.1 for the activity, guarded).

## Status log
- 2026-07-16 — filed from the remarks-sweep; large, ADR-gated. Depends on the FCM/APNs push foundation
  (T-0403/T-0404).
