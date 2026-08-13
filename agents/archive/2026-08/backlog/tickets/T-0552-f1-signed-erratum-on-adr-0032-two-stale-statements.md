---
id: T-0552
title: F1 — ADR-0032 carries TWO stale statements and is `accepted`; it needs one signed erratum, not a quiet edit
status: done
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

**ADR-0032 is `accepted`, so it cannot be quietly edited.** `agents/archive/2026-08/adr-deliberation/README.md:7-29` allows
exactly three instruments: a dated appended section, a superseding ADR, or — for a narrow class of
corrections — an **in-body annotation ratified by a signed, dated erratum block appended to the ADR**.
*"An unsigned in-body edit — whoever made it — is a process violation until ratified or reversed."*

It carries **two** stale statements, not one. The second was made stale by the T-0471 round itself,
which is why the original F1 filing understates the scope.

| # | Line | What it says | Why it is stale (PM-verified at HEAD, 2026-08-05) |
|---|---|---|---|
| **1** | `0032-…md:23-25` — the **Number note** | *"**0031 is taken** by `0031-nswag-regen-drift-is-guarded-at-regen-time.md`, which exists **only in T-0439's worktree and has not reached `master`** … A reader on `master` sees a gap at 0031 until T-0439 merges."* | **0031 is on `master`.** `git log -- docs/decisions/0031-…md` → `acf2f0bc` *"feat(web): guard the NSwag regen against client/call-site drift [T-0439] (#175)"*. There is no gap, and a reader following this note goes looking for a merge that already happened |
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
- `docs/decisions/adr-0032.md` — `:14`, `:23-25`, plus
  the appended erratum block at the end of the file.
- `agents/architecture/decisions/catalog-governance.md` — `:4-8`, `:280`.

**The PM does not write this.** Only the architect signs an erratum (`README.md:25-26`). This ticket
specifies it; it does not perform it — the same separation as T-0549.

### Staleness detectability (sprint-15 §D3)

`agents/architecture/**` is excluded from the candidate-3 path rule and `docs/decisions/**` is not a
product path, so **no path-based signal can flag this ticket**. It is also, ironically, a ticket *about*
a stale document — which is the argument for closing it rather than carrying it. Manual check at
dispatch, two commands:
`grep -n 'ADR-0033 is .proposed' docs/decisions/0032-*.md` and
`git log --oneline -1 -- docs/decisions/0031-*.md`.

**No-decision note:** a record correction with no decision content — no panel. AC2 is the guard that
keeps it that way: if the correction turns out to change decision content, it becomes a supersede and
gets one.

## Status log
- 2026-08-05 — created `ready` by pm. Filed from finding F1 as **widened** by the independent lead pass:
  two stale statements, not one. Had no `INDEX.md` row before this filing.
- 2026-08-05 — **ARCHITECT: DONE. The instrument is NOT an erratum — AC2's alternate lane fired, and
  that is the substance of the ticket rather than a technicality.** Both statements were re-verified at
  HEAD before anything was written (Gate 0): ADR-0033 `:3` reads `**Status:** accepted` dated
  2026-08-05, and `docs/decisions/adr-0031.md` is a tracked
  file (T-0439 merged as `acf2f0bc`, PR #175; ADR-0033's own corrected Number note `:19-27` records the
  same fact).
  - **Erratum lane refused, for both.** `adr/README.md:16-26` opens it only for *"a **transcription
    error** — a value mis-copied from the ADR's **own cited source**"*, and closes *"for digits, not
    meaning"* (`:29`). **Both sentences were true when written on 2026-08-01** and were falsified by
    later events. Nothing was mis-copied. Per AC2 the ticket therefore lands a **dated appended
    section** (`README.md:9-12`) — a **record-only closure** — which is the same call the T-0549/T-0551
    pass made four days ago on ADR-0033's false *"does not reverse"* header claim
    (`catalog-governance.md`, finding L3: *"meaning, not digits, so not the erratum lane"*). The closure
    states the line as reusable: **an erratum corrects the ADR against its own source; a closure records
    that the world moved.**
  - **AC1 — satisfied in substance, via AC2.** ONE dated, signed block, naming **both** `:14` and
    `:23-25`, each with the fact that made it stale and its evidence. It is a closure block, not an
    erratum block, because an erratum block here would have been the process violation the ticket exists
    to prevent.
  - **AC3 — satisfied.** Two in-body annotations, bracketed, dated, self-describing, each citing what it
    corrects and each **signed by the closure**. They are explicitly labelled `pointer … not an erratum`
    and follow the ADR-0031 §"Amendment ledger" V9 form (*"a dated pointer annotation … carries no
    decision content of its own"*), not the ADR-0031 §A erratum form. They exist because both stale
    sentences sit in the **header block** a reader consults to ask *"does this bind?"* — a correction 700
    lines below does not reach that reader. Original text left standing, byte-for-byte.
  - **AC4 — satisfied, and half of it was already true.** The living doc's F1 open-item row is now
    closed with the instrument and the reason. **Its header (`:4-8`) needed no edit — verified: it
    already reads ADR-0033 `accepted` 2026-08-05.** Recorded rather than edited, per the standing rule
    about not manufacturing a diff. A dated Deliberation-history bullet was added.
  - **AC5 — satisfied.** ADR-0032 is still `accepted`; no clause altered; no `superseded` marking. No
    decision content changed, so this correctly did not route to a panel.
  - 🔎 **One finding the ticket did not have: there is a THIRD occurrence of the same phrase, and it
    must NOT be corrected.** ADR-0032's `## Verdict` **C5** row (`:624` before the annotations shifted
    it) also says *"ADR-0033 is
    `proposed`, not accepted"*, there as the panel's stated **reason** for leaving the split-off decision
    unaccepted. It is **left untouched** and the closure says so in a named section: a verdict row pins
    what was ruled, and ADR-0031 §A already settled that class (*"re-anchor citations that help a future
    reader find current config; **leave citations that pin what was ruled on**"*). The discriminator is
    the sentence's **job**, not its wording — `:14` and `:23-25` are forward-looking pointers (*"see its
    status block"*, *"until T-0439 merges"*); `:624` is a finding. Anyone re-running the ticket's own
    dispatch grep (`grep -n 'ADR-0033 is .proposed' …`) will get a hit at `:624` and must not act on it.
  - **Out of scope confirmed untouched:** FT-7 (the file rename), ADR-0033's own status, every other ADR.

## Review
<!-- reviewer / architect write verdicts here; PM reconciles before advancing state -->
