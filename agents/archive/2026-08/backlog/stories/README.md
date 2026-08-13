# User Stories

Stories written by the Business Analyst, with Given/When/Then acceptance criteria and out-of-scope
lists. The PM converts these into tickets (assigning final stable `US-*` ids on intake — the audit
batch reused some numbers across personas; renumber when promoting).

## From the 2026-06-01 full audit
**[`AUDIT-2026-06-01-user-stories.md`](./AUDIT-2026-06-01-user-stories.md)** — **83 user stories**, one
per confirmed functional gap, spanning customer / partner / admin personas. These are the spec for the
Wave 2 feature work (and the gap-fix half of Waves 0–1) in
[`../audits/AUDIT-2026-06-01-execution-plan.md`](../audits/AUDIT-2026-06-01-execution-plan.md). Every
gap finding in `../audits/AUDIT-2026-06-01-findings.json` with `isGap: true` has a story here.

**Process:** stories are written **first** (this file), then implemented **test-first (TDD)** — the
story's AC become the failing tests, which the implementation makes pass. The PM promotes a story to a
ticket only when it passes the Definition of Ready (`../../process/ticket-lifecycle.md`), and sequences
it into the right wave.
