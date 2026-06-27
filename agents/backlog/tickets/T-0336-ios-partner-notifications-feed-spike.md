---
id: T-0336
title: SPIKE — iOS partner in-app notifications feed (persistence choice + push-receipt contract + dashboard bell badge)
status: draft
size: S
owner: pm
created: 2026-06-26
updated: 2026-06-26
depends_on: [T-0311]
blocks: []
stories: []
adrs: [0013]
layers: [ios, analyst]
security_touching: false
manual_steps: []
sprint: 12
source: sprint-12 §7.7 Scope B (architect)
---

> **Deferred SPIKE — NOT a build ticket.** Surfaced by the T-0310 Understand pass (sprint-12 §7.7 Scope B). The
> Understand pass found NO Android prefs surface, NO backend prefs/push-prefs API, and NO generated client — the
> Android partner "Notifications" is an **in-app push FEED** (the dashboard bell, partner-app-local Room DB), not
> a Profile-tab prefs screen. So "Notifications prefs" was **DROPPED** from T-0310 (nothing to port; an ADR-0016
> hidden/placeholder-feature risk). The in-app **feed** (mis-homed to T-0310 by T-0303's §7.2 deferral) is a
> **richer feature** than a profile row — a local persistence store + bell badge + a push-receipt path depending
> on T-0311 APNs — so it gets a scoping spike, not a smuggled build. **This is a spike** (decide the shape), not a
> feature build.

## Context

T-0303's §7.2 deferral map homed "the unread-notifications DB feed + the bell badge → T-0310". The T-0310
Understand pass (§7.7 Scope B) found that's a mis-home:

- Android partner "Notifications" is `NavRoute.Notifications` (`partner-app/.../navigation/NavRoutes.kt:51-52`:
  "In-app push-notifications feed — reached from the dashboard bell"), backed by the **partner-app-local Room DB**
  (`core.notifications.db`, per `patterns-mobile.md` §"Modules": "Has a local Room DB for notifications that
  customer-app does not").
- There is **no preferences screen**, **no backend prefs/push-prefs endpoint**, and **no client** for one.
- The feed is a persistence feature (the iOS Room analogue = SwiftData / Core Data / a UserDefaults cache) + the
  dashboard bell badge + a push-receipt path that depends on **T-0311** (APNs registration → `/api/Device/*`).

Building it inside T-0310's profile scope would over-stuff a screen ticket; building it before T-0311 lands means
no push to feed. Hence a scoping spike, sequenced after APNs.

## Acceptance criteria (spike deliverables — a recorded decision, not a feature)
- [ ] **AC1 — Persistence choice recorded.** Pick the iOS local-store for the feed (SwiftData vs Core Data vs a
  lightweight cache), mirroring the Android Room shape, with the trade-off written down (offline read, badge
  count source, eviction). A short architect record (or a §7.x note), not a build.
- [ ] **AC2 — Push-receipt contract mapped.** Trace how an APNs payload (T-0311) maps to a feed row + the
  unread-badge count; whether a backend list/read-receipt endpoint exists or is needed (and if needed, flag it as
  a backend ticket — do NOT invent the contract here).
- [ ] **AC3 — Bell badge wiring sketched.** How the dashboard bell (inert since T-0303) reads the unread count
  from the store; the T-0303-deferred bell badge home is reconciled.
- [ ] **AC4 — A follow-up build ticket (or tickets) filed** with the decided shape, depending on T-0311 (and any
  backend contract ticket AC2 surfaces). The spike itself ships the decision + the ticket(s), no feature code.

## Out of scope
- **No "Notifications prefs" screen** — there is no backend contract for per-channel toggles; not building one.
- **No feature build in the spike** — the build is the follow-up ticket(s) AC4 files.
- **Customer notifications** — partner only (customer has its own surface; T-0314 cluster).

## Implementation notes
Read `partner-app/.../core/notifications/` (the Room feed) + `NavRoutes.kt:51-52` + `docs/architecture/push-notifications.md`
+ the T-0311 APNs ticket. Analyst + ios on the spike (the contract trace is analyst-flavored). No `security` gate
(a scoping spike); no `optimizer`. **Routing:** `[ios, analyst]`. **Suggested home:** after T-0311 (APNs must
exist to have anything to feed).

## Status log
- 2026-06-26 — draft (created by architect ruling, sprint-12 §7.7 Scope B). "Notifications prefs" dropped from
  T-0310 (no Android prefs surface / no backend API / no client); the in-app feed (mis-homed by T-0303 §7.2) is a
  richer feature gated on T-0311 → this scoping spike. Dedup-checked: not an existing INDEX ticket.
  `depends_on: [T-0311]`; `security_touching: false`; `manual_steps: []`. No panel (no-decision: a scoping spike
  that defers the real decision to its own deliverable).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
