---
id: T-0552
title: F1 — ADR-0032 carries TWO stale statements and is `accepted`; it needs one signed erratum, not a quiet edit
status: ready
size: XS
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0031, 0032, 0033]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 15
source: finding **F1**, widened by the independent lead pass (ADR-0033 `:1051-1053`, `:1063`;
  `catalog-governance.md:280`). Filed as an `INDEX.md` row for the first time 2026-08-05
---

## Context

**ADR-0032 is `accepted`, so it cannot be quietly edited.** `agents/backlog/adr/README.md:7-29` allows
exactly three instruments: a dated appended section, a superseding ADR, or — for a narrow class of
corrections — an **in-body annotation ratified by a signed, dated erratum block appended to the ADR**.
*"An unsigned in-body edit — whoever made it — is a process violation until ratified or reversed."*

It carries **two** stale statements, not one. The second was made stale by the T-0471 round itself,
which is why the original F1 filing understates the scope.

| # | Line | What it says | Why it is stale (PM-verified at HEAD, 2026-08-05) |
|---|---|---|---|
| **1** | `0032-…md:23-25` — the **Number note** | *"**0031 is taken** by `0031-nswag-regen-drift-is-guarded-at-regen-time.md`, which exists **only in T-0439's worktree and has not reached `master`** … A reader on `master` sees a gap at 0031 until T-0439 merges."* | **0031 is on `master`.** `git log -- agents/backlog/adr/0031-…md` → `acf2f0bc` *"feat(web): guard the NSwag regen against client/call-site drift [T-0439] (#175)"*. There is no gap, and a reader following this note goes looking for a merge that already happened |
| **2** | `0032-…md:14` — the **Split note** | *"**ADR-0033 is `proposed`, not accepted** (see its status block)."* | **ADR-0033 is `accepted`** — its status block reads `accepted`, dated 2026-08-05, amended and accepted by the T-0471 panel (`34a3c733`, `0e1af548`). Made stale by the very round that produced this finding |

Statement 2 is the one that does damage: a reader who checks whether ADR-0033 binds, and trusts
ADR-0032's sentence, concludes the routing rule is not yet decided. The truth is more specific and
worse — ADR-0033 is `accepted` **and not in force**, for a different reason entirely (T-0549/FT-11).
One document saying "proposed" while the other says "accepted and not in force" gives a reader two
wrong answers and no way to choose between them.

## Acceptance criteria

- [ ] **AC1 — one erratum, covering both statements.** Given ADR-0032, When the correction is made,
      Then a **single dated, signed erratum block is appended** to the ADR naming **both** `:14` and
      `:23-25`, each with the fact that made it stale and the evidence for that fact (`acf2f0bc` for
      one; ADR-0033's status block + `0e1af548` for the other). Two separate quiet fixes, or an erratum
      naming only the Number note, fails this AC.
- [ ] **AC2 — the instrument is chosen and justified against `adr/README.md`.** Given the erratum
      lane's four conditions (`adr/README.md:16-26`), When the architect writes the block, Then it
      states which instrument was used and why it qualifies — in particular that **no decision content
      changes**: not the chosen option, not a threshold, not the scope, not an alternative's
      disposition, not the rationale. If the architect judges either statement to fail that test, the
      correct instrument is a dated appended section (`README.md:9-12`), **not** an in-body edit, and
      the ticket lands that instead. **The one thing that is not permitted is editing either line
      silently.**
- [ ] **AC3 — any in-body annotation is bracketed, dated and self-describing** (`README.md:23-24`), and
      cites the source line it corrects. If the architect chooses to annotate in place, `:14` and
      `:23-25` each carry such an annotation, and both are covered by AC1's signed block.
- [ ] **AC4 — the living doc moves in the same change** (`README.md:31-32`). Given
      `agents/architecture/decisions/catalog-governance.md`, When the erratum lands, Then its header
      (`:4-8`) and open-items row for F1 (`:280`) reflect the corrected state, so the current-shape
      record never contradicts the ADR trail.
- [ ] **AC5 — ADR-0032's status is unchanged.** Given the ADR, When this ticket closes, Then it is
      still `accepted`, with no `superseded` marking. This is a correction of two facts, not a decision
      change — and if it turns out to be one, this ticket stops and routes to a panel.

## Out of scope

- **Renaming ADR-0032's file** to match its amended title — that is **FT-7**, still unfiled as a ticket
  (`catalog-governance.md:290`); a `git mv` + link sweep, and a different change.
- **Anything about ADR-0033's own status.** It stays `accepted`; its not-in-force state is recorded in
  the living doc and repaired by T-0549.
- Correcting stale statements in any other ADR. This ticket is scoped to the two lines above.

## Implementation notes

**Files this ticket touches:**
- `agents/backlog/adr/0032-catalog-law-declarations-require-a-named-ci-gate.md` — `:14`, `:23-25`, plus
  the appended erratum block at the end of the file.
- `agents/architecture/decisions/catalog-governance.md` — `:4-8`, `:280`.

**The PM does not write this.** Only the architect signs an erratum (`README.md:25-26`). This ticket
specifies it; it does not perform it — the same separation as T-0549.

### Staleness detectability (sprint-15 §D3)

`agents/architecture/**` is excluded from the candidate-3 path rule and `agents/backlog/adr/**` is not a
product path, so **no path-based signal can flag this ticket**. It is also, ironically, a ticket *about*
a stale document — which is the argument for closing it rather than carrying it. Manual check at
dispatch, two commands:
`grep -n 'ADR-0033 is .proposed' agents/backlog/adr/0032-*.md` and
`git log --oneline -1 -- agents/backlog/adr/0031-*.md`.

**No-decision note:** a record correction with no decision content — no panel. AC2 is the guard that
keeps it that way: if the correction turns out to change decision content, it becomes a supersede and
gets one.

## Status log
- 2026-08-05 — created `ready` by pm. Filed from finding F1 as **widened** by the independent lead pass:
  two stale statements, not one. Had no `INDEX.md` row before this filing.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
