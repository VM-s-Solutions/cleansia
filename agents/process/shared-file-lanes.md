# Shared-File Lanes — the serialization list

When a batch fans out in parallel, tickets that touch the **same shared file** must be **serialized**
into a single lane (one writer at a time). This file is the **maintained data list** of those files.
The PM validates every parallel batch's lane assignments against this list **before dispatch**; a
batch plan that puts two concurrent writers on any cluster below is wrong by construction.

Why this is a file and not folklore: on 2026-06-23 (Wave-8 close-out) T-0291 and T-0289 both edited
`consistency.md` in one parallel batch, and a third ticket's fix-agent ran `git restore` on it to
"clean contamination" — silently wiping T-0291's committed deliverable. The orchestrator's
combined-tree re-verify caught it and the note was restored by hand. The structural fix is this list +
the serialization rule + the restore ban (`quality-gates.md` §"Serialize shared-file lanes — and NEVER
`git restore` a shared file in a parallel batch").

## The clusters (paths verified 2026-07-02)

| Cluster | Files | Why it collides |
|---|---|---|
| Consistency catalog | `agents/knowledge/consistency.md` | Every canonicalization/deviation ticket appends its note; two concurrent writers (or one blanket revert) destroy each other's hunks — the 2026-06-23 incident file. |
| Backlog manifest | `agents/backlog/INDEX.md` | One row per ticket in one table — every ticket in a batch wants to update its own row at close-out. |
| i18n bundles (15 files) | `src/Cleansia.App/apps/{cleansia.app, cleansia-partner.app, cleansia-admin.app}/src/assets/i18n/{en,cs,sk,uk,ru}.json` | Any FE ticket adding a user-visible string edits all 5 locale files of its app; two FE tickets on the same app collide on all 5. Serialize **per app** (the three apps' bundles are independent). |
| Policy cluster (3 files — they move together) | `src/Cleansia.Core.AppServices/Authentication/Policy.cs` + `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs` + `src/Cleansia.Tests/Authentication/FrozenPermissionMapTests.cs` | A new `Policy.*` const needs all three in ONE change or `AssertComplete` bricks host boot / the frozen snapshot fails. Exactly one cluster editor per pass (the T-0285 rule). |
| Project guardrails | `CLAUDE.md` (repo root) | Read by every agent at spawn; a mid-batch edit changes the rules under running lanes. Owner/orchestrator-gated — never a ticket-lane edit. |

## Other observed serialization clusters (same rule, narrower blast radius)

- `src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` — every backend ticket adding an
  error key appends here (the T-0262 → T-0234 lane).
- The admin shell — `apps/cleansia-admin.app` `app.component.ts` (sidebar) + `app.routes.ts`: every
  new admin feature adds exactly one entry (the T-0173 → T-0175 → T-0176 → T-0186 chain).
- The Android `:core` module — hoists and shared-component changes (the T-0277 ↔ T-0278 serial lane).

## The rules

1. **The PM validates lane assignments against this list** before dispatching a parallel batch: any
   two tickets touching the same cluster are serialized into one lane (or handed to one agent, in
   sequence). `routing.md` sequencing rule 3 binds this.
2. **Parallel agents edit only their own hunks** when adjacency on a shared file is unavoidable —
   never a rewrite, never a reformat.
3. **NEVER `git restore` / `git checkout --` / wholesale-revert a shared file** in a parallel batch.
   An agent that believes a shared file is contaminated **reports it to the PM** (a note in its
   ticket); it does not revert. This rule is also in every dev charter's constraints.
4. **Maintain the list.** A collision on a file not listed here is two bugs: the collision, and the
   missing row. The fix adds the row in the same change.
