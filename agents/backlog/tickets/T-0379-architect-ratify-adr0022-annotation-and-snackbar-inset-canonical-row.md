---
id: T-0379
title: "Architect ratification — (a) the in-body 74pt transcription annotation on ACCEPTED ADR-0022 (ADRs are supersede-never-edit) and (b) the patterns-mobile SnackbarInsetState canonical-mapping row REPLACEMENT (a 'one way' redefinition)"
status: proposed
size: S
owner: architect
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0022, ADR-0018]
layers: [architect, docs]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix1 D+F review flags (2026-07-03) — decision-hygiene items the PM routes to the architect
---

> **Two decision-hygiene items from the phase/ios-fix1 D+F review, needing the architect's ratifying
> signature.** Both edits are believed CORRECT on substance (each is evidence-backed); what's missing is
> the owning role's ratification, because dev slices edited decision artifacts the architect owns.
> Dispatch BEFORE the next iOS ticket that cites the ADR-0022 D3 metrics or the snackbar-inset canonical
> row (T-0376 cites both).

## Context

**(a) The ADR-0022 in-body annotation (`fef5745c`).** ADR-0022 D3 originally recorded the BookFab as
64pt/— but its own cited source (`MainShell.kt:456-462`) is `Modifier.size(74.dp)` + a 34dp icon. The
Slice-D commit `fef5745c` fixed the code to 74pt/34pt (correct — D3's own "copy Android exactly" ruling
governs) and ANNOTATED the accepted ADR's body in place with a bracketed transcription-correction note
(ADR line ~61). Our ADR discipline is supersede-never-edit for accepted decisions; a pure transcription
correction arguably qualifies as an erratum rather than a decision change, but that call belongs to the
architect, not a dev slice.

**(b) The patterns-mobile canonical-row replacement (rode `e69a0283`).** The `patterns-mobile.md` D3
table's SnackbarInsetState row previously declared the "one way" as a view-local `bottomInset:` parameter
on `.snackbarHost`. The T-0371 harvest REPLACED it: the canonical mapping is now a `@Published
bottomInset` on `SnackbarController` (`setBottomInset`/`resetBottomInset`) that un-pinned hosts follow —
the shell lifts while its path is empty and resets on push/disappear (`ShellSnackbarInset`); modal-sheet
hosts PIN an explicit inset so a shell lift never leaks into a sheet. This is a redefinition of a
canonical "one way" (not an addition), shipped by the dev harvest with the PM's coordination but without
the architect's signature.

## Acceptance criteria
- [ ] **AC1 (ADR-0022 ruling)** — The architect either (i) RATIFIES the in-body annotation as an erratum —
  adding a signed erratum line to ADR-0022 (author: architect, date, "transcription correction 64→74pt
  ratified; supersede not required for errata" + folding the same note into the living doc), and records
  the erratum convention (when an in-body annotation is permissible vs when a superseding ADR is
  required) in the ADR README/process note so the next case doesn't improvise — or (ii) REVERSES it:
  strips the in-body edit and issues the correction as a proper superseding note. Either outcome is
  acceptable; unsigned in-body edits to accepted ADRs are not.
- [ ] **AC2 (SnackbarInset row ruling)** — The architect reviews the replaced canonical row against the
  landed implementation (`SnackbarController.bottomInset`, `ShellSnackbarInset`, the pin-vs-follow
  semantics, the modal-sheet-host convention) and either RATIFIES it (signature line on the row or in the
  doc's changelog) or amends it; if amended, a follow-up ticket for the code delta is filed.
- [ ] **AC3 (traceability)** — Both verdicts are recorded in this ticket's Review section and
  cross-referenced from T-0368/T-0371's Review notes (which flagged them); T-0376's references stay
  consistent with the ratified text.

## Out of scope
- Any code change (if AC2's review demands one, it is filed as its own ticket).
- Re-litigating ADR-0022 itself (accepted; the deliberation record stands).

## Implementation notes
- No-decision note in the panel sense: this ticket IS the ratification mechanism — a single-owner
  decision-hygiene pass on two bounded artifacts; no multi-role panel needed. The D+F reviewer's flags
  are the input; both underlying claims are already evidence-verified (the 74pt source line; the landed
  inset implementation + its tests).

## Status log
- 2026-07-03 — filed `proposed` by pm at the phase/ios-fix1 close, routing the two D+F review
  ratification flags to the architect per process (dev slices must not be the last word on
  architect-owned artifacts). Medium priority, but dispatch BEFORE T-0376 (which cites both artifacts).

## Review
<!-- architect writes the ratification verdicts here; PM reconciles -->

## Status log (additions)
- 2026-07-04 — scope addition from the fix-round-3 review: also ratify the new patterns-mobile
  rule row "a `format: date` field ridden as plain `Date` is a defect — use the generator's
  `useCustomDateWithoutTime` / `OpenAPIDateWithoutTime`" (defines the one way for date-only
  wire on iOS; it codifies the shipped 5d6654a2 fix). And note: the string-catalog junk-entry
  churn class (SWIFT_EMIT_LOC_STRINGS: NO not fully holding) recurred in round 3 — the knob
  re-investigation stays open (T-0373 finding b); junk entries stripped at commit time again.
