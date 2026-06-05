# Flow Change — Defense Panels + Role-Owned Living Docs (2026-06-01)

What the owner asked for, what the setup had before, and exactly what changed. (Gap analysis requested
before execution.)

## The ask
A *panel* of analysts and a *panel* of architects who **see the whole business logic**, **critically
challenge each other and make authors DEFEND their decisions**, reach **consensus** before delegating,
and keep **role-owned living documentation** (analysts: business logic + diagrams; architects:
decisions; developers: their own) updated **in parallel**.

## What the setup had BEFORE
- **One** `analyst` and **one** `architect` charter, spawned as N independent instances working on
  *different* subsystems. No mechanism for peers to challenge each other or reach consensus.
- Stories/ADRs were produced by a single instance and handed off. No adversarial defense.
- Architecture docs existed (`docs/architecture/*`) but there was no **role-owned living doc**
  discipline — no analyst business-logic+diagram tree, no architect living-decision tree, no rule that
  docs update in parallel with the work.

## What CHANGED (the deltas now in place)
1. **Defense panels** — `agents/process/deliberation.md`. Every story and every decision is now
   produced by a panel: an **author** who owns and defends it, **2–3 challengers** tasked to attack it
   (find AC gaps, missing states, wrong rules, broken seams), and a **lead** who adjudicates. Finalized
   only when **no challenge survives unanswered** (consensus). The trail (`## Challenge` / `## Defense`
   / `## Verdict`) stays in the artifact so developers see *why* and *what was rejected*.
2. **Panel modes on the existing charters (DRY)** — the `analyst` and `architect` charters gained
   **author / challenger / lead** modes the PM assigns at spawn time. No duplicate charters; N
   instances in different modes.
3. **Consensus precedes ticketing** — the PM charter + lifecycle now run the deliberation panel
   **before** a story/ADR becomes a ticket. Mechanical no-decision tickets skip it.
4. **Role-owned living docs, updated in parallel** — `agents/process/documentation.md` +
   `agents/analysts/<domain>.md` (business logic + **Mermaid** diagrams + story map) +
   `agents/architecture/decisions/<topic>.md` (living decision docs; immutable ADRs stay in
   `backlog/adr/`). A finalized artifact with a stale doc **is not finalized** — the lead enforces it.
5. **Whole-business-logic view** — challengers and the lead read the domain's living business-logic
   doc (not just the one story), so the panel reasons about the *whole* application, as asked.

## What did NOT change (and why)
- **Developers/testers/reviewers/security/optimizers/PM** still pick up the *finalized* output — the
  panel sits *upstream* of them, exactly as asked. Their charters are unchanged except they now read the
  deliberation trail and the living docs.
- **TDD, the 8 gates, the consistency system, mechanical enforcement** — all unchanged and still apply;
  the panel feeds them better specs.
- **One-charter-per-role / parallelize-at-runtime** — preserved. Panels are runtime instance groupings,
  not new charter files.

## Cost note (honest)
"Every story and every decision" through a defense panel is the **maximum-rigor** setting you chose —
it spends materially more tokens/time up front per story than a single-instance draft. The trade is
fewer defects/reverts after launch. If it ever feels too heavy for routine work, the one dial to turn
is `deliberation.md` "When a panel convenes" (e.g. → big-decisions-only). No other change needed.
