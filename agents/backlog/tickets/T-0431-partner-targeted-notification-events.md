---
id: T-0431
title: "Partner-targeted notification events — assignment, accepted-job cancellation, invoice-ready"
status: proposed
size: M
owner: analyst
created: 2026-07-18
updated: 2026-07-18
depends_on: [T-0393]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: T-0393 Q-FEED-02 (default taken: dedicated follow-up rather than inventing events inside feed v1)
---

> Feed v1 ships partner = `order.new_available` only, because that is the only partner-targeted
> dispatch that exists. This ticket adds the missing partner events end-to-end (catalog key +
> producer + push loc-keys ×5 in both partner apps + feed row via the shared notify seam):
> **accepted-job cancellation is the highest-impact candidate — a cleaner currently learns nothing
> when a job they accepted is cancelled.** Also: assignment confirmation, invoice-ready.
> Analyst decides the exact keyset + copy first (client-first rule for the APNs display map).

## Status log
- 2026-07-18 — filed by the PM as the Q-FEED-02 default disposition.
