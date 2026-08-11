---
id: T-0496
title: Currency bug in the express surcharge
status: done
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0009]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** The audit found *"a real currency bug in the express
surcharge"* — traced by the investigation to file:line.

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it before anything changes.

### Why this is filed alone, with no dependency on T-0491

**It is a correctness defect, not a product question.** A currency bug computes the wrong amount of
money regardless of what express *means*, who is entitled to it, or whether Plus enforces it. It is
the only item in the Plus block that can ship immediately, and it should — it is `S`, it is
independent, and every day it stands the platform is charging or displaying a wrong number.

**It is also evidence in T-0493's investigation**: an express *surcharge* path existing tells you
something about which of the three readings of "express" is live today. Recorded on both tickets.

### The class of bug, stated so the fix is not narrower than the defect

Currency defects in this codebase have three recurring shapes, and the fix must say which one this is:
- **a hardcoded symbol or code** where a per-order/per-tenant currency should be read
  (`Q-W3-2` records a live instance: partner pay surfaces hardcode `Kč`);
- **mixing minor and major units** (Stripe works in minor units; the domain works in decimals);
- **a missing currency on a DTO**, so the client renders a number with the wrong symbol.

Each has a different blast radius. AC1 must name which.

## Acceptance criteria

- [ ] **AC1 — the defect is RE-ESTABLISHED and CLASSIFIED at file:line, before any change.** State
      the shape (hardcoded currency / unit mismatch / missing currency on a DTO / other) and the
      concrete wrong value it produces: *"for an order in X the surcharge computes/displays Y and
      should be Z."* **A fix with no stated wrong value cannot be mutation-proved.** Evidence: the
      classification plus the worked wrong number.
- [ ] **AC2 — a test that goes red against the pre-fix code (Gate 0.5 leg 1), asserting the value
      from AC1.** The verifier **re-runs it un-cached** and states what it could not verify.
      Evidence: the red run, then green.
- [ ] **AC3 — the sibling instances are found, and either fixed or listed.** If AC1's shape is
      "hardcoded currency", grep the express/surcharge path for the same shape. Fix them **only if
      they are in the same method**; otherwise list them with file:line in `## Review` for a separate
      ticket with a real scope. **A one-site fix on a class defect is how the same bug ships twice.**
      Evidence: the list, or "no siblings in this path".
- [ ] **AC4 — rounding is stated.** If the fix touches a conversion or a multiplication, name the
      rounding mode and match what the surrounding money code uses (`OrderFactory.cs:193-196` uses
      `MidpointRounding.AwayFromZero` — do not introduce a second convention in the same order total).
      Evidence: the named mode plus the citation.
- [ ] **AC5 — nothing else in the order total moves.** `git diff --stat` confined to the surcharge
      path and its tests. In particular **`ResolveLoy003Discount` is not touched** — that is
      **T-0492**, which is a different ticket with a different ruling behind it. Evidence: the diff.
- [ ] **AC6 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75** (sprint-14 §2.9 — the "DEFERRED-TO-CI" excuse is
      retired; all three run in ~5m30s).

## Out of scope

- **What express means, and who is entitled to it** — **T-0493**. Deliberately no dependency: this
  fixes the arithmetic, that decides the entitlement.
- **The Plus/tier/promo composition** — **T-0492**.
- **The `Kč`-hardcoding on partner pay surfaces** (`Q-W3-2`, open since 2026-06-10). Same *class*,
  different surface, already has an owner question. **Name it in `## Review` if AC3's shape matches;
  do not widen into it.**
- **Adding a currency field to any DTO** unless AC1 finds that to be the defect — and if it is, that
  carries **`manual_steps: nswag-regen`** and this ticket **stops and re-files**, because a DTO change
  in the owner's regen bundle is not an `S`.

## Implementation notes

**No panel — one-line "no-decision" note:** a currency computation producing a wrong number is a
defect, not a decision. **Unless AC1 finds the DTO shape**, in which case see `## Out of scope` — stop
and re-file.

**Gate 0.5 and Gate 6.5 both apply** (`routing.md` rules 7 and 8): money math.

**This is the one Plus ticket with no dependencies and no panel. Ship it first.**

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **Finding marked RELAYED, not
  PM-verified** — AC1 re-establishes and classifies it first. Filed with **no dependency on T-0491**,
  deliberately: a wrong amount of money is wrong under every product ruling, and this is the only item
  in the Plus block that is dispatchable today.

## Review
