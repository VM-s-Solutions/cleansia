---
id: T-0475
title: iOS — move the Stripe publishable key and DEVELOPMENT_TEAM out of project.yml into a gitignored xcconfig
status: draft
size: S
owner: ios
created: 2026-08-01
updated: 2026-08-01
depends_on: []
blocks: [T-0474]
stories: []
adrs: []
layers: [ios, docs]
security_touching: true
manual_steps: [xcode-project]
sprint: 14
---

> **Owner-approved (relayed 2026-08-01) and being implemented outside the normal dispatch.** Filed so
> the work is **tracked and reconcilable**, not to schedule it. If the implementation differs from the
> AC below, **the implementation wins and this ticket is corrected to match** — the AC are the PM's
> reconstruction of the intent, not a spec handed to a developer.

## Context

The owner's live Stripe publishable key and their `DEVELOPMENT_TEAM` identifier exist **only as
uncommitted working-tree edits** to files that are otherwise committed and machine-regenerated. Every
operation that restores those files from their source destroys both values.

**This has now happened repeatedly.** Recorded instances this sprint: the key was wiped **twice**, and
the mitigation — *"never read or modify `Info.plist` or `project.yml`"* — is carried on **every single
iOS ticket** as a hand-written warning. That is a per-ticket reminder standing in for a structural fix,
which is the fragile mechanism a config seam replaces. **This has been offered before and never taken
up; 2026-08-01 is the third occurrence.**

### Why the loss is over-determined — both files are exposed, by different mechanisms

**The PM did NOT open either working-tree file** (the live key is in both). Grounded from the
**committed** copies via `git show HEAD:…`, which are key-free by design:

| Committed line | Content | What destroys the owner's value |
|---|---|---|
| `CleansiaCustomer/project.yml:22` | `DEVELOPMENT_TEAM: ""` (base `settings:`) | any `git pull` / `checkout` / `reset` touching the file |
| `CleansiaCustomer/project.yml:99` | `STRIPE_PUBLISHABLE_KEY: $(STRIPE_PUBLISHABLE_KEY)` (the `info.properties` reference) | — *(this is the indirection, not the value)* |
| `CleansiaCustomer/project.yml:137` | `STRIPE_PUBLISHABLE_KEY: ""` (the target build setting) | any `git pull` / `checkout` / `reset` |
| `CleansiaCustomer/Info.plist` *(generated)* | the resolved value | **`xcodegen generate`** — it rewrites `Info.plist` from `project.yml` |

`git status` at the time of writing shows **both** `Info.plist` and `project.yml` carrying uncommitted
modifications. So there are two independent destruction paths — **`git` for the source, `xcodegen` for
the generated output** — and today the owner has to survive both.

**The good news, and why this is `S` and not `M`:** **the `$(...)` indirection already exists.**
`info.properties` already reads `$(STRIPE_PUBLISHABLE_KEY)` from a build setting. An xcconfig's entire
job is to *supply a build setting*. So the change is to move the **value** out of `project.yml` into a
gitignored `.xcconfig` that `project.yml` references via `configFiles:` — not to invent a new
configuration mechanism.

### The precedent this mirrors

`src/cleansia_ios/.gitignore` already carries exactly this pattern for a different owner-local secret:

```
# Firebase config — owner-local, per-app; never committed (contains the
# project's sender id / API key). Owner drops it into each app's Firebase/ folder.
**/GoogleService-Info.plist
```

Same shape: a file the owner supplies locally, gitignored, never committed, referenced by the build.
**That is the whole argument** — one more file of a kind the repo already has, rather than a new idea.

### The real trade-off this ticket must not paper over

`DEVELOPMENT_TEAM` is **not a secret** — it is a signing identifier. Bundling it into the same gitignored
file as the Stripe key means a **fresh clone cannot build at all** without the owner's private file,
whereas today it builds and merely has a broken Stripe path. That is a genuine regression in
onboarding, traded for the key never being wiped again. It is the one real decision in this ticket and
AC4 forces a choice rather than letting it happen by default.

## Acceptance criteria

- [ ] **AC1** — Given a fresh `git checkout` or `git pull` of `src/cleansia_ios`, When the tree is
      inspected, Then **no Stripe key and no `DEVELOPMENT_TEAM` value is present in any tracked file**,
      and the owner's local values are **untouched** by the operation. Evidence: the committed diff plus
      a stated verification that the owner's values survived a checkout.
- [ ] **AC2** — Given `xcodegen generate` run in the **main checkout**, When it completes, Then the
      owner's Stripe key and team id are **still in force** for the build. **This is the load-bearing
      AC**: `xcodegen` is the destruction path that `git`-side fixes alone do not close, and it is
      exactly what makes T-0474's post-checkout regeneration safe to prescribe. Evidence: the value
      resolved after a regenerate, stated as an executed check.
- [ ] **AC3** — Given the `.xcconfig` files, When `.gitignore` is read, Then they are ignored by the
      **same kind of rule** that already covers `GoogleService-Info.plist`, with a comment in the same
      voice explaining what the file is and who supplies it. Evidence: the `.gitignore` diff.
- [ ] **AC4 — the `DEVELOPMENT_TEAM` trade-off is DECIDED and RECORDED, not defaulted.** Given that
      `DEVELOPMENT_TEAM` is a signing identifier and **not a secret**, When the split is made, Then the
      ticket states **explicitly** whether it lives in the gitignored xcconfig (a fresh clone cannot
      build without the owner's file) or stays committed (a fresh clone builds; the value is public,
      which it arguably already is in every provisioning profile). **Either answer is defensible; an
      unstated one is not.** Evidence: the decision written in `## Review`.
- [ ] **AC5 — a fresh clone has a documented path to a working build.** Given the gitignored file(s),
      When someone clones the repo, Then there is a committed **`*.xcconfig.example`** (or equivalent)
      with the keys present and the values empty, **plus** a line in the iOS README naming what to copy,
      where, and what breaks if you don't. Without this, the first symptom of a missing file is a
      **signing or Stripe failure with no pointer to the cause** — which is a worse failure than the one
      being fixed. Evidence: the example file + the README diff.
- [ ] **AC6 — fail-closed is preserved, not weakened.** The existing behaviour — an empty publishable
      key means **do not offer card payment, offer cash, never present an unconfigured PaymentSheet**
      (`sprint-12.md` Decision 4, the T-0311 ship-fail-closed pattern) — **still holds** when the xcconfig
      is absent. Evidence: the code path named, and a statement of what a missing xcconfig produces at
      runtime. **A missing config must degrade, not crash and not silently take a card payment.**
- [ ] **AC7 — the key never becomes committable by accident.** Given the new layout, When someone runs
      `git add -A`, Then no file containing the key is staged. The owner's standing operating note is
      *"hunk-stage only, never `git checkout` the iOS plists"* — **this ticket's whole value is retiring
      that rule.** Evidence: `git status` / `git check-ignore` output on a tree with the real values in
      place.
- [ ] **AC8 (Gate 0.5)** — iOS build proven against the **new** configuration: `xcodebuild build` (or
      `xcodegen generate` + build) succeeds with the xcconfig present, and the fail-closed path is
      exercised with it absent. Leg 2: not a cached/`UP-TO-DATE` run. Leg 3: name anything that could
      not be verified — in particular, **whether the owner's real key was ever used in verification**
      (it should **not** need to be; a placeholder proves the plumbing).

## Out of scope

- **Any other secret.** `GoogleService-Info.plist` already has its own mechanism and keeps it. The `.p8`
  push key, `AuthKey_*`, `*.p12`, `*.mobileprovision` and `fastlane/.env` are all already gitignored
  (`src/cleansia_ios/.gitignore`) — do not restructure them.
- **CI signing.** How GitHub Actions obtains a team id / signing identity is a separate concern; this
  ticket is about the **local developer and owner** tree. If the change affects `ios-ci.yml`, say so —
  do not redesign CI signing here.
- **Rotating the Stripe key.** Nothing here exposes it; it has never been committed. This is a
  durability fix, not an incident response.
- **The `shared-file-lanes.md` rule** about regeneration destroying uncommitted state — that is
  **T-0456 AC8**, already written and already owning that file. This ticket removes the *instance*;
  T-0456 documents the *class*. **Both are wanted** — the class outlives this instance.

## Implementation notes

**Panel: none — this applies an existing pattern to a second instance.** The repo already carries the
"owner-local, gitignored, never committed, referenced by the build" shape for
`GoogleService-Info.plist`, and the `$(STRIPE_PUBLISHABLE_KEY)` indirection already exists in
`project.yml`. **The one genuine decision — `DEVELOPMENT_TEAM` in or out — is carried as AC4** rather
than sent to a panel, because it is a two-option trade-off with a stated cost on each side, not a
design space. *(If the implementer finds it is more than that, escalate; do not let it grow.)*

**`security_touching: true`** — this moves a live credential between storage locations. The security
gate's job here is narrow and should be stated as such: **confirm the key cannot reach a tracked file,
and that the absent-config path fails closed.** It is not a re-review of Stripe integration.

**`manual_steps: [xcode-project]`** — the owner supplies the real values into the new local file, in
both app directories. Until they do, the owner's own build has **no** key. **Sequence that hand-off
explicitly**: the change lands, the owner drops the values in, *then* T-0474's regeneration step is safe
to use. A landing that leaves the owner keyless with no instruction is a worse day than the wipes.

**⚠️ Do not open the working-tree `project.yml` or `Info.plist` to write this change without a plan for
the live values already in them.** They are modified right now. The safe route is the one already
documented for iOS work: a **scratch worktree**, where the committed `project.yml` is key-free.

**This ticket `blocks: [T-0474]`** — specifically T-0474's `xcodegen generate` leg. See that ticket.

## Status log
- 2026-08-01 — **draft (created by pm).** Owner-approved per relay; **being implemented by the
  coordinating agent outside the normal dispatch.** Filed for traceability and reconciliation, not to
  schedule the work. Third time this fix has been offered.
- 2026-08-01 — **PM grounding, and one refinement to the brief.** The brief said the key *"lives only as
  an uncommitted edit in `project.yml`"* and that *"`xcodegen generate` itself wipes it"*. Read against
  the **committed** `project.yml` (via `git show`, the working copies deliberately not opened): the
  value has **two** homes and **two** destruction paths — the source `project.yml` (destroyed by `git`
  operations, **not** by `xcodegen`, which only reads it) and the generated `Info.plist` (destroyed by
  `xcodegen generate`, which rewrites it). `git status` shows both files modified. **This makes the case
  stronger, not weaker**, and it means the fix has to close both paths — hence **AC1 (git-side)** and
  **AC2 (xcodegen-side)** as separate criteria. The implementer should confirm which file actually holds
  the owner's value today before assuming either.
- 2026-08-01 — **`blocks: [T-0474]` recorded.** T-0474 prescribes running `xcodegen generate` after every
  pull. Today that command **wipes the key**, so automating it before this lands would turn an occasional
  loss into one on every pull. AC2 is what discharges that.

## Review
<!-- reviewer + security verdicts. AC4's decision and AC6's named fail-closed path go here. -->
