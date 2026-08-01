---
id: T-0462
title: Code no gate can see — delete the stale second admin client, resolve the app-unreachable lib file, and correct the three CLAUDE.md entries (including six Nx commands that all fail)
status: draft
size: S
owner: frontend
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0439]
blocks: []
stories: []
adrs: [0031]
layers: [frontend, architect, docs]
security_touching: false
manual_steps: [owner-claude-md]
sprint: 14
---

## Context

Filed from the **ADR-0031 panel** (accepted). Three instances of **one class**: TypeScript that no
gate in this repo can see. Not three coincidences — a build that typechecks only what an app imports,
plus a lint gate that cannot fail, leaves a blind region, and things accumulate in it.

### Instance 1 — a second, stale, generated admin client

`src/Cleansia.App/libs/core/services/src/lib/client/admin-client.ts` — **280 KB**, last written
2026-06-25. **PM-verified, all four independently:**

- **No `nswag-*.json` writes to it.** `nswag-admin.json:39` outputs to
  `libs/core/**admin-services**/src/lib/client/admin-client.ts` — a **different lib**. So no regen has
  ever refreshed this copy.
- **No barrel exports it.** `libs/core/services/src/index.ts` exports `./lib/{auth,enums,interceptors,services,validators}`
  — **not** `./lib/client`.
- **Nothing imports it.** A repo-wide search for an import of this path returns zero hits.
- **No app build typechecks it**, because nothing reaches it from an app entry point.

It is frozen at whatever the schema looked like when it was last generated, and **it will drift
further every time the owner regenerates** — silently, because nothing reads it. Note the irony
relevant to T-0461: `libs/core/services/tsconfig.json:18` **does** set `strictTemplates: true`. The
configuration is correct; no build ever runs it over this file.

### Instance 2 — the app-unreachable lib file (T-0439's finding 3)

`email-template-form.facade.ts`. The ADR-0031 **challenger established it is caught by nothing**: the
app builds do not reach it, and lint — the one gate that would — is `continue-on-error: true`.

**Line-number precision (PM-verified, and the two differ):** `continue-on-error: true` is at
**`frontend-ci.yml:41` on `master`** and at **`:63` in T-0439's working tree**, because T-0439 modifies
that workflow. The panel cited `:63`. Check which tree you are in before quoting a line.

### Instance 3 — two `CLAUDE.md` entries that actively send agents into the blind region

**`CLAUDE.md` is owner-gated. This ticket PROPOSES the exact text; the OWNER applies it.** No agent
edits that file. Exact proposals are in "Proposed CLAUDE.md corrections" below.

- The repo map at **`CLAUDE.md:29`** still advertises `core/services/` as *"NSwag-generated API
  clients"*. **An agent following the map imports a client that no regen updates** — which is how
  instance 1 stops being harmless and becomes a live trap.
- **`CLAUDE.md:93-96`** documents only the three per-client commands, leaving the new
  **`generate-clients`** undocumented — the one that pays **one** typecheck instead of three.
- **`CLAUDE.md:84-91` — all six documented Nx commands are wrong and every one of them fails.**
  Reported after a frontend developer hit `Cannot find project` on its **first** invocation.
  **PM-verified directly against the `name` field in each `apps/*/project.json`:**

  | `CLAUDE.md:84-91` says | actual project name |
  |---|---|
  | `cleansia-partner-app` | **`cleansia-partner.app`** |
  | `cleansia-admin-app` | **`cleansia-admin.app`** |
  | `cleansia-app` | **`cleansia.app`** |

  A **dot** before `app`, not a hyphen. That is all three `nx serve` lines **and** all three
  `nx build --configuration=production` lines. **This is the worst item on the ticket**, ahead of the
  stale repo map: every agent that reads `CLAUDE.md` and tries to build burns a cycle on it, and a
  developer who does not stop to investigate could reasonably conclude **the build is broken rather
  than the docs** — and then start "fixing" a build that was never broken.

### What the PM found while checking whether the docs repeat it — the fix is NOT "add the dots"

The coordinator asked whether `docs/architecture/frontend.md` repeats the wrong names. **It does not —
and it is the one that is right.** It advertises **npm aliases**, and the PM verified **all seven
exist** in `src/Cleansia.App/package.json` and already resolve to the correct dotted names:

```
start:cleansia          = nx serve cleansia.app --configuration=development
start:cleansia-partner  = nx serve cleansia-partner.app --configuration=development
start:cleansia-admin    = nx serve cleansia-admin.app --configuration=development
build:cleansia-customer = nx build cleansia.app --configuration=production
build:cleansia-partner  = nx build cleansia-partner.app --configuration=production
build:cleansia-admin    = nx build cleansia-admin.app --configuration=production
```

**`frontend-ci.yml` invokes these same aliases** (`npm run build:cleansia-partner`,
`npm run build:cleansia-admin`). So there are three sources of truth and **`CLAUDE.md` is the only
wrong one** — and mechanically inserting the dots would "fix" it into a *fourth* command style that
matches neither the docs nor CI. **Correction 3 below therefore proposes the aliases as primary**,
with the corrected raw names kept as a secondary note for the cases the aliases do not cover. That is
a deliberate departure from the reported fix; the reasoning is here so the owner can overrule it.

**Additional wrongness found, deliberately NOT fixed here:** `agents/tools/wave2-2c-final.workflow.js:95`
also uses `cleansia-admin-app`. Several closed tickets (T-0239, T-0259, T-0294) do too, but those are
historical records and must not be rewritten. The `workflow.js` hit is a live tool file — **noted for
the PM to file separately**, not folded in, because `agents/tools/` is T-0454's and T-0461's lane.

**Not repeated in `docs/architecture/frontend.md`.** Its one genuine staleness — the `::: info` block
claiming only a formatter runs after generation, omitting `generate-clients` — **is already in flight
with the T-0439 developer. Do not duplicate it.**

## Dependency — this cannot go before T-0439, and the reason is concrete

**PM-verified:** `generate-clients` **does not exist on `master`.** `package.json` there has only
`generate-{partner,admin,customer}-client`, each `npx nswag run … && bash …-formatter.sh`. The script
exists **only in T-0439's uncommitted working tree**, where the four scripts are restructured onto
`nswag:*` + a shared `typecheck`. **Proposing a `CLAUDE.md` line documenting a command that does not
exist yet would hand the owner an edit that is wrong until T-0439 merges** — so this ticket waits, and
the implementer must **re-read `package.json` after T-0439 lands** and copy the *real* script names
rather than the ones quoted below.

Same applies to ADR-0031 itself (`agents/backlog/adr/0031-…md`) and the living doc
`agents/architecture/decisions/generated-client-contract.md`: **both currently exist only in T-0439's
worktree**, uncommitted. Do not create either on `master` from this ticket.

## Deliberation

**No panel.** Instances 1 and 3 are mechanical (delete dead code; correct two stale doc lines).
Instance 2 carries one judgement call — what to *do* with unreachable code — posed as AC3, with the
**gate-flip question explicitly deferred to T-0455** (see Out of scope) so the two tickets do not
fork.

## Acceptance criteria

- [ ] **AC1** — `libs/core/services/src/lib/client/admin-client.ts` is **deleted**. Evidence, produced
      **before** deleting and shown in the PR: (a) a search proving no import of the path anywhere in
      `apps/` or `libs/`, (b) `libs/core/services/src/index.ts` not exporting `./lib/client`, (c) no
      `nswag-*.json` targeting it. **Then** all three production app builds green after the deletion.
- [ ] **AC2 (Gate 0.5 leg 3 — state what the deletion does NOT prove)** — Deleting a file nothing
      imports cannot make a build red, so **a green build is not evidence the file was dead** — it is
      consistent with the file having been dead *and* with the build not looking. AC1's (a)/(b)/(c)
      searches are the actual evidence. Say so explicitly in the PR rather than presenting three green
      builds as proof.
- [ ] **AC3** — The `email-template-form.facade.ts` instance is **resolved and the choice justified**:
      delete it if genuinely dead (same three-part evidence as AC1), or make it reachable/typechecked
      if it is wanted. **"Left as-is" is an acceptable outcome only with a written reason** naming who
      owns it next.
- [ ] **AC4** — A short note in the PR (and the status log) records **how many other files sit in the
      same blind region** — files under `libs/` reachable from no app entry point. A count and the
      command used, not an impression. If the number is large, **stop and tell the PM** rather than
      widening this ticket.
- [ ] **AC5 (owner-gated, `manual_steps: owner-claude-md`)** — **All three** `CLAUDE.md` corrections
      are **proposed as exact replacement text** (below), each name re-verified against
      `apps/*/project.json` and `package.json` **as they stand after T-0439 merges**, and handed to
      the owner. **The ticket does not reach `done` until the owner confirms.** No agent edits
      `CLAUDE.md`.
- [ ] **AC5a (Correction 3 must be proven, not asserted)** — Before proposing the replacement, **run
      one command from each block** and show it: a wrong one failing with `Cannot find project`, and
      its replacement succeeding. Six commands documented as working, none of which run, is precisely
      the failure this ticket is about — **do not close it by swapping one unverified block for
      another.**
- [ ] **AC6** — No `nswag-*.json`, formatter script, or generated client under `libs/core/*-services/`
      is touched. This ticket removes a **duplicate**; the three live clients are T-0439's and the
      owner's territory.

## 🔁 2026-08-01 — THE RE-VERIFICATION AC5/AC5a DEMANDED IS NOW POSSIBLE, AND THE PM HAS RUN THE FIRST HALF

`depends_on: [T-0439]` is satisfied — T-0439 merged as `acf2f0bc` (PR #175). The block at
`## Dependency` said the proposed text was **quoted from an unmerged worktree** and must be re-read
against the merged `package.json`. **That re-read has now been done, and it changed the answer for two
of the three corrections. This is a step, not an assumption — read this before touching the text
below.**

**PM-run on `master` at `1c8fdd00`** (`node -e` over `src/Cleansia.App/package.json`, so the names are
parsed, not eyeballed). The full public script list is:

```
start:cleansia · start:cleansia-partner · start:cleansia-admin · start:cleansia-ssr
build · build:prod · build:cleansia-partner · build:cleansia-customer · build:cleansia-admin
test · lint · e2e · typecheck · typecheck:test
_nswag:partner · _nswag:admin · _nswag:customer
generate-partner-client · generate-admin-client · generate-customer-client · generate-clients
```

| Claim in the text below | Verified on merged `master`? |
|---|---|
| `generate-clients` exists | **YES** — it is real now. Correction 2's premise holds |
| the three `generate-*-client` names survived T-0439's restructure | **YES** — unchanged, exactly as M1 promised |
| T-0439 "restructures all four onto `nswag:*`" | **NO — and the warning at `:202-204` is now MISLEADING.** M1 renamed them to **`_nswag:*`** (underscore-prefixed = internal). There is **no public `nswag:*` script.** Do not propose one, and do not document `_nswag:*` — the whole point of M1 is that a human never invokes it |

### ⛔ Correction 3 is **STALE and must NOT be applied as written — it would REVERT a shipped fix**

**The owner already fixed the six Nx commands, in `d6969fef` (PR #177), while this ticket sat in
`draft`.** PM-verified against the merged file. And the owner adopted **the npm aliases** — i.e.
**exactly the departure this ticket argued for** at `:78-98` over the reported "insert the dots" fix.
That argument was accepted and shipped. `CLAUDE.md` now reads:

```
# Dev servers — prefer the npm aliases; CI invokes these same ones
npm run start:cleansia-partner          # Partner :4200
npm run start:cleansia-admin            # Admin :4201
npm run start:cleansia                  # Customer :4202

# Production builds
npm run build:cleansia-partner
npm run build:cleansia-admin
npm run build:cleansia-customer

# The Nx project names are cleansia-partner.app / cleansia-admin.app / cleansia.app
# — a DOT before `app`, not a hyphen. `npx nx build cleansia-partner-app` fails with
# "Cannot find project". Check with `npx nx show projects` before hand-writing one.
```

**The `find` block Correction 3 tells the owner to replace no longer exists in the file.** Handing the
owner a replacement for absent text is the same class of error the ticket was filed about. **Correction
3 is DISCHARGED — strike it from the owner's handoff.** Its AC5a obligation (prove one failing and one
succeeding command) is discharged with it: the owner's edit already carries the dot rule inline, and
the six commands it documents are the aliases `frontend-ci.yml` itself invokes.

### What is still genuinely owed to the owner — TWO corrections, not three

- **Correction 1** (`CLAUDE.md:29`, `core/services/` described as "NSwag-generated API clients") —
  **still wrong on `master`, PM-verified.** And still a live trap: `libs/core/services/src/lib/client/admin-client.ts`
  (280 KB, dated Jun 25) is still present, and `nswag-admin.json:39` still writes to
  `libs/core/admin-services/...` instead. Instance 1 is unchanged.
- **Correction 2** (`CLAUDE.md:97-100`) — **still owed.** `generate-clients` is still undocumented, and
  so is the sentence that every `generate-*` ends in `npm run typecheck`. **This is the same owner
  action as T-0439's `manual_steps: claude-md-generate-clients-line`** — they are one edit, and T-0439's
  M6 text is now stale for the same reason Correction 3 was. **This ticket owns the corrected
  proposal**; T-0439 does not re-propose it.

**Instance 2 is unchanged and still live** — `libs/cleansia-admin-features/template-management/src/lib/email-template-form/email-template-form.facade.ts`
is still present (PM-verified). Note the line-number caveat at `:48-50` resolves now that T-0439 has
merged: quote `frontend-ci.yml` from **`master`**, and re-locate `continue-on-error` rather than
reusing `:41` or `:63` — T-0439's F2 change added ~10 lines to that workflow.

- [ ] **AC5b (NEW) — the handoff to the owner contains exactly the corrections that are still true.**
      Correction 3 is struck as discharged; Corrections 1 and 2 are re-quoted against the file **as it
      stands after `d6969fef`**, with line numbers re-read rather than carried. Evidence: the `git
      show d6969fef -- CLAUDE.md` diff cited in the handoff, so the owner can see their own prior edit
      was accounted for rather than reverted.

## Proposed CLAUDE.md corrections (owner applies — exact text)

> **⚠️ 2026-08-01 — READ THE BLOCK ABOVE FIRST. Correction 3 below is STALE and DISCHARGED** (the
> owner shipped it in `d6969fef`); Correction 2's `nswag:*` warning is wrong (the real name is
> `_nswag:*`, internal). Only Corrections 1 and 2 go to the owner, re-quoted per AC5b.

**Correction 1 — `CLAUDE.md:29`, the repo map.** Replace:

```
│   │       ├── core/services/               # NSwag-generated API clients
```

with:

```
│   │       ├── core/services/               # Shared auth, interceptors, validators, enums (NOT generated)
│   │       ├── core/{partner,admin,customer}-services/  # NSwag-generated API clients (regen targets)
```

*Why:* the current line is factually wrong about the only thing an agent reads it for. The generated
clients live in the three `*-services` libs — those are the paths `nswag-{partner,admin,customer}.json`
actually write to.

**Correction 2 — `CLAUDE.md:93-96`, the regen commands.** Replace:

```
# Regenerate NSwag API clients (after backend changes)
npm run generate-partner-client
npm run generate-admin-client
npm run generate-customer-client
```

with:

```
# Regenerate NSwag API clients (after backend changes)
npm run generate-clients          # all three + ONE typecheck — prefer this
npm run generate-partner-client   # or one at a time (each pays its own typecheck)
npm run generate-admin-client
npm run generate-customer-client
```

*Why:* `generate-clients` pays one typecheck instead of three, and an undocumented command does not
get used. **⚠️ The implementer must re-verify these four script names against `package.json` after
T-0439 merges** — they are quoted from T-0439's working tree, not from `master`, and T-0439
restructures all four onto `nswag:*` + a shared `typecheck`.

**Correction 3 — `CLAUDE.md:84-91`, the six broken Nx commands.** Replace:

```
# Dev servers
npx nx serve cleansia-partner-app       # Partner :4200
npx nx serve cleansia-admin-app         # Admin :4201
npx nx serve cleansia-app               # Customer :4202

# Production builds
npx nx build cleansia-partner-app --configuration=production
npx nx build cleansia-admin-app --configuration=production
npx nx build cleansia-app --configuration=production
```

with:

```
# Dev servers — prefer the npm aliases (same ones CI and docs/architecture/frontend.md use)
npm run start:cleansia-partner          # Partner :4200
npm run start:cleansia-admin            # Admin :4201
npm run start:cleansia                  # Customer :4202

# Production builds
npm run build:cleansia-partner
npm run build:cleansia-admin
npm run build:cleansia-customer

# Direct Nx form, if you need a flag the alias doesn't pass. NOTE the project names
# end in ".app" (a DOT, not a hyphen) — "cleansia-app" does not exist and will fail
# with "Cannot find project".
npx nx serve cleansia-partner.app
npx nx build cleansia.app --configuration=production
```

*Why:* the six commands as written **all fail** — the project names in `apps/*/project.json` are
`cleansia.app`, `cleansia-partner.app`, `cleansia-admin.app`. Leading with the npm aliases makes
`CLAUDE.md` agree with `frontend-ci.yml` and `docs/architecture/frontend.md` instead of introducing a
fourth style, and the explicit note about the dot is what stops the next reader re-deriving it. **The
implementer re-verifies every name against `apps/*/project.json` and `package.json` before handing
this to the owner** — do not copy it from this ticket on trust.

## Out of scope

- **Flipping the lint gate to blocking — `T-0455` owns that**, and it is the reason instance 2 is
  invisible. This ticket resolves **the file**; T-0455 answers **the gate**. Deliberately split so the
  two do not fork; cross-reference, do not duplicate.
- The 33 module-boundary lint errors — **T-0455**.
- Adding a repo-wide "typecheck every lib" step to CI. That is a real idea and the honest general fix
  for this whole class, but it is a **CI-architecture decision with an unknown red-build baseline**,
  not a cleanup. **Note it in the status log** for the PM to file as its own ticket after AC4's count
  is known.
- Regenerating any client — **owner-only**.
- Editing `CLAUDE.md` — **owner-gated**; this ticket only proposes text.
- ADR-0031 and `agents/architecture/decisions/generated-client-contract.md` — both live in T-0439's
  worktree today. **Do not create or edit either here.**

## Implementation notes

- **Archetype:** none — this is deletion plus a doc proposal. The nearest discipline to copy is
  T-0438's: prove the call sites before touching them, then build all three apps.
- **Shared-file lanes:** `libs/core/services/**` — no other sprint-14 ticket writes it (T-0455's
  cluster is `libs/core/partner-services`, `libs/core/services`, `libs/data-access/partner-stores`,
  `libs/shared/pipes` — **`libs/core/services` overlaps**, so **serialize behind T-0455 if it is in
  flight**, and coordinate with the PM if both are dispatched). `CLAUDE.md` — **owner only**.
- Run the three production builds with `--skip-nx-cache`; a cached green is not a green (Gate 0.5
  leg 2).

## Status log
- 2026-07-30 — draft (created by pm from the ADR-0031 panel; three instances of one class, filed as one ticket per the lead's hand-off)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0439]` unsatisfied — `generate-clients` does not exist on `master` (PM-verified), so AC5's proposed text cannot be finalized yet. Lane overlap with T-0455 on `libs/core/services` noted.
- 2026-08-01 — **`depends_on: [T-0439]` SATISFIED** (merged `acf2f0bc`, PR #175). **The re-verification
  this ticket said was impossible is now done and is recorded as an explicit step, not assumed** — see
  the 🔁 block. Outcome: `generate-clients` is real; T-0439's scripts are `_nswag:*` (internal), not
  the `nswag:*` the text warns about; and **Correction 3 is DISCHARGED — the owner shipped it in
  `d6969fef` (#177), adopting this ticket's npm-alias recommendation over the reported "insert the
  dots" fix.** Applying the text as written would now **revert** the owner's edit.
- 2026-08-01 — **stays `draft`, and NOT because of a dependency.** Every `depends_on` is satisfied; what
  is unsatisfied is the ticket's own content. AC5's proposal is now provably stale in two of three
  parts, and **AC5b** (added above) is the work that has to happen before this can be handed to anyone.
  Scope shrank: 3 corrections → **2**, and the AC5a "prove one failing and one succeeding command"
  obligation is discharged with Correction 3.
  **Promote to `ready` once AC5b's re-quote is written** — the two code instances (the stale
  `libs/core/services/.../admin-client.ts`, the unreachable `email-template-form.facade.ts`) are both
  still live on `master`, PM-verified, and neither has moved.
- 2026-08-01 — **de-duplication ruling: this ticket owns the `CLAUDE.md` `generate-clients` line, not
  T-0439.** T-0439 is `done` and carries `manual_steps: claude-md-generate-clients-line` as a flagged
  owner action (which `ticket-lifecycle.md` §"Done means" item 4 permits — it is neither a migration
  nor a regen). Its M6 literal text is stale for the same reason Correction 3 was. **One edit, one
  owner, one proposal — this one.** Do not send the owner two versions of the same block.

## Review
<!-- reviewer verdict here -->
