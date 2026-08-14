# Backlog

Where new work is filed. One folder, two things in it: `INDEX.md` and `tickets/`.

## The one rule

**One row per ticket, one status, and `INDEX.md` is the only place a status lives.**

The previous backlog is gone — 428 files, archived on 2026-08-13 and deleted on 2026-08-14. It is in
git history if it is ever wanted, and the reason it is not here is worth carrying forward: it filed
each ticket **twice**, a *filing* row and a *close-out* row with independent statuses. On 2026-08-11
that sent four lanes at 24 tickets that had all already shipped.

A ticket's `status:` frontmatter is a copy. Copies drift. The row wins.

## The second rule

**A row is a claim about the past. Ground-truth it before working it.** Does the thing it describes
still exist in the tree, right now? One grep is cheaper than one lane — that is not a maxim, it is the
measured cost of the 2026-08-11 incident.

## Filing a ticket

1. Add a row to [`INDEX.md`](INDEX.md).
2. Write `tickets/T-NNNN-<kebab-slug>.md` from [`../templates/ticket.md`](../templates/ticket.md).
3. Ids are sequential and never reused. The highest id the old backlog ever mentions is **T-0606**
   (files go to T-0566; the INDEX cites higher ones), so the next one is **T-0607** — reusing an id
   would collide with a citation in a published ADR.

## What does NOT live here

- **Decisions** → `docs/decisions/adr-NNNN.md`. An ADR is the record; a ticket is the work.
- **Cleanup-track state** → `../cleanup/INDEX.md`, which is a closed manifest, not a queue.
- **Owner-only steps** → `../cleanup/MANUAL_STEPS.md`.
- **Anything explaining what the platform does** → `docs/`. That is the source of truth.
