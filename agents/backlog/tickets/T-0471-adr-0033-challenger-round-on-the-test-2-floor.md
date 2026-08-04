---
id: T-0471
title: ADR-0033 — run the one challenger round on the test-2 floor, then accept or amend
status: ready
size: S
owner: architect
created: 2026-08-01
updated: 2026-08-01
depends_on: []
blocks: []
stories: []
adrs: [0032, 0033]
layers: [architect]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

**ADR-0033 is `proposed` and cannot bind until one challenger round runs against exactly one item.**
This ticket is that round. It exists because the panel lead did the right thing and said so out loud,
and the record is only worth having if somebody acts on it.

**What happened.** ADR-0032's panel split the draft on challenge C8 (`adr/README.md:3` — one decision
per ADR). Two of the three decisions were one decision and moved to **ADR-0033**: *what a ticket may
write into the catalog by itself, and how strong a claim it may make about a stack it never ran.*
Inside it, **challenge C5 demanded a floor on test 2 without proposing one** — the objection being
that `conventions.md:132` already sets the bar for *any* catalog entry at "makes the codebase more
consistent", so read literally **every** entry that earns its place forbids some less-consistent
alternative, test 2 fires on everything, and the inline harvest lane dies.

**The lead authored the floor itself, and then declined to ratify its own repair.** ADR-0033's status
block states the reason in the terms that matter:

> A lead may adjudicate between positions the parties argued; **inventing the repair and then
> ratifying it is not adjudication.** So this ADR needs **one** challenger round on **exactly one
> item** — the floor — and nothing else is re-opened.

That is correct and it is the whole justification for this ticket. It is also **cheap**: one item, one
round, three named lines of attack already written down by the person who wants them pressed.

**Nothing regresses while it sits.** `conventions.md:125-127` already routes "anything that changes
'the one way to do X'" to the Architect, unchanged, and **ADR-0032 is `accepted`** and already governs
what a constraining entry must state. This is a ticket about closing an open panel, not about an
unguarded gap.

## The one item under challenge — quoted, so nobody re-scopes it

**ADR-0033 §D1, test 2, "The floor":**

> Test 2 fires on a **narrowing** — withdrawing a form the catalog previously permitted, or replacing
> a named canonical form — and **not** on the first statement of a canonical form where the catalog was
> silent and no shipped call site becomes a deviation.

**The three lines of attack the lead nominated** (ADR-0033 `## Challenge`, `:365-381`) — the challenger
is not limited to these, but must address them:

1. **Is "previously permitted" decidable?** Catalog *silence* is not the same as *permission*. If "the
   catalog said nothing about X" is always arguable, the floor is an escape hatch and test 2
   under-routes exactly like the "gap vs clarification" axis it replaces. The ADR names this as the
   floor's own soft edge; **is the reviewer's "name the withdrawn form" check (§How a reviewer verifies
   compliance, item 3) enough to close it?**
2. **Does the floor contradict test 1?** If a first-statement-of-a-form is inline whenever no shipped
   call site violates it, the catalog can acquire canonical forms with **no Architect involvement at
   all**. That is either the right answer (nothing is obliged, the harvest loop stays open) or it is
   how "the one way to do X" gets redefined by whoever ships first — which is what
   `conventions.md:125-127` exists to prevent.
3. **Is the retro-validation honest, or fitted?** The floor changes **exactly one row** of the
   four-row table: T-0441 stays inline *because* it added a test obligation where the catalog was
   silent rather than withdrawing a permitted form. **One row is thin evidence for a rule that governs
   every future catalog edit.** Find a case where the floor gets it **wrong** — a
   first-statement-of-a-form that plainly should have been an Architect call.

## Acceptance criteria

- [ ] **AC1 (the round runs, and it is a real panel)** — A **challenger instance distinct from the
      ADR-0033 lead** attacks the floor, and a **lead instance distinct from both** adjudicates, per
      `agents/process/deliberation.md`. The trail lands in ADR-0033's existing `## Challenge` /
      `## Defense` / `## Verdict` sections. **A self-challenge does not satisfy this AC** — a
      self-authored, self-ratified floor is the precise defect this ticket exists to repair, and
      T-0439 already shipped an ADR that had to be re-panelled for the same reason (`proposed` →
      `accepted` only after a real challenger round).
- [ ] **AC2 (scope held)** — **Only the test-2 floor is re-opened.** Explicitly NOT open, and carried
      consensus from the ADR-0032 panel: **test 1** (called *objective and unattacked* by the
      challenger), **test 3 / D2 cross-stack claim strength** (its structural-vs-behavioural line
      called *drawn on the right property*), and everything ADR-0032 settled. If the challenger
      believes one of those must move, it says so as a **separate finding** for the PM to file — it
      does not fold it into this round.
- [ ] **AC3 (each nominated line of attack is ruled, none silently dropped)** — All three lines above
      get an explicit SUSTAINED / SUSTAINED-IN-PART / OVERRULED with reasoning. The ADR-0031 panel set
      the precedent that mattered here: three challenges once reached the lead late and were
      back-filled specifically so that **no challenge in that panel closed unruled**. Same bar.
- [ ] **AC4 (attack 3 is answered with a CASE, not an argument)** — The retro-validation table is
      four rows and the floor moves one of them. The challenger must **either** produce a real
      historical catalog edit where the floor routes wrongly, **or** state plainly that it searched and
      found none, naming what it searched. **"I could not find a counter-example" is a pass of this AC
      and a useful result; silence is a fail** (Gate 0.5 leg 3 applied to a deliberation).
- [ ] **AC5 (the ADR ends the round in a terminal state)** — ADR-0033 leaves this ticket either
      **`accepted`** (floor as written, or amended per the ruling) or **`rejected`** with the
      replacement named. `proposed` is not an acceptable end state; that is where it already is.
- [ ] **AC6 (whatever lands is consistent with ADR-0032, which is already accepted and binding)** —
      ADR-0033 declares it *"consumes ADR-0032"*. If the ruling changes what routes inline, the
      **enforcer + tier obligation** (ADR-0032 D2 — every constraining entry carries
      `**Enforced by:** <enforcer> — <tier token>`) must still hold on an inline entry, or ADR-0032 is
      being amended by side effect and needs its own erratum.
- [ ] **AC7 (Gate 0.5 leg 3 — say what this could not verify)** — This is a deliberation with no
      executable assertion. **Leg 1 does not apply and leg 2 does not apply**; say so explicitly rather
      than manufacturing a test. `agents/knowledge/testing.md` calls the alternative theatre, and
      ADR-0032's own D-series was written against exactly this temptation.

## Out of scope

- **ADR-0032.** It is `accepted` (amended, 2026-08-01). Its follow-ups **FT-1…FT-7** are its own
  business and are not this ticket's. *(One of them, **FT-1**, is already discharged — see the note
  below.)*
- **Re-litigating the ADR-0032/0033 split.** The split was ruled on C8 and is not re-opened.
- **Writing any catalog entry.** This ticket rules on the routing rule; it edits no
  `agents/knowledge/*.md` file and touches no `patterns-*.md` lane.
- **The `agents/knowledge/conventions.md` edit that would follow acceptance.** If the ruling requires
  `conventions.md` to change, that is a follow-up ticket (the T-0445 / T-0456 / T-0460 precedent —
  process/knowledge docs are routed as `architect` + `docs` tickets), filed by the PM after the verdict.
- **ADR-0032 FT-7's rename of the 0032 file.** Untouched here.

## Implementation notes

- **Read in this order:** ADR-0033's status block (why it is `proposed`) → §D1 test 2 and its floor →
  §Consequences "What could go wrong" (the lead names the soft edge itself) → §How a reviewer verifies
  compliance items 3–4 → `## Challenge`. Then ADR-0032 §D1/D2 for the tier vocabulary the ruling must
  stay consistent with.
- **Worked precedent for the shape of this round:** the ADR-0031 panel on T-0439 — author's
  pre-answers, `CH-1…CH-14`, a third-instance lead, mandated amendments **M1–M6**, and a **signed
  erratum** appended in a dated section when the accepted ADR later needed correcting. `adr/README.md`
  rules an unsigned in-body edit to an `accepted` ADR a process violation. Follow that.
- **A note the challenger will otherwise trip over:** ADR-0032 and ADR-0033 both carry a "Number note"
  saying **0031 exists only in T-0439's worktree and a reader on `master` sees a gap at 0031.**
  **That is no longer true** — T-0439 merged as `acf2f0bc` (PR #175) and
  `agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md` is on `master`. There is no
  gap. Both notes are stale and each needs a **signed erratum**, not an inline edit — ADR-0032 is
  `accepted`, so the rule binds it. Fold the ADR-0033 one into this round's verdict; **file the
  ADR-0032 one as a separate finding for the PM** rather than editing an accepted ADR from inside
  another ticket.
- **Also worth knowing, because ADR-0032 predicted it and it has now happened:** **FT-1** (verify and
  close the `check-consistency.mjs` zero-file-scope `NOT RUN` banner) is **discharged on `master`**.
  `d6969fef` (PR #177) landed the fix. PM-verified on `1c8fdd00`:
  `--paths=<absolute>/src/Cleansia.App/libs` → **32 violations, exit 1** (it previously printed
  `OK (0 files scanned)` with exit 0), and `--paths=src/cleansia_ios` → **`NOT RUN`, exit 1**. FT-1 was
  re-scoped by the ADR-0032 panel from "build it" to "verify + close" precisely for this case. It does
  **not** change ADR-0032's D1 tier for the checker — it is still **T2-ADVISORY**, because it is still
  in **zero** `.github/workflows` (PM-verified).
- **No-decision note does NOT apply** — this ticket **is** a deliberation. It is `ready` because the
  deliberation it schedules is its deliverable, not a precondition to it.

## Status log
- 2026-08-01 — draft → **ready**, created by pm at the sprint-14 close-out. DoR: not a duplicate ✅
  (searched `INDEX.md` and `backlog/audits/`; ADR-0033 has no ticket at all — it was raised out of the
  ADR-0032 panel with `Ticket: none`) · AC observable ✅ · sized **S** — one item, one round, three
  pre-named lines of attack ✅ · `depends_on: []`, nothing gates it ✅ · `manual_steps: []` ✅ ·
  `security_touching: false`, `layers: [architect]` ✅ · archetype = the ADR-0031 panel on T-0439 ✅.
- 2026-08-01 — **priority: post-demo, but do not let it rot.** It blocks no shipping work — ADR-0032
  is accepted and `conventions.md:125-127` still routes conservatively meanwhile. What it costs to
  defer is that **every catalog edit made in the interim is routed by an unratified rule**, and the
  next reviewer/developer disagreement about an inline harvest has nothing to appeal to. The lead
  already declined to close it itself; leaving it `proposed` indefinitely converts that good judgement
  into an open question nobody owns.
- 2026-08-04 — **ARCHITECT DISPOSITION: STANDS UNCHANGED, `ready`, still `S`, nothing shipped against
  it.** Verified against the tree, not the ticket text:
  1. **ADR-0033 is still `proposed`** — its `:3` reads `- **Status:** proposed`, and its `## Challenge`
     is still the lead's three nominated lines of attack with no challenger text under them.
  2. **The round has not run.** `agents/backlog/adr/challenges/` holds **14** files — 0034 ×2, 0035 ×3,
     0036 ×3, 0037 ×2, 0038, 0039 ×2, 0040 — and **none for 0033**. Every other ADR in that range got a
     real challenger pass; this is the one that did not.
  3. **The Implementation-notes correction is still owed and is still accurate.** ADR-0032's and
     ADR-0033's "Number note" both still claim *"0031 exists only in T-0439's worktree… a reader on
     `master` sees a gap at 0031"*. `agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md`
     **is** on disk, so both notes are false. Fold ADR-0033's into this round's verdict; ADR-0032's stays
     a separate PM-filed finding (it is `accepted`, so it needs a signed erratum, not an inline edit).
  4. **Its cost of deferral just went up.** `ADR-0042` (the wire-enum decision, filed today) carries a
     bound `patterns-frontend.md` edit that **replaces a named canonical form** — i.e. exactly a test-2
     *narrowing*, routed to the Architect. That routing is correct under the floor as written *and* under
     the floor's most likely amendment, so ADR-0042 is **not** blocked on this. But it is the second
     catalog edit this sprint routed by an unratified rule, and the next one may not be so clear-cut.
  **No AC changes. No rescope. This ticket was correct when written and is still exactly what it says.**

## Review
<!-- challenger + lead write the verdict into ADR-0033; the PM records the outcome here -->
