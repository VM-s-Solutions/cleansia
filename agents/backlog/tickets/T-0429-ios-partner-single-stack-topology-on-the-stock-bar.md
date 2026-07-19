---
id: T-0429
title: "iOS partner — adopt the D2 single-NavigationStack/ShellRoute topology on the STOCK tab bar"
status: proposed
size: M
owner: ios
created: 2026-07-17
updated: 2026-07-17
depends_on: []
blocks: []
stories: []
adrs: [ADR-0022]
layers: [ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: re-scoped from T-0376 (owner approved the re-scope 2026-07-17)
note: T-0376 was retired — the 2026-07-08 ADR-0022 supersede killed the pill/pager FAMILY (corrupted
  rendering on real hardware), and its closing line cancelled the pill port. This ticket carries the
  SURVIVING half of the old D2 mandate — the navigation TOPOLOGY, which the supersede did not retire.
---

> The ADR-0022 supersede retired the pill bar and `.page` pager, restoring the stock `TabView` —
> but the D2 navigation topology (ONE shell-level `NavigationStack` + a merged `ShellRoute` enum,
> instead of the crash-fix interim per-tab `NavigationPath` stacks) was retired only by association.
> Both apps now run the stock bar; the partner still carries the structurally divergent per-tab
> stacks. Converge the partner on the single-stack topology UNDER the stock bar.

## Acceptance criteria
- [ ] **AC1** — partner shell: one `NavigationStack` at the shell level with a merged `ShellRoute`
  destination enum; per-tab `NavigationPath` stacks removed. Stock `TabView` + `.tabItem` UNTOUCHED
  (the supersede's mandate).
- [ ] **AC2** — behavior parity: tab switching preserves each tab's drill-in state exactly as today
  (or a deliberate, documented simplification the architect signs off), deep links (order push →
  order detail) still resolve, and the cold-launch buffered-tap path (T-0423 fix) still works.
- [ ] **AC3** — existing shell tests (`PartnerShellSelectionTests` etc.) green; new tests pin the
  route-merge (every old per-tab destination reachable through `ShellRoute`).
- [ ] **AC4** — architect reviews the final topology against the ADR-0022 supersede text before
  merge (this is the ticket that closes the D2 remnant, so the ADR status log gets an entry).

## Status log
- 2026-07-17 — filed as the owner-approved re-scope of retired T-0376; the pill/pager half stays
  dead per the supersede.
