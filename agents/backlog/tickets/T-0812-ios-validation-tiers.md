---
id: T-0812
title: Wave E C05 — write down what validates iOS where, and what CI cannot see
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
---

## Context

The roadmap asks to "characterize the Ubuntu/hosted-desktop gap for iOS workflow validation", without claiming that hosted Linux validates iOS simulator behaviour. Checked against the read-only Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-e-2026-10-11/raw/ground-truth/`).

**The gap is structural:**
- Only `ios-ci.yml` (`macos-latest`) compiles Swift and runs XCTest, on one newest-runtime simulator, Debug only.
- Every other iOS-relevant check runs on Ubuntu and reads source text, or reads string catalogs from a JVM or .NET test.
- These run only locally:
  - the iOS 16.4 floor smoke;
  - UI walks;
  - the VoiceOver test;
  - `check-available-status-parity.mjs`;
  - archive and TestFlight.

## Doing

- `docs/mobile-app/overview.md#ios-validation-tiers`: every check, what it decides, and what it cannot.
- `docs/deployment/ci-cd.md#runners`:
  - the runner roster;
  - how to read the resolved image (job log only, 1-day retention);
  - the images observed on 2026-10-11;
  - GitHub's Ubuntu 26 notice;
  - the required context.
- A pointer from quality-gates Gate 8.5.

## NOT doing

No workflow edit: no runner, image or Xcode pin, no 16.4 CI destination, no Node 20 bump, no retention change. Each of these is a CI decision for the owner (T-0813).

## Acceptance criteria

- [ ] **AC1** — Both pages carry the characterization, with dates and run ids, and claim nothing about simulator behaviour from Ubuntu checks.
- [ ] **AC2** — The docs build and the repository checkers pass.
