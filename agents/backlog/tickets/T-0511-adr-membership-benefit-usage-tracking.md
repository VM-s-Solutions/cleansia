---
id: T-0511
title: ADR — how a metered membership benefit is counted, consumed, reset and reversed
status: done
size: M
owner: architect
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: [T-0512, T-0493]
stories: []
adrs: [0035]
layers: [architect]
security_touching: false
manual_steps: []
sprint: 15
---

> **ARCHITECT PANEL REQUIRED (author + 2–3 challengers + lead) — `agents/process/deliberation.md`.**
> This ticket's deliverable **is** the panel's output. No code, no migration, no schema.
> `git diff --stat -- src/` must be empty.

## Context

**Owner decision, 2026-08-02: *"You can upgrade."*** The express-upgrade perk becomes real code rather
than the copy being deleted. That answers T-0493's "which of the three readings" question in favour of
**reading A (a price benefit)** — *and it adds a second thing that does not exist anywhere in the
platform: a **metered** benefit.*

### The gap, PM-verified first-hand at `master` (2026-08-02)

| Claim | Verification |
|---|---|
| `MembershipPlan.AllowsExpressUpgrade` is read by zero pricing code | **CONFIRMED.** Every reference is a mapper, a DTO, an admin CRUD command, an entity config, a migration or a seed script. `GetMyMembership.cs:60` and `GetMembershipPlans.cs:68` return it to clients; nothing consumes it |
| `BookingPolicy.RequiresExpressSurcharge` has no membership parameter | **CONFIRMED.** `BookingPolicy.cs:68` — `RequiresExpressSurcharge(DateTime cleaningUtc, DateTime nowUtc)`. Pure time function |
| A Plus member pays the same +20% | **CONFIRMED.** Three call sites, none membership-aware: `OrderFactory.cs:100-102`, `QuoteOrder.cs:168`, `OrderPricingCalculator.cs:65` |
| The domain defers the counter to a future tracker | **CONFIRMED.** `MembershipPlan.cs:99-104`: *"When true, usage is capped — see the future 'membership benefit usage' tracker."* **This ticket is that tracker's decision.** |

### Why this is a decision and not a build

The advertised copy is **"One free same-day booking per month, no surcharge"**
(`values/strings.xml:844`, and the same string on iOS). A *quota* is not a flag. It needs a counted
thing, a period boundary, a consumption point, and — the part that is always underestimated — a
**reversal rule**. Orders get cancelled and refunded. If a cancelled express booking does not return
the quota unit, the member has paid for a perk and lost it to a cancellation; if it always returns it,
book-and-cancel is a free denial-of-quota exploit against our own margin.

### The archetype the panel must consider first (`consistency.md` — do not invent a new seam)

**This platform already threads a membership benefit into a policy, and there is exactly one way it
does it.** `BookingPolicy.CalculateCancellationFeeRate` takes a `freeCancellationHoursOverride`
that `CancellationPolicyResolver` fills from the member's plan (`BookingPolicy.cs:101-111` documents
the contract in detail). **The express waiver's default shape is the same shape: a resolver that
answers "what does this customer's plan do here", and a policy function that takes the answer as a
parameter.** A design that departs from it must say why.

## Acceptance criteria

- [ ] **AC1 — the counted unit is named and defined.** What exactly is one use? An order created inside
      the express window with the surcharge waived — or an order *completed*? These differ for every
      cancelled order. Evidence: the definition, plus the lifecycle point it is decided at, cited
      against the `New → Pending → Confirmed → … → Completed / Cancelled` states in `CLAUDE.md`.
- [ ] **AC2 — the period boundary is designed to support BOTH answers to `Q-PLUS-02`.** The owner has
      not yet ruled calendar-month vs billing-anchored. **The schema must not have to change when they
      do.** Show the shape that survives both (e.g. a stored period key + the rule that computes it)
      and state the cost of the alternative. Evidence: the shape plus the both-ways demonstration.
- [ ] **AC3 — the reversal rule is decided, with the exploit named.** Cancel / refund / admin-cancel /
      no-show: does the unit come back? **Name the abuse case for whichever way it goes** and say what
      bounds it. "We will decide later" fails this AC — later means in production. Evidence: the rule
      plus the abuse analysis.
- [ ] **AC4 — concurrency is addressed.** Two simultaneous bookings by a member with one unit left.
      This is a money decision resolved by a row, so it needs an ordering guarantee — a unique
      constraint, a transaction boundary, or an explicit "we accept double-spend once and reconcile".
      Evidence: the mechanism, named at the DB level.
- [ ] **AC5 — the tracker is GENERAL, not express-specific, or the ADR says why not.** The domain
      comment calls it a *"membership benefit usage"* tracker. Plus has five perks; a second metered
      one (free cancellations? priority support?) is plausible. Decide: one table keyed by benefit, or
      a column per benefit. **State the extension cost of the choice** — this is the same
      generality question T-0517 answers for payout details, and the two should not answer it
      differently by accident. Evidence: the decision plus the extension cost.
- [ ] **AC6 — the resolver seam matches `CancellationPolicyResolver` or the departure is defended.**
      Evidence: the seam, and either the mirror or the justification at file:line.
- [ ] **AC7 — the read path for the client is specified.** The clients must be able to show *"1 free
      express left this month"* (T-0514) — that is a field on an existing response, not a new endpoint,
      unless the ADR argues otherwise. **`GetMyMembership.cs` is the obvious host and it already
      returns `AllowsExpressUpgrade`.** Evidence: the DTO delta, exact enough for T-0493 to build and
      T-0514 to consume.
- [ ] **AC8 — the ADR is written to `agents/backlog/adr/00NN-*.md` and the living decision doc under
      `agents/architecture/decisions/` is updated in the same step** (`process/documentation.md` — a
      finalized artifact with stale docs is not finalized). Evidence: both files.
- [ ] **AC9 — the deliberation trail stays in the artifact.** `## Challenge` / `## Defense` /
      `## Verdict` sections survive into the ADR. A challenger that finds nothing says so and names
      what it checked. Evidence: the sections.
- [ ] **AC10 — alternatives and why-not are recorded.** At minimum: a counter column on
      `UserMembership`; a usage-event table; deriving the count from `Order` rows at query time (no new
      table at all — **this one is genuinely attractive and must be argued against, not ignored**).
      Evidence: the alternatives table.
- [ ] **AC11 (Gate 0.5 leg 3)** — state what the panel did not examine and which claims are reads
      rather than runs.

## Out of scope

- **Writing the migration or the entity** — **T-0512**, which consumes this ADR.
- **Changing the pricing code** — **T-0493**.
- **The quota's VALUES** (one per month? rollover? which boundary?) — those are `Q-PLUS-02`, an owner
  answer. **The ADR designs the mechanism and states a default for each; it does not pick the
  business number.**
- **The other four Plus perks.** T-0492 / T-0494 / T-0495 / T-0498.
- **The copy discrepancy** — **T-0513**. The three clients currently advertise three different express
  perks; that is a client ticket and it is independent of this mechanism.

## Implementation notes

**Read first:** `MembershipPlan.cs:99-105`, `UserMembership.cs` + `MembershipStatus.cs`,
`BookingPolicy.cs` in full (especially `:80-111`), `CancellationPolicyResolver.cs`,
`OrderPricingCalculator.cs`, `QuoteOrder.cs:150-180`, `OrderFactory.cs:90-130`, and
`agents/knowledge/consistency.md`.

**The predicate warning, carried from T-0493 and T-0494:** "does this user have an active Plus
membership" must exist **once**. T-0494 (server-side recurring gate) needs the same predicate. Whoever
lands first owns it; the ADR names where it lives so the second one reuses it.

## Status log
- 2026-08-02 — **draft → `ready` (created by pm from the owner's 2026-08-02 answer *"You can
  upgrade"*).** Filed as an **architect panel** because the perk the owner approved requires an
  extension point the domain explicitly defers (`MembershipPlan.cs:100-104`) — a metered benefit with
  a period boundary, a consumption point and a reversal rule, none of which exist. **`ready` rather
  than `draft`: it passes DoR, has no unmet dependency, and the panel is step 1.** The three quota
  *values* the owner still owes are `Q-PLUS-02` and are deliberately **not** a dependency — AC2
  requires the design to survive either answer.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). The deliverable is **ADR-0035**
  (`agents/backlog/adr/0035-metered-membership-benefit-usage.md`), drafted `e052684f`, challenged in
  `eee24957`, **accepted `15d80faa`** with 16 binding amendments. **Verified at HEAD:** the ADR header
  reads `- **Status:** accepted`. Four of D3's five mechanisms did not survive the panel — AM-5 replaced
  COUNT-of-live with `generate_series + NOT EXISTS + ORDER BY LIMIT 1` because the original yielded
  cardinality, not the smallest free ordinal, and never restored capacity. The owner's four membership
  rulings (PastDue, trial, plan swap, lapsed recurring) were folded in at `eefa6293` as AM-17/18/19.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read the ADR header at HEAD; consensus reached with
three challengers and 30 independent challenges. Two rulings went AGAINST the author's own
recommendations, which is the evidence that the panel was adversarial rather than confirmatory. **No
`manual_steps` on this ticket.**

