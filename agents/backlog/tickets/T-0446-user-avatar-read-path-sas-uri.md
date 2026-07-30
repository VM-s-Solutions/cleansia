---
id: T-0446
title: User avatar READ path — return a resolvable URI for the profile photo instead of a bare blob name
status: draft
size: M
owner: —
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0438]
blocks: [T-0447, T-0448, T-0449]
stories: [US-user-avatar]
adrs: []
layers: [backend]
security_touching: true
manual_steps: [nswag-regen, mobile-spec-redump]
sprint: 14
---

## Context

**Owner approved the avatar as a feature in this batch.** A prior diagnosis established, and I
re-verified every claim in code on 2026-07-30:

- **The WRITE path works.** `UpdateCurrentUser.Command` carries `BlobFileDto? Photo`
  (`Features/Users/UpdateCurrentUser.cs:103`) and, since `fe0c985b`, `bool RemovePhoto = false`
  (`:108`). The handler uploads to the `UserFiles` container and stores a bare **GUID** as the blob
  name (`:155` — `Guid.NewGuid().ToString()`), reusing the name on replace so old URLs keep resolving
  (`:154`). Removal deletes the blob and nulls the name (`:143-151`). The no-op guard at `:135` is
  correct and is what T-0438's `removePhoto: false` relies on.
- **There is NO read path.** `MyProfileDto.ProfilePhoto` is a `BlobFileDto?`
  (`Features/Users/DTOs/MyProfileDto.cs:20`), populated by
  `Mappers/UserMappers.cs:41` → `user.ProfilePhotoName?.MapToDto()`, and that mapper is
  `Mappers/BlobMappers.cs:12-18`:

  ```csharp
  public static BlobFileDto MapToDto(this string fileName)
  {
      return new BlobFileDto(
          FileName: fileName,
          Base64Content: null,
          ContentType: null);
  }
  ```

  So the API hands the client `{ fileName: "<a guid>", base64Content: null, contentType: null }` —
  **no URI, no bytes**. No client can render an avatar even after a completely successful upload.
  `UserItem.cs:17` and `UserListItem.cs:17` have the same shape and the same problem.

This ticket is the **spine** of the avatar feature: T-0447 (web), T-0448 (Android) and T-0449 (iOS)
all stand on it and none can start until it lands *and* the owner has run the regen bundle.

**The working reference to copy, not reinvent** (the brief is right about this):

- `Features/Orders/GetOrderPhotos.cs:104-126` — `GenerateSasUrl`, including the hard-won Azurite vs
  Azure container-segment handling. Read the comment at `:107-118` before writing anything; the naive
  `Skip(1)` it replaced produced doubled paths on Azurite.
- `Mappers/DisputeMappers.cs:65-70` — the mapper-takes-`IBlobContainerClient` shape, the closest
  precedent for what `UserMappers` needs to become.
- `Features/Disputes/UploadDisputeEvidence.cs:116` — the same 1-hour SAS window.

Note one asymmetry the implementer must handle: order photos and dispute evidence store an **absolute
URL** and recover the blob name from it; `ProfilePhotoName` is already **just the name**. Do not
copy the URL-parsing branch blindly.

## Deliberation required — NOT yet `ready`

This defines a new response contract on a widely-consumed DTO and a new exposure of a blob, so it
needs **both** panels per `agents/process/deliberation.md`:

- **Analyst panel** — the story `US-user-avatar`, shared with T-0447/0448/0449: what the user sees
  while uploading, on failure, on removal, what the fallback is (initials today), and whether removal
  needs a confirm step.
- **Architect panel** — the read-path decision. The space to defend:
  - **A. Time-limited SAS URI on the DTO** (matches order photos / dispute evidence). Simplest,
    consistent. Con: the URI expires — a cached profile response outlives its own image URL, so the
    client must tolerate a dead URI, and the expiry window must be defended (1h matches precedent).
  - **B. A dedicated `GET /users/me/photo` endpoint** streaming the blob through the API. Con: puts
    image bytes on the API's hot path; Pro: no URL expiry, authorization is explicit per request.
  - **C. Base64 inline on the profile DTO.** Con: bloats every profile response; almost certainly
    wrong, but it is the option the existing `BlobFileDto.Base64Content` field invites, so the record
    should say why not.
  - The panel must also rule on whether `UserItem`/`UserListItem` (admin-facing lists) get the same
    treatment now or stay bare — a SAS per row on a paged list is an N-call performance question, and
    that is a Gate 5 concern the ADR should pre-empt.

## Acceptance criteria

_(PM floor; the panels finalize)_

- [ ] **AC1** — Given a signed-in user with a profile photo, When `GetCurrentUser` is called, Then the
      response carries a **resolvable** reference to that image (per the ADR's chosen option), and a
      client can render it with no further contract knowledge. Evidence: an integration test asserting
      the shape, plus a fetch of the returned reference returning 200 + image bytes.
- [ ] **AC2** — Given a user with **no** photo, When `GetCurrentUser` is called, Then the field is
      null/absent and no broken reference is emitted. Evidence: test.
- [ ] **AC3 (Gate 3, security)** — Given user A's returned reference, When user B (or an
      unauthenticated caller) uses it, Then the exposure is exactly what the ADR sanctions and no
      more. The security verdict must name the **specific** risk it cleared — e.g. "the SAS grants
      read on one blob name for 1h and cannot be widened to list the container" — not "authorization
      checked". Walk S1-S10.
- [ ] **AC4** — Given the blob name is a raw `Guid` with no extension and the upload sets no
      content-type (`UpdateCurrentUser.cs:160-164` uploads the stream with `Metadata.CacheMetadata`
      only), When the reference is fetched, Then the image renders in a browser and in both mobile
      image loaders. **Verify this — do not assume.** If a content-type must be recorded at upload
      time, that is in scope here. Evidence: an actual fetch, headers shown.
- [ ] **AC5 (Gate 6, TDD)** — The tests are written **before** the implementation, red→green, and the
      status log records it. Handler unit test (mocked blob client, asserting the emitted shape) +
      route integration test.
- [ ] **AC6 (Gate 6.5 — this is a SPINE ticket)** — At least one test goes **RED** if the read-path
      body is stubbed back to `MapToDto`'s bare-filename behaviour. The reviewer **names that test**.
      A suite that stays green against the current (broken) mapper is asserting scaffolding.
- [ ] **AC7 (Gate 8)** — `dotnet build` + **all three** test projects green: `Cleansia.Tests`,
      `Cleansia.IntegrationTests`, `Cleansia.HostTests` (real Postgres via Testcontainers). If Docker
      is unavailable, the integration/host suites are **DEFERRED-TO-CI / UNVERIFIED-LOCALLY**, named
      explicitly — never reported as PASS.
- [ ] **AC8 (Gate 7)** — The `manual_steps` bundle is written up for the owner (see below) and the
      ticket does not reach `done` until the owner confirms it.

## Out of scope

- Any client UI — T-0447 (web), T-0448 (Android), T-0449 (iOS).
- Changing the write path. It works; leave `UpdateCurrentUser` alone except for AC4's content-type if
  the panel rules it necessary.
- Image resizing/thumbnailing, EXIF stripping, moderation. Note them if you think they are needed
  (EXIF geolocation on a user-uploaded avatar is a real privacy question — raise it to the security
  panel rather than silently building it).
- Partner **employee document** photos — a separate, working pipeline.

## Manual steps (owner-only — batch these, do not interleave)

- `nswag-regen` — the profile DTO changes shape, so **all three** TypeScript clients must be
  regenerated. Per `quality-gates.md`, after the regen **build all three apps** before pushing; this
  is the exact step whose omission caused T-0438.
- `mobile-spec-redump` — `src/cleansia_android/openapi/customer-mobile-api.json` (and
  `partner-mobile-api.json` if the shared DTO is reachable from it) must be re-dumped, because both
  the Kotlin client and the **iOS Swift client** are generated from those committed specs
  (`src/cleansia_ios/openapi/README.md:13`). Without it, T-0448 and T-0449 cannot see the field.

**T-0447/0448/0449 are HELD until the owner confirms this bundle.**

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 5, backend spine)
- 2026-07-30 — awaiting analyst + architect deliberation panels before `ready` (DoR not met: read-path option not chosen)

## Review
<!-- reviewer + security verdicts here; AC6 must name the mutation-proving test -->
