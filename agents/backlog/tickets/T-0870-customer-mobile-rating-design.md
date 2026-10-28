---
id: T-0870
title: Improve customer mobile rating design and action visibility
size: M
owner: Michael Chaban
created: 2026-10-28
updated: 2026-10-28
depends_on: []
blocks: []
layers: [android, ios, docs]
security_touching: false
---

## Context

The owner requested a better completion-rating popup and a more visible rating entry on completed,
unrated order details, selected Android and iOS, and approved the bounded design on 2026-10-27.
The existing review card was below prices and timeline; popup actions scrolled with optional fields.

## Acceptance criteria

- **AC1** — Both customer apps lead the rating popup with the question and large star targets, group
  optional feedback and keep Submit visible below scrolling content with quieter dismissal.
- **AC2** — A completed non-guest booking without a review offers Rate your cleaning as the fixed
  primary action, with Book again secondary. Saving a review restores the normal action priority.
- **AC3** — Saved reviews remain visible/editable; guest, incomplete and cancelled bookings do not
  gain a review action. Rating payloads, edit prefill, optional fields and prompt frequency are unchanged.
- **AC4** — Short viewports and larger text keep the fixed actions visible. Light/dark native captures
  and relevant automated gates demonstrate the result in the five supported languages.

## Out of scope

Backend/API/database changes, web and partner redesigns, unrelated findings, merge, deployment and
app upload. No PRO access. Shared iOS SnapSheet configuration defaults preserve existing callers.

## Implementation notes

The iOS minimum-height option mirrors the existing Android sheet reservation; the customer screen
measures its compact header and footer. No new screen, dependency, backend contract or migration.
Status is recorded only in INDEX.md; merging remains subject to the owner's separate approval.

## Review

Android suites passed: core 343, partner 1001 and customer 1754 tests, zero failures. iOS Core
passed 861 tests; customer passed 1850 in both Debug and optimized builds, zero failures and one
existing skip per build. The four native layout/capture tests also passed separately in both modes
after tightening the modal capture to wait for presentation to finish. Native fixtures cover
light/dark, short screens and larger text; iOS detail captures cover all five languages. Android's
keyboard placement was checked interactively; iOS keyboard placement was not.

Repository checks, the merged validator and docs build passed. The PR records hosted CI results;
merging requires the owner's separate approval.
