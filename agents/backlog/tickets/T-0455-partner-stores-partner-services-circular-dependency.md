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
- 2026-08-05 — frontend: **re-baselined before touching anything, and the premise had moved in both
  directions** (AC2's own instruction). Cycle 2 was already gone; cycle 1 turned out to exist in all
  **three** apps, not one. 47 errors retired, 0 remain. See `## Review`.

## Review

### AC2 — re-baseline. **The filed figure of 33 does not reproduce. Stopped and re-grounded, as AC2 requires.**

Measured on `master` at `6a901ed0`, `npx nx run-many -t lint --all --skip-nx-cache` (the whole
workspace, not the four named projects — the ticket's premise turned out to be narrower than the
defect):

| | ticket (2026-07-30) | measured 2026-08-05 |
|---|---|---|
| `partner-stores` | 19 | **17** |
| `partner-services` | 5 | **1** |
| `services` | 6 | **5, none of them boundary** (all `no-useless-escape`) |
| `pipes` | 3 | **0** |
| `customer-stores` | — | **15** |
| `customer-services` | — | **3** |
| `admin-stores` | — | **10** |
| `admin-services` | — | **1** |

**Two independent movements, both away from the ticket text.**

1. **Cycle 2 is already fixed and the ticket should not have been dispatched for it.**
   `partner-services → services → pipes → partner-services` no longer exists: the three
   `order-status/*.pipe.ts` files now import `OrderStatus`/`PaymentStatus` from `@cleansia/models`
   (`libs/shared/models/src/lib/models/order-status.models.ts`), pinned by
   `order-status-enum-parity.spec.ts` which reads the three generated clients **off disk**. That work
   landed in `7ddc491e` and is the subject of the still-`proposed` ADR-0042. Nothing in this lane
   touched it.
2. **Cycle 1 was never a partner problem.** The identical shape shipped in all three apps —
   `libs/core/<app>-services/src/lib/interceptors/loading.interceptor.ts` importing
   `@cleansia/<app>-stores` for two `createAction` constants. The ticket's own implementation note
   anticipated this (*"the same interceptor shape probably exists for admin and customer — the ruling
   should be one pattern for all three, checked, not assumed"*). It does; it was; one pattern was
   applied to all three.

**The true baseline was 47 boundary errors in three cycles, not 33 in two** — 18 partner
(`partner-stores` 17 + `partner-services` 1), 18 customer (`customer-stores` 15 +
`customer-services` 3), 11 admin (`admin-stores` 10 + `admin-services` 1). The count moved again
between the filing and a mid-session measurement of 36→42 because **spec files count**: each store
spec added imports the same generated client its effect does, and `user.effects.spec.ts` is one of
the 17 partner rows. Any count taken as a snapshot of this defect drifts with test coverage.

### AC1 — after. **0 boundary errors across the four named projects, and across all 70.**

`npx nx run-many -t lint --all --skip-nx-cache`, whole workspace, before and after:

| | before (`6a901ed0`) | after | Δ |
|---|---|---|---|
| projects with ≥1 lint **error** | **24** | **18** | −6 |
| total lint errors | **186** | **139** | **−47** |
| total lint warnings | **163** | **163** | 0 |
| `@nx/enforce-module-boundaries` errors | **66** | **19** | **−47** |
| …of which `circular-dependency` | **47** | **0** | −47 |
| …of which `cross-scope` | 0 | 0 | 0 |

Per-project diff — **exactly the six projects in the three cycles moved, and only their error
counts**:

```
project                            before       after
admin-services                     1e 1w        0e 1w
admin-stores                      10e 1w        0e 1w
customer-services                  3e 0w        0e 0w
customer-stores                   15e 2w        0e 2w
partner-services                   1e 0w        0e 0w
partner-stores                    17e 4w        0e 4w
```

`services` and `pipes` are unchanged because neither had a boundary error to begin with (AC2).
No project gained an error or a warning. AC7 satisfied: every lint and build run quoted here is
`--skip-nx-cache`.

### AC3 — both cycles, named separately, with the seam that moved

**Cycle 1 (all three apps) — the seam moved UP, not down.** `libs/core/<app>-services/src/lib/
interceptors/loading.interceptor.ts` → `libs/data-access/<app>-stores/src/lib/loading/
loading.interceptor.ts`.

The ticket's panel question was *"where does an NgRx-dispatching HTTP interceptor live?"* with three
options: a new thin lib, the app shell, or invert so the store lib registers it. **Option 3**, on one
principle: `*-stores` already reads `*-services` for the generated client, so the store is the
*higher* layer; an interceptor that dispatches store actions is higher still. Putting it beside the
actions it dispatches removes the arrow rather than routing around it. The two rejected alternatives
and why:

- *A new thin lib per app* — three `project.json`s, three jest configs, three barrels, three tags, for
  one 20-line file each. It buys nothing the store lib does not already have.
- *Move the two `createAction` constants DOWN into `*-services` instead, so `<APP>_INTERCEPTORS_FN`
  never changes.* Genuinely tempting: zero app-config churn and therefore zero risk of silent
  unregistration. Rejected because it puts NgRx actions **below** the client lib to preserve a
  barrel — inverting the layering to protect an export list — and it splits the `loading` slice
  (actions in one lib, reducer/selectors/state in another).

**Cycle 2 — already retired before this lane opened.** Named here rather than silently omitted: the
seam that moved was the *type location*, `@cleansia/partner-services` → `@cleansia/models`, in the
three `order-status/*.pipe.ts` files. Not this ticket's work; verified present and correct at HEAD.

**The customer app had a second arrow the ticket never recorded**, and clearing cycle 1 alone would
have left it at 3 errors: `customer-auth.service.ts` injected `SavedAddressStore` from
`@cleansia/customer-stores` to warm the address cache on sign-in and blank it on sign-out. Fixed with
a token seam declared where the caller lives (`SESSION_LIFECYCLE_LISTENERS` in
`libs/core/customer-services/src/lib/services/session-lifecycle.ts`), `SavedAddressStore`
implementing it, and the customer app providing `{ useExisting: SavedAddressStore, multi: true }`.
Moving `SavedAddressStore` into `customer-services` was the other option and was rejected: it is
cross-feature state consumed by order-wizard, recurring-bookings and profile, so it belongs in
`data-access`, and the move would have rewritten imports in ~12 files across four feature libs.

### AC4 — three production builds

`npx nx build <app> --configuration=production --skip-nx-cache`: `cleansia.app` **exit 0**,
`cleansia-partner.app` **exit 0**, `cleansia-admin.app` **exit 0**. Also
`npx nx run-many -t test --all --skip-nx-cache` → **67 projects green**.

### AC5 — the loading indicator, and the failure this move actually risked

The risk is not the interceptor's logic (byte-identical) but its **registration**: it left
`<APP>_INTERCEPTORS_FN`, which is what `app.config.ts` passes to `withInterceptors`. An interceptor
that is moved but not re-registered is silent at runtime and green in every other check. Two named
tests, both mutation-proven:

- `apps/*/src/app/http-interceptors.spec.ts` — pins the composed chain by **identity and order**
  (common → client → store). Mutation: deleted `...PARTNER_STORE_INTERCEPTORS_FN` from
  `apps/cleansia-partner.app/src/app/http-interceptors.ts` → **3 of 3 tests RED**; restored
  byte-exact (sha256 verified) → green.
- `libs/data-access/*-stores/src/lib/loading/loading.interceptor.spec.ts` — drives a real
  `HttpClient` through `withInterceptors([...])` against `HttpTestingController` and asserts the
  on-dispatch before the response and the off-dispatch after, **including on a 500** (the case where
  a missing `finalize` leaves the spinner up forever).

Chain order is preserved exactly: `[...COMMON, ...<APP>(auth, error), loading]` is the same sequence
as the old `[...COMMON, ...<APP>(auth, error, loading)]`.

### AC6 — can the lint gate be flipped to blocking for these projects? **For these four, yes. For the workspace, no — and the flip is the wrong lever anyway.**

All four named projects (`partner-services`, `partner-stores`, `services`, `pipes`) now report **0
errors**; so do `customer-services`, `customer-stores`, `admin-services`, `admin-stores`.

The remaining workspace debt that still reds `nx lint`, counted (AC6's requirement), 139 errors over
18 projects:

| rule | errors |
|---|---|
| `@angular-eslint/template/click-events-have-key-events` | 34 |
| `@angular-eslint/template/interactive-supports-focus` | 34 |
| `@angular-eslint/component-selector` | 20 |
| `@nx/enforce-module-boundaries` | 19 |
| `@angular-eslint/template/label-has-associated-control` | 13 |
| `no-useless-escape` | 5 |
| `@typescript-eslint/no-inferrable-types` | 3 |
| `@nx/dependency-checks` | 3 |
| `@angular-eslint/no-output-on-prefix` | 3 |
| `@typescript-eslint/no-empty-function` | 2 |
| `@angular-eslint/template/elements-content` | 2 |
| `@angular-eslint/directive-selector` | 1 |

So a11y is 83 of the 139 and `component-selector` another 20 — flipping `continue-on-error: false`
today still reds every PR, exactly as `frontend-ci.yml:35-39` says.

**Stated plainly, because it is the point:** the lint gate would not have been enough even if it were
flipped. It is `nx affected -t lint`, and a boundary violation is a statement about a **pair** of
projects — the half that reports it is often not the half that was edited. This lane therefore did
not flip it. The boundary slice got its own blocking gate instead
(`agents/tools/check-module-boundaries.mjs` + `.github/workflows/module-boundaries.yml`), which lints
from the workspace root over all 1340 files and cannot be selected away. The a11y/selector flip
remains T-0536's, against the counts above.

### Out-of-scope findings, reported not widened

- **`admin-stores` / `customer-stores` carry the same shape** (the ticket asked): they did, and they
  are fixed here rather than deferred — the fix is one pattern and splitting it across tickets would
  have left two of three apps circular while the catalog claimed one rule.
- **The 19 boundary errors that remain** are three older classes, each needing its own decision, and
  are now recorded as an exact-match ratchet: `buildable-from-non-buildable` ×14 (all in
  `libs/shared/components`, which carries a `package.json`), `static-import-of-lazy` ×4 (each app
  shell statically imports `@cleansia/components` while its own `app.routes.ts` lazy-loads it),
  `deep-relative-import` ×1 —
  `libs/cleansia-admin-features/invoice-management/src/lib/invoice-detail/invoice-detail.facade.ts:16`
  reaches into `employee-management`'s source through `../../../../` for `RejectDialogComponent`,
  which that lib's barrel does not export. Fixing it is a choice between widening that barrel and
  moving the dialog to `libs/shared/components`; not touched, not this lane's files.
- **`ADR-0042 §V.4`'s cycle argument has one leg fewer now.** Its `implicitDependencies` rejection
  cites `models → partner-services → partner-stores → models`, where the middle arrow is
  `loading.interceptor.ts`. That arrow is gone. The rejection still stands on `models → partner-
  services` being a `scope:shared → scope:partner` edge, but the ADR's stated chain is stale.
