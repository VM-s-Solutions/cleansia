---
id: T-0464
title: MetadataName.ContentType is a decoy — every order photo and dispute-evidence file is served as application/octet-stream; fix via SAS response-header override
status: draft
size: M
owner: backend
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0446]
blocks: []
stories: []
adrs: []
layers: [backend, architect]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Filed from **QA's T-0446 AC4 run** (DEF-1). This is the **root cause** behind AC4's whole
investigation — and AC4 passed *anyway*, which is why this is its own ticket and not a fold-in.

**Five constants are named as if they map to `BlobHttpHeaders`. None of them do.**

`MetadataName.ContentType` / `.ContentDisposition` / `.ContentEncoding` / `.ContentLanguage` /
`.CacheControl` (`src/Cleansia.Core.Blobs.Abstractions/Extensions/Metadata.cs:22-26`) are all routed
by `BlobContainerClient.UploadAsync` (`:64-67`) into **`SetMetadataAsync`** — i.e. `x-ms-meta-*`
custom metadata, which Azure **never** uses to serve the blob:

```csharp
if (metadata is not null)
{
    await client.SetMetadataAsync(metadata.ToDictionary(), cancellationToken: cancellationToken);
}
```

`IBlobContainerClient.UploadAsync` has **no way at all** to set real blob HTTP headers.

Three call sites compute a **correct** content type and hand it to a sink that discards it:
`SaveOrderPhotos.cs:125-127`, `UploadOrderPhoto.cs:101-103`, `UploadDisputeEvidence.cs:101-103`.
QA proved it on the order-photo shape: the stored blob carries
**`x-ms-meta-ContentType: image/jpeg`** *alongside* **`Content-Type: application/octet-stream`**.

**Consequence: every order photo and every dispute-evidence file in production is already served as
`application/octet-stream` today.** It has not been noticed because browsers sniff (see T-0446 AC4).

**Not a finding, checked and cleared:** QA verified all four **download** consumers take the content
type from the **DB record**, not from the blob. **No download is mislabelled.** Do not "fix" those.

## 🚨 The trap — read this before touching anything

**`Metadata.CacheMetadata` sets `CacheControl` to `"public, max-age=31536000"`
(`Metadata.cs:7-10`) — and the AVATAR uses it** (`UpdateCurrentUser.cs:163`).

Today that string is **inert**, because it goes to `x-ms-meta-CacheControl`. **The naive version of
this fix — mapping the five `MetadataName` constants onto real `BlobHttpHeaders` — would ACTIVATE
`Cache-Control: public` on a private, SAS-protected avatar.** That directly violates the security
condition already on record from the T-0446 gate:

> *Anyone who later fixes the blob `Content-Type` gap must **not** set `Cache-Control: public` on an
> avatar; a private image behind a SAS must be `private` or an intermediary may retain it.*

So the decoy is currently *protecting* us from a security-condition violation, and removing the decoy
without addressing the constant re-introduces it. **This is the single most important thing on this
ticket.** See AC5.

## The two candidate fixes — QA verified both work, and one is markedly better

| | **A. `BlobHttpHeaders` at upload** | **B. SAS response-header override** |
|---|---|---|
| Mechanism | set real headers when writing the blob | `BlobSasBuilder.ContentType` / `.CacheControl` → `rsct` / `rscc` on the mint |
| Existing blobs | **not fixed** — new uploads only | **FIXED** — QA fetched an *already-stored* octet-stream blob and got `Content-Type: image/jpeg`, `Cache-Control: max-age=3600, private` |
| Migration | a backfill over every existing blob | **none** |
| Blast radius | `IBlobContainerClient` interface + 3 call sites | **one file** — the shared SAS mint |

**Option B is the recommendation**, and the reason is not elegance: it **retro-fixes every existing
order photo and dispute-evidence file with zero migration**. Note QA's returned value was
`max-age=3600, private` — already the correct, condition-compliant shape.

**But B touches the shared SAS mint used by three other features**, which is exactly why this is not
folded into T-0446. See AC4.

## Deliberation

**Architect input on the A-vs-B choice**, not a full panel. The evidence is unusually complete —
both options were *executed* — so this is a short call, not a defended decision. **Escalate to a
panel only if the implementer wants A**, since that reintroduces a migration the evidence says is
avoidable.

## Acceptance criteria

- [ ] **AC1** — Given a stored order photo, dispute-evidence file, or avatar, When fetched via its
      SAS URL, Then `Content-Type` is the correct `image/*` (or the recorded type) rather than
      `application/octet-stream`. Evidence: an executed fetch with headers shown, **including at least
      one blob stored BEFORE this change** — that is the property that distinguishes B from A and it
      must be demonstrated, not asserted.
- [ ] **AC2** — The five `MetadataName` constants are either **wired to real blob HTTP headers** or
      **renamed/removed so they stop advertising a capability they do not have**. A comment is not
      sufficient: the next developer will read the constant name, not the comment. State which was
      chosen and why.
- [ ] **AC3** — The three call sites that already compute a correct content type
      (`SaveOrderPhotos.cs:125-127`, `UploadOrderPhoto.cs:101-103`, `UploadDisputeEvidence.cs:101-103`)
      end up actually applying it. Evidence: one executed fetch per pipeline.
- [ ] **AC4 (blast radius — this is why the ticket is separate)** — The SAS mint
      (`BlobContainerClient.GenerateSasUri`) is shared by **order photos, dispute evidence and the
      avatar**. Prove no regression on **all** of them, and confirm the grant scope is unchanged:
      **`ProfilePhotoSasGrantScopeTests` must still pass unmodified.** Per the security gate, that
      test is a **tenant-isolation control** (`user-files` is a flat container shared by all tenants,
      and only `sr=b` prevents cross-tenant enumeration) — **it must not be softened to accommodate
      new SAS parameters.**
- [ ] **AC5 (SECURITY — the trap above)** — Given the avatar, When fetched, Then `Cache-Control` is
      **`private`**, never `public`. `Metadata.CacheMetadata`'s `"public, max-age=31536000"` must not
      reach a real header on a SAS-protected avatar. Evidence: an executed fetch of an avatar showing
      the `private` directive. **Also decide what the order-photo and dispute-evidence cache policy
      should be** — they are behind SAS too, so `public` is wrong there as well, and the 1-year
      max-age was written for a world where these were assumed public.
- [ ] **AC6 (Gate 0.5 leg 1)** — A test goes **RED** if the header/override is removed. The reviewer
      **names it**. Given this defect survived in three shipped pipelines precisely because nothing
      asserted the served header, a suite that does not assert the actual `Content-Type` on the wire
      is repeating the original mistake.
- [ ] **AC7 (Gate 8)** — `dotnet build` + all three suites green **with real counts** — and note that
      per the correction in `status/sprint-14.md` §2.9 these suites **run locally**
      (IntegrationTests 108/108, HostTests 75/75). **"DEFERRED-TO-CI" is no longer an acceptable
      default here**; if Docker genuinely is unavailable in your environment, say so explicitly rather
      than inheriting a stale caveat.

## Out of scope

- Changing what the **download** endpoints report — QA cleared all four; they read the DB record.
- Backfilling `x-ms-meta-*` values on existing blobs. Option B makes it unnecessary; if the
  implementer picks A, **stop and re-open the A-vs-B call** rather than quietly adding a migration.
- EXIF stripping / resizing — **T-0458** / **T-0459**.
- Avatar caching behaviour — **T-0465**, which the same override half-fixes. **Coordinate:** if both
  are dispatched, they touch the same mint, so **serialize** them.
- `X-Content-Type-Options` / CORS on the storage account. CORS is a real gap (blocks canvas/`fetch`
  on the web — see T-0447 C2), but it is a **deploy/bicep** change, not this ticket.

## Implementation notes

- **Archetype:** the existing SAS mint at `BlobContainerClient.cs:86-97` — extend it, do not fork it.
- **⚠️ SHARED-FILE LANE — `src/Cleansia.Infra.Azure.Storage.Blobs/BlobContainerClient.cs`:**
  **T-0446 → T-0464 → T-0465**. T-0446 is in flight in this file's neighbourhood; **do not start until
  it lands.**
- **Do not touch the managed-identity branch (`:99-111`)** while you are in this file. It is dead code
  in every environment (`AccountUrl` unset everywhere — PM-verified) and untested; the security gate
  recorded it as requiring its own review before managed identity is switched on. **If your change
  needs to apply to both branches, say so explicitly** — an override silently applied to only the
  shared-key branch would be a latent inconsistency the moment MI is enabled.

## Status log
- 2026-07-30 — draft (created by pm from QA's T-0446 AC4 run, DEF-1). **PM-verified independently:** the `SetMetadataAsync` routing at `BlobContainerClient.cs:64-67`, the five constants at `Metadata.cs:22-26`, and — **not in the QA report** — that `Metadata.CacheMetadata` hardcodes `"public, max-age=31536000"` and is used by the avatar, making the naive fix a live security-condition violation. That is now AC5 and the ticket's lead warning.
- 2026-07-30 — **not `ready`**: `depends_on: [T-0446]` (shared-file lane on the SAS mint) and the A-vs-B call wants a short architect confirmation.

## Review
<!-- reviewer + security verdicts here; AC6 must name the mutation-proving test -->
