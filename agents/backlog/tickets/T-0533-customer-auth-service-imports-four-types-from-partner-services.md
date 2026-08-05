---
id: T-0533
title: A live cross-app client import — the customer auth service imports four types from `@cleansia/partner-services`
status: in_progress
size: S
owner: frontend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0031]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 15
source: found by the web lane during the object-literal sweep; recorded in `e4dd27f5` under "THREE
  FINDINGS WORTH MORE THAN THE FIXES". Filed by the PM in the sprint-15 reconciliation so the fix that
  is already in flight has a ticket to land against.
---

## Context

`src/Cleansia.App/libs/core/customer-services/src/lib/services/customer-auth.service.ts:5-10` imports
**four types from the partner client**:

```ts
import {
  GoogleAuthCommand,
  JwtTokenResponse,
  RequestPasswordChangeCommand,
  ResendConfirmationEmailCommand,
} from '@cleansia/partner-services';
```

The same file imports its other auth types from `@cleansia/customer-services`' own client on the very
next lines — so this is not a missing customer equivalent, it is four symbols reaching across an app
boundary that `agents/knowledge/patterns-frontend.md` forbids.

**Why it matters more than a tidiness rule.** These are *generated* types. The customer client and the
partner client are regenerated from **different OpenAPI documents**, on different runs, by the owner.
The moment the partner API's `GoogleAuthCommand` or `JwtTokenResponse` diverges from the customer API's —
a field added on one host and not the other, or a rename — the **customer app's login, register and
refresh paths** compile against a shape their own server does not send. It fails at runtime, in the
authentication path, for the app with the widest audience.

`e4dd27f5` also records that the two auth services were the worst object-literal clusters in the
workspace at 8 each, *"where a regen touching any auth DTO reddens login, register and refresh at
once"* — the same blast radius, reached by a second route. That sweep converted them and wrote 21 tests
pinning each command's serialized body first; this ticket closes the remaining half.

**This is verified, not suspected — and the tree state is named.** At **HEAD (committed)** the import is
present: `git show HEAD:…/customer-auth.service.ts` matches `@cleansia/partner-services` once. In the
**working tree** it is already **gone** — the live lane has removed it. So this ticket is `in_progress`
against an uncommitted fix, and its job is to make sure that fix is gated (AC3's untouched body-pinning
tests, and AC4's guard) rather than merely present.

## Acceptance criteria

- [ ] **AC1 — no import in `libs/core/customer-services/**` resolves to `@cleansia/partner-services`.**
      Given the customer services lib, When it is grepped, Then there are zero such imports.
      **Evidence:** the grep, plus a green build.
- [ ] **AC2 — the four types are sourced from the customer client.** Given `customer-auth.service.ts`,
      When it is read, Then `GoogleAuthCommand`, `JwtTokenResponse`, `RequestPasswordChangeCommand` and
      `ResendConfirmationEmailCommand` come from the customer client. **If a type genuinely does not
      exist on the customer OpenAPI document, do NOT invent it and do NOT keep the cross-import** —
      record which one, and stop: that is a backend contract gap and needs its own ticket plus an
      owner-only regen.
- [ ] **AC3 — the serialized wire body is unchanged.** Given the 21 body-pinning tests `e4dd27f5` added
      for these two services, When the imports are swapped, Then every one still passes **unmodified**.
      A test edited to accommodate this change invalidates the evidence.
- [ ] **AC4 — the boundary is enforced, not just cleaned.** Given T-0534 lands the real
      module-boundary constraint, When a future import crosses this boundary, Then lint fails. If
      T-0534 has not landed yet, add the tag pair this lib needs so AC4 is satisfied the moment it does,
      and say so in the status log. **A cleanup with no guard is a cleanup that comes back.**
- [ ] **AC5 — the customer app builds and its Jest suites pass.** `npx nx build cleansia.app` (note the
      DOT, per `CLAUDE.md`) and the customer app's tests.

## Out of scope

- The other 97 generated-DTO object literals — **T-0535**.
- Turning the module-boundary guard on — **T-0534**. This ticket fixes the violation; that one makes it
  unrepeatable.
- Any backend or OpenAPI change. If AC2 cannot be met without one, stop and file.

## Implementation notes

**Archetype:** `agents/knowledge/patterns-frontend.md` — generated-client ownership, one app one client.

The customer client is `libs/core/customer-services/src/lib/client/customer-client.ts`; the partner
client is `libs/core/partner-services/src/lib/client/partner-client.ts`. Never hand-edit either
(`CLAUDE.md`).

**No-decision note.** This is a mechanical import correction against a documented rule. No panel.

## Status log
- 2026-08-04 — created by pm during the sprint-15 reconciliation, at `in_progress`: a frontend instance
  is already working this finding. The ticket exists so the diff has a home and the reviewer has ACs to
  gate against; it does not start the work.
- 2026-08-05 — frontend: the import removal is **committed** (`7ddc491e`), not uncommitted as the
  Context says. AC1/AC2/AC3/AC5 verified at HEAD. **AC4 was the open one** and is now closed with a
  gate that can go red plus a mutation proof. See `## Review`.

## Review

### Gate 0 — the Context's tree-state note is stale

It says the fix is *"in the working tree … the live lane has removed it"*. At HEAD it is **committed**
(`7ddc491e`) and `git status` is clean over `src/Cleansia.App/`. Nothing to land.

### AC1 — zero `@cleansia/partner-services` imports in the customer services lib

```
$ grep -rn "@cleansia/partner-services" src/Cleansia.App/libs/core/customer-services/
(no output)
```

Widened as a cross-check: `grep -rn "stores'" libs/core/{partner,customer,admin}-services/src` — the
only remaining cross-lib imports were the three `loading.interceptor.ts` files and
`customer-auth.service.ts`'s `SavedAddressStore`, all same-scope (not cross-app) and all retired by
T-0455 in the same change.

### AC2 — the four types come from the customer client, and all four exist on it

`libs/core/customer-services/src/lib/services/customer-auth.service.ts:7-18` imports
`GoogleAuthCommand`, `JwtTokenResponse`, `RequestPasswordChangeCommand` and
`ResendConfirmationEmailCommand` from `'../client/customer-client'` alongside the other seven auth
types. **No backend contract gap; no `nswag-regen` needed** — the escape hatch in this AC was not
taken and did not need to be.

### AC3 — the body-pinning tests pass, and the assertions are byte-identical

`npx nx test customer-services --skip-nx-cache` green. `customer-auth.service.spec.ts`'s eleven
`sentBody(...)` assertions are untouched.

**One honest disclosure.** That spec's `providers` array **was** edited in this change — not by
T-0533's import swap, but by T-0455's removal of the `customer-services → customer-stores` arrow:
`{ provide: SavedAddressStore, useValue: {refresh, clear} }` became
`{ provide: SESSION_LIFECYCLE_LISTENERS, useValue: {onSessionStarted, onSessionEnded}, multi: true }`.
That is DI wiring for a dependency that no longer exists, not an accommodation of an assertion. Every
`expect(...)` in the file is unchanged, which is what AC3 is protecting.

### AC4 — the boundary is enforced now, not merely tagged

`libs/core/customer-services/project.json` carries `["scope:customer","type:util"]` and
`libs/core/partner-services/project.json` carries `["scope:partner","type:util"]`, so the constraint
`scope:customer → [scope:customer, scope:shared]` refuses the import. T-0534 landed that table.

**Tags plus a rule were still not a gate**, and this AC would have been satisfiable on paper while the
violation remained un-catchable: `frontend-ci.yml`'s only lint step is `continue-on-error: true`
(`:73`). So the same lane shipped `agents/tools/check-module-boundaries.mjs` +
`.github/workflows/module-boundaries.yml` (blocking, self-test first).

**Mutation proof, on the real tree.** Reintroduced the import
(`import { GoogleAuthCommand as … } from '@cleansia/partner-services'` in `customer-auth.service.ts`):

```
module-boundaries: linted 1340 file(s), 20 @nx/enforce-module-boundaries violation(s) … 18 known
    1  cross-scope
module-boundaries: 1 drift(s) from the recorded set:
  NEW      libs/core/customer-services/src/lib/services/customer-auth.service.ts::cross-scope (x1)
```

**exit 1 — RED.** Restored from a pre-mutation copy; `shasum -a 256 -c` → `OK`; re-run → **exit 0, 0
drift — GREEN**. The gate's own self-test carries this scenario permanently
(*"T-0533's own violation: a customer lib importing the partner client -> RED, classed cross-scope"*),
and stubbing the tool to `process.exit(0)` reddens it along with all 20 others.

### AC5 — the customer app builds and its tests pass

`npx nx build cleansia.app --configuration=production --skip-nx-cache` → **exit 0**.
`npx nx test cleansia.app --skip-nx-cache` → **5 suites, 22 tests, green**. Workspace-wide:
`npx nx run-many -t test --all --skip-nx-cache` → **67 projects green**; all three production builds
exit 0.
