---
id: T-0447
title: Web — avatar upload, render and removal on the customer profile
status: blocked
size: M
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0446, T-0438]
blocks: []
stories: [US-user-avatar]
adrs: []
layers: [frontend]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Part of the owner-approved avatar feature (batch item 5). The backend write path exists; the read
path is T-0446. Web has **no avatar UI at all** — verified 2026-07-30:

- `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts:224` builds
  `UpdateCurrentUserCommand` with `photo: undefined as any` — a hardcoded no-photo, and an `as any`
  that violates the no-`any` convention (Gate 1). It is the **only live** `UpdateCurrentUserCommand`
  caller in the whole monorepo.
- The partner store has a full `updateUserCurrent` action/reducer/**effect** chain
  (`libs/data-access/partner-stores/src/lib/user/user.effects.ts:80-119`) that constructs the command
  with a `BlobFileDto` — **and nothing dispatches it.** A repo-wide grep for `updateUserCurrent`
  returns only the action/reducer/effect definitions in the partner and admin stores; **no component
  or facade dispatches it**. So the partner web profile-photo path is dead code, not a working
  reference. Do not read it as one.
- `removePhoto` now exists on both regenerated clients
  (`libs/core/customer-services/.../customer-client.ts:12879`,
  `libs/core/partner-services/.../partner-client.ts:12860`) and **no client offers removal**, so
  removing an avatar is currently impossible from any surface. T-0438 wires `removePhoto: false` only.

**Held** until T-0446 lands *and* the owner confirms the `nswag-regen` bundle — the profile DTO shape
changes, so building against the current client would be building against a contract that is about to
move.

## Acceptance criteria

_(PM floor; the `US-user-avatar` analyst panel finalizes)_

- [ ] **AC1** — Given a signed-in customer, When the profile page loads and the user has a photo, Then
      the avatar renders from the T-0446 reference; when they have none, Then the existing
      initials/placeholder shows. Evidence: screenshots of both states.
- [ ] **AC2** — Given the profile page, When the user picks an image, Then it is uploaded via
      `UpdateCurrentUserCommand.photo` and the rendered avatar updates without a full reload.
      Evidence: screenshot + a facade unit test.
- [ ] **AC3** — Given a user **with** a photo, When they choose remove, Then `removePhoto: true` is
      sent, the avatar reverts to the placeholder, and a reload confirms it is gone server-side.
      Evidence: a facade unit test asserting `removePhoto: true`, plus a manual round-trip.
- [ ] **AC4** — Given a normal profile save with no photo action, When it is submitted, Then
      `photo` is absent **and** `removePhoto` is `false`, so the existing avatar survives — the
      regression `fe0c985b` fixed ("profile saves were deleting avatars"). Evidence: a test pinning
      this exact combination. **This is the mutation-prove case**: the test must go RED if the code
      sends `removePhoto: true` unconditionally.
- [ ] **AC5** — Given an oversized or non-image file, When the user selects it, Then it is rejected
      client-side with a translated message before any upload. Size/type limits must match whatever
      the backend enforces — **read the validator, do not invent a limit**
      (`Features/Users/UpdateCurrentUser.cs:61-63`).
- [ ] **AC6 (Gate 1)** — `photo: undefined as any` at `profile.component.ts:231` is gone; no `any`
      remains on the changed lines. All new strings use `TranslatePipe` with keys in **all 5** locales.
- [ ] **AC7 (Gate 8)** — All three production builds exit 0 and `nx affected -t test` is green. The
      builds must be run **after** T-0438 is on the branch, or the evidence is worthless.

## Out of scope

- Android / iOS — T-0448 / T-0449.
- **Reviving the dead partner-store `updateUserCurrent` chain** or adding a partner-web avatar UI. If
  the analyst panel wants a partner avatar, it is a separate ticket. Do, however, **report** the dead
  chain in the verdict so the PM can file its removal.
- Image cropping/rotation UI, or a Gravatar-style fallback.

## Implementation notes

- Logic goes in the **facade**, not the component (`patterns-frontend.md`). Component delegates.
- Use `<cleansia-*>` / PrimeNG controls — no raw `<input type="file">` styling; check whether a shared
  upload control already exists before adding one.
- **Shared-file lane:** `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — this ticket is the
  sole writer of the customer bundle in its wave. Edit only your own hunks; never `git restore` a
  locale file.
- The order-photo upload UI in the partner app is the closest **working** front-end precedent for the
  file→`BlobFileDto` conversion; prefer it over the dead user-store effect.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 5, web client)
- 2026-07-30 — blocked (on T-0446 + the owner's nswag-regen bundle; also awaiting the US-user-avatar panel)

## Review
<!-- reviewer + security verdicts here -->
