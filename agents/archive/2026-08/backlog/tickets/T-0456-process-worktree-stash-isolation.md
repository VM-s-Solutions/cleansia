---
id: T-0456
title: PROCESS — worktrees share one repo-global stash stack; extend the shared-file rules to cover it
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-08-01
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

**The incident (wave 1, sprint 14).** A reviewer instance ran `git stash -u` inside a **developer's
worktree** while that developer's ticket was mid-flight. `git stash` stashes and **hard-resets the
working tree**, so the developer's uncommitted work vanished under them. Recovery cost roughly
**50 minutes** on the demo critical path.

**Why the existing rule did not catch it.** `agents/process/shared-file-lanes.md` rule 3 bans
`git restore` / `git checkout --` / wholesale-revert on a shared file, and names the 2026-06-23
incident that produced it. `git stash` is the **same failure class** — an agent using a
tree-wide state operation to "clean" something it did not own — but the rule enumerates *commands*
rather than the *class*, and `stash` is not on the list.

**PM verification, 2026-07-30:** grepped all of `agents/` and `.claude/` — the string `worktree`
appears in **seven ticket files and zero process or charter documents**, and `git stash` appears
**nowhere at all**. So the multi-worktree execution model the team actually runs on is entirely
undocumented, and every property of it is folklore.

**The specific property that bit us, and that no doc states:** `git stash` writes to `refs/stash`,
which lives in the **shared `.git` directory**, not in the per-worktree `.git` file. So:
- every worktree pushes onto **one global stack**;
- `git stash pop` in worktree A can restore worktree B's entries;
- and the stash *push* hard-resets whichever tree ran it, regardless of who owns the ticket in that
  tree.

This generalises past `stash`. The rule should be written to the class — **tree-wide state
operations** — not to a growing list of command names, or the next agent will reach for
`git clean -fdx`, `git checkout <branch>`, `git reset --hard` or `git worktree remove` and the doc
will be silent again.

**Why this is a ticket and not a PM edit.** The PM owns tickets, `INDEX.md` and sprint status; it does
not own `agents/process/*.md`. T-0445 set the precedent this sprint — an owner-approved process change
was routed as an `architect` + `docs` ticket, deliberated, and landed as Gate 0.5 in
`quality-gates.md:52`. Same shape here.

## Acceptance criteria

- [ ] **AC1** — Given `agents/process/shared-file-lanes.md`, When rule 3 is read after this change,
      Then it bans the **class** (tree-wide state operations that discard or relocate uncommitted work
      an agent does not own) and names `git stash` explicitly alongside `git restore` /
      `git checkout --`, with the 50-minute wave-1 incident recorded the way the 2026-06-23 incident
      already is at `shared-file-lanes.md:8-13`. Evidence: the diff.
- [ ] **AC2** — Given the repo-global stash stack, When the doc is read, Then it states the mechanism
      (`refs/stash` lives in the shared `.git`, so all worktrees share one stack) — not just the
      prohibition. A rule whose reason is stated survives; a bare "don't" gets reasoned around.
      Evidence: the diff.
- [ ] **AC3** — Given an agent that believes a worktree is contaminated, When it follows the doc, Then
      it has a **prescribed alternative** — the existing "report it to the PM, do not revert" escalation
      at `shared-file-lanes.md:40-42`, extended to cover "I need a clean tree to run X". An agent with
      a real need and no sanctioned path will improvise, which is what happened. Evidence: the diff.
- [ ] **AC4** — Given the developer and reviewer charters in `.claude/agents/`, When they are read,
      Then the ban appears in their constraints — `shared-file-lanes.md:42` states the `git restore`
      ban "is also in every dev charter's constraints"; **verify that claim is true today** before
      extending it, and record whether it was. The reviewer charter matters most here: the incident was
      a **reviewer** acting inside a **developer's** tree. Evidence: the diff plus the verification.
- [ ] **AC5** — Given the panel's ruling, When it lands, Then the doc says whether an agent may run
      **any** write-side git command inside a tree it does not own, or only read-side ones — the
      narrow lesson is "not stash", the general lesson may be "a reviewer never mutates the tree it is
      reviewing". Evidence: the ruling.
- [ ] **AC6** — Gate 0.5 leg 3: this is a documentation change with no executable assertion. Say so
      explicitly under leg 3; do not manufacture a test. Leg 2 does not apply.

## Out of scope

- Changing the multi-worktree execution model itself (one tree per agent, isolation strategy, cleanup).
  That is the orchestrator's operating model and a much larger decision. This ticket documents a
  property of the model that already exists and burned us.
- Any tooling / git hook to enforce the ban. If the panel wants one, it is a follow-up — a rule that
  reviewers read is the cheap 80% and it is what T-0445 did for its gate.
- `quality-gates.md`. T-0439 is already queued on that file behind T-0445; keep this ticket in
  `shared-file-lanes.md` + the charters so the two do not collide.

## Implementation notes

**Architect + docs panel** — same shape as T-0445, which is the worked example
(`agents/archive/2026-08/backlog/tickets/T-0445-process-verification-integrity-gate.md`, landed as Gate 0.5). The
challengers should press on AC5: banning all tree-mutation by reviewers is the safe rule and may be
too strict — a reviewer that cannot build cannot review, and building writes to the tree
(`node_modules`, `bin/`, `obj/`, `.gradle`, generated iOS projects). The ruling must distinguish
**build artifacts** from **the developer's uncommitted source**, or it will be ignored the first time
a reviewer needs to run a suite.

**Shared-file lane:** `agents/process/shared-file-lanes.md` — no other sprint-14 ticket writes it
(T-0439 is queued on `quality-gates.md`, a different file). The `.claude/agents/*.md` charter files
have no other writer this sprint. Note T-0455 asks for a **new cluster row** in the same file — if
both are in flight, serialize (**T-0456 → T-0455's row**, or hand the row to T-0456).

**Priority: process, non-blocking, but cheap and the incident is fresh.** The value of writing an
incident down decays fast.

## ⚠️ SCOPE EXTENDED 2026-07-30 — add `patterns-*.md` to the shared-file-lane table

**This is a PM lane ruling routed here rather than forked into a new ticket**, because this ticket is
already the **sole writer of `agents/process/shared-file-lanes.md`** and dedup discipline says extend,
don't fork.

**The question, raised by the T-0441 reviewer:** T-0441 harvested into
`agents/knowledge/patterns-mobile.md`. The reviewer confirmed its lane reasoning was correct **by the
letter of the current list** — the table enumerates only `consistency.md`, `INDEX.md`, the 15 i18n
bundles, the Policy trio and root `CLAUDE.md` (PM-verified: `shared-file-lanes.md:19-23`). It
correctly flagged that the call is the PM's/Architect's, not a reviewer's.

**PM ruling: YES — and the rule must cover the whole `patterns-*.md` family, not just `patterns-mobile.md`.**

The reasoning, which the panel should keep or overrule explicitly:

1. **The stated rationale for `consistency.md` applies verbatim.** The table's own words are *"every
   ticket appends its note; two concurrent writers destroy each other's hunks."* Nothing in that
   sentence is specific to `consistency.md`; it describes an append-only catalog with many authors.
   `patterns-*.md` is exactly that.
2. **It is not hypothetical — three of them were written this sprint, by three different tickets.**
   `patterns-mobile.md` (T-0441), `patterns-backend.md` (T-0446's diff), `patterns-frontend.md` (the
   T-0439 developer's harvest). The class is already live; the table just does not know it.
3. **The next collision is already scheduled.** **T-0440 is the iOS port of T-0441's exact feature**
   and is the single most likely next writer of `patterns-mobile.md`. *(It has already been told not
   to re-harvest, so the immediate collision is averted by instruction — which is precisely the
   fragile mechanism a lane table exists to replace.)*
4. **Enumerating one file would repeat the bug being fixed.** This ticket's own core argument is that
   `shared-file-lanes.md` describes **commands** where it should describe the **class**. Adding
   `patterns-mobile.md` alone — leaving `patterns-backend.md` and `patterns-frontend.md` out — would
   make the same mistake in the same edit.

- [ ] **AC (added)** — The lane table covers `agents/knowledge/patterns-*.md` **as a family**, with a
      rationale line, and states whether lanes serialize **per file** (likely — the four files are
      independent, exactly as the i18n bundles serialize per app) or across the family. **Recorded in
      `INDEX.md`'s lane list immediately by the PM**; this AC is the durable home.

## ⚠️ SCOPE EXTENDED 2026-08-01 — two MORE incidents of the same class, both hit for real this sprint

**Routed here rather than forked, for the reason this ticket already argues:** its own thesis
(`## Context`, AC1) is that `shared-file-lanes.md` enumerates **commands** where it should describe the
**class** — *tree-wide state operations that discard or relocate uncommitted work an agent does not
own.* Two more instances landed this sprint. **Both are that class; neither is `git stash`.** Filing
them separately would repeat, a third time, the enumerate-instances-not-the-class bug this ticket
exists to fix. They are also both **already covered by AC1's wording** — what they add is *evidence
that the wording has to reach further than `git`.*

### Hazard 2 — `cd X && <destructive git>` silently redirects to the MAIN checkout when `X` is missing

**What happened:** an agent ran a compound `cd <worktree> && <git command>`. The worktree path did not
exist, `cd` failed, the shell **carried on to the second command**, and the destructive git ran in the
**owner's main checkout** — leaving it on a **detached HEAD**.

**Why it is the same class and not a new one:** the agent's *intent* was scoped to a tree it owned;
the *effect* landed on a tree it did not. The mechanism is different from `git stash` (mis-targeting
rather than a shared ref), the consequence is identical — someone else's tree mutated under them.

**Why an enumeration of git commands cannot catch it:** the dangerous half is **the shell**, not git.
`cd X && Y` runs `Y` in whatever directory the process was already in when `cd` fails. A rule that
lists forbidden git verbs is silent about the connective that decides *where* they run. Any rule
written here has to say: **a command that must run in a specific tree names that tree unconditionally**
— `git -C <path> …`, or `cd X || exit 1`, or a `set -e` script — **never `cd X && <destructive>`.**

**Aggravating detail worth writing down:** this instance is *harder* to notice than the stash one,
because nothing errors. `cd` prints its failure, the git command succeeds, and the overall exit code is
the git command's — **0**. It reads as a clean run.

### Hazard 3 — `xcodegen generate` regenerates `Info.plist` from `project.yml`, wiping the owner's working-tree-only Stripe key

**What happened / what is at risk:** the iOS `Info.plist` files are **generated artifacts**, produced
by `xcodegen` from `info.properties` in each app's `project.yml`. The owner's live Stripe key exists
**only in the working tree** — it is deliberately never committed. Running `xcodegen generate` in the
main checkout **overwrites `Info.plist` from the committed `project.yml`** and the key is gone. It is
also why `git pull` costs the same thing, and why the standing instruction on every iOS ticket is *do
not read or modify `Info.plist` / `project.yml`*.

**Why it belongs in this rule and not only on iOS tickets:** it is the **general** shape —
*regenerating an artifact from its source destroys any uncommitted local state layered on top of it* —
and today it is carried entirely by per-ticket warnings, i.e. by whoever remembers to write one. That
is exactly the fragile mechanism a lane table replaces. The same shape exists elsewhere in this repo
(`./scripts/generate-api-clients.sh`, `npm run generate-*-client`, Gradle's openapi codegen) and is
**harmless there** because those outputs carry no hand-edited state — which is the discriminator the
rule should state, rather than banning regeneration.

**The safe form is already known and should be written down as the prescribed alternative:**
`xcodegen generate` is safe **in a scratch worktree** (the committed `project.yml` holds no key) and
unsafe in the main checkout. That is AC3's "prescribed alternative" applied to this hazard.

- [ ] **AC7 (added)** — The rule covers a **destructive command that ran in the wrong tree**, not only
      one that ran in the right tree with the wrong effect. It names the `cd X && <destructive>`
      redirect explicitly, states the mechanism (`cd` fails, `&&`'s left side is false only for `&&` —
      the compound still resolves to the second command's exit code in the observed incident, so the
      run reads as clean), and prescribes the unconditional form (`git -C <path>`, or `cd X || exit 1`).
      Evidence: the diff.
- [ ] **AC8 (added)** — The rule reaches **non-git** operations that regenerate a tracked-but-generated
      file over uncommitted local state, with `xcodegen generate` → `Info.plist` (owner's Stripe key)
      as the worked example, and states the **discriminator** — regeneration is safe when the output
      carries no hand-edited state (`generate-api-clients.sh`, `generate-*-client`) and unsafe when it
      does. Include the prescribed safe form: scratch worktree, never the main checkout. Evidence: the
      diff. **Do not open `Info.plist` or `project.yml` to write this AC** — the key is live; the
      generated-from-`project.yml` relationship is established by `d6969fef`-era history and the
      standing ticket warnings, not by reading the file.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 process finding with no home — a reviewer's `git stash -u`
  hard-reset a developer's worktree, ~50 minutes lost; routed to an architect+docs panel per the
  T-0445 precedent, since the PM does not own `agents/process/*.md`)
- 2026-07-30 — **scope extended** with the `patterns-*.md` lane ruling from the T-0441 review (see
  above). Routed here rather than forked: this ticket is already `shared-file-lanes.md`'s sole writer,
  and the two changes are the same edit to the same table. Size unchanged (**S**) — it is one more row
  plus a rationale line.
- 2026-08-01 — **scope extended again: two more incidents of the same class, both hit for real this
  sprint** — the `cd X && <destructive git>` redirect (an agent detached HEAD in the **owner's** repo)
  and `xcodegen generate` regenerating `Info.plist` over the owner's working-tree-only Stripe key.
  **AC7 and AC8 added.** Routed here rather than forked for this ticket's own reason: it already argues
  that the rule must describe the **class** (tree-wide state operations that discard work an agent does
  not own) rather than enumerate commands, and forking two more command-specific tickets would commit
  that error a third time. **Both hazards were already inside AC1's wording** — what they add is the
  evidence that the class reaches past `git` (hazard 3 is not a git command at all) and past the tree
  the agent *meant* to touch (hazard 2 is a shell mis-target).
- 2026-08-01 — **size re-checked: still `S`, and this is a deliberate call, not an oversight.** The
  deliverable is unchanged in kind — one rewritten rule in `shared-file-lanes.md` plus the charter
  constraints in AC4. Three incidents make the rule *better argued*, not larger; the whole point is
  that one class-shaped sentence replaces N command-shaped ones. **If the panel finds itself writing
  three separate rules, that is the signal to stop and tell the PM to split** (per `ticket-lifecycle.md`
  §Sizing), not to let it grow into an `L`.
- 2026-08-01 — **dependencies re-checked: none, and nothing blocks dispatch.** `depends_on: []` is
  correct; `agents/process/shared-file-lanes.md` has no other writer this sprint (T-0439's
  `quality-gates.md` lane is a different file and is now `done`). **T-0455 still wants a new cluster row
  in the same file** — serialize (**T-0456 → T-0455's row**, or hand the row to T-0456). The value of
  writing an incident down decays fast and there are now three of them; this is cheap and overdue.

## Review
<!-- reviewer writes verdict here; AC4's charter verification goes here -->
