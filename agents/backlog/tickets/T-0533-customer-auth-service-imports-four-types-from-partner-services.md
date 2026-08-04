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

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
