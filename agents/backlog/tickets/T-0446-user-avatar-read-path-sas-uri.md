---
id: T-0446
title: User avatar READ path — return a resolvable URI for the profile photo instead of a bare blob name
status: in_progress
size: M
owner: backend
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0438]
blocks: [T-0447, T-0448, T-0449, T-0457]
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
- [x] **AC4 — CLOSED 2026-07-30 by QA. PASSES.** Given the blob name is a raw `Guid` with no extension
      and the upload sets no content-type, When the reference is fetched, Then the image renders.
      **Executed, not reasoned** — Azurite, the app's **own** `BlobContainerClientFactory`, the **real**
      `UpdateCurrentUser.Handler` (only the two repositories mocked), real image files from this repo:
      - **Chromium 140** rendered the JPEG at **800×600** and the PNG at **48×48** from a bare-GUID URL
        served as `Content-Type: application/octet-stream`.
      - **WebKit** (Safari's engine) did the same.
      - **`CGImageSource`** — what `UIImage(data:)`, Kingfisher and SDWebImage all sit on — sniffed
        `public.jpeg` and `public.png` from the bytes.
      - `X-Content-Type-Options: nosniff` is **absent**; SAS fetch returns **200** with byte-identical
        content (sha256 match on both files); grant on the wire is exactly `sr=b`, `sp=r`, 1h, and a
        container-list with the same token returns **403**.
      **No content-type work is required in this ticket.** The blob-header defect is real but is
      codebase-wide and pre-existing — filed as **T-0464**, deliberately *not* folded in here (see
      Out of scope).
      **⚠️ The one honest threat to this result: real Azure was not reachable.** QA had no credentials
      and **correctly declined to use any**. If real Azure returns `nosniff`, every render above
      breaks. Corroboration is strong but is **inference from a shipped feature** — order photos
      already travel this identical `application/octet-stream` path and are already bound to
      `<img [src]>` on DEV today (`order-photos.component.html:125`, `:207`). **A one-minute owner
      check settles it** (see `status/sprint-14.md` §6) — it is not a ticket and not a blocker.
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

### Added 2026-07-30 by the security gate (SEC-1 and SEC-4 folded in — see `## Review`)

- [ ] **AC9 (SEC-1 — the redaction control must actually execute)** — Given a **real**
      `GET /api/User/GetCurrent` response body (not a hand-trimmed one), When it passes through
      `RequestLoggingMiddleware` on each of the five hosts, Then no SAS `sig=` value appears in the
      emitted log line. Two parts, both required:
      1. **Swap the composition to `TruncateBody(RedactSensitiveFields(...))`** on the response path
         and fix the same inversion on the request path. Exact lines — the four non-Customer hosts
         differ from `Cleansia.Web.Customer` by 4 lines, so **check the line before editing**:
         `Cleansia.Web.Customer` `:100` / `:78`; `Cleansia.Web.Admin`, `Cleansia.Web.Mobile.Customer`,
         `Cleansia.Web.Mobile.Partner`, `Cleansia.Web.Partner` all `:96` / `:74`. On the request path
         the truncation happens one level down in `ReadRequestBodyAsync` (`:143` / `:147`), so it must
         be moved or the redaction hoisted above it.
      2. **Rebuild `RequestLogSignedUrlRedactionTests` on a realistic payload.** The current fixture
         is **335 bytes**; a real `MyProfileDto` response is **~758–798 bytes** with the `blobUrl`
         value starting at index ~381–419 and closing at ~574–612 — past the 500-byte
         `ResponseBodyLimit`. Evidence: the rebuilt test goes **RED against the current
         `RedactSensitiveFields(TruncateBody(...))` ordering** and green after the swap. A test that
         is green both ways has not been fixed.
      Also assert the `base64Content` case on a `PUT UpdateCurrentUser` **request** body, which has
      the identical gap against the 1000-byte `RequestBodyLimit`.
- [ ] **AC10 (SEC-4 — mint a fresh blob name on replace)** — Given a user who already has an avatar,
      When they upload a replacement, Then the new image is stored under a **new** `Guid` and
      `ProfilePhotoName` changes. `UpdateCurrentUser.cs:154-155` currently reuses the stored name;
      the existing blob is already deleted first at `:145`, so a fresh name orphans nothing and
      `GdprDeletionService.cs:129-134` (which deletes by `ProfilePhotoName`) stays correct. Delete the
      now-false `// Replacing reuses the stored blob name…` comment rather than leaving it stale.
      Evidence: a handler test asserting the name changed across two saves, plus the deletion of the
      old blob still being asserted.

## Out of scope

- Any client UI — T-0447 (web), T-0448 (Android), T-0449 (iOS).
- ~~Changing the write path. It works; leave `UpdateCurrentUser` alone except for AC4's content-type
  if the panel rules it necessary.~~ **AMENDED 2026-07-30 by the PM** — the write path
  (`UpdateCurrentUser`) **is in scope**, narrowly, for **AC10 only**. Reasoning in `## Review`; the
  short version is that AC4 already reopens `UpdateCurrentUser.cs:160-164` for the content-type, so
  this widens the file's role rather than opening a file the ticket had closed.
- Image resizing/thumbnailing, EXIF stripping, moderation. **Now tracked, not "noted"** — the security
  panel answered the EXIF question: it is a real gap (SEC-3), it is **not new** and it discloses
  nothing new via this ticket, and it belongs to the **upload pipeline** rather than to the avatar.
  Filed as **T-0458** (decision + sanitizer seam) and **T-0459** (apply to the three pipelines).
  **Do not build it here.**
- PII in the `GetCurrent` response log — a pre-existing S6 violation on all five hosts, filed as
  **T-0457**. It shares a file with AC9, so **T-0457 is serialized behind this ticket** on the
  `RequestLoggingMiddleware` lane. Do not fix it here and do not let it block this.
- **The blob `Content-Type` defect — filed as `T-0464`, NOT folded in.** AC4 proved the avatar renders
  *without* it, so this ticket has no need of it. The fix touches the **shared SAS mint used by three
  other features** and deserves its own review. **Do not touch `BlobContainerClient.GenerateSasUri`
  here.**
- **Avatar caching — filed as `T-0465`.** Not a blocker for the read path.
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

- 2026-07-30 — **in_progress** — dispatched by the orchestrator: analyst + architect panels, then backend + paired reviewer. **Re-sequenced: the owner has ruled the avatar feature IS part of the demo**, overriding the PM's recommendation in `status/sprint-14.md` §3. This ticket is now the demo **critical path** — T-0447, T-0448 and T-0449 and the demo itself sit behind it, with an owner-run regen bundle mid-chain.
- 2026-07-30 — **security gate returned: APPROVE-WITH-CONDITIONS.** Stays `in_progress` per
  `ticket-lifecycle.md` ("a ticket that fails review does not go backwards; the same developer
  instance fixes it"). **Two findings folded in as AC9 (SEC-1) and AC10 (SEC-4)**; `## Out of scope`
  **amended** to admit `UpdateCurrentUser` for AC10 only. Three findings filed **out** of this ticket
  as **T-0457 / T-0458 / T-0459 / T-0460** so they cannot compress the demo path. Full verdict and
  the PM's reconciliation in `## Review`; findings doc opened at
  `agents/backlog/security/user-profile-avatar.md`.

## Review

### 2026-07-30 — SECURITY GATE (Gate 3): **APPROVE-WITH-CONDITIONS** for the demo

**No live vulnerability. The read path is correctly scoped and no exploit could be constructed
against it.** Full findings, with the PM's independent re-derivation of every load-bearing number,
in **`agents/backlog/security/user-profile-avatar.md`**.

#### Cleared — and this is what AC3 asked for, so it is named specifically

- **No IDOR.** The query is **field-less**; the user is resolved from the **JWT email claim**
  (`GetCurrentUser.cs:41`, `userSessionProvider.GetUserEmail()`). The blob name is **server-generated**
  (`UpdateCurrentUser.cs:155`). The inbound `BlobUrl` now present on the shared request DTO is
  **write-ignored** — `UpdateCurrentUser` reads only `Photo.Base64Content` (`:131`, `:156`).
- **The grant is `sr=b sp=r` for 1h**, asserted against a **real** `BlobContainerClient` (not a mock)
  by `ProfilePhotoSasGrantScopeTests`, **on the branch that is actually deployed**. It cannot be
  widened to list the container, cannot write, cannot outlive its window.
- **The container is private on two independent switches** — `storage.bicep:81`
  `allowBlobPublicAccess: false` (account kill-switch) and `:101` `publicAccess: 'None'` (per
  container).
- **No SAS is minted for any paged list.** The Gate 5 N-call risk the ticket pre-empted did not
  materialise: `UserListItem`, `UserItem` (`UserMappers.cs:23`, `:66`) and both `EmployeeMappers`
  shapes (`:37`, `:64`) still call the no-arg `MapToDto()` and emit `BlobUrl: null`. The optional
  parameter on `BlobMappers.cs:11` is what makes that hold **by default rather than by discipline** —
  the right seam.

#### SEC-1 — the redaction control this diff added never executes on this endpoint → **AC9**

`RequestLoggingMiddleware` **truncates before it redacts**:
`RedactSensitiveFields(TruncateBody(rawBody, 500))`. The regex needs a **complete quoted string**
(`"(?:[^"\\]|\\.)*"`), and in a `MyProfileDto` response the `blobUrl` value starts at index ~381–419
while the shortest realistic SAS is 193 chars — so the closing quote lands at ~574–612, **always past
the 500-byte cut. Redaction fires 0% of the time here.**

No signature escapes today — **but that is the truncation doing it, not the new control.** And the
test that "proves" the control uses a hand-trimmed payload the endpoint never emits: a **Gate 0.5
leg-1 failure**, a test that passes for the wrong reason.

> **PM correction, recorded for honesty.** The reviewer sized that fixture at 187 bytes; the PM
> measured it at **335**. The finding is unchanged and slightly sharper — 335 is still only ~43% of a
> real 786-byte response, and its closing quote sits at index 332, comfortably inside the cut.
> The reviewer's other arithmetic (366–460 start, ~208-char SAS) brackets the PM's own
> independently-derived range and reaches the same conclusion.

**Folded into this ticket, not filed out**, because the vacuous test is **this ticket's own defect**.
The fix also closes the **identical `base64Content` gap on `PUT UpdateCurrentUser` request bodies**.

**The unplanned win — recorded because it is genuine.** Adding `blobUrl` to the regex closes a
**real, live, pre-existing credential leak elsewhere**: on `GetOrderPhotos`, `OrderPhotoDto.BlobUrl`
sits at index **49–346**, inside the window, so complete signed URLs **including `sig=`** were being
written to Information-level logs before this diff. Dispute evidence has the same shape. This ticket
was not asked to fix that and does.

#### SEC-4 — fresh blob name on replace → **AC10**. The PM's call, with reasoning

The reviewer explicitly asked the PM to weigh this rather than rubber-stamp it. **The PM agrees:
fold it in.** Four reasons, in order of weight:

1. **The stale-avatar defect is demo-visible, and the owner ruled the avatar IS the demo.** The
   contract comment this diff adds (`BlobFileDto.cs:3-5`) tells clients to cache on `fileName` —
   which **never changes on replace**. Coil and Kingfisher will therefore render the **old** avatar
   after a successful upload, indefinitely. That ships as *"the new photo didn't upload"* on the
   single screen the demo is being run to show. It is the same argument the owner already accepted as
   well-made in `status/sprint-14.md` §3 ("landing a half-chain is strictly worse than today's honest
   placeholder") — applied to a defect rather than to scope.
2. **The "don't touch the write path" objection is already dead.** This ticket's own **AC4** reopens
   `UpdateCurrentUser.cs:160-164` for the content-type. AC10 changes **one line at `:155`** in the
   same method the developer is already editing. The scope amendment widens a file's role; it does
   not open a file the ticket had closed.
3. **Three workarounds vs one line.** Deferring makes T-0447, T-0448 **and** T-0449 each carry a
   cache-eviction workaround for a two-line backend fix — and three places to get it wrong, in three
   languages, none of which can be tested against the others.
4. **It restores consistency rather than inventing a pattern.** Order photos
   (`SaveOrderPhotos.cs:120-121`, `UploadOrderPhoto.cs:95-97`) and dispute evidence all mint a unique
   name per upload. **The avatar is the only blob in this codebase that reuses a name.**

The security consequence is real but bounded and, on its own, would **not** have justified folding
in: an outstanding SAS keeps resolving for ≤1h and serves the **new** image, so "replace the photo"
is not a remediation for a leaked URL — and there is **no revocation handle at all**, because the SAS
is ad-hoc with no stored access policy (`BlobContainerClient.cs:90-97` sets no `Identifier`), leaving
only blob deletion or account-key rotation. Today that SAS is only ever handed to the photo's own
owner, so the exposure requires the owner's own credential to have leaked first. **It is the product
defect that carries this decision, not the security one** — said plainly so a later reader can
disagree with the right argument.

The code comment's stated rationale (*"so URLs already handed out keep resolving"*) was written when
**there was no read path at all** — it never had a consumer, and T-0446 gives it one that is actively
harmful. Delete the comment with the line.

#### Filed OUT of this ticket — these must not compress the demo path

| Finding | Ticket | Why not here |
|---|---|---|
| **SEC-2** — `GET /api/User/GetCurrent` writes the caller's `email`, `firstName`, `lastName`, `phoneNumber`, `birthDate` to the Information-level log on all five hosts. All five close by index ~264–302, **inside** the 500-byte window, on **every** request. `IsSensitivePath` does not cover the route. **Arguably the largest S6 exposure in the codebase** — it is the most-called authenticated endpoint. | **T-0457** (P1, pre-demo) | Pre-existing, unrelated to this diff, and must not block a demo-path ticket. Shares the middleware file → **serialized behind this ticket.** |
| **SEC-3** — no EXIF stripping on **any** user-uploaded image; `ImageFileValidator` is a 3–4 byte magic-prefix check and `UpdateCurrentUser:160-164` stores bytes verbatim. Scoped to the **upload pipeline**, not the avatar: order photos and dispute evidence already have the gap and are **already cross-user visible**. The avatar is the least exposed instance — today only the photo's own owner can reach it, so **T-0446 discloses nobody's EXIF to anyone new**. | **T-0458** + **T-0459** (post-demo) | **HARD PRECONDITION for any cross-user avatar display** — the obvious next feature. Split in two so neither is an `L`. Includes a size cap + resize: there is no per-image limit anywhere beyond Kestrel's 30 MB default. |
| **SEC-5** — a **gap in the rule set, not a violation of it**: nothing in S1–S11 addresses bytes embedded in a stored artifact later served by URL (S4 governs DTO fields, S6 governs logs). | **T-0460** (post-demo) | The PM does not own `agents/knowledge/*.md`; routed `architect` + `docs` per the T-0445 / T-0456 precedent. |

#### Known-not-fixed — carried deliberately

- **The managed-identity SAS branch (`BlobContainerClient.cs:99-111`) is dead code in every
  environment and untested.** PM-verified: `AccountUrl` is set in **no** configuration file in the
  repository, and `UseManagedIdentity` derives from it
  (`BlobContainerConfiguration.cs:11-13`). It **must be re-reviewed before managed identity is
  switched on** — the grant-scope assertions have never run against it, and it adds a **blocking
  (synchronous) `GetUserDelegationKey` round-trip to every profile read**, a Gate 5 cost that does not
  exist today.
- **`user-files` is a flat container shared by all tenants** — no tenant prefix, no per-user prefix.
  The **only** thing preventing cross-tenant enumeration is the `sr=b` grant. That makes
  `ProfilePhotoSasGrantScopeTests` a **tenant-isolation control**, not merely a scope assertion:
  **it must not be softened.**

#### Conditions carried into the blocked client tickets

Appended to each ticket individually; the single source is the findings doc.

- **T-0447 (web)** — do **not** move an authenticated profile route to `RenderMode.Server` (the
  customer profile is `RenderMode.Client` today); server-rendering puts a live credential into an
  HTML document a proxy could cache.
- **T-0448 (Android)** — do **not** raise OkHttp logging to `Level.BODY` (`HEADERS` in debug, `NONE`
  in release on both apps today).
- **All three** — cache on `fileName`, **never** on `blobUrl`. With AC10 folded in, the `fileName`
  key changes on replace and this is sufficient; the per-client eviction workaround is **no longer
  required**.
- **Anyone** closing the AC4 content-type gap must **not** set `Cache-Control: public` on an avatar.

### 2026-07-30 — knowledge harvest from this ticket (folded in by the PM, not by its author)

The frontend developer working this ticket's lane added a catalog section to
`agents/knowledge/patterns-frontend.md` — **"Building a generated DTO — construct-then-assign, never
an object literal"** — and **deliberately did not write this note here**, because the backend
developer and a reviewer were both live in this file at the time. **That was the correct call** (a
concurrent write to a file two agents are editing is the T-0456 class of incident), and it is recorded
as such rather than as an omission.

The PM is folding the note in now that the lane has cleared. Implementers and reviewers on this
ticket and on **T-0447 / T-0448 / T-0449** should read that section before touching a generated
client — it is the pattern the regen breaks in T-0438 and PR #166 kept violating, and **T-0463 AC4**
now tests for the same shape.

#### What this ticket still needs before `in_review`

AC9 + AC10 implemented, and AC6's mutation-proving test **named**. **AC4 is now CLOSED** — see below.
The reviewer re-gates; the PM does not approve its own reconciliation.

### 2026-07-30 — QA: AC4 CLOSED, and the prediction was right

AC4 was the last open unknown on this ticket and it has been **closed with executed evidence**, not
argument: two browser engines and `CGImageSource` all render a bare-GUID blob served as
`application/octet-stream`, `nosniff` is absent, and the grant on the wire is exactly `sr=b sp=r` for
1h with container-list returning 403. Full detail in the AC4 checkbox above.

Three things this changes, recorded so nobody re-opens them:

1. **No content-type work belongs in this ticket.** The header defect is real, codebase-wide and
   pre-existing — **T-0464**. AC4 passing *without* it is precisely why it does not need to ride here.
2. **The grant-scope assertions now have wire-level corroboration**, not just
   `ProfilePhotoSasGrantScopeTests`' locally-computed token. The security gate's §0 clearance stands
   on two independent legs.
3. **The residual risk is named, not hidden:** real Azure was unreachable, QA declined to obtain
   credentials (correct), and a `nosniff` header on real Azure would break every render above. The
   owner can settle it in one minute on DEV — routed to `status/sprint-14.md` §6, deliberately **not**
   a ticket and **not** a blocker.

<!-- reviewer + security verdicts here; AC6 must name the mutation-proving test -->
