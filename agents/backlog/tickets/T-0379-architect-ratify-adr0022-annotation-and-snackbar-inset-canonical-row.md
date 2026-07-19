---
id: T-0379
title: "Architect ratification — (a) the in-body 74pt transcription annotation on ACCEPTED ADR-0022 (ADRs are supersede-never-edit) and (b) the patterns-mobile SnackbarInsetState canonical-mapping row REPLACEMENT (a 'one way' redefinition)"
status: done
size: S
owner: architect
created: 2026-07-03
updated: 2026-07-19
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

**Architect ratification verdicts (lead-adjudicated, 2026-07-19):**

- **AC1 — ERRATUM RATIFIED (not reversed into a supersede).** Author case: the `fef5745c` bracketed
  note corrects a mis-transcribed NUMBER (64→74pt) whose true value the ADR's own cited source
  (`MainShell.kt:456-462` = `Modifier.size(74.dp)` + 34dp icon) and its own "copy Android exactly"
  ruling already fix — zero decision content changed; a superseding ADR would be ceremony that leaves
  the wrong digit standing in the text people copy from. Challenger case (steelmanned):
  supersede-never-edit is only as strong as its narrowest exception — letting anyone self-classify an
  edit as "just a transcription" is the loophole, and a dev slice editing an architect-owned accepted
  artifact normalizes the violation even when right. Ruling: challenge answered by BOUNDING the class
  and making it rule-governed — the erratum convention is now recorded in `agents/backlog/adr/README.md`
  (cited-source-determined value only; no meaning change; bracketed+dated+self-describing; architect
  signature mandatory), and a signed erratum block is appended to ADR-0022. The dev edit was
  procedurally out of lane, substantively correct; ratified retroactively. Living doc already carried
  the folded note (`ios-app-architecture.md` R1). **Stale-row sweep (scope add 2026-07-17) done:**
  `ios-app-architecture.md` — customer-shell D3 row (~79) rewritten to the post-supersede truth (stock
  `TabView` + FAB disc; D2 topology survives), the §"iOS-16 shell crash" record section got a
  superseded-in-part banner, the ledger ADR-0022 row (~895) status cell updated;
  `patterns-mobile.md` — the "iOS shell navigation — the ONE way" block rewritten (stock bar both
  apps; the old text called the shipped stock bar a defect); `ios-app-review-checklist.md` AR-DP-3
  shell-bar clause flipped (a resurrected pill/pager is now the finding). The liquid-glass inventory
  already carries its own RETIRED banner — left as record.
- **AC2 — SnackbarInset row RATIFIED with one wording correction.** Verified against code: `@Published
  private(set) bottomInset` + `setBottomInset`/`resetBottomInset` (`SnackbarController.swift:14,55-60`);
  un-pinned hosts follow via `pinnedInset ?? controller.bottomInset` (`GlobalSnackbarHost.swift:16`);
  the shell sets `ShellSnackbarInset.inset(pathDepth:)` on appear/path-change and resets on disappear
  (`CustomerShellView.swift:104-109`; depth>0 → default inset); every modal-sheet host pins an explicit
  inset (BookingSheetView, Promo/ReferralCodeSheet, AddressManager at `CustomerShellView.swift:88`,
  OrderDetail sheets). Tests pin it (`SnackbarControllerTests`, `ShellSnackbarInsetTests` —
  `overShellBar = chromeEnvelope + 12 = 94`). The row's mechanism was EXACT; the one drift was the
  pill-era phrase "bar-composite clearance" → corrected to the post-supersede bottom chrome (stock tab
  bar + docked Book FAB). No code delta needed; no follow-up ticket. Note: ADR-0022's 2026-07-08
  supersede text computes a 129pt clearance from a then-56pt bottom-padded FAB — the code has since
  moved to the center-docked 66pt FAB (94pt total); the ADR appendix is a correct record of its moment,
  the catalog row (which names the constants, not a number) is the living truth.
- **Scope addition (2026-07-04) — the `format: date` row RATIFIED as-is.** Both generator configs carry
  `useCustomDateWithoutTime: true` (`openapi-generator-config.{partner,customer}.yaml:21`); the row
  (patterns-mobile `OpenAPIDateWithoutTime`) codifies the shipped 5d6654a2 fix correctly. The
  string-catalog junk-entry churn item stays open under T-0373 finding (b) — no action here.
- **AC3 — traceability:** verdicts recorded here; cross-ref lines added to T-0368/T-0371 Review notes;
  T-0376 is retired (cancelled by the same supersede), so its citations are moot — nothing to keep
  consistent there.

## Status log (additions)
- 2026-07-04 — scope addition from the fix-round-3 review: also ratify the new patterns-mobile
  rule row "a `format: date` field ridden as plain `Date` is a defect — use the generator's
  `useCustomDateWithoutTime` / `OpenAPIDateWithoutTime`" (defines the one way for date-only
  wire on iOS; it codifies the shipped 5d6654a2 fix). And note: the string-catalog junk-entry
  churn class (SWIFT_EMIT_LOC_STRINGS: NO not fully holding) recurred in round 3 — the knob
  re-investigation stays open (T-0373 finding b); junk entries stripped at commit time again.
- 2026-07-19 — **done** by architect (lead ruling, see Review): (a) the ADR-0022 in-body 74pt note
  ratified as a SIGNED ERRATUM (block appended to ADR-0022; convention recorded in
  `agents/backlog/adr/README.md`) + the post-supersede stale pill rows swept in
  `ios-app-architecture.md` / `patterns-mobile.md` / `ios-app-review-checklist.md`; (b) the
  SnackbarInsetState canonical row RATIFIED against the landed code with one wording correction
  (pill-era "bar-composite" → stock-bar+FAB chrome); the 2026-07-04 `format: date` row addition
  ratified as-is (both yaml configs verified).
