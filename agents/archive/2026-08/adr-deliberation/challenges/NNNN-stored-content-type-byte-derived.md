# MOVED — this challenge is now **`challenges/0044-stored-content-type-byte-derived.md`**

> Renamed 2026-08-06 per the lead's verdict §E-12 (the numbered form is the majority convention in this
> folder — `0034-` … `0042-`). **The content at the new path is byte-identical**; nothing in the
> challenger's text was edited.

**The record is now:**

- **`agents/archive/2026-08/adr-deliberation/challenges/0044-stored-content-type-byte-derived.md`** — this challenge.
- **`docs/decisions/adr-0044.md`** — the ADR it
  attacks, rev 3, `proposed`, whose `## Defense` answers all ten findings and whose `## Verdict` rules
  on them.

**One note for anyone following a citation out of this file:** its `patterns-backend.md:NNNN` references
are **pre-edit** and were correct when written. The 2026-08-06 disclosure callout lengthened that file,
shifting everything after `:1311` by **+7** — e.g. this challenge's `:1364-1366` ("the read path reads
the intake's own signature table") is `:1371-1373` at HEAD. The landed ADR's Method declaration carries
the same disclosure.

*This file is a tombstone only because the authoring instance had no shell and could not `git mv`.
**It should be `git rm`'d.***
