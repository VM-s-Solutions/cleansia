---
id: T-0448
title: Android — avatar upload, render and removal on the customer profile
status: ready
size: M
owner: android
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0446, T-0450]
blocks: [T-0453]
stories: [US-user-avatar]
adrs: []
layers: [android]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Part of the owner-approved avatar feature (batch item 5). Android is the platform that already has the
**placeholder wired to nothing** — verified 2026-07-30:

- `customer-app/.../features/profile/EditProfileScreen.kt:230` —
  `.clickable { /* TODO: launch photo picker */ }` — the camera pill is tappable and does nothing.
- `EditProfileScreen.kt:193-215` — `AvatarPreview(initials: String)` takes **only** initials; there is
  no image parameter and no image loader on this path.
- `customer-app/.../features/profile/ProfileTab.kt:262-278` — the hero avatar is likewise an
  initials-only `Box`.
- No removal affordance anywhere, and `removePhoto` is present on the customer mobile spec
  (`src/cleansia_android/openapi/customer-mobile-api.json` → `UpdateCurrentUser_Command`).

**Held** until T-0446 lands *and* the owner confirms the `mobile-spec-redump` bundle — the Kotlin
client is generated from that committed spec at build time, so until it is re-dumped the read field
does not exist.

**Also serialized behind T-0442 and T-0441** (see the lane note below) — both edit files this ticket
edits.

## Acceptance criteria

_(PM floor; the `US-user-avatar` analyst panel finalizes)_

- [ ] **AC1** — Given a signed-in customer with a photo, When the profile tab renders, Then the hero
      shows the image; with no photo, Then the existing initials circle. The hero **layout must be
      byte-equivalent to what T-0442 shipped** — dropping an image in must not re-lay-out the header.
      Evidence: screenshots of both states + a diff showing the hero geometry unchanged.
- [ ] **AC2** — Given the edit-profile screen, When the user taps the camera pill
      (`EditProfileScreen.kt:230`), Then a photo picker opens, the selection previews immediately, and
      saving uploads it. The `/* TODO */` is gone. Evidence: screenshots + a ViewModel unit test.
- [ ] **AC3** — Given a user **with** a photo, When they choose remove, Then `removePhoto = true` is
      sent and the avatar reverts to initials. Evidence: a ViewModel test asserting the flag.
- [ ] **AC4** — Given a normal profile save with no photo action, When it is submitted, Then no photo
      is sent **and** `removePhoto` is false, so the avatar survives (the `fe0c985b` regression).
      **Mutation-prove it**: the test must go RED if the code sends `removePhoto = true`
      unconditionally. Name that test in the verdict.
- [ ] **AC5** — Given the picked image, When it is encoded for upload, Then it is downscaled/compressed
      to a sane bound before base64 — a modern phone camera JPEG is multi-megabyte and base64 adds
      ~33%. State the bound chosen and why; confirm it against whatever the backend accepts (read
      `Features/Users/UpdateCurrentUser.cs:61-63`, do not invent a limit).
- [ ] **AC6** — Runtime permissions handled for the picker on the supported API range, with a graceful
      denial path (no crash, a translated message). Evidence: the denial path screenshotted.
- [ ] **AC7 (Gate 8)** — `:core` + `:customer-app` `compileDebugKotlin` + `testDebugUnitTest` succeed
      and the run is **not `UP-TO-DATE`**. Kotlin diff byte-clean (no BOM/mojibake), especially the 5
      `strings.xml`.

## Out of scope

- The **partner** Android app's avatar (it has its own document pipeline; different surface).
- iOS / web — T-0449 / T-0447.
- Cropping/rotation UI.
- Changing the hero layout — T-0442 owns that and lands first.

## Implementation notes

- **Shared-file lane — this ticket is third in a serialized lane and must not start early:**
  - `customer-app/.../features/profile/ProfileTab.kt` — **T-0442** writes it first (hero parity).
  - `customer-app/src/main/res/values*/strings.xml` (5 files) — **T-0441** writes them first
    (booking access-instructions hint).
  Edit only your own hunks; never `git restore` a shared file — report contamination to the PM instead.
- The image loader: check whether `:core` already depends on Coil (or equivalent) before adding a
  dependency; a new image library is a Gate 5 (bundle/cost) item.
- The **working reference** for multipart/blob upload in this codebase is the partner app's document
  and order-photo pipeline — copy it rather than invent one.
- Read `agents/knowledge/patterns-mobile.md` first.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 5, Android client)
- 2026-07-30 — blocked (on T-0446 + the owner's mobile-spec-redump; serialized behind T-0442 and T-0441; awaiting the US-user-avatar panel)

- 2026-07-30 — **re-prioritised, still blocked.** Owner ruling: the avatar is demo scope. This is now
  the Android leg of the demo critical path behind T-0446 + the owner's `mobile-spec-redump`.
- 2026-07-30 — **dependencies updated.** `T-0442` **removed** (merged as `ce2416a0`, so the
  `ProfileTab.kt` lane head is clear). **`T-0450` added** — it writes the same
  `values-{ru,uk}/strings.xml` and the same hero name style, and it must go first because it changes
  what the hero renders in ru/uk. Full `ProfileTab.kt` lane: T-0442 (done) → **T-0450 → T-0448 →
  T-0453**.
- 2026-07-30 — **now blocks T-0453** (Android hero edge-to-edge). T-0453 was deliberately sequenced
  *behind* this ticket so a non-demo restructure cannot be inserted in front of the demo path, and so
  the restructure happens against the final hero rather than the initials placeholder.
  **Standing constraint from T-0442 still applies:** the avatar `Box` (`ProfileTab.kt:272-286`) was
  left shaped so an image drops in without re-laying-out the hero — do not re-lay it out here.

- 2026-07-30 — **security conditions attached** from the T-0446 gate (APPROVE-WITH-CONDITIONS). See
  the block below; they are binding on this ticket and its reviewer. Source:
  `agents/backlog/security/user-profile-avatar.md`. Still `blocked` — dependencies unchanged.
- 2026-08-01 — **RE-CHECKED against the merged tree; STAYS `blocked`, but the blocker has changed
  shape and shrunk to ONE item.** Each dependency taken separately, PM-verified on `master` at
  `1c8fdd00`:

  | `depends_on` | State | Effect on this ticket |
  |---|---|---|
  | **T-0446** | **`done`** — merged `a63b776e` (#176) | cleared |
  | *the owner's `mobile-spec-redump`* | **DONE, and it shipped inside `a63b776e`** — `src/cleansia_android/openapi/customer-mobile-api.json` carries `blobUrl` (4 hits), `partner-mobile-api.json` 6. The Kotlin client generates from that committed spec at Gradle build time, so **this ticket can see the field with no owner action** | cleared |
  | **T-0441** | **`qa`, code MERGED** `1d85b35f` (#178) | **cleared in substance.** The dependency was the `values*/strings.xml` lane head plus the field itself; both are on `master`. What T-0441 still owes is an **AC1 screenshot**, which does not gate a code lane. Do not wait for it |
  | **T-0450** | **`draft`** — and blocked on **Q-I18N-02**, an unanswered `blocking: yes` owner question | **THE ONLY REMAINING BLOCKER** |

  **So: `blocked` on T-0450 alone, and T-0450 is blocked on the owner.** It writes the same
  `values-{ru,uk}/strings.xml` and changes what the hero renders in ru/uk, so it must precede this
  ticket on both lanes. Full lane unchanged: T-0442 ✅ → **T-0450 → T-0448 → T-0453**.
  **Do not "unblock" this by dropping the T-0450 dependency** — that would put an Android avatar into
  a hero whose ru/uk label is about to change underneath it, on the same five files.
- 2026-08-01 — **Q-I18N-02 IS ANSWERED. T-0450 is `ready`. This ticket stays `blocked`, on T-0450
  alone, and here is exactly what changed and what did not.**

  | | Before | Now |
  |---|---|---|
  | **Q-I18N-02** | `blocking: yes`, unanswered, no PM default | **ANSWERED** — verb-only label (`Edit`/`Редактировать`) + truncate, don't wrap |
  | **T-0450** | `draft`, blocked on the owner, size `M`, two defects | **`ready`**, size `S`, **half (A) only** — the label |
  | **the Poppins half** | inside T-0450, needing an architect panel + the unanswered Q-BRAND-01 | **split out to `T-0472`** — and **T-0472 does NOT block this ticket** |
  | **this ticket** | `blocked` on T-0450, which was blocked on the owner | `blocked` on T-0450, which is **dispatchable today** |

  **The blocker is no longer an owner reply — it is one `ready` ticket's write landing.** T-0450 changes
  `values-{ru,uk}/strings.xml`, which this ticket also writes, and changes what the hero chip renders in
  ru/uk. It must precede this ticket on that lane. **Do not "unblock" by dropping the T-0450 dep** — that
  would put an Android avatar into a hero whose ru/uk label is about to change underneath it, on the
  same five files.

  **⚠️ New lane neighbour: `T-0472`.** If T-0472's architect ruling touches the hard-coded Poppins call
  sites it writes `ProfileTab.kt:437` and `EditProfileScreen.kt:215` — both inside this ticket's files
  (`EditProfileScreen.kt:230` is this ticket's photo-picker TODO). **T-0472 is sequenced LAST**, after
  this ticket. Do not run them concurrently.
- 2026-08-01 — **`T-0441` REMOVED from `depends_on` — a discharge, not a drop.** It was a **lane**
  dependency (the `values*/strings.xml` head) and its write is on `master` (`1d85b35f` #178). T-0441
  itself is still `qa` on an owed AC1 **screenshot**, and *a screenshot does not gate a code lane* — the
  ruling already recorded at `status/sprint-14.md` §8.2. Leaving it listed would have made this ticket
  un-`ready` on a screenshot forever. **T-0446 stays listed and is `done`.** Full remaining lane:
  T-0442 ✅ → **T-0450 → T-0448 → T-0453** (then T-0472).
- 2026-08-01 — **implemented (android)** on `feat/T-0448-android-avatar` off
  `docs/sprint-14-owner-rulings`. `EditProfileScreen.kt`'s dead `/* TODO: launch photo picker */` is
  gone. AC1–AC5 + AC7 have executed evidence; **AC6 has no denial path to screenshot** (the picker
  needs no permission on 26–35); **C2's device run is still outstanding** — see the verdict below,
  which names exactly which leg of the chain is unproven.
- 2026-08-01 — **when it does run, AC evidence must be a DEVICE RUN, not an inference from the other
  two platforms.** QA could execute Chromium, WebKit and `CGImageSource`; it could **not** execute
  Android (no emulator, and `BitmapFactory` is not exercisable on the JVM). The static trace found no
  `image/*` MIME literal anywhere in Coil 3.0.4, so there is no MIME gate to fail — **but that is a
  trace, not a run.** See the QA constraints block.

## Security conditions — BINDING (from the T-0446 gate, 2026-07-30)

These are not advisory. The reviewer checks them, and a diff that violates one does not pass Gate 3.

- [ ] **Do NOT raise OkHttp logging to `Level.BODY`.** Both apps are `HEADERS` in debug and `NONE` in
      release today, and that must not change. `BlobUrl` is a **live credential** (a read SAS valid
      for one hour); `Level.BODY` would write the complete signed URL — `sig=` and all — into logcat,
      where any app with log access on a debug build can read it. This is a tempting change while
      debugging an image that will not load. **Do not make it**; add a targeted log of the
      **`fileName`** instead, never the URL.
- [ ] **Cache on `fileName`, never on `blobUrl`.** The URL **changes on every fetch** — minted per
      request with a fresh expiry — so Coil would treat every profile read as a cache miss and
      re-download the image. Use `fileName` as the Coil `memoryCacheKey`/`diskCacheKey` and let the
      URL be the fetch target only.
      **Note the change since this condition was first written:** T-0446 **AC10** now mints a fresh
      blob name on replace, so `fileName` changes when the user uploads a new photo — which is
      precisely what makes "cache on `fileName`" safe. The per-client eviction workaround that would
      otherwise have been required is **no longer needed**, but **verify AC10 actually shipped**
      before relying on it. Without AC10, caching on `fileName` renders the **stale** avatar forever.
- [ ] **S11 — the avatar is per-user state.** Any new `@Singleton` holding the decoded avatar, its
      URL, or a Coil cache key **joins the session-wipe set** (`SessionScopedCache` +
      `@Binds @IntoSet` in the app's `SessionScopedModule`) — or the previous user's face survives to
      the next account on a shared handset. If you instead reuse an existing profile holder, confirm
      **it** is already in the set. Also consider whether Coil's own disk cache needs clearing on
      session end; if you decide it does not, **write down why**.
- [ ] **Do not persist the `blobUrl` to DataStore or any on-disk cache.** It expires within the hour,
      so it is useless there — and it is a credential at rest.

## ⚠️ QA constraints — added 2026-07-30, READ BEFORE STARTING

### C1 — Image-error handling: re-fetch once, do NOT branch on the status code

**Record correction:** a "403 means expiry, 404 means deleted" rule was reported as having been
written into this ticket. **It never was** — there is nothing to reword; this is the first version.

The codes are real (QA confirmed 403 on expiry / tampered `sig` / no SAS, 404 on a deleted blob), but
an image loader does not reliably surface them, and building an AC on a distinction the client cannot
observe produces a client that cannot satisfy its own acceptance criteria.

- [ ] **On ANY Coil load error, re-fetch the profile once**, with a **single-retry guard** so a
      genuinely deleted blob falls back to the initials placeholder rather than looping.

### C2 — This ticket MUST carry a real device/emulator run

**Android is the one platform QA could not execute.** No emulator was available, and `BitmapFactory`
is not exercisable on the JVM. QA's static trace found **no `image/*` MIME literal anywhere in Coil
3.0.4** — so there is no MIME gate that could reject an `application/octet-stream` avatar — **but
that is a trace, not a run.**

- [ ] **AC (new, blocking): the avatar renders on a real device or emulator**, loaded from the live
      SAS URL, and the result is recorded as executed evidence (screenshot or logcat, plus the device
      /API level). **A passing unit test is not evidence for this AC.** The web and iOS equivalents
      were executed against real engines; this is the leg that is missing, and it must not be closed
      by inference from the other two.

## Implementation — the surface iOS (T-0449) must reproduce 1:1

Built on `feat/T-0448-android-avatar` off `docs/sprint-14-owner-rulings`.

### States (the ViewModel holds all of it)

`ProfileViewModel` gains `avatarDraft: StateFlow<AvatarDraft>` and `avatarState: StateFlow<ActionState>`.
`AvatarDraft` is a **three-case sealed interface**, not a nullable image, because "no image" is
ambiguous between *leave it alone* and *delete it* and the server treats those very differently
(`UpdateCurrentUser.cs:135`):

| case | preview shows | on save |
|---|---|---|
| `Unchanged` | the saved `avatarUrl`, else initials | `photo = null`, `removePhoto = false` |
| `Picked(previewUri, image)` | the local content URI, immediately | `photo = <compressed>`, `removePhoto = false` |
| `Removed` | initials, immediately | `photo = null`, `removePhoto = true` |

`avatarState` is `Idle`/`Submitting` around the decode+downscale only (single-flight guarded; the
second tap of a double-tap is dropped). A failed save **keeps** the draft so the user can retry; a
successful save resets it to `Unchanged`. Leaving the edit screen disposes the draft
(`DisposableEffect` in `CleansiaNavHost`), so an abandoned pick never leaks into the hero.

### API calls

- Read: `GET /api/User/GetCurrent` → `MyProfileDto.profilePhoto` → **two** new `CurrentUser` fields,
  `avatarFileName` (the cache key) and `avatarUrl` (the fetch target).
- Write: `PUT /api/User/UpdateCurrentUser` — `photo: BlobFileDto{fileName, base64Content, contentType}`
  and `removePhoto: Boolean`. Both flow through the existing `updateCurrentUser`, which already
  re-fetches on success, so a replacement's new blob name and fresh SAS arrive with no extra call and
  no cache eviction.

### Navigation

None added. The picker is an `ActivityResult` launcher inside the screen; removal is a
`ModalBottomSheet` with two rows. No new routes, no VM-driven navigation.

**One instance detail iOS must match:** the shell and the edit route each get their **own**
`ProfileViewModel` (`hiltViewModel()` scopes per back-stack entry), so the draft lives only on the
edit screen's instance while the hero reads the saved state. The hero still updates after a save
because `updateCurrentUser` re-fetches into the **singleton** `UserRepository.currentUser` that both
observe — the repository, not the VM, is the shared source. An iOS port that puts the draft on a
shared observable object will show a pending pick in the hero before it is uploaded.

### Where the code is

- `features/profile/ProfileAvatar.kt` (new) — `AvatarPhoto` (`Remote(url, fileName)` / `Local(uri)`),
  the pure `avatarPhotoFor(user, draft)` mapper, and `ProfileAvatarContent` (the disc **contents**,
  so both call sites keep their existing geometry).
- `features/profile/ProfileViewModel.kt` — `pickAvatar` / `removeAvatar` / `discardAvatarDraft` /
  `onAvatarLoadFailed` / `onAvatarLoadSucceeded`, and the save-path threading.
- `core/user/UserRepository.kt` + `UserDto.kt` — the wire mapping both directions.
- `features/profile/EditProfileScreen.kt` — picker, options sheet, `AvatarPreview` (the `TODO` is gone).
- `features/profile/ProfileTab.kt` — the hero renders the image.

## Verdict — 2026-08-01 (android)

### AC status

- **AC1 — hero renders the image, geometry unchanged.** Code-verified: `ProfileTab.kt`'s avatar `Box`
  (`.size(72.dp)` + white fill + 3dp border) is **byte-identical**; only its *child* changed from a
  bare `Text` to `ProfileAvatarContent`, which fills the parent and never sizes itself. `git diff`
  shows no modifier line touched. **Screenshots NOT produced — see "could not verify".**
- **AC2 — the camera pill opens a picker, previews, and saving uploads.** Done; `EditProfileScreen.kt`
  line 230's `/* TODO: launch photo picker */` is gone. Tests:
  `pickAvatar compresses and holds the result as a preview draft`,
  `pickAvatar uploads nothing on its own`,
  `saveProfile sends the picked image and does not ask for removal`, and the wire-level
  `updateCurrentUser_givenAPickedPhoto_putsTheEncodedImageOnTheGeneratedCommand`.
- **AC3 — removal sends `removePhoto = true`.** `removeAvatar makes the next save ask for removal
  without an image` (VM) + `updateCurrentUser_givenRemoval_putsRemovePhotoTrueAndNoImageOnTheGeneratedCommand`
  (wire).
- **AC4 — a normal save touches nothing, mutation-proven.** The named test is
  **`updateCurrentUser_givenNoPhotoAction_sendsNoImageAndRemovePhotoFalse`**
  (`UserRepositoryTest`), with **`saveProfile with no photo action sends no image and does not ask for
  removal`** (`ProfileViewModelTest`) covering the VM half. Both mutations recorded below.
- **AC5 — bound + backend limit.** Reused `:core` `ImageCompressor`: **longest side ≤ 1920px, JPEG
  q70**, giving roughly 200–400 KB → ~270–530 KB base64. Read against the backend, not invented:
  `UpdateCurrentUser.cs:61-63` runs **`ImageFileValidator`**, which checks **magic bytes only** and
  has **no size cap** (`Constants.ImageSignatures` includes JPEG `FF D8 FF`, so our output passes);
  the 10 MB cap lives in the *other* validator, `FileValidator.cs`, which this command does not use.
  So the bound is a client-side courtesy against Kestrel's 30 MB body default plus the ~33% base64
  tax, and 1920/q70 sits three orders of magnitude inside it. Choosing it also means one compression
  path in the app rather than a rival avatar-specific one.
- **AC6 — permissions.** `ActivityResultContracts.PickVisualMedia` needs **no runtime permission on
  the whole 26–35 range** — it grants per-item access, and androidx falls back through the
  Play-services picker to `ACTION_OPEN_DOCUMENT`, also permission-free. So there is no denial dialog
  to screenshot: **the graceful path is that the permission never appears.** The residual failure is
  `launch()` throwing where no picker and no document provider exist; that is caught and shown as
  `profile_avatar_picker_unavailable_*` in five locales. **The dialog was not screenshotted** (no
  device — see below).
- **AC7 — Gate 8.** Green, un-cached, and **not** `UP-TO-DATE`; numbers below.

### Security conditions (from the T-0446 gate)

- **No `Level.BODY`.** Untouched — `AuthModule.kt:75-76` still reads `HEADERS` in debug / `NONE` in
  release. No log statement was added at all, not even of the `fileName`.
- **Cache on `fileName`.** `ProfileAvatarContent` sets `memoryCacheKey(fileName)`; the URL is only
  `.data(...)`. **AC10 verified shipped** before relying on it — `UpdateCurrentUser.cs:149` mints
  `Guid.NewGuid()` per upload and deletes the superseded blob after, so the name is content-addressed
  and no eviction is needed on save.
- **Coil disk cache — decided, with the reason written down.** Set to
  `CachePolicy.DISABLED` for the avatar rather than left enabled and cleared on sign-out. The bytes
  are a photograph of the signed-in user; disabling removes the "when does the previous user's face
  leave this handset" question instead of answering it, and costs one ~300 KB refetch per cold start
  on a screen visited rarely. Note this is *stricter* than the condition asked for.
- **S11.** No new `@Singleton`. The avatar lives on `CurrentUser` inside `UserRepository._currentUser`,
  which is already a `SessionScopedCache` member (`SessionScopedModule.bindUserRepository`) and whose
  `clear()` nulls the snapshot — so sign-out, forced-401 and account-deletion all wipe it. Coil's
  memory cache is keyed on an unguessable per-upload GUID and dies with the process; nothing is on
  disk.
- **`blobUrl` never persisted.** It exists only in the in-memory `CurrentUser`; no DataStore, no disk
  cache.

### C1 — image-error handling

Implemented as specified, with no status-code branching (the loader cannot see one).
`onAvatarLoadFailed()` refetches the profile **once per blob name**; the composable falls back to the
initials meanwhile, so a deleted blob settles on the placeholder instead of looping. A *successful*
load hands the budget back, so a session that outlives more than one SAS can still recover. Four tests:
`an avatar load failure refetches the profile for a fresh SAS`,
`repeated failures on the same photo refetch only once`,
`a different photo gets its own retry budget`,
`a successful load restores the retry budget`.

### Gate 0.5 — verification

Command (`--offline` added; every dependency was already in the Gradle cache, and it removes network
flakiness — no other flag changed):

```
./gradlew :core:compileDebugKotlin :customer-app:compileDebugKotlin \
          :core:testDebugUnitTest :customer-app:testDebugUnitTest \
          --rerun-tasks --no-build-cache --console=plain --no-daemon --offline
```

`BUILD SUCCESSFUL`, **61 actionable tasks: 61 executed** (0 up-to-date, 0 from cache).
`:core` **141 tests / 0 failures** (20 classes) · `:customer-app` **351 tests / 0 failures** (36 classes).
New/extended: `ProfileViewModelTest` 29 · `UserRepositoryTest` 19 · `ProfileAvatarTest` 5 ·
`ProfileAvatarStringsTest` 2.

**Mutations — three, each restored and confirmed byte-exact (`git diff --numstat` back to
additions-only, no residue):**

1. **Deleted `removePhoto = removePhoto,`** from the generated command in `UserRepository`.
   → `48 tests completed, 3 failed`: `updateCurrentUser_givenRemoval_…`,
   `updateCurrentUser_givenNoPhotoAction_sendsNoImageAndRemovePhotoFalse`,
   `updateCurrentUser_givenAPickedPhoto_…`. **All 29 `ProfileViewModelTest` cases stayed green** —
   the exact shape the ticket warned about.
2. **`removePhoto = true` unconditionally, at the wire (AC4's stated mutation).**
   → `19 tests completed, 2 failed`: `updateCurrentUser_givenNoPhotoAction_sendsNoImageAndRemovePhotoFalse`,
   `updateCurrentUser_givenAPickedPhoto_…`.
   And **the same mutation applied in the ViewModel** → `29 tests completed, 2 failed`:
   `saveProfile with no photo action sends no image and does not ask for removal`,
   `saveProfile sends the picked image and does not ask for removal`.
3. **Deleted the whole `photo = photo?.let { WireBlobFile(…) }` block.**
   → `48 tests completed, 1 failed`: `updateCurrentUser_givenAPickedPhoto_…` — and, again, the entire
   ViewModel suite stayed green. This is the proof that the wire assertion, not the VM assertion, is
   the one carrying AC2/AC5.

### What I could NOT verify — read this before closing

- **C2's blocking AC is NOT satisfied. There was no device or emulator run.** No avatar was ever
  rendered from a live SAS. Every claim about *rendering* — that Coil accepts the blob's content type,
  that the disc crops correctly, that the crossfade looks right, that the hero is visually unchanged —
  is **static analysis, not execution.** The unit tests do not touch it.
- **`ImageCompressor` never executed here.** `BitmapFactory` needs an Android runtime; the compressor
  is mocked in every VM test. So *"a real camera JPEG decodes, downscales, loses its GPS block and
  produces bytes the backend's magic-byte validator accepts"* is **unproven on this branch.** The
  pipeline is shared with the shipped partner order-photo and dispute-evidence paths, and `:core`'s
  `ImageCompressorMathTest` covers the pure sizing/orientation maths, but neither is a run of *this*
  path.
- **No Compose UI test.** `customer-app` has **no `androidTest` source set**, so nothing executes the
  picker launch, the options sheet, the busy spinner, the picker-unavailable dialog, or the
  tap-to-open wiring. Every one of those is verified by reading the code only. The logic they drive is
  in the ViewModel and is tested; the wiring between them is not.
- **No screenshots** for AC1, AC2 or AC6 — same cause.
- **The end-to-end round trip was never exercised**: no request was sent to a live
  `UpdateCurrentUser`, so the 200 path, the validator's response to our JPEG, and the SAS that comes
  back on the next `GetCurrent` are all inferred from the backend source, not observed.

Concretely, for the owner tapping the button: the **pick → compress → hold → send** chain is proven
to the wire by executed tests. The **render** chain (SAS → Coil → pixels on the disc) is proven only
by reading. If one leg of this feature fails on device, that is the leg.

### Notes for the reviewer

- **Picker contract choice.** `PickVisualMedia` rather than the codebase's existing `GetContent`. The
  two existing customer/partner sites are *mixed-content* (both accept PDFs) so they need `GetContent`
  and the `isImageMimeType` split; `PhotosSection.kt` is image-only `GetContent` predating this.
  `PickVisualMedia` is Google's recommendation for image-only and is what makes AC6's answer "no
  permission exists on 26–35" rather than "we handle the denial". Recorded, not silently introduced —
  see the harvest below. **No existing call site was migrated.**
- **No new dependency.** Coil 3.0.4 is already in `customer-app`; `:core` gains nothing, so its
  serialized lane is untouched.
- **The avatar composable is app-local**, in `features/profile/`, not `:core`. `:core` has no Coil
  dependency and adding one is a Gate 5 call for a component with two call sites in one app;
  `customer-app/ui/components/MascotAnimation.kt` is the existing precedent for an app-local
  Coil component. Flagging it rather than assuming.
- **Harvested** into `agents/knowledge/patterns-mobile.md` — a new "Picking an image, and rendering
  one that lives behind a SAS" section covering the image-only-vs-mixed picker split, the single
  `ImageCompressor` path, and the SAS cache/logging/error-retry rules. Additive clarification; it does
  not redefine an existing "one way".

## Review
<!-- reviewer + security verdicts here -->
