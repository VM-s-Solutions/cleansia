---
id: T-0429
title: "iOS partner — adopt the D2 single-NavigationStack/ShellRoute topology on the STOCK tab bar"
status: done
size: M
owner: ios
created: 2026-07-17
updated: 2026-07-19
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
- 2026-07-19 — architect defense panel convened (AUTHOR → CHALLENGER → LEAD). Verdict: **(B) —
  premise void / D2-remnant resolved**. No refactor. See `## Decision (panel)`.

## Decision (panel)

**Verdict: (B) — close as premise-void. The partner's per-tab `NavigationStack`s ARE the correct,
idiomatic end state under the restored stock `TabView`. No wholesale topology swap is warranted. This
record is the architect's AC4 ratification that the 2026-07-08 supersede already settled the partner
topology.** AC1/AC2/AC3 are **not applicable** (they describe a refactor that should not happen); AC4
is **satisfied by this section**.

### Author (draft) — the trade-off space and the chosen option

Three options were on the table (as framed in the ticket brief):

- **(A)** merge the per-tab stacks into one shell `NavigationStack` + a merged `ShellRoute` enum under
  the stock `TabView` (the ticket-as-written);
- **(B)** ratify per-tab stacks as the correct end state — nothing to refactor;
- **(C)** scope a narrower, genuine defect if one exists.

Author chooses **(B)**, on this reasoning:

1. **The single stack was coupled to the PAGER, not the pill — and the pager is dead.** ADR-0022 D2's
   single shell stack was structurally *required* by the `.page`-style pager: Alternative 3 in ADR-0022
   was rejected precisely because "*pushed children would live inside pages — swipeable between tabs
   mid-flow*." That objection is 100% pager-specific. The 2026-07-08 supersede restored the **stock,
   non-swiping** `TabView` (`PartnerShellView.swift:56`, `CustomerShellView.swift:127`). With no page
   pager, per-tab stacks can no longer be swiped mid-flow — the sole structural forcing-function for a
   single stack is gone. So the premise "adopt D2 under the stock bar" imports a shape whose reason for
   existing was retired.

2. **The customer kept the single stack for CUSTOMER-specific drivers the partner does not have.** The
   supersede preserved D1/D2 on the *customer* because it also solved (a) the iOS-16 sibling-typed-path
   crash and (b) genuine cross-tab route **de-duplication** (`orderDetail`/`subscribePlus` are pushed
   from Home *and* Orders *and* Profile — `CustomerShellView.swift:140,142,163,187`). Neither driver
   exists on the partner:
   - **Crash: already fixed on partner by D4.** The partner has no outer stack (`PartnerRootView.swift`
     is a flat-enum switch) and every per-tab path is already a type-erased `NavigationPath`
     (`OrdersListView.swift:35`, `EarningsView.swift:7`, `ProfileView.swift:9`,
     `RegistrationLockView.swift:7`). It does not crash on the iOS-16 floor. There is no defect to fix.
   - **De-dup: absent on partner.** `OrderRoute` has **one** case (`detail`), pushed only inside the
     Orders tab; `EarningsRoute` (3 cases) only inside Earnings; `ProfileRoute` (9 cases) only inside
     Profile. No route is shared across tabs — the Dashboard's `onOpenOrders` merely *switches tabs*
     (`PartnerShellView.swift:63`), it does not push a detail. Merging these three cohesive, per-tab
     enums into one 13-case `ShellRoute` is **consolidation without de-duplication** — a god-enum + one
     giant `switch`, i.e. *worse* cohesion, not better.

3. **A shell-level `ShellRoute` merge is structurally ill-defined for the partner.** `ProfileRoute` is
   pushed by TWO stacks that live in DIFFERENT audiences: the shell's Profile tab (`ProfileView.swift:53`)
   **and** the `RegistrationLock` — which is a **root audience state OUTSIDE the shell**
   (`PartnerRootView` `.registrationLock`) that owns its own `NavigationStack` and registers
   `.navigationDestination(for: ProfileRoute.self)` (`RegistrationLockView.swift:38,41`), pushing the
   shared section set over itself (the fail-closed gate, living doc §7.7 D2). A single *shell-level*
   `ShellRoute` cannot own the lock's pushes because the lock is not in the shell. The customer's clean
   four-enum→one-enum merge simply does not map onto the partner's two-audiences-share-one-route-set shape.

4. **Converging would IMPORT the customer's accidental complexity.** The customer's single-stack +
   hidden-shell-nav-bar design forced two workarounds the partner does not need: the
   `InteractivePopGestureEnabler` UIKit swipe-back shim (`CustomerShellView.swift:383-411`) and a
   snackbar-inset-by-path-depth recompute (`ShellSnackbarInset`). The partner's per-tab stacks give
   working swipe-back for free (each pushed child shows its own nav bar). Adopting the single stack would
   break swipe-back and require porting the shim — trading a working, simpler shell for an inherited bug
   plus its patch.

5. **Per-tab stacks were already ratified for the partner ON THE MERITS — not as a crash-fix stopgap.**
   §7.7 D1 / §7.9 / §7.12 canonicalized the partner's per-tab `NavigationStack` as the ADR-0020 D2
   intra-audience push container: "*per-tab `NavigationStack` = the native tab-local back-stack … iOS
   mirrors the tree, not the mechanism.*" ADR-0022 D4's "recorded interim" label was scoped to the
   **stock bar pending the pill port** (T-0376), which the supersede **cancelled** — it was never a
   verdict against the per-tab stack topology itself.

### Challenge (challengers attack)

- **CH1 — "Two divergent shell topologies is a real maintenance tax; the customer is the canonical
  lead-app shell and the partner is the recorded interim. (B) entrenches divergence forever, against
  ADR-0013's 'prove on one app, copy it' and ADR-0022 D4's own 'interim' language."**
- **CH2 — "ADR-0018 holds navigation *structure* identical to Android, and Android partner is one back
  stack (NavHost above `MainShell`). Per-tab stacks are the divergence D2 was meant to remove; keeping
  them is an unfixed parity gap — the bottom bar stays visible on an order detail on iOS but hides on
  Android."**
- **CH3 — "The `deepLinkOrderId` two-hop `@State`→`@Binding` handoff (`PartnerShellView.swift:21,50-53`
  → `OrdersListView.swift:54-63`) is fragile on cold launch — it depends on the Orders tab's stack
  existing and its `onChange`/`onAppear` firing after the binding is set. That's a genuine (C)-shaped
  defect (T-0423 class) you're leaving in place by choosing (B)."**
- **CH4 — "The owner approved the re-scope on 2026-07-17. Closing it as premise-void overrides an owner
  decision."**

### Defense (author answers)

- **CH1 — REBUT.** "Consistency" is not the architect bar; "does it make *future* change cheaper?" is.
  Converging makes future partner change *more* expensive (god-enum, imported swipe-back shim +
  snackbar-inset, the `RegistrationLock`/`ProfileRoute` collision in §3). The two shells already share
  what matters — `ShellTab`, the stock `TabView`, `PushNavigationModel`, the flat-enum root router
  (ADR-0020). The divergence is confined to the *intra-tab push container*, which is genuinely simpler
  on the partner precisely because the partner has no cross-tab route sharing. "Prove on one app, copy
  it" copies **what earns its place**; the single stack earned its place on the customer by solving the
  customer's crash + de-dup, neither of which the partner has. ADR-0022 D4's "interim" was the *stock
  bar pending the pill* — the pill is dead (supersede), so the label's referent no longer exists.

- **CH2 — REBUT (with a bounded concession).** ADR-0018's conflict rule gives **iOS the win on a genuine
  component conflict**, and per-tab back stacks are the iOS-native tab affordance; §7.7 D1 already
  applied exactly this ("mirror the tree, not the mechanism"). The bar-visible-on-detail delta is real
  but (a) has never been owner-reported (unlike the pill crash, which was), and (b) is the *component*
  layer where iOS convention is sanctioned to win. Concession, bounded: this is a **deliberate,
  documented parity note**, not a defect — recorded here, not fixed by a risky un-testable refactor.

- **CH3 — CONCEDE the fragility, REBUT the disposition.** The `deepLinkOrderId` handoff is fragile *by
  construction* — the shell-owned single-hop `applyPushTap` (`CustomerShellModel.swift:58-63`) is the
  robust pattern. BUT: (i) it is **not a demonstrated live defect** — I cannot device-test it and must
  not manufacture one; (ii) the actual cold-launch drop that WAS proven (T-0423) is an **app-delegate
  buffering** bug, orthogonal to shell topology and already ticketed for both apps; (iii) if the T-0423
  device pass shows the partner order-tap still drops, the fix is a small **shell-owned push** (move the
  `path.append` onto a shell model, ~20 lines, `CustomerShellModel.applyPushTap` shape) that **does not
  require the single-stack topology swap**. Disposition: fold into the T-0423 device pass as a
  conditional S-sized follow-up, not this ticket's wholesale rewrite.

- **CH4 — REBUT.** The owner approved re-scoping T-0376 into a topology question *for the architect to
  decide*; the panel is that decision. Approving that the question be asked is not approving option (A)
  as the answer. Recording (B) with the evidence trail is the faithful discharge of the re-scope, and it
  is surfaced to the owner via this ticket + the recommended doc updates. No owner business decision is
  overridden (no lasting business impact — this is an internal topology call); nothing escalates to
  `questions/open.md`.

### Verdict (lead adjudicates)

**Consensus: (B). Zero blocking challenges remain.**

- CH1 resolved (rebutted): convergence raises partner's future-change cost; the shared seams already
  hold; the "interim" label's referent (the pill) is dead.
- CH2 resolved (rebutted + bounded concession): recorded as a deliberate ADR-0018-component parity note,
  not a defect; owner never flagged it.
- CH3 resolved (conceded fragility, rebutted disposition): tracked as a **conditional** scoped fix under
  the T-0423 device pass; explicitly does **not** need the topology swap.
- CH4 resolved (rebutted): the panel IS the sanctioned discharge of the owner-approved re-scope.

**The load-bearing finding:** the D2 single shell stack was structurally required by the `.page`
**pager** (ADR-0022 Alternative 3), which the 2026-07-08 supersede retired. On the customer it *also*
solved a crash + real cross-tab de-dup, so it stayed; the partner has **neither** driver, has already
taken the D4 crash fix, and has a two-audiences-share-`ProfileRoute` shape (`RegistrationLockView.swift`)
that a shell-level `ShellRoute` cannot cleanly own. Per-tab `NavigationStack`s are therefore the correct,
idiomatic, already-on-the-merits-ratified (§7.7 D1 / §7.9 / §7.12) partner end state.

### AC disposition

- **AC1 (merge to one shell stack + `ShellRoute`)** — **N/A / declined.** The merge is unwarranted and
  structurally ill-defined for the partner (§3). No code change.
- **AC2 (behavior parity)** — **preserved by NOT changing anything.** Per-tab drill-in state, deep-link
  resolution, and the cold-launch buffered-tap path all remain exactly as today. The bar-visible-on-detail
  delta vs Android is recorded as a deliberate ADR-0018-component parity note (CH2), not a regression.
- **AC3 (new route-merge tests)** — **N/A.** No route merge; `PartnerShellSelectionTests` stay green
  unchanged.
- **AC4 (architect ratifies the topology vs the supersede text)** — **SATISFIED by this section.** The
  supersede's `TabView` restoration + the pager retirement already settled the partner topology; the
  per-tab stacks are ratified as the end state.

### Recommended finalization bookkeeping (not code; outside this ticket's edit)

1. **Living doc** (`agents/architecture/decisions/ios-app-architecture.md`): update the ADR-0018 D3
   table's partner-shell row (currently "*PM-filed follow-up … recorded interim*", line ~80) to read
   that the partner shell is **final on the stock `TabView` + per-tab `NavigationStack`s** — the D2
   single stack was pager-coupled and the pager is retired; per-tab stacks ratified per §7.7 D1 /
   §7.9 / §7.12. Add a dated "T-0429 — D2-remnant resolved (B)" note beside the §7.13 shell entries.
2. **ADR-0022 status log**: append a dated (non-superseding) "**2026-07-19 — D2-remnant resolution
   (T-0429)**" note recording that the partner topology is closed as already-settled by the 2026-07-08
   supersede (this is a record-only append in the same sanctioned form as the supersede section — it
   does not alter the accepted decision).
3. **No new ticket now.** Track the `deepLinkOrderId` shell-owned-push tidy as a **conditional** item on
   the **T-0423** partner device pass: file an S-sized fix only if the cold-launch order-tap is observed
   to drop on the partner; it does not require the topology swap.

### Owner real-device testing

**Not required for this verdict.** (B) changes no code, so there is no device-fragility risk to gate.
(The only device dependency — the `deepLinkOrderId` cold-launch path — rides the already-planned T-0423
partner device pass, per bookkeeping item 3.)
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — closed premise-void (architect AC4 ratification, f289c4db): per-tab stacks are final under the stock TabView.
