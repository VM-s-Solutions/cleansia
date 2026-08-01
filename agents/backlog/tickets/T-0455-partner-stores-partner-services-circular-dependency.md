---
id: T-0455
title: Break the partner-stores ↔ partner-services circular dependency (33 lint errors, invisible because lint is non-blocking)
status: draft
size: M
owner: architect
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, frontend]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Pre-existing debt, baselined identically across several sprint-14 branches, and currently **invisible**
because frontend CI runs lint with `continue-on-error: true`
(`.github/workflows/frontend-ci.yml:40-42`, with the reason written at `:35-39`: *"the lint baseline
has pre-existing debt in several libs (module-boundary, a11y, unused-var), so making it blocking
today would red every PR on debt the change didn't introduce… Flip to blocking once the baseline is
clean (tracked as a follow-up lint-cleanup ticket)."* **This is that ticket** for the module-boundary
slice.)

**PM verification, 2026-07-30** — ran `npx nx lint <project> --skip-nx-cache` for each of the four
implicated libs and counted:

| Project | Errors | Warnings |
|---|---|---|
| `partner-stores` | 19 | 4 |
| `services` | 6 | 1 |
| `partner-services` | 5 | 0 |
| `pipes` | 3 | 0 |
| **total** | **33** | 5 |

**33 errors — the reported figure is exact.** All are `@nx/enforce-module-boundaries`, and they
resolve to **two distinct cycles**, which matters because they have very different fixes:

**Cycle 1 — `partner-services → partner-stores → partner-services`.** The `→ partner-stores` leg is
**a single import in a single file**:

`libs/core/partner-services/src/lib/interceptors/loading.interceptor.ts:3-6`
```ts
import {
  setLoadingOffAction,
  setLoadingOnAction,
} from '@cleansia/partner-stores';
```

That is the **only** `@cleansia/partner-stores` import in all of `partner-services` (verified: grep
count = 1). The return leg is broad and legitimate — `partner-stores` imports generated client types
and `PartnerClient` from `partner-services` across `order.effects.ts`, `order.actions.ts`,
`order.state.ts`, `code.effects.ts`, `user.state.ts` and more. So the cycle is created by one
interceptor sitting on the wrong side of the seam, and the arrow that should be deleted is obvious.

**Cycle 2 — `partner-services → services → pipes → partner-services`.** Different shape, not fixed by
the same move. The reported chain, from the lint output:
- `libs/core/partner-services/src/lib/interceptors/error.interceptor.ts` and
  `libs/core/partner-services/src/lib/services/partner-auth.service.ts`
- → `libs/core/services/src/lib/services/file-validation-error.service.ts`
- → `libs/shared/pipes/src/lib/order-status/{order-status-icon,order-status-severity,payment-status-severity}.pipe.ts`
- → back into `partner-services`.

The three order-status pipes reach back into the partner client for enums. That is a **type-location**
problem, not a misplaced-file problem, and it is the reason this is `M` and not `S`: the naive fix
(move the pipes) breaks the customer/admin apps that also consume them.

**Why it is worth fixing rather than baselining forever:** every one of these cycles is a real
initialization-order hazard in a bundler, and — more immediately — 33 permanent red lines are exactly
what trains a team to stop reading lint output. The T-0438 regen break shipped through a check nobody
was reading.

## Acceptance criteria

- [ ] **AC1** — Given `npx nx lint partner-services partner-stores services pipes --skip-nx-cache`,
      When it runs on the branch, Then `@nx/enforce-module-boundaries` reports **0 errors** across all
      four. Evidence: the command, its full output and exit code, in `## Review`.
- [ ] **AC2** — Given the same command on `master` before the change, When it runs, Then it reproduces
      **33 errors** (the baseline above). If the number differs, **stop and re-baseline** before
      fixing — the debt moved and the ticket's premise needs re-grounding. Evidence: both runs.
- [ ] **AC3** — Given **both** cycles, When the fix lands, Then each is addressed and named
      separately in `## Review`, with the seam that was moved and why. Fixing only cycle 1 and
      declaring victory on the count fails this AC.
- [ ] **AC4** — Given all three web apps, When
      `nx build <app> --configuration=production --skip-nx-cache` runs for `cleansia.app`,
      `cleansia-partner.app` and `cleansia-admin.app`, Then all three exit 0. Non-negotiable: this
      change moves code between shared libs, and the admin app is the one that broke last time
      through exactly this shared-lib path (T-0438). Evidence: three commands, three exit codes.
- [ ] **AC5** — Given the partner app at runtime, When a request is in flight, Then the global loading
      indicator still shows and hides. Moving `loading.interceptor.ts` must not silently unregister
      it. Evidence: a test or a recorded manual walk, named.
- [ ] **AC6** — Given the four projects are clean, When the change lands, Then it states in `## Review`
      whether the **non-blocking lint gate can now be flipped to blocking** for those projects — and
      if not, exactly which remaining rule classes (a11y, unused-var) still red them, with counts.
      This ticket does not have to flip the gate; it does have to report whether it can.
- [ ] **AC7** — Gate 0.5 leg 2: every lint and build run is `--skip-nx-cache`. A cached Nx run here is
      worthless — the whole ticket is "the checker's output changed."

## Out of scope

- The **a11y** and **unused-var** lint debt named in `frontend-ci.yml:36`. Different rule classes,
  different fixes, different reviewers. AC6 only requires them **counted**.
- Flipping `continue-on-error: false` in the workflow. That is a separate change gated on the whole
  baseline, not just the module-boundary slice.
- `admin-stores` / `customer-stores`. If they carry the same shape, **report it in `## Review`** and it
  becomes a follow-up ticket — do not widen this one.
- Any behaviour change. This is a pure structural move; a diff that alters what the app does is out of
  contract.

## Implementation notes

**Architect panel required before this leaves `draft`.** Two questions, both real:
1. **Where does an NgRx-dispatching HTTP interceptor live?** It is neither a client nor a store — it is
   app wiring. Options: a new thin lib; the app shell (`apps/cleansia-partner.app`); or invert the
   dependency so `partner-stores` registers the interceptor. Note the same interceptor shape probably
   exists for admin and customer — the ruling should be one pattern for all three, checked, not
   assumed.
2. **Where do the order-status enums live** such that `libs/shared/pipes` can render a status without
   importing a per-tenant generated client? A shared domain-enum lib is the obvious answer and has a
   real cost: the generated clients are NSwag output and must never be hand-edited
   (`CLAUDE.md`), so any mapping layer must survive a regen. That constraint has bitten twice already
   (PR #166, T-0438) and the panel must answer it explicitly.

Record the ruling in `agents/architecture/decisions/`.

**Shared-file lane — new cluster, add it to `shared-file-lanes.md`:** while this ticket runs, it is the
**sole writer** across `libs/core/partner-services`, `libs/core/services`, `libs/data-access/partner-stores`
and `libs/shared/pipes`. Any concurrent web ticket touching those four is a collision. T-0447 (web
avatar) is in `libs/cleansia-customer-features/profile` + the customer i18n bundle and does **not**
intersect — verified: the live `UpdateCurrentUserCommand` caller is
`libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts:224`, and the
`partner-stores` `updateUserCurrent` action/effect/reducer trio remains **dead code** with no
dispatcher anywhere in any app (re-verified 2026-07-30, post-#171).

**Priority: post-demo.** Zero user-visible change; it buys back a checker nobody currently reads.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, baselined across several branches;
  needs an architect panel on two seams; count independently re-derived = 33)

## Review
<!-- reviewer writes verdict here; AC1/AC2 baselines and AC6's remaining-debt counts go here -->
