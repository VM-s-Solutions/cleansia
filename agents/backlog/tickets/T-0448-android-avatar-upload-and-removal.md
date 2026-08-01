---
id: T-0448
title: Android — avatar upload, render and removal on the customer profile
status: blocked
size: M
owner: android
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0446, T-0441, T-0450]
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

## Review
<!-- reviewer + security verdicts here -->
