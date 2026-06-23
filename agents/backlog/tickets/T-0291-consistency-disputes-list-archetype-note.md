---
id: T-0291
title: consistency.md note — prefer the disputes-management list archetype for new admin lists
status: ready
size: XS
owner: —
created: 2026-06-23
updated: 2026-06-23
depends_on: []
blocks: []
stories: []
adrs: [0012]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 11
---

> **No-decision note (panel skipped):** a single documentation note recording an ALREADY-OBSERVED
> convention — that the disputes-management list is the canonical admin-list exemplar (T-0286 itself
> mirrored it). It codifies an existing pattern; it introduces no new rule, behavior, or decision.
> Pure mechanical consistency-doc edit.

## Context

ADR-0012 audit-log follow-up **(c)**. When the team built the Wave-9 admin audit-log feature, T-0286
**mirrored the disputes-management list archetype** (its status log: "mirroring the disputes-management
archetype: facade extends `UnsubscribeControlDirective`, injects the dedicated generated client …
signal state with the three explicit data states … `cleansia-table` + filters"). That choice was made
ad hoc per-ticket; it is **not yet recorded** in the knowledge catalog, so the next admin-list builder
has no documented pointer to the reference exemplar. `agents/knowledge/consistency.md` §C ("Frontend —
list features", rules C1–C8) already fixes the canonical paged-admin-list **shape** but names no single
reference feature to copy. This ticket adds the one-line note naming the disputes-management list as the
exemplar new admin lists should mirror.

## Acceptance criteria

- [ ] **AC1 — Archetype note added to consistency.md §C.** `agents/knowledge/consistency.md` §C
  ("Frontend — list features") gains a short note: **"For a new admin paged list, mirror the
  disputes-management list feature (`cleansia-admin-features/.../dispute-management`) as the reference
  archetype — it is the canonical C1–C8 implementation (facade extends `UnsubscribeControlDirective`,
  dedicated generated client, signal state with the three explicit data states, `cleansia-table` +
  `getXxxTableDefinition`, filter controls)."** The note points at the real path and ties to the
  existing C1–C8 rules (no rule renumbering, no new rule).
- [ ] **AC2 — Cross-referenced from the audit-log precedent.** The note (or an adjacent line) records
  that T-0286's audit-log lib already followed this archetype, so the exemplar is proven, not aspirational.
- [ ] **AC3 — Docs-only, accurate path.** The named disputes-management path is verified to exist and to
  be a genuine C1–C8 list (not a form/detail feature). No other file changes — this is a documentation
  edit only.

## Out of scope
- **No code change** — `consistency.md` (a knowledge doc) only; no feature lib is touched.
- **No new consistency RULE and no rule renumbering** — C1–C8 stand as-is; this adds a "which feature to
  copy" pointer, not a new constraint.
- **No `check-consistency.mjs` tooling change** — the script already enforces the C-rules; naming an
  exemplar is documentation, not a new automated check.

## Implementation notes
Edit `agents/knowledge/consistency.md` §C only. Confirm the exact disputes-management feature-lib path
under `libs/cleansia-admin-features/` before writing it into the note (so the pointer is real). Keep it
to a couple of sentences appended to / beside the C-rule block — the catalog style is terse.

**Routing:** `[docs]`. `reviewer` confirms the note is accurate (the named feature really is the C1–C8
exemplar) and docs-only. No `qa` test plan (documentation), no `security`, no `optimizer`.

## Status log
- 2026-06-23 — draft → ready (created by pm). ADR-0012 follow-up **(c)**, surfaced when the audit log
  was built and **never previously captured** (verified: no disputes-archetype note in
  `consistency.md`; highest pre-existing ticket id was T-0288). DoR met: AC observable; sized **XS**
  (one knowledge-doc note); `depends_on: []`; `layers: [docs]`; `security_touching: false`;
  `manual_steps: []`. Pure mechanical consistency-doc edit recording an already-observed convention →
  one-line no-decision note, no panel.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
