---
id: T-0447
title: Web — avatar upload, render and removal on the customer profile
status: blocked
size: M
owner: frontend
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

- 2026-07-30 — **re-prioritised, still blocked.** The owner has ruled the avatar feature **IS part
  of the demo**, so this ticket moved from "nice-to-have, post-demo" to **demo scope**. The block is
  unchanged and is not a scheduling choice: T-0446 must land, and then the **owner** must run the
  `nswag-regen` bundle, before this can compile. Dependencies unchanged.
- 2026-07-30 — lane note refreshed: **no** collision with the newly filed T-0455
  (partner-stores/partner-services cycle). This ticket's live call site is
  `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts:224`; the
  `partner-stores` `updateUserCurrent` action/reducer/effect trio is **still dead code** with no
  dispatcher in any app — re-verified by the PM 2026-07-30, post-#171. It remains **not** a reference
  implementation.

- 2026-07-30 — **security conditions attached** from the T-0446 gate (APPROVE-WITH-CONDITIONS). See
  the block below; they are binding on this ticket and its reviewer. Source:
  `agents/backlog/security/user-profile-avatar.md`. Still `blocked` — dependencies unchanged.

## Security conditions — BINDING (from the T-0446 gate, 2026-07-30)

These are not advisory. The reviewer checks them, and a diff that violates one does not pass Gate 3.

- [ ] **Do NOT move an authenticated profile route to `RenderMode.Server`.** The customer app's
      profile is `RenderMode.Client` today. `BlobUrl` is a **live credential** (a read SAS valid for
      one hour); server-rendering it would embed that credential in an HTML document that an
      intermediary proxy could cache. If SSR is genuinely needed for this screen, that is a **new
      architect decision**, not a judgement call inside this ticket — stop and raise it.
- [ ] **Cache on `fileName`, never on `blobUrl`.** The URL **changes on every fetch** — it is minted
      per request with a fresh expiry — so using it as a cache key defeats caching entirely and
      re-downloads the image on every profile read.
      **Note the change since this condition was first written:** T-0446 **AC10** now mints a fresh
      blob name on replace, so `fileName` changes when the user uploads a new photo. The
      per-client cache-eviction workaround that would otherwise have been required is therefore **no
      longer needed** — but if AC10 is ever dropped from T-0446, it comes back, so **verify AC10
      shipped** before relying on this.
- [ ] **If you close the blob `Content-Type` gap (T-0446 AC4) from this side, do not set
      `Cache-Control: public`.** A private image behind a SAS must be `private`, or an intermediary
      may retain it past the SAS window.
- [ ] **Do not log the profile response** (or the `BlobUrl`) in any interceptor, effect, or
      `console.*`. The backend redacts it in server logs — do not re-create the leak in the browser
      or in SSR server output.

## ⚠️ QA constraints — added 2026-07-30, READ BEFORE STARTING

Executed against Azurite with the app's own blob factory and the real handler.

### C1 — Image-error handling: re-fetch once. Do NOT branch on the status code

**Record correction first:** a "403 means expiry, 404 means deleted" rule was reported as having been
written into this ticket. **It never was** — no such text has ever existed here, so there is nothing
to reword. The correct guidance is being added now for the first time; do not go looking for a
previous version.

The status codes are real — QA confirmed **403** on an expired SAS, a tampered `sig` and a missing
SAS, and **404** on a deleted blob. **But an `<img>` tag cannot see either.** Chromium surfaces only a
bare `error` event (the 403 body is eaten by ORB — `net::ERR_BLOCKED_BY_ORB`); WebKit behaves the
same. A ticket telling this client to "treat 403 as re-fetch" would be an AC the client **cannot
implement**.

- [ ] **On ANY image error, re-fetch the profile once** — do not attempt to distinguish expiry from
      deletion in the browser, because you cannot.
- [ ] **Single-retry guard**, so a genuinely deleted blob falls back to the initials placeholder
      instead of looping profile reads forever.

### C2 — CORS: the storage account has none, and this kills a whole class of design

From a real origin, **`<img src>` loads fine** — but everything else fails, and **none of it is
fixable client-side**:

| Approach | Result |
|---|---|
| `<img [src]="blobUrl">` | **works — this is the only sanctioned approach** |
| `fetch(blobUrl)` | fails with `TypeError` |
| `<img crossorigin="anonymous">` | errors — no `Access-Control-Allow-Origin` |
| `fetch(mode: 'no-cors')` | opaque, unreadable response |
| canvas `getImageData` | blocked (tainted canvas) |
| `HttpClient.get(blobUrl, {responseType:'blob'})` | fails |

`deploy/bicep/modules/storage.bicep` has **no `cors` block** (PM-verified), so **real Azure is in the
same state** — this is not an Azurite artifact.

- [ ] **Bind the SAS straight into `[src]`**, exactly as `order-photos.component.html:125` and `:207`
      already do for order photos. That is the working precedent in this repo; copy it.
- [ ] **If this ticket's design assumes a crop-on-edit flow that reads the EXISTING stored avatar,
      that design is dead.** Say so and re-scope before writing code — it needs a CORS rule on the
      storage account, which is a **deploy change**, not a front-end change.
- [ ] **A pre-upload preview of the user's LOCALLY selected file is still fine** — that is a local
      `objectURL`/data-URL and never touches the blob. `order-photos.component.html:168` / `:254`
      (`staged.preview`) is the existing precedent for exactly that, and it is the distinction to
      hold on to: **previewing what the user just picked is fine; reading back what is stored is
      not.**

## Review
<!-- reviewer + security verdicts here -->
