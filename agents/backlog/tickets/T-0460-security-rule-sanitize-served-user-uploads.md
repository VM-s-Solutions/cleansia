---
id: T-0460
title: SECURITY RULE — user-supplied artifacts served back by URL are sanitized at upload (magic-byte validation is not a sanitizer)
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Filed from the **T-0446 security gate** (finding **SEC-5**). Full write-up:
`agents/backlog/security/user-profile-avatar.md`.

**This is a genuine gap in the rule set, not a violation of it.** Nothing in **S1–S11** addresses
**bytes embedded inside a stored artifact that is later served by URL**:

- **S4** governs **DTO fields** — what the handler puts in a response object.
- **S6** governs **logs**.
- **S8/S10** govern query scoping.

None of them reach *metadata inside a stored image*. That is why SEC-3 could sit undetected in
**three shipped upload pipelines** — order photos, dispute evidence and the avatar — without any gate
catching it. **The reviewers were not wrong against the rules; the rules were silent.** That is the
strongest possible argument for adding one, and it is the same argument that produced Gate 0.5
(T-0445) and the worktree rule (T-0456) this sprint.

The rule to add to `agents/knowledge/security-rules.md`, in substance (**the panel owns the final
wording**):

> **User-supplied artifacts that will ever be served back by URL are sanitized at upload** — metadata
> stripped, dimensions/size bounded, re-encoded. **Magic-byte validation is an accept/reject check,
> not a sanitizer:** passing it establishes that the bytes are an image, not that the image is safe to
> hand back. The check that matters is not *"did we validate the upload?"* but *"what travels with
> these bytes when someone else fetches them?"*

## Deliberation required — NOT `ready`

**Architect panel** (author + 2–3 challengers + lead). A new law in the S-series is an architect call,
and the panel must settle more than the wording:

- **Is it S12, or an extension of S4?** S4 is "DTO leak prevention" — this is arguably the same
  *principle* (do not hand the client something you did not intend) at a different layer. A new
  number is more discoverable; an S4 extension is more honest about the shared root. Rule and say why.
- **How wide is "artifact"?** Images are the live case. PDFs carry author/producer metadata; Office
  documents carry revision history and author names. Employee documents are a **PDF** pipeline in this
  codebase. A rule scoped to images will need re-opening; a rule scoped to "any user-supplied file
  served back" may be unenforceable today. **Pick the honest scope and name what it excludes.**
- **What is the enforceable check?** `agents/knowledge/enforcement.md` distinguishes rules a reviewer
  reads from rules a checker asserts. A grep for `UploadAsync` reachable from a request-bound byte
  array without an intervening sanitizer is plausible; if it is not, say so — an unenforceable rule
  should be **written as guidance and labelled as such** rather than dressed as a law nobody can gate.
- **Retroactive scope.** Does the rule oblige an audit of already-stored artifacts, or only new
  uploads? T-0458/T-0459 deliberately excluded the backfill; the rule should not silently mandate it.

## Acceptance criteria

- [ ] **AC1** — The rule is added to `agents/knowledge/security-rules.md` in the established S-series
      voice (statement of the law → the concrete failure it prevents → a reference to the code that
      complies once T-0458 lands). Evidence: the diff.
- [ ] **AC2** — The **audit checklist** at the foot of `security-rules.md` gains a corresponding
      numbered item, so the rule is reachable from the checklist a reviewer actually walks — not only
      from the prose above it.
- [ ] **AC3** — The rule cites the **real** finding that produced it (three shipped pipelines,
      `ImageFileValidator`'s 3–4 byte magic-prefix check, EXIF GPS on cross-user-visible order
      photos), because the S-series' authority comes from every rule naming the incident behind it.
- [ ] **AC4** — Any other agent doc that enumerates the security laws is updated in the same change so
      the count does not drift. **Find them first** — at minimum check `.claude/agents/security.md`,
      `agents/knowledge/enforcement.md`, `agents/knowledge/consistency.md` and the developer charters
      that say "walk S1–S10" / "S1–S11". Evidence: a grep for `S1-S1` / `S1–S1` across `agents/` and
      `.claude/` **in the PR body, with results**. Note that T-0446's own AC3 says "Walk S1-S10" and is
      already one rule stale — that drift is the failure mode this AC exists to stop.
- [ ] **AC5** — `enforcement.md` records whether the rule is **mechanically checkable** and, if not,
      says so explicitly rather than leaving it ambiguous.

## Out of scope

- Implementing any sanitization — **T-0458** / **T-0459**.
- Rewriting or renumbering the existing S1–S11. If the panel picks "extend S4" over "add S12", that is
  an edit to S4, not a renumbering of the series.
- The `CLAUDE.md` summary — **owner-gated**. If the panel thinks it needs a line there, **flag it to
  the owner**; do not edit it.

## Implementation notes

- **Precedent to mirror:** **T-0445** (Gate 0.5 → `process/quality-gates.md`) and **T-0456**
  (worktree/stash → `process/shared-file-lanes.md`) — both are approved process/knowledge changes
  routed as `architect` + `docs` tickets this sprint, because **the PM does not own
  `agents/knowledge/*.md` or `agents/process/*.md`**. Follow that shape.
- **Sequencing preference (not a hard dependency):** this should ideally land **before or alongside
  T-0458's panel**, so that ADR can cite the rule rather than invent the reasoning from scratch. It is
  deliberately left without a `depends_on` in either direction so neither can deadlock the other.
- **Shared-file lane:** `agents/knowledge/security-rules.md` — this ticket is the **sole writer**. No
  overlap with T-0454 (`check-consistency.mjs`), T-0456 (`shared-file-lanes.md`) or T-0439
  (`quality-gates.md`).

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-5)
- 2026-07-30 — **not `ready`**: awaiting the architect panel (a new S-series law is an architect call, per the T-0445 precedent).

## Review
<!-- architect + docs verdicts here -->
