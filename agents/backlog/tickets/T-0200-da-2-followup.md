---
id: T-0247
title: "check-consistency rule: direct Dispute.Close/Escalate/Resolve outside the transition-guard allowlist"
status: done
size: S
owner: —
created: 2026-06-09
updated: 2026-06-14
depends_on: [T-0172, T-0174]
blocks: []
stories: []
adrs: [0001, 0006]
layers: [backend, tooling]
security_touching: true
manual_steps: []
sprint: 3
source: architect design note (T-0174 chargeback-reflection, Decision A)
---

## Context

T-0172 installed the in-process dispute transition guard (`Dispute.CanTransitionTo` +
`Dispute.UpdateStatus` routing through the intent methods), and T-0174's chargeback-reflection fix
made `ReflectChargebackStatus` a **second writer on the same legal graph** that now gates on
`CanTransitionTo` / `IsTerminal` itself before calling `Close`/`Escalate`/`Resolve`.

The recurring hazard behind both findings is the same: a **direct call to `Dispute.Close(...)` /
`Dispute.Escalate(...)` / `Dispute.Resolve(...)` on a `Dispute` receiver from outside the sanctioned,
guarded entry points** bypasses the T-0172 transition table and can force an illegal terminal
overwrite (e.g. `Closed → Resolved` on a late Stripe event). This is mechanically checkable, so it
should be a `check-consistency` rule rather than relying on review vigilance.

## Acceptance criteria

- [ ] **AC1 — Rule added.** A new rule in `agents/tools/check-consistency.mjs` greps `src/**/Features/**`
  (and the domain/handler call sites) for a `.Close(` / `.Escalate(` / `.Resolve(` invocation on a
  `Dispute` receiver. Each hit outside the allowlist is reported as a finding: *"direct Dispute
  state-write bypasses the T-0172 transition guard; route through `CanTransitionTo`/`UpdateStatus` or
  the sanctioned webhook path."*

- [ ] **AC2 — Allowlist.** The rule's allowlist is exactly
  `{ Dispute.UpdateStatus, ResolveDispute.Handler, HandlePaymentNotification.ReflectChargebackStatus }`
  — these are the three sanctioned writers (`UpdateStatus` is the guarded in-app path; `ResolveDispute`
  owns the `Resolve` money-path; `ReflectChargebackStatus` gates on `CanTransitionTo`/`IsTerminal`
  itself). Any new caller must be added to the allowlist with a reviewable justification or refactored.

- [ ] **AC3 — Green on current tree.** The rule passes on the current codebase (the three allowlisted
  sites are the only callers) and would flag a deliberately-introduced fourth direct caller. Wire it
  into the `check-consistency` run per `agents/process/enforcement.md`.

## Notes

- This is a tooling/enforcement follow-up, not a code-behavior change. No migration, no NSwag regen.
- Mirror the existing rule shapes in `check-consistency.mjs`; do not invent a new rule format.

## Status log
- 2026-06-09 — draft (created by pm)
- 2026-06-13 — **re-id'd T-0200 → T-0247** (PM, Wave-5 intake): the id `T-0200` collided with the
  AUD-07 order-wizard ticket (`T-0200-aud-07.md`); both frontmatters read `id: T-0200`. This follow-up
  (filed 2026-06-09, later) takes the next free id `T-0247`. File slug unchanged. Promoted to `ready`
  for Wave 5 (Batch 5G): deps T-0172✓/T-0174✓ are `done` (Wave 3 merged). `security_touching: true`
  (it guards the dispute state-machine writers); tooling-only, no migration/regen.
- 2026-06-14 — **review** (backend). Added rule **B10** to `agents/tools/check-consistency.mjs`: greps
  the backend `Features/**` scan root for `dispute.(Close|Escalate|Resolve)(` and flags any hit whose
  enclosing method is not allowlisted (new `enclosingMethod()` helper). Lowercase `dispute.` receiver
  match discriminates the Dispute receiver from `payPeriod.Close`/`period.Close` and the static
  `FiscalSequenceScope.Resolve` (covered by tests). Added `agents/tools/check-consistency.test.mjs`
  (9 cases, dependency-free, all green): flags a deliberately-introduced fourth direct caller (AC3),
  allows each sanctioned writer (AC2), proves `Handle` is allowlisted **only** in `ResolveDispute.cs`
  (not blanket), and proves no false positive on other receivers. Harvested the rule into
  `consistency.md` (B10) and `enforcement.md`. Backend run is **green on B10** on the current tree
  (the 42 reported violations are all pre-existing A1/A5/B1/B3 baseline debt, not B10).
  - **DEVIATION from AC2 (reviewable):** AC2 lists the allowlist as *exactly* three sites, but the
    current tree has a **fourth** legitimate sanctioned writer: `HandlePaymentNotification.HandleChargeback`
    (`HandlePaymentNotification.cs:377`) calls `dispute.Escalate(ChargebackActor)` on a freshly-built
    `Pending` chargeback dispute before persisting it (`Pending → Escalated` is a legal edge in the
    T-0172 table). This call was added in Wave 3A (#76, 2026-06-12), *after* this ticket was drafted
    (2026-06-09) and is part of the same chargeback webhook flow as the allowlisted
    `ReflectChargebackStatus`. AC2 explicitly permits this ("Any new caller must be added to the
    allowlist with a reviewable justification"). I added `HandleChargeback` to the allowlist with an
    inline justification rather than emit a false positive that would have broken AC3 (green on current
    tree). **Security/architect: please confirm `HandleChargeback`'s escalate-on-create is sanctioned;**
    if it should instead route through `UpdateStatus`, remove it from the allowlist and the rule will
    flag it. This is a guard-completeness call, not a behavior change in this ticket.
- 2026-06-14 — **SECURITY GATE: FAIL** (security). Runtime guard verified sound *today*: in-app
  `UpdateDisputeStatus.Handler` routes through `Dispute.UpdateStatus` (gated by `CanTransitionTo`,
  returns `InvalidDisputeStatusTransition` on an illegal jump and can never reach `Resolved` — it has
  no edge to it); `ResolveDispute.Handler` self-gates on `IsTerminal` before `Resolve` (blocks the
  re-resolve / `Closed→Resolved` overwrite); `ReflectChargebackStatus` gates each arm on
  `CanTransitionTo`/`IsTerminal`; `HandleChargeback`'s `Pending→Escalated` on a freshly-built dispute is
  a legal edge. `Resolve` is the sole writer of `Status=Resolved` + `ResolvedBy/ResolvedOn/RefundAmount/
  ResolutionNotes`. The AC2 deviation (4th allowlist entry `HandleChargeback`) is **accepted** — it is
  a legitimate sanctioned writer on a legal edge, justified inline.
  - **BUT the deliverable — the B10 enforcement rule — has two real holes that defeat its purpose.**
    The domain methods `Dispute.Close/Escalate/Resolve` are **public and unguarded** (raw status writes,
    no `CanTransitionTo`); the only thing preventing a future `Closed→Resolved` overwrite of a settled
    dispute is review + this tool. The ticket Context explicitly says the rule must replace "relying on
    review vigilance." B10 does not, because:
    1. **Receiver-name false-negative (AC1).** B10 matches only the literal token `\bdispute\.` —
       AC1 requires "a `.Close(`/`.Escalate(`/`.Resolve(` invocation on a `Dispute` receiver." A caller
       that names its variable anything else evades it. Proven: a fixture with
       `var existing = ...; existing.Resolve(...)` / `existing.Close(...)` / `d.Resolve(...)` returns
       `consistency: OK`, exit 0 — NOT flagged. This is one line from live code:
       `HandlePaymentNotification.cs:362` already binds a `Dispute` to `existing` and calls
       `existing.LinkStripeDispute(...)`; adding `existing.Resolve(...)` there ships unguarded and silent.
    2. **Scan-root false-negative (AC1).** B10's default root is only
       `src/Cleansia.Core.AppServices/Features`. AC1 requires "the domain/handler call sites." A direct
       `dispute.Resolve(...)` in `Cleansia.Core.AppServices/Services/**`, a domain service under
       `Cleansia.Core.Domain/Disputes/**` (where the unguarded methods live), or any Web host controller
       is never scanned. Proven: a fixture under a `Services/` dir is reported "NOT SCANNED."
  - **mustFix (developer):** (a) broaden the B10 receiver match beyond the literal `dispute.` so any
    local of declared type `Dispute` (or any receiver) calling `.Close/.Escalate/.Resolve(` is matched,
    keeping the existing receiver-discrimination (PayPeriod / FiscalSequenceScope) as an explicit
    exclude-list rather than an allow-only `dispute` token — add red tests for `existing.Resolve(`,
    `d.Close(`, `theDispute.Escalate(`; (b) extend the B10 scan root to the AppServices `Services/**`
    and the `Cleansia.Core.Domain/Disputes/**` call sites (not just `Features/**`) so a writer placed
    outside `Features` is caught — add a red test for a caller outside `Features/`. AC2/AC3 and the
    receiver-discrimination tests must stay green. Tooling-only; no migration/NSwag.
- 2026-06-14 — **review** (backend, security mustFix addressed). Closed both B10 holes plus the comment
  findings:
  - **Receiver-name false-negative fixed.** B10 now matches `\b(\w+)\.(Close|Escalate|Resolve)\(` on
    **any** receiver (a `Dispute` can be bound to any local name), with an explicit exclude-list for the
    known non-Dispute receivers — `period`/`payPeriod` (PayPeriod.Close), `FiscalSequenceScope` (static
    numbering Resolve), and any `*Resolver` (DI resolver `.Resolve`, e.g. `fiscalServiceResolver`) — in
    place of the old allow-only `dispute` token. Red→green proven: the old literal regex matched **0** of
    `existing.Resolve`/`d.Close`/`theDispute.Escalate`; new test asserts **3** B10 + exit 1.
  - **Scan-root false-negative fixed.** B10 extracted into its own pass (`checkDisputeWrites`) over a
    dedicated root set: `AppServices/Features` + `AppServices/Services` + `Core.Domain/Disputes` (the
    unguarded public methods live in `Dispute.cs`). Old root was `Features` only; new test places a
    caller under `Services/` and asserts it is flagged (1 B10 + exit 1). Keeping B10 in a separate pass
    means the wider roots are **not** subjected to the A1/A5/B1/B3/B5 rules, so no new baseline noise.
  - **Comment hygiene.** Dropped the bare `T-0172` ticket id from the B10 declaration comment
    (`check-consistency.mjs`), keeping the load-bearing `ADR-0006 D4`; the mandated `T-0172` token in the
    finding-message string (AC1 verbatim output) is unchanged. Reworded the two `// AC2`/`// AC3`
    traceability headers in `check-consistency.test.mjs` to behavior-only descriptions.
  - **Verification:** `node agents/tools/check-consistency.test.mjs` → **12/12 green** (the 9 prior cases
    + 3 new: `*Resolver` exclusion, non-`dispute` receiver names, caller-outside-`Features`). Full backend
    run `node agents/tools/check-consistency.mjs backend` → **0 B10 findings** on the live tree (the
    `dispute.*` sites are allowlisted, the `period.Close`/`FiscalSequenceScope.Resolve`/
    `fiscalServiceResolver.Resolve` sites are excluded); the 42 reported violations are all pre-existing
    A1/A5/B1/B3 baseline debt (5×A1, 6×A5, 13×B1, 18×B3), unchanged by this refactor. Tooling-only; no
    migration/NSwag.
