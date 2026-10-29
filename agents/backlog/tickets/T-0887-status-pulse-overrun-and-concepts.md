---
id: T-0887
title: Restore active status pulse and align overrun feedback
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Current native active status pulses gently and respects reduced motion/background state; Partner elapsed progress and overrun use the same estimate semantics as Customer.
- [x] **AC2** — Provide three meaningfully different status-component concepts with normal/overrun states for Mike to select; do not silently select a new layout.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS restores a gentle pulse only on the current live phase/marker, paused for Reduce Motion/inactive scene. Partner maps the estimated duration, shows live fraction/remaining time and amber overtime using the shared tracker surface. Native light/dark overtime captures and floor timing boundaries pass. Final replacement layout remains Mike’s concept choice. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Current pulse/overrun behavior is covered by full native suites, iOS16.4 focus and native captures. Flow, Focus and Timeline concepts pass36 browser layout/state/theme cases plus positive pulse, reduced-motion and isolated choice checks. Mike has not selected a replacement layout.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
