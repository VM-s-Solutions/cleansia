---
id: T-0447
title: Web — avatar upload, render and removal on the customer profile
status: done
size: M
owner: frontend
created: 2026-07-30
updated: 2026-08-05
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
  `agents/archive/2026-08/backlog/security/user-profile-avatar.md`. Still `blocked` — dependencies unchanged.
- 2026-08-01 — **`blocked` → `ready`. BOTH halves of the block are gone, and each was verified rather
  than assumed** (PM, first-hand, on `master` at `1c8fdd00`):
  1. **`depends_on: [T-0446, T-0438]` — both `done`.** T-0438 merged `7c82cd2e` (#171); T-0446 merged
     `a63b776e` (#176) with reviewer APPROVED, the security gate's two conditions shipped as AC9/AC10,
     and AC4 closed by the owner on DEV.
  2. **The owner's `nswag-regen` is DONE and shipped inside `a63b776e`.** `blobUrl` is present on the
     `BlobFileDto` shape in `libs/core/customer-services/src/lib/client/customer-client.ts` and
     `libs/core/partner-services/src/lib/client/partner-client.ts`. `admin-client.ts` already carried
     `blobUrl` on another DTO and needed no delta. **This ticket now compiles against a client that
     has the field** — which was the entire block.
  **The hold clause on T-0446 ("T-0447/0448/0449 are HELD until the owner confirms this bundle") is
  therefore discharged for this ticket.** This is the **only** one of the three avatar client tickets
  that is genuinely `ready` — T-0448 and T-0449 remain blocked on **T-0450**, not on T-0446.
- 2026-08-01 — **DoR check, with the one gap named rather than hidden.** AC observable ✅ · sized M ✅ ·
  deps `done` ✅ · `manual_steps: []` and the regen is already run ✅ · `security_touching: true` +
  `layers: [frontend]` ✅ · archetype identified ✅ (the binding security-conditions block, the QA
  constraints block, and `agents/knowledge/patterns-frontend.md` §"Building a generated DTO —
  construct-then-assign, never an object literal", which is the shape the regen keeps breaking).
  **The gap:** the story `US-user-avatar` **does not exist as an artifact** — PM-verified, a search of
  `agents/archive/2026-08/backlog/stories/` and `agents/analysts/` returns nothing, and T-0446's own log records that
  "no ADR / panel record existed in the tree at implementation time". This ticket is going `ready`
  **without** that panel, on the ground that it introduces **no new decision**: the read-path option
  was chosen and has now shipped, the security conditions below are already adjudicated and binding,
  and QA has already executed the constraint that would otherwise have been the open design question
  (the CORS finding — any crop-or-canvas design that reads the stored avatar is dead client-side).
  **Stated explicitly so it can be overruled**: if the orchestrator wants the analyst panel, convene
  it before dispatch rather than after.
- 2026-08-01 — **read the QA constraints block below before writing a line.** The two that will
  otherwise cost a rewrite: (a) **CORS** — `storage.bicep` has no `cors` block, so `fetch()`,
  `crossorigin="anonymous"`, canvas `getImageData` and `HttpClient.get(blobUrl, {responseType:'blob'})`
  all fail against the stored blob; only a plain `[src]` binding works, and a **pre-upload preview of
  the locally picked file is still fine**. (b) **an `<img>` cannot see a 403 vs a 404** — on any image
  error, re-fetch the profile once, with a single-retry guard.

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

- 2026-08-01 — **implemented** (frontend, branch `feat/T-0447-web-avatar` → PR #184).
  Four files under `libs/cleansia-customer-features/profile/src/lib/profile/`: the new
  `profile.models.ts` (validation + the `AvatarIntent` union + the construct-then-assign
  `buildUpdateCurrentUserCommand`, per ADR-0031), `profile.facade.ts` (all logic — signals,
  the generated-client calls, the file→`BlobFileDto` read), and the component/template as pure
  delegation. `BlobFileDto`/`IBlobFileDto` added to the hand-maintained
  `libs/core/customer-services/src/index.ts` barrel (a barrel re-export, **not** a regen).
  Nine `pages.profile.avatar.*` keys in all five customer locales; no new `api.*` key was needed —
  the backend's `file.content_type_doesnt_match` is already in the contract and in all five bundles.
  - **CORS/C2 honoured:** the SAS is bound straight into `[src]` in both places it renders; nothing
    reads the stored blob back. No cropper, no canvas, no `fetch`. The only local read is
    `FileReader.readAsDataURL` on the **newly picked file**, which never touches the blob URL.
  - **C1 honoured:** `onAvatarLoadFailed()` re-reads the profile **once** (no status branching —
    an `<img>` cannot see 403 vs 404); a second failure falls back to the initials. `(load)` re-arms
    the guard so a later SAS expiry in a long-lived session can still recover.
  - **Cache key = `fileName`:** `applyAvatar` holds the rendered URL steady while the blob name is
    unchanged, so a re-read's fresh signature does not re-download the image. T-0446 AC10 (fresh
    blob name per replace) verified present at `UpdateCurrentUser.cs:148-152`, so no eviction is
    needed. No render-mode change (profile stays under `**` → `RenderMode.Client`), and nothing logs
    the profile response or the SAS.
  - **AC5 limits, read not invented:** `UpdateCurrentUser` uses `ImageFileValidator`
    (`UpdateCurrentUser.cs:61-63`), which enforces **type only**, by magic bytes
    (`Constants.ImageSignatures:88-97` → jpeg/png/gif/bmp/tiff/webp) and **no size cap**. The client
    mirrors that type list and applies the platform's existing 10 MB convention
    (`FileValidator.MaxFileSizeInMB`, `order-photos.helpers.ts` `PHOTO_MAX_SIZE`,
    `CleansiaFileComponent.maxFileSize`) — flagged here because it is a client-only floor.
  - **Gate 8:** `nx test cleansia-customer-profile` 53/53 (4 suites); `nx affected -t test
    --base=95debd57` **19 projects green**, incl. `error-contract-parity.spec.ts` 5/5; all three
    production builds exit 0; `check-consistency.mjs` OK (37 files).
    `nx lint`: profile lib **0 errors, 2 warnings**, both pre-existing and on untouched lines —
    baselined by running the same lint in a clean worktree at `95debd57`, which showed **3**
    warnings, the third being the `as any` this ticket deletes. `cleansia.app` (5 errors) and
    `customer-services` (3 errors, the customer twin of T-0455's cycle) are **byte-identical to the
    baseline** and touch no file in this diff.
  - **Gate 0.5 leg 1 / Gate 6.5 — mutation-proved, restored byte-exact:**
    `command.removePhoto = intent.kind === 'remove'` → `= true` turns **5 tests RED**, named:
    `buildUpdateCurrentUserCommand › sends neither a photo nor a removal for an unchanged avatar`,
    `… › serializes an unchanged-avatar save without a photo or a removal flag`,
    `… › sends the photo and no removal for an upload`,
    `ProfileFacade › uploading an avatar › sends the picked image and never a removal`, and the AC4
    case `ProfileFacade › saving the profile details › sends no photo and no removal, so an existing
    avatar survives` (5 failed / 0 after restore). The cache-key branch and the retry guard were
    each mutated separately: 1 failed each
    (`… › keeps the rendered url when a re-read returns the same file with a fresh signature`,
    `… › falls back to the placeholder instead of re-reading a second time`), 0 after restore.
  - **BLOCKER for AC2/AC3 manual evidence — `Q-PROFILE-01` filed in `questions/open.md`.**
    `UpdateCurrentUser.Validator.AllowedToUpdateUser` (`UpdateCurrentUser.cs:33-36, 66-71`) requires
    a client-supplied `Command.Id` equal to the session user's id, the customer controller does not
    stamp it (`UserController.cs:28-38`), and **the customer web app has no id to send**:
    `MyProfileDto` carries none (`UserMappers.cs:44`) and the web session is an HttpOnly cookie, so
    JS cannot read the JWT — which is exactly where Android (`UserRepository.kt:82-86`) and iOS
    (`UserProfileClient.swift:55`) get it. Every customer-web profile save therefore 400s with
    `user.not_allowed_to_update`. **Pre-existing** (`id: undefined` since `29de7b48`, 2026-05-16) and
    **not fixable from the frontend**; this ticket sends the same `id` it always did, so it
    regresses nothing, but the AC2/AC3 round-trip cannot be demonstrated until the backend is fixed.
  - **Dead-code report the ticket asked for:** the partner-store `updateUserCurrent`
    action/reducer/effect chain (`libs/data-access/partner-stores/src/lib/user/user.effects.ts:80-119`)
    is **still dead** — re-verified on this branch, no dispatcher anywhere. Note `partner-stores` has
    **no `test` target at all**, so anything landing there is untested by construction.
  - **Not verified by me:** the three screenshot ACs (AC1 both states, AC2, AC3) and the AC3 manual
    round-trip — no running app/DEV session here, and AC2/AC3 are blocked by `Q-PROFILE-01` regardless.

- 2026-08-05 — **re-verified against the two backend changes that landed after implementation, plus
  one client-side narrowing.** Nothing in the shipped design had to be undone.
  - **`Q-PROFILE-01` is RESOLVED, as shape (a), and the AC2/AC3 round-trip is unblocked.**
    `85c453f1` made the caller's identity server-truth: `AllowedToUpdateUser` no longer compares
    `Command.Id` (`UpdateCurrentUser.cs:75-83`) and the handler resolves the row from
    `IUserSessionProvider`. `Command.Id` stays on the wire as a nullable, never-read no-op for the
    mobile clients and carries the `[OWN-DATA] (S1)` annotation. The web client sends no id, which is
    now correct rather than fatal. Recorded under the question; **the manual round-trip is now
    executable and still owed by whoever has a DEV session.**
  - **AC4 does not regress, and the backend now agrees in the same shape.** `UpdateProfilePhoto`
    (`UpdateCurrentUser.cs:148-157`) returns early when there is neither a photo nor `RemovePhoto`,
    and `UpdateUserAndOrders` states the general rule — *"Every optional field here means 'nothing to
    say about it', never 'delete it'"*. The client's half is `command.removePhoto = intent.kind ===
    'remove'` with `photo` assigned only on an upload, still mutation-proved (5 tests red on
    `= true`).
  - **The content-type column: my implementation does NOT need it.** The avatar renders through a
    plain `<img [src]>` and browsers sniff image bytes regardless of `application/octet-stream`
    (which is what `ServedContentType.Opaque` serves and what T-0464 proved against Azurite). Nothing
    in this feature fetches, canvases or downloads the blob — CORS/C2 forbids all of that anyway. So
    the column is a nice-to-have here, **not a blocker**. Two things worth the owner knowing while
    the reseed window is open: (1) the avatar blob name is a bare GUID with **no extension**, so
    `ServedContentType.ForFileName` can never rescue it either — without a recorded type the avatar
    stays permanently opaque, which costs a correct `Content-Disposition`/save-as and any future
    non-`<img>` read; (2) if the column is added, **derive its value from the magic bytes
    `ImageFileValidator` already sniffs (`Constants.ImageSignatures:95-104`), never from
    `command.Photo.ContentType`** — that member is client-declared and is exactly the input
    `ServedContentType` exists to distrust. The client keeps sending `contentType` on `BlobFileDto`
    (the backend ignores it for the avatar today) purely for parity with the order-photo DTO.
  - **AC5 allowlist narrowed, and this is a real defect fixed, not tidying.** The client list was
    the backend's magic-byte set, which includes **bmp and tiff**. `ServedContentType.ServableTypes`
    will only ever serve jpeg/png/webp/gif as an image, and **no desktop browser renders a tiff in an
    `<img>`** — so a tiff avatar uploaded successfully, then never appeared, and the C1 single-retry
    guard burned its one re-read before falling back to initials. `AVATAR_ALLOWED_CONTENT_TYPES` is
    now the intersection (jpeg/jpg/png/webp/gif); the backend still accepts everything the client can
    send, so nothing new can 400. Pinned by three added cases in `profile.models.spec.ts`, including
    an explicit svg rejection.
  - **T-0465 (avatar not cached) — the client half is done and the remaining half is not ours.**
    Cause 1 is fixed upstream: `BlobContainerClient.GenerateSasUri` now sets
    `CacheControl = "private, max-age=3600"` on every mint (`private`, per the binding condition).
    Cause 2 — the SAS query changes per read, so the HTTP cache key changes — is inherent to the
    per-read-SAS design and is **not fixable from the web**: `<img [src]>` is the only sanctioned
    read (C2) and the URL is the cache key. `applyAvatar` already holds the rendered URL steady while
    `fileName` is unchanged, so within a session a re-read (save, avatar change, error retry) does
    **not** re-download. Across page loads it still will. That is option **A** in T-0465 and should
    be recorded there as accepted; option **B** (bucketed expiry) remains a backend + security
    re-gate call.
  - **Gate 8 re-run at this commit:** `nx test cleansia-customer-profile` **67/67** (5 suites); all
    three production builds exit 0; `npm run typecheck` OK 3/3; `nx run-many -t lint --all` failing
    set **byte-identical to the 24-project baseline**.
  - Unchanged and re-checked: no render-mode change (profile stays `RenderMode.Client`), no logging
    of the profile response or the SAS, cache key is `fileName`, T-0446 AC10 still mints a fresh blob
    name per replace.
  - **Heads-up for the reviewer, from a live parallel lane — the AC5 size caveat is about to
    disappear.** A backend agent has `ImageFileValidator.cs` + a new `BlobFileSize.cs` open in the
    working tree adding `Must(BlobFileSize.HasContentWithinLimit)` with `MaxFileSizeInMB = 10`.
    That is **the same 10 MB this client already enforces**, so the note above ("a client-only
    floor") retires the moment it lands and the two sides agree. They also agree numerically: the
    server derives the size from the **encoded** base64 length (`len * 3 / 4`, rounding up by ≤ 2
    bytes) while the client measures `file.size` (decoded), so a file at exactly the cap passes both.
    Its error is `BusinessErrorMessage.FileSizeExceeded` → `api.file.size_exceeded`, which is
    **already present and non-empty in all five customer locales** (verified) — so no i18n work is
    owed here and the interceptor will not fall back to the generic message. I did not touch that
    lane's files.
  - **Still not verified by me:** AC1/AC2/AC3 screenshots and the manual round-trip — no running app
    here. The `Q-PROFILE-01` obstacle to them is gone.

## Review
<!-- reviewer + security verdicts here -->
