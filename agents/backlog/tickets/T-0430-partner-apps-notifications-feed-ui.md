---
id: T-0430
title: "Partner apps — notifications feed UI (server inbox replaces the partner Android Room feed; partner iOS gains the inbox)"
status: done
size: M
owner: android
created: 2026-07-18
updated: 2026-07-19
depends_on: [T-0393]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: T-0393 analyst-panel rebuttal condition — the partner-host endpoint ships in the feed v1 backend so rows accumulate from day one; the partner UIs follow up
---

> The T-0393 feed v1 backend exposes the NotificationController on BOTH mobile hosts, and partner
> rows (order.new_available, digest-collapsed) accumulate from day one. This ticket lands the
> partner-side UI: Android partner migrates its local Room-backed notifications list to the server
> inbox (single source of truth; delete the Room path after parity), and partner iOS gains the
> inbox for the first time (mirror the customer feed UI shipped with T-0393). Badge + watermarked
> mark-all per FD-AC5/6; keyset scoping already server-side.

## Status log
- 2026-07-18 — filed by the PM per the analyst panel's condition on keeping the partner-host
  endpoint in feed v1.
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped on feature/i18n-cluster-3 (merged): both partner feed UIs to the customer FD-ACs; Android Room→server migration.
