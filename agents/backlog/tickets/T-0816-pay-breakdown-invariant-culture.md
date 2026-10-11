---
id: T-0816
title: Write the pay breakdown text the same way on every machine
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
---

## Context

Mike: "You can run against CI, but I guess it has to be adaptable" (Mike's rulings of 2026-10-11 (`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/owner-decisions-2026-10-11.md`)). The ground truth reads that as: CI's text (`Collected fee: 300.00`) is right, and it must hold on every machine. No app displays `PayBreakdown`; its only durable reader is the company archive, which copies it verbatim, so the books text must not vary by host culture [BE-24, D5 lane].

## Doing

- Format `PayBreakdown` with `CultureInfo.InvariantCulture` at its two writers: `CalculateOrderPay.cs` (fee share) and `PayCalculatorExtensions.cs` (job and lockout rows).
- Add one RED-first test per writer that pins the host culture, using the house pattern (en-US and cs-CZ hosts). Change the one assertion that builds its expected value in the current culture.
- The integration suite passes on a decimal-comma Mac with no `LC_ALL` override.

## NOT doing

- No test-infrastructure, csproj or CI change.
- No change to how any reader formats figures.

## Acceptance criteria (done looks like)

- [ ] **AC1** — `PayBreakdown` is identical under en-US and cs-CZ hosts; RED then GREEN.
- [ ] **AC2** — The full integration suite passes under this Mac's own culture.
- [ ] **AC3** — CI stays green.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
