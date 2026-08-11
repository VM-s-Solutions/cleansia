# MOVED — this draft landed as **ADR-0044**

> **Do not read, cite, or implement from this path.** The body that was here was **rev 2**
> (2026-08-06). A lead ruled on it the same day — *REVISE on a closed twelve-item list, zero blocking
> challenges surviving, no further round* — and rev 3 transcribes that list. Rev 2's citations into
> `SaveOrderPhotos.cs` are stale by **+1** in the validator region and **+3** in the handler region,
> because ADR-0043's metadata scrub landed on that file mid-panel (`SaveOrderPhotos.cs:137`); its
> Consequences section over-claimed universal closure; and its instruction to the sibling draft asked
> for an exemption where the lead ruled **coverage**.

**The record is now:**

- **`agents/backlog/adr/0044-stored-content-type-is-byte-derived-on-every-intake.md`** — rev 3,
  `proposed`, carrying the full deliberation trail (`## Challenge` — the author-run rev-1 round, as
  labelled — `## Defense`, `## Verdict`) plus a `## Transcription record` mapping each of §E's twelve
  items to where it landed.
- **`agents/backlog/adr/challenges/0044-stored-content-type-byte-derived.md`** — the independent panel
  challenge (2026-08-06), moved from `challenges/NNNN-stored-content-type-byte-derived.md` per the
  verdict's §E-12, byte-identical. Its `patterns-backend.md` citations are **pre-edit** and were correct
  when written; the landed ADR discloses the +7 offset.
- **`agents/architecture/decisions/user-uploaded-artifacts.md`** §7.1 — the living companion
  (current shape).

**Number:** 0044, allocated 2026-08-06 (0043 was allocated the same day and is `accepted`).
**Acceptance:** per the verdict, the PM checks rev 3 against §E **only**, then flips the status to
`accepted`. The author does not accept their own ADR.

*This file is a tombstone only because the authoring instance had no shell and could not `git mv`.
**It should be `git rm`'d** — the landed ADR carries everything, and nothing links here.*
