---
id: T-0448
title: Android — avatar upload, render and removal on the customer profile
status: blocked
size: M
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0446, T-0442, T-0441]
blocks: []
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

## Review
<!-- reviewer + security verdicts here -->
