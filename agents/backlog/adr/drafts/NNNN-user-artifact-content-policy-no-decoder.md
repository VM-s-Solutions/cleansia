# MOVED — this draft landed as **ADR-0043**

> **Do not read, cite, or implement from this path.** The body that was here was **rev N**
> (2026-08-05), which the defense panel found to be *"the right answers on a stale map"* — roughly
> seven of its citations pointed at symbols deleted earlier in the sprint (`Constants.ImageSignatures`,
> `DocumentContentType`, `Base64UploadIntakeRosterTests`), its audience table was materially wrong, and
> two of its decisions described work that had already shipped.

**The record is now:**

- **`agents/backlog/adr/0043-user-artifact-metadata-is-scrubbed-at-intake-by-audience-without-a-decoder.md`**
  — rev N+1, `proposed`, carrying the full deliberation trail (`## Challenge` / `## Defense` /
  `## Verdict`, including rev N's own §Challenge relabelled as the author's self-challenge).
- **`agents/backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md`** — the
  independent panel challenge (2026-08-06) with the lead's ruling index.
- **`agents/architecture/decisions/user-uploaded-artifacts.md`** — the living companion (current shape).

**Number:** 0043, allocated 2026-08-06 (highest on disk was 0042).
**Acceptance:** per the verdict, the PM checks rev N+1 against §Verdict §C **only**, then flips the
status to `accepted`. **Rev N+1 being accepted is the only true gate on T-0459.**

*This file is a tombstone only because the authoring instance had no shell and could not `git mv`.
**It should be `git rm`'d** — the landed ADR carries everything, and nothing links here.*
