---
id: T-0495
title: "Favourite cleaner" is sold as a Plus perk, has no matching algorithm, and is not Plus-gated
status: draft
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0491]
blocks: []
stories: []
adrs: []
layers: [analyst, backend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** *"Favourite cleaner has no matching algorithm and
isn't Plus-gated."*

**Status: RELAYED from the investigation, traced by it to file:line, NOT re-verified by the PM.**

### Why this is the odd one out of the three unenforced perks

The other two (express, recurring) are **features that exist and are not gated**. This one is
different in kind: **there is nothing to gate.** If there is no matching algorithm, then "favourite
cleaner" is not a perk that leaks to non-subscribers — it is a perk that **nobody receives, including
the people paying for it.** Those are different problems with different urgency:

- an ungated perk costs the platform margin;
- an unbuilt perk that is **advertised on a paid subscription** is a misrepresentation.

**The second is worse, and it is why this ticket is `analyst`-owned rather than `backend`-owned.**
The first question is not "how do we build matching" — it is "do we build it, or do we stop selling
it until we do."

### What must be established before any build

1. **Does a favourite/preferred-cleaner relationship exist in the data model at all?** If a customer
   cannot even *mark* a cleaner as a favourite, then matching has no input and the perk is a
   marketing string with no backing anywhere.
2. **Is there an assignment path to influence?** Cleaners take orders from an available pool
   (the partner order-detail flow is take/start/complete, and there is an
   `order.new_available` 30-minute digest per `Q-FEED-02`). **A pull model has no assignment step to
   bias.** Preferring a specific cleaner in a pull model means either notifying them first and
   holding the order, or moving to an assignment model — a substantial change to how the platform
   works, not a filter.
3. **What does the customer-facing copy actually promise?** "Your favourite cleaner when available"
   and "the same cleaner every time" are different products. T-0491 AC1 quotes it.

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH the finding, with the three questions above answered at file:line.** Does
      a favourite relationship exist in the domain? Is there an assignment step? What does the copy
      promise? Evidence: three answers, each cited. **If matching turns out to exist, close this
      ticket and say so.**
- [ ] **AC2 — the RECOMMENDATION comes before the design, and it is allowed to be "stop selling
      it".** Three options, each priced: **(a)** build matching, **(b)** re-scope the perk to what is
      buildable on the current pull model (e.g. "your favourite cleaner is notified first"),
      **(c)** withdraw the perk from the copy until it exists. **Option (c) is a legitimate outcome
      and the story must not treat it as failure** — for a live paid subscription, removing an
      untrue claim is faster and safer than building a feature to make it true. Evidence: the three
      priced options plus the recommendation.
- [ ] **AC3 — if (a) or (b), the perk is SPECIFIED to the standard T-0491 AC2 requires:** one
      sentence a test could check. Including the failure case — what does the customer see when the
      favourite is unavailable? Evidence: the specification.
- [ ] **AC4 — the assignment-model consequence is stated plainly.** If the recommendation requires
      moving from pull to assignment (even partially, even for one order), **say so in one sentence
      at the top of the recommendation.** That is an architecture change and the owner must see it as
      one, not discover it in a ticket estimate. Evidence: the sentence, or an explicit "no
      assignment-model change required".
- [ ] **AC5 — the Plus gate is specified alongside**, and it lands **server-side** for the same
      reason T-0494 exists. Evidence: the specification.
- [ ] **AC6 — the output is a SPECIFICATION plus at most three sized implementation candidates.**
      **This ticket builds nothing.** `git diff --stat -- src/` is empty. Evidence: the candidates.
- [ ] **AC7 (Gate 0.5 leg 3)** — state what was not investigated, and every claim that is a read
      rather than a run.

## Out of scope

- **Building matching.** AC6. If the recommendation is (a), the build is a new ticket sized from the
  specification — and given AC4, quite possibly an epic.
- **The other perks** — T-0492, T-0493, T-0494.
- **Changing the assignment model.** AC4 *names* it as a consequence; nothing here changes it.
- **Removing the marketing copy.** If (c) is recommended, the copy change is a client ticket across
  iOS + Android + web × 5 locales, filed after the owner accepts the recommendation.

## Implementation notes

**Analyst-led, with a `backend` instance for AC1's code archaeology.** The panel is **T-0491**; this
ticket is one of its outputs and does not convene a second one — but its recommendation (AC2) is
adversarial by nature and should be challenged by at least one of T-0491's challengers before it
reaches the owner.

**Read first:** the order assignment/availability path, `Core.Domain/Memberships/*`, and T-0491 AC1's
copy table.

**The honest framing for the owner, and the story should carry it:** of the five perks, this is the
one where "we will build it" may be the wrong answer. A subscription with four honest perks is a
better product than one with five, of which one is a promise nobody can keep.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **Finding marked RELAYED, not
  PM-verified.** Filed `analyst`-owned rather than `backend`-owned because the defect is a perk that
  *nobody* receives — including paying subscribers — which makes the first question "should this be
  sold at all", not "how do we gate it". **AC2 explicitly permits "withdraw the claim" as the
  recommended outcome**, so the story is not structurally forced toward a build it cannot justify.

## Review
