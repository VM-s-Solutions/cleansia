# ADRs — record discipline

Numbered ADRs (`NNNN-*.md`), one decision each, carrying the `## Challenge` / `## Defense` /
`## Verdict` deliberation trail. Status flow: `proposed → accepted → superseded` (or `rejected`).
See `agents/process/deliberation.md` for the panel protocol.

## Accepted ADRs are immutable — with exactly two sanctioned append forms and one erratum exception

1. **Dated appended sections** (the normal way an accepted ADR's story continues): an
   owner-directed supersede, a partial supersede, or a record-only closure is APPENDED as a dated,
   attributed section at the end of the ADR (see ADR-0022's 2026-07-08 owner supersede + the
   2026-07-19 T-0429 record). The original body above it is never rewritten.
2. **A superseding ADR** for any real decision change — new number, `Supersedes:` header, the old
   ADR's status flips to `superseded`.

**The erratum exception (ratified T-0379, 2026-07-19 — the ADR-0022 64→74pt precedent).** An
in-body annotation to an accepted ADR is permissible ONLY when ALL of these hold:

- it corrects a **transcription error** — a value mis-copied from the ADR's **own cited source**,
  where that source plus the ADR's own ruling already determine the correct value;
- **no decision content changes** — not the chosen option, a threshold, the scope, an alternative's
  disposition, or the rationale;
- the annotation is **bracketed, dated, and self-describing** in place (states what it corrects and
  cites the source line);
- it is **ratified by the architect** via a signed, dated erratum block appended to the ADR (an
  unsigned in-body edit — whoever made it — is a process violation until ratified or reversed).

Anything that fails any test above is a decision change: write a superseding ADR (or a dated
owner-supersede section). When in doubt, supersede — the erratum lane is for digits, not meaning.

The living companion docs (`agents/architecture/decisions/*.md`) are updated in the same change as
any erratum or supersede, so the current-shape record never contradicts the ADR trail.
