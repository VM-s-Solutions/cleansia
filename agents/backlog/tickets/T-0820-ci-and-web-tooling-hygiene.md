---
id: T-0820
title: Bring the workflows and web tooling up to date without changing a gate
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0812]
blocks: []
stories: []
adrs: []
layers: [ci, frontend, docs]
security_touching: false
---

## Context

CI and tooling findings that need no owner ruling, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). None of them changes what runs, what is required or what passes.

## Doing

- [WCD-03] Bump every action in the PR-checking workflows to its first Node-24 major, after reading that major's breaking changes for the inputs we pass. Deploy workflows are not touched.
- [WCD-04] `node-version: 22` in `ios-symbols-ci.yml`, `booking-policy-parity.yml` and `docs-ci.yml`.
- [WCD-05] Drop the `build-root-directory` input that setup-gradle ignores.
- [WCD-21/IOS-27] Workflow comments stop stating stale facts: Windows-only development, '10x' billing while the repo is public, workflow counts.
- [WCD-12] `--output-style=static` on the informational lint step.
- [WCD-15] `nx.json` lint inputs name the three imported config modules and `^default`, not two files that do not exist.
- [WCD-16] Delete the dead root `local-registry` target, `verdaccio` and its two overrides, and the stale `graph.json`.
- [WCD-17] `order-wizard`'s coverage directory stays inside the workspace.
- [WCD-09/WCD-10] Correct the `jest.polyfills.ts` comment, and say in one line which runs read the root `workerIdleMemoryLimit`.
- [WCD-28] Because the lockfile change makes every project affected, this PR's hosted Frontend CI shows every suite. Record its worker-exit warning count on T-0810.

## NOT doing

- Required checks, runners, image pins, caches, worker bounds, retention, lint becoming blocking (T-0813 and Mike).
- `deploy-pro.yml`, `deploy-azure.yml`, `deploy-dev.yml`, `deploy-docs.yml`.
- The Nx 24 conversion (T-0811).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Every PR-checking workflow runs its actions on Node 24 majors with no deprecation or ignored-input warning, and stays green on the exact head.
- [ ] **AC2** — `npm ci`, every project's tests and lint, the three production builds and the e2e smokes pass locally on Node 22 / npm 10.
- [ ] **AC3** — The worker-exit observation is recorded on T-0810.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
