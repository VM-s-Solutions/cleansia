---
id: T-0158
title: "Bucket-B sweeps/called-services migrate onto the per-iteration-commit outbox row"
status: draft
size: M
owner: —
created: 2026-06-05
updated: 2026-06-05
depends_on: [T-0157, T-0148]
blocks: []
stories: []
adrs: [0002, 0008]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 1
source: split of T-0143 (child d); ADR-0002 D5 Bucket B
---

## Context
Split child **(d)** of L-epic **T-0143**. Migrates the 7 Bucket-B sends (the loop-and-commit-per-
iteration sites carved out of Wave-0) onto the per-iteration-commit outbox row, so each commit drains
its own row — the correct shape ADR-0002 D5 names. **Strictly serial** in the a→b→c→d chain (after the
durable backing T-0157 exists). Its `LoyaltyService.cs` edit **depends on T-0148** (also `LoyaltyService.cs`)
and must serialize against it — this is the lower-id-depends-on-higher-id edge the parent T-0143 records.

## Acceptance criteria
- [ ] **AC1 (Bucket-B migrates, D5 Bucket B)** — Given the 7 Bucket-B sends
  (`AutoCancelStaleRecurringOrders.cs:87`, `SendRecurringOrderReminders.cs:77`,
  `SendMembershipLifecycleNotifications.cs:87,125`, `NewJobsDigestService.cs:170`,
  `SendSitewidePromo.cs:88`, `LoyaltyService.cs:75`), When this child lands, Then each writes its message
  as an outbox row **inside its own per-iteration commit** (each commit drains its own row), removing the
  direct `IQueueClient.SendAsync` from those sites.
- [ ] **AC2 (carve-out whitelist cleared)** — The Wave-0 reviewer-check-#1 carve-out whitelist entries
  for these 7 sites are removed. `SendSitewidePromoFanoutFunction.cs:123` (Bucket C, a Function with no
  commit to gate) **stays direct** per ADR-0002 D2.3 — not migrated.
- [ ] **AC3 (no Bucket-A/consumer regression)** — Bucket-A handlers and consumers remain untouched; the
  ADR-0002 verification gate stays green.
- [ ] **AC4 (test-first)** — The per-iteration outbox write is logic → tests written first (red→green,
  visible in commit order / status log) per `agents/knowledge/testing.md`; an integration-style test
  drives a per-iteration commit → drain and asserts the terminal effect happens exactly once.

## Out of scope
- The durable backing + drainer + host (T-0157), the table (T-0156), the ADR (T-0155) — consumed here.
- `SendSitewidePromoFanoutFunction.cs:123` (Bucket C) — stays direct per D2.3.
- Consumer effects / poison consumers / dual-read — out of scope (separate tickets).
- NSwag regen — internal contract; `manual_steps: []`.

## Implementation notes
- **Gated on T-0157 done.** The `LoyaltyService.cs:75` edit **serializes against T-0148** (the tier-
  threshold + grant/revoke Reason ticket also edits `LoyaltyService.cs`, TICKET-MAP row 7) — never run
  the two concurrently; `depends_on: T-0148` enforces order.
- **Serialization cluster:** part of the `UnitOfWorkPipelineBehavior.cs` + queue cluster (TICKET-MAP
  row 3) — strictly serial within the T-0143 chain; not concurrent with T-0151 on Functions files.
- Spawn a reviewer in parallel with the developer.
- Anchors: the 6 Bucket-B files in AC1; `SendSitewidePromoFanoutFunction.cs:123` (stays direct).

## Status log
- 2026-06-05 — draft (created by pm; split of T-0143 child d; blocked on T-0157 + T-0148)
- 2026-06-06 — STAYS draft/blocked (Batch 1B; its ADR gate **ADR-0008 / T-0155 is done ✓**, but it
  `depends_on: T-0157` (not done — itself blocked) AND `T-0148` (now `ready`, not done). Tail of the
  strictly-serial outbox chain. Also edits `LoyaltyService.cs` → serialize against T-0148 (T-0148 first) and
  the Wave-2 T-0163 partial-revoke edit. Promote to `ready` only when T-0157 AND T-0148 are both `done`.
  `adrs` set to `[0002,0008]`).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
