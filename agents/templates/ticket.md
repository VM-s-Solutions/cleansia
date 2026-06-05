---
id: T-NNNN
title: <short imperative title>
status: draft            # draft | ready | in_progress | in_review | qa | done | blocked
size: M                  # S | M | L  (L must be split before going ready)
owner: <charter>         # the agent currently working it (pm sets this)
created: YYYY-MM-DD
updated: YYYY-MM-DD
depends_on: []           # ticket ids that must be done first
blocks: []               # tickets waiting on this one
stories: []              # US-<persona>-NNNN ids this satisfies
adrs: []                 # ADR numbers in force
layers: []               # any of: analyst, architect, db, backend, frontend, android, ios, docs
security_touching: false # true → Security gate mandatory
manual_steps: []         # owner-only: ef-migration, nswag-regen, db-seed, xcode-project, docs-build
sprint: N
---

## Context
Why this ticket exists; the problem it solves; links to the audit finding / story / owner request.

## Acceptance criteria
- [ ] **AC1** — Given <state>, When <action>, Then <observable outcome>.
- [ ] **AC2** — ...
(Every AC is an observable outcome with verifiable evidence at review time.)

## Out of scope
- What this ticket deliberately does NOT do (prevents scope creep).

## Implementation notes
Contract details (DTO shape, error codes, entity fields), the sequence of layers, any ADR to read.

## Status log
- YYYY-MM-DD HH:MM — draft (created by pm)
- YYYY-MM-DD HH:MM — ready (deps satisfied)
- ...one line per transition...

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
