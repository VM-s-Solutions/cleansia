# Deliberation — Defense Panels for Stories & Decisions

Stories and architectural decisions are not produced by a lone specialist and handed off. They are
**defended in front of challenging colleagues** and only **finalized by consensus**. An author must
*defend* their work; challengers are tasked to *attack* it; a lead *adjudicates*. Nothing reaches
developers until no challenge survives unanswered.

This is the spec-first heart of the system: a story/ADR that has survived adversarial defense is a far
better spec than one written once and shipped. It costs more up front and saves far more downstream.

## When a panel convenes
**Every user story and every architectural decision** (the owner's standing instruction — maximum
rigor). The Analyst panel deliberates stories/business-logic; the Architect panel deliberates ADRs/
decisions. Pure mechanical tickets that introduce **no** new behavior or decision (a magic-number fix,
a consistency-cleanup `T-*`) carry a one-line "no-decision" note from the PM and skip the panel — but
anything that defines *what the system does* or *how it's structured* goes through it.

## The roles (assigned at spawn time — same charter, different mode)
The PM spawns instances of the existing `analyst` / `architect` charter in one of three **modes**,
named in the invocation:
- **Author** — drafts the artifact and **owns** it. Must defend every part of it.
- **Challenger** (2–3 of them) — tasked to **attack**: poke holes in the AC, surface missing edge
  cases and lifecycle states, dispute the business logic, challenge the seam/trade-off, find the
  unstated assumption. A challenger that finds nothing says so explicitly *and* names what they
  checked (silence is not assent).
- **Lead** — adjudicates. A challenge stands only if the author failed to defend it convincingly or
  conceded. Declares consensus reached (or escalates to the owner if it can't be).

The author and lead must be **different instances**. Challengers must be **different instances** from
the author. (Same charter, parallel instances — DRY.)

## The loop (author defends → challengers attack → lead rules)
```
1. AUTHOR drafts the story/ADR (grounded in real code + the audit findings).
        │
2. CHALLENGERS attack it (in parallel). Each writes, in the artifact's `## Challenge` section:
     - the specific hole (AC gap, missing state, wrong business rule, broken seam, unstated assumption)
     - why it matters (cite the code / lifecycle / a persona scenario)
        │
3. AUTHOR DEFENDS each challenge in writing in the `## Defense` section, one of:
     - REBUT (the challenge is wrong — here's the evidence), or
     - CONCEDE + REVISE (fold the fix into the artifact), or
     - ESCALATE (a real business decision only the owner can make → questions/open.md)
        │
4. CHALLENGERS re-check the revised artifact. New holes → repeat from 2.
        │
5. LEAD adjudicates every open point: each challenge is RESOLVED (defended or fixed) or it BLOCKS.
     Consensus = zero blocking challenges remain. The lead records the verdict + the key decisions.
        │
6. FINALIZED. The artifact is locked; the PM may now create tickets / the architect may accept the ADR.
```
Cap at a sensible number of rounds; if consensus can't be reached, the lead escalates the *specific
disagreement* to the owner via `questions/open.md` rather than letting it loop.

## What "defended" means (the bar)
- A REBUT must cite evidence (code at file:line, the documented lifecycle, a persona scenario) — "I
  disagree" is not a defense.
- A CONCEDE must actually change the artifact, not just acknowledge the point.
- An AC that a challenger showed is ambiguous or unobservable does not survive — it gets rewritten or
  the challenge blocks.
- A decision with a real trade-off must have its **alternatives and why-not** in the record (the
  challenge surfaced them; the defense answers them). This is what makes the ADR trustworthy later.

## The output handed to developers
A finalized story/ADR carries its **deliberation trail**: the `## Challenge` / `## Defense` / `##
Verdict` sections stay in the artifact. Developers (and testers, reviewers, security, optimizers) read
not just the conclusion but *why it's the conclusion and what was rejected* — which prevents them
re-litigating settled points and tells them the edges that were considered. The story's AC are then
the **TDD targets** (`knowledge/testing.md`): the tests encode what survived the defense.

## Parallel documentation (non-negotiable, happens during deliberation)
Each role keeps its own **living documentation**, updated *as part of* finalizing — not a later chore:
- **Analysts** own `agents/analysts/<domain>.md` — the business logic in prose **+ Mermaid diagrams**
  (flows, state machines, decision trees) + the living story map for the domain. When a story is
  finalized, the domain's business-logic doc is updated in the same step so it never drifts.
- **Architects** own `agents/architecture/decisions/<topic>.md` — living design notes, the trade-off
  space, and the current shape (the **immutable ADRs** stay in `backlog/adr/`; these are the
  *evolving* companion). Updated when a decision is finalized.
- **Developers** own their implementation notes alongside the canonical `docs/architecture/*` (kept in
  sync by the `docs` agent). When a ticket lands, the dev updates the implementation pointer.

See `process/documentation.md` for the structure and the diagram conventions. The rule: **the
documentation is updated in parallel with the work, by the role that owns it — a finalized artifact
with stale docs is not finalized.**

## Why this isn't just bureaucracy
The platform is going to production and is large. A story written once and shipped carries the author's
blind spots straight into code; an ADR decided alone carries one person's trade-off preference. The
defense panel converts *individual judgment* into *surviving-the-best-objections judgment* — which is
the difference between "it compiled" and "it's right." The cost is paid in tokens up front; the saving
is paid in defects, rework, and reverts not happening after launch.
