---
id: T-0542
title: There is no changelog, though four process documents and the docs charter all say the docs agent owns one
status: ready
size: S
owner: docs
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
manual_steps: []
sprint: 15
source: PM sprint-15 reconciliation. Verified at HEAD 2026-08-04 — no `CHANGELOG*` file exists anywhere
  in the repository.
---

## Context

Four documents assert a changelog exists and is owned:

| document | claim |
|---|---|
| `agents/process/documentation.md:15` | *"**Docs agent** \| the **published** site \| `docs/**` (VitePress) + changelog"* |
| `agents/process/documentation.md:58` | *"the `docs` agent updates the published page + changelog (Gate 7)"* |
| `agents/process/quality-gates.md:175` | *"If shipped behavior changed, the Docs agent updates the relevant `docs/**` page and the changelog"* |
| `agents/process/routing.md:24` | *"Shipped behavior changed; docs/changelog stale → `docs`"* |
| `.claude/agents/docs.md:16, :30` | owns *"The changelog (Keep a Changelog format: Added / Changed / Deprecated / Removed / Fixed / Security)"*; step 4 is *"Add a changelog entry under the right category."* |

**There is no such file.** A `find` for `CHANGELOG*` at HEAD returns nothing.

So **Gate 7 has been passing on a step nobody could perform.** That is the same defect class this sprint
spent itself closing — a mitigation that lives only in prose (`7e1cf7f5` found a test citing a
`CurrentStatus` backfill script that never existed; ADR-0040 found a specification excusing a
fail-closed exclusion by citing a runbook backfill that never existed; `01b21746` found four of
`CLAUDE.md`'s seven false claims were of exactly this shape). A gate that cannot fail is not a gate.

**The cost is concrete right now.** This sprint alone changed shipped behaviour that a user or an
operator would want to know about, and none of it is recorded anywhere a non-agent will look: the
cancellation-fee rule changed (customers were being charged for a cleaner who never took the job), the
express perk was **removed** from all three clients and a metered waiver was **added**, payout details
moved to their own capture with real validation, the favourite-cleaner perk became Plus-only and
rate-limited, and `OrderStatus.Pending` was declared dead. That is a release note, and it does not exist.

## Acceptance criteria

- [ ] **AC1 — a `CHANGELOG.md` exists at the repository root in Keep a Changelog format** with the six
      categories the docs charter names (Added / Changed / Deprecated / Removed / Fixed / Security) and
      an `## [Unreleased]` section.
- [ ] **AC2 — sprint 15 is backfilled from what SHIPPED, not from what was planned.** Given
      `git log master..HEAD`, When the entry is written, Then every user-visible or operator-visible
      change is present, sourced from the commits. **Do not backfill from `INDEX.md`** — the whole
      premise of this reconciliation is that the index had drifted from the tree.
- [ ] **AC3 — entries are written for a reader, not for an agent.** Given any line, When it is read by
      someone who has not seen the code, Then it says what changed and what it means for them. No ticket
      ids as the whole entry, no commit shas as the whole entry, no internal class names in a user-facing
      line. A ticket id **may** appear as a trailing reference.
- [ ] **AC4 — `Removed` and `Security` are actually used where they apply.** The express perk was
      **removed** from every client (`0c665c08`); the favourite-cleaner feed gained a rate limit, a
      server-side Plus gate and an active-cleaner filter (`b6f1c2a2`), and a customer-facing handler
      stopped pulling IBAN and PassportId into memory. Those are not "Changed".
- [ ] **AC5 — the process documents are made true.** Given the four documents above, When this lands,
      Then each points at the file's real path. If a document's claim is still not implementable after
      this ticket, **correct the document** rather than leaving the claim standing.
- [ ] **AC6 — there is a stated rule for what does NOT get an entry.** Given a refactor, a test-only
      change or an agent-process change, When the rule is read, Then it says so. Without this, the
      changelog becomes a second commit log and stops being read — which is how it silently dies again.

## Out of scope

- Release tagging, versioning or automating generation from commit messages. If the owner wants
  automation later, that is its own ticket; a hand-written file that exists beats a generator that is
  discussed.
- Backfilling sprints before 15. Start where the record is trustworthy. Say so in the file.
- `docs/mobile-app/**` — **T-0541**.

## Implementation notes

**Archetype:** `.claude/agents/docs.md` (Keep a Changelog categories) + `agents/process/documentation.md`.

`git log --oneline master..HEAD` is ~56 commits with unusually detailed messages; they are the source for
AC2. Prefer the commit body over the subject — several subjects under-describe what landed, and
`e4dd27f5` says so explicitly about its predecessor.

**No-decision note:** the artifact is already specified by the docs charter, down to its format. This
creates it. No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Passes DoR: AC observable, `S`,
  no dependencies, no owner-only steps, format and categories already fixed by the charter.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
