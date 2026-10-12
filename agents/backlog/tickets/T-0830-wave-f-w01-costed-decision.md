---
id: T-0830
title: Wave F W01: put the evidence and a measured, costed decision in front of Mike
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0812]
blocks: []
stories: []
adrs: []
layers: [frontend, docs]
security_touching: false
---

## Context

The roadmap's W01, endpoint-scoped client loading. The ground truth refutes the audit's mechanism on all three web apps: esbuild keeps each generated client file whole in one chunk, and eager code imports from it, so the whole file is initial (customer 254 KB, partner 225 KB, admin 546 KB raw). Endpoint-scoped injection would move about 0 B. Per the roadmap, the levers that would move bytes (splitting the generated client per declaration, or letting the build drop unreferenced injectables) need a costed decision [W-customer, W-partner, W-admin].

## Doing

- Write up the evidence for each app.
- Measure the structural options in scratch only, with no tree change: a split-client counterfactual build for each app, and the `isolatedModules: false` build. Comparable before/after initial-JS bytes, with source, toolchain and counterresults recorded.
- A costed decision for Mike, with the measured numbers.

## NOT doing

- No application code change.
- No change to the generated-client layout, routing, SSR or a CI gate before Mike decides.

## Acceptance criteria (done looks like)

- [ ] **AC1** — The report and measurements are recorded with receipts.
- [ ] **AC2** — Mike has a decision line with costs and measured gains.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
