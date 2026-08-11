# Security findings — user profile avatar (read path, upload pipeline)

Opened **2026-07-30** by the PM to hold the `security` gate verdict on **T-0446** (avatar READ path —
short-lived read SAS on `MyProfileDto.ProfilePhoto.BlobUrl`) and the four findings the gate surfaced
that are **not** T-0446 defects.

**Verdict on T-0446: APPROVE-WITH-CONDITIONS for the demo.** No live vulnerability. The read path is
correctly scoped and no exploit could be constructed against it.

> **Provenance note (Gate 0.5 leg 3).** The verdict and findings below are the security reviewer's.
> The PM independently re-derived the load-bearing numbers before filing — the byte offsets, the
> line numbers, the container switches, the blob-name comparison and the `AccountUrl` reachability.
> Where the PM's measurement differs from the reviewer's, **both** are shown and the difference is
> called out. Nothing here was accepted on narration.

---

## 0. What was cleared, and why it is clear

Recorded so a later reader does not re-litigate it. Each of these was checked, not assumed.

| Claim | Status | Evidence |
|---|---|---|
| **No IDOR** on the read path | **Clear** | `GetCurrentUser.Handler` (`Features/Users/GetCurrentUser.cs:41`) resolves the user by `userSessionProvider.GetUserEmail()` — the **JWT email claim**. The query carries no user-identifying field at all. The blob name is server-generated (`UpdateCurrentUser.cs:155`, `Guid.NewGuid()`), never client-supplied. The inbound `BlobUrl` that now exists on the shared `BlobFileDto` is **write-ignored** on the request DTO — `UpdateCurrentUser` reads only `Photo.Base64Content` (`:131`, `:156`) and never `Photo.BlobUrl`. |
| **Grant scope** is one blob, read, 1 hour | **Clear** | `BlobContainerClient.GenerateSasUri` (`src/Cleansia.Infra.Azure.Storage.Blobs/BlobContainerClient.cs:86-97`) sets `Resource = "b"` and `BlobSasPermissions.Read`; lifetime is `TimeSpan.FromHours(1)` (`GetCurrentUser.cs:38`), matching the `UploadDisputeEvidence.cs:116` precedent. Asserted **against the real client** (not a mock) by `src/Cleansia.Tests/Infrastructure/ProfilePhotoSasGrantScopeTests.cs` — `sr=b`, `sp=r`, `se` within the window, path ends `/user-files/<blobname>`. Verified on the branch that is actually deployed, not on a stale copy. |
| **Container is private** | **Clear — two independent switches** | `deploy/bicep/modules/storage.bicep:81` `allowBlobPublicAccess: false` (account-level kill switch) **and** `:101` `publicAccess: 'None'` (per-container, applied to every container in the loop). Either one alone would prevent anonymous read; both are set. |
| **No SAS minted per row on a paged list** | **Clear** | The N-call Gate 5 concern T-0446's ticket pre-empted did not materialise. `UserMappers.cs:23` (`UserItem`) and `:66` (`UserListItem`) still call the **no-argument** `MapToDto()`, as do `EmployeeMappers.cs:37` and `:64`. All four shapes emit `BlobUrl: null`. The optional parameter (`BlobMappers.cs:11`, `string? blobUrl = null`) is what makes this hold by default rather than by discipline — that is the right seam. |

---

## SEC-1 — The redaction control added by this diff never executes on this endpoint

**Severity: not an exposure today. A vacuous control and a vacuous test.**
**Disposition: FOLDED INTO T-0446** — the test is this ticket's own defect.

The diff adds `blobUrl` to `SensitiveFieldRegex` in all five hosts' `RequestLoggingMiddleware`. On the
response path the middleware **truncates before it redacts**:

```csharp
: RedactSensitiveFields(TruncateBody(rawBody, ResponseBodyLimit));   // ResponseBodyLimit = 500
```

The regex requires a **complete quoted string** — `"(?:[^"\\]|\\.)*"`. If the closing quote was cut by
the truncation, there is no match and no redaction.

**The PM's own measurement** (a `MyProfileDto` serialized camelCase, compact, with a realistic
193-char service SAS — 44-char base64 HMAC — and `Code` expanding to `{type,name,value}`):

| Profile | Response bytes | `blobUrl` value starts | Closing quote at | Inside the 500-byte window? |
|---|---|---|---|---|
| Short name (`Jo Ng`, 7-char email) | 758 | 381 | 574 | **no** |
| Typical (`Michael Chaban`) | 786 | 409 | 602 | **no** |
| Long (`Oleksandra Kovalenko`, uk) | 798 | 419 | 612 | **no** |

The reviewer's figures were "value starts at 366–460, shortest SAS ~208". The PM's independently
derived range (381–419 start, 193-char SAS floor) sits inside the reviewer's and reaches the same
conclusion by a wider margin. **Redaction fires 0% of the time on this endpoint.** No signature
escapes — but that is the truncation doing it, not the new control.

**The test passes for the wrong reason — a Gate 0.5 leg-1 failure.**
`src/Cleansia.Tests/Logging/RequestLogSignedUrlRedactionTests.cs` builds a hand-trimmed payload the
endpoint never emits. **PM correction to the reviewer's figure:** the payload is **335 bytes**, not
187 — but the finding is unchanged and if anything sharper, because 335 is still only ~43% of the
real 786-byte response and its closing quote lands at index **332**, comfortably inside the 500-byte
cut. Delete the regex change and the test still goes red for the payload it uses; feed it a real
response body and it is green either way. It asserts scaffolding.

**Fix — one line per host.** Swap the composition to `TruncateBody(RedactSensitiveFields(...))`.
Exact line numbers (the reviewer quoted the `Cleansia.Web.Customer` copy; the other four hosts differ
by 4 lines — **check the line before editing**):

| Host | Response path | Request path |
|---|---|---|
| `Cleansia.Web.Customer` | `:100` | `:78` |
| `Cleansia.Web.Admin` | `:96` | `:74` |
| `Cleansia.Web.Mobile.Customer` | `:96` | `:74` |
| `Cleansia.Web.Mobile.Partner` | `:96` | `:74` |
| `Cleansia.Web.Partner` | `:96` | `:74` |

The request path has the same shape one level down: `ReadRequestBodyAsync` already returns
`TruncateBody(body, RequestBodyLimit /* 1000 */)` (`:143` / `:147`), so `RedactSensitiveFields(rawBody)`
is also redacting an already-truncated body. Fixing it closes the **identical `base64Content` gap on
`PUT UpdateCurrentUser` request bodies**, where a base64 image pushes the closing quote thousands of
bytes past the 1000-byte cut.

### The unplanned win — credit where due

Adding `blobUrl` to the regex closes a **real, live, pre-existing credential leak elsewhere.** On
`GetOrderPhotos`, `OrderPhotoDto.BlobUrl` sits at index **49–346** of the response — comfortably
inside the 500-byte window — so complete signed URLs **including `sig=`** were being written to
Information-level logs before this diff. Dispute evidence has the same shape. That is a genuine
S6 fix this ticket was not asked for, and it lands as soon as SEC-1's ordering fix goes in.

---

## SEC-2 — `GET /api/User/GetCurrent` logs the caller's PII at Information on all five hosts

**Severity: P1. Pre-existing S6 violation. NOT caused by T-0446 and must not block it.**
**Disposition: filed as `T-0457`. Pre-demo.**

Same middleware, opposite arithmetic. The response body's PII block — `email`, `firstName`,
`lastName`, `phoneNumber`, `birthDate` — **closes at index ~264–302** (PM's measurement across the
three profiles above; the reviewer said ~330). Either way it is **entirely inside** the 500-byte
window, on **every** request, on all five hosts.

`IsSensitivePath` (`:180` / `:184`) covers `/auth/`, `/login`, `password` and `/order/lookup`. The
route is `[HttpGet("GetCurrent")]` on `UserController` (`src/Cleansia.Web.Customer/Controllers/UserController.cs:17`)
→ `/api/User/GetCurrent`, which matches **none** of them.

S6 is verbatim: *no email, phone, name … in logs at Information level or higher.* This is arguably
the **largest S6 exposure in the codebase**, because `GetCurrent` is the most-called authenticated
endpoint on the platform — every app calls it on launch, on resume and after every profile save.

It predates T-0446 by a long way. It is filed now rather than later **because the demo will be
logging real people's data**, and DEV is already live and pointed at by the owner's iPhone.

---

## SEC-3 — No EXIF stripping on any user-uploaded image

**Severity: P2 for the avatar (no new disclosure today). P1-latent for order photos and dispute
evidence, where it is already cross-user visible.**
**Disposition: filed as `T-0458` (decision + sanitizer seam) and `T-0459` (apply to the three
pipelines). Post-demo — but a HARD PRECONDITION for cross-user avatar display.**

`ImageFileValidator` (`src/Cleansia.Core.AppServices/Common/Validators/ImageFileValidator.cs`) checks
a **3–4 byte magic prefix** against `Constants.ImageSignatures` and nothing else.
`UpdateCurrentUser.UploadPhotoAsync` (`:160-164`) then stores the decoded bytes **verbatim**:

```csharp
await using var stream = new MemoryStream(Convert.FromBase64String(base64Content.ExtractBase64Data()));
await client.UploadAsync(fileName, stream, Metadata.CacheMetadata, cancellationToken);
```

JPEG and TIFF both carry EXIF, and EXIF carries GPS. A magic-byte check is an **accept/reject** test;
it is not a sanitizer, and nothing downstream does the sanitizing.

### CORRECTION 2026-07-30 — the gap is narrower than first recorded, and it is NOT "all uploads"

The coordinator corrected the original framing: **the Android apps already compress and strip
EXIF/GPS client-side** (`2815c4f6`, PR #154). **The PM verified this and found the correction is
itself incomplete — iOS does it too**, and did it *first*:

| Platform | Client-side strip | Evidence |
|---|---|---|
| **Android** | **yes** | `cz.cleansia.core.media.ImageCompressor` (`core/src/main/java/cz/cleansia/core/media/ImageCompressor.kt`), wired into the partner order-photo + document pickers and the customer dispute-evidence path. Decodes to a fresh `Bitmap` and re-encodes via `Bitmap.compress` → a bare JFIF stream with **no EXIF segment at all**. Metadata is dropped **by construction, not by erasure** — there is no tag list to keep in sync. |
| **iOS** | **yes — and it was the original** | `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift`, used by `CleansiaPartner/.../OrderPhotosViewModel.swift` and `CleansiaCustomer/.../Disputes/Data/EvidencePreparer.swift`. Fresh `CGContext` + empty properties dictionary — the same structural argument. PR #154's own commit message says it **mirrored** the iOS design rather than inventing a second one. |
| **Web** | **no** | No equivalent exists. |
| **Any future/third-party client** | **no** | Nothing obliges one. |

**The server-side gap is still real and still worth closing** — a client-side strip is
**unenforceable**: the server cannot tell a stripped upload from an unstripped one, and any caller
that declines to strip (the web apps today, a modified app, a direct API call with a stolen token)
lands raw EXIF in blob storage. **Defence in depth, not a wide-open hole.** Both dependent tickets
must state it this way; overstating it would be the same verify-not-trust failure this gate was
convened to catch.

**What this changes about priority:** SEC-3 drops from "three pipelines wide open" to "the two mobile
platforms are covered, web is not, and nothing is enforced". It stays post-demo. It does **not**
change the hard precondition below — an unenforceable control is not a control.

**Scope the work to the upload pipeline, not to the avatar.** The avatar is the *least* exposed
instance of this gap:

- **Order photos** and **dispute evidence** already have it **on any path that is not the two mobile
  apps**, and there it is **already cross-user visible** — a cleaner's photos of a customer's home
  carry the customer's home coordinates, and customer, cleaner and admin can all fetch them. The
  realistic exposure today is **historical blobs** (uploaded before PR #154) and **web uploads**.
- The **avatar** is reachable only by the photo's own owner (`GetCurrent` is self-only, and no list
  shape emits a URL — see §0). So **T-0446 discloses nobody's EXIF to anyone new.**

But it **must land before any cross-user avatar display**, which is the obvious next feature (a
cleaner's face on an assigned order; an admin user list). The moment a second person can fetch that
blob, this becomes a live geolocation disclosure.

**Fold into the same work:** a **per-image size cap and a resize**. There is no per-image limit
anywhere in the pipeline beyond Kestrel's 30 MB request default — no `MaximumLength` on
`Base64Content`, no dimension bound, no re-encode.

---

## SEC-4 — Avatar replacement reuses the blob name; every other blob in the codebase does not

**Severity: security = low-and-bounded. Product = demo-visible defect.**
**Disposition: FOLDED INTO T-0446** (PM ruling — see the reasoning in T-0446's `## Review`).

`UpdateCurrentUser.cs:154-155`, with the rationale written in:

```csharp
// Replacing reuses the stored blob name so URLs already handed out keep resolving.
var fileName = hasExistingPhoto ? user.ProfilePhotoName! : Guid.NewGuid().ToString();
```

**Security consequence.** An outstanding SAS keeps resolving for up to an hour and now serves the
**new** image. "Replace the photo" is therefore **not a remediation for a leaked URL**. And there is
no revocation handle at all: the SAS is **ad-hoc**, with no stored access policy —
`BlobContainerClient.cs:90-97` builds the `BlobSasBuilder` with `BlobContainerName`, `BlobName`,
`Resource`, `ExpiresOn` and permissions, and sets **no `Identifier`**. The only invalidations
available are deleting the blob or rotating the account key.

**Product consequence — the sharper one.** The developer's own contract comment
(`Shared/DTOs/Files/BlobFileDto.cs:3-5`) instructs clients to *"key their image cache on FileName
(stable across replacement), never on this value."* `FileName` **never changes on replace** — so
Coil and Kingfisher will render the **stale** avatar after a successful upload, indefinitely. That
ships as *"the new photo didn't upload."*

**The precedent claim in the code comment is false here specifically.** Every other blob in this
codebase mints a unique name per upload:

| Pipeline | Blob name | Line |
|---|---|---|
| Order photos (save) | `{year}/{orderId}/{orderId}_{type}_{timestamp}_{guid8}{ext}` | `SaveOrderPhotos.cs:120-121` |
| Order photos (upload) | same shape | `UploadOrderPhoto.cs:95-97` |
| Dispute evidence | unique per upload | `UploadDisputeEvidence.cs` |
| **Avatar** | **reuses `user.ProfilePhotoName`** | **`UpdateCurrentUser.cs:154-155`** |

**The avatar is the only blob in this codebase that reuses a name.** Note also that the order-photo
pipelines record a `contentType` in metadata (`SaveOrderPhotos.cs:117`, `:124-125`) while the avatar
uploads with `Metadata.CacheMetadata` only — the same asymmetry T-0446's AC4 is already chasing.

No collateral: the blob is already deleted before re-upload (`UpdateCurrentUser.cs:145`), so a fresh
name orphans nothing, and `GdprDeletionService.cs:129-134` deletes by `ProfilePhotoName` and stays
correct.

---

## SEC-5 — A genuine gap in the rule set, not a violation of it

**Disposition: filed as `T-0460` (architect + docs).**

Nothing in **S1–S11** addresses **bytes embedded inside a stored artifact that is later served by
URL**. S4 governs *DTO fields*. S6 governs *logs*. Neither reaches metadata inside a stored image.
That is why SEC-3 could sit in three shipped pipelines without any gate catching it — the reviewers
were not wrong against the rules; the rules were silent.

The rule to add to `agents/knowledge/security-rules.md`, in substance:

> User-supplied images that will ever be served back by URL are **sanitized at upload** — metadata
> stripped, dimensions bounded, re-encoded. **Magic-byte validation is an accept/reject check, not a
> sanitizer**; passing it says the bytes are an image, not that the image is safe to hand back.

The PM does **not** own `agents/knowledge/*.md` — routed as an `architect` + `docs` ticket per the
T-0445 / T-0456 precedent.

---

## Known-not-fixed (recorded deliberately; no ticket)

**1. The managed-identity SAS branch is dead code in every environment, and untested.**
`BlobContainerClient.cs:99-111` takes the user-delegation-key path when `_useManagedIdentity` is set.
`UseManagedIdentity` is `!string.IsNullOrWhiteSpace(AccountUrl)`
(`src/Cleansia.Core.Blobs.Abstractions/BlobContainerConfiguration.cs:11-13`), and **`AccountUrl` is
set in no configuration file in the repository** — the only three references to it are the property,
the derived flag, and the factory that consumes it. So this branch has never executed anywhere and
`ProfilePhotoSasGrantScopeTests` exercises the shared-key branch only.

It **must be re-reviewed before managed identity is switched on**, for two reasons: the grant-scope
assertions have never run against it, and it adds a **blocking `GetUserDelegationKey` round-trip**
(note: a *synchronous* call, `:104`) to **every profile read** — a Gate 5 concern that does not exist
today.

**2. `user-files` is a flat container shared by all tenants.** There is no tenant prefix and no
per-user prefix — every avatar for every tenant sits at the container root under a bare GUID. The
**only** thing preventing cross-tenant enumeration is the `sr=b` single-blob grant. That makes
`ProfilePhotoSasGrantScopeTests` a **tenant-isolation** control, not merely a scope assertion:
**it must not be softened.** If anyone ever needs a container-scoped SAS here, the container layout
has to change first.

---

## Conditions carried into the blocked client tickets

Recorded here as the single source; each is also appended to its own ticket.

- **T-0447 (web)** — do **not** move an authenticated profile route to `RenderMode.Server`. The
  customer app's profile is `RenderMode.Client` today, and server-rendering would put a live
  credential into an HTML document a proxy could cache.
- **T-0448 (Android)** — do **not** raise OkHttp logging to `Level.BODY`. It is `HEADERS` in debug
  and `NONE` in release on both apps; `BODY` would write the SAS to logcat.
- **All three** — cache on `fileName`, **never** on `blobUrl` (the URL changes on every fetch). With
  SEC-4 folded into T-0446 the `fileName` key changes on replace, so this is sufficient on its own;
  had SEC-4 been deferred, each client would have needed to evict its own `fileName` key on a
  successful save.
- **Anyone** who later closes the blob `Content-Type` gap (T-0446 AC4) must **not** set
  `Cache-Control: public` on an avatar. A private image behind a SAS must be `private`, or an
  intermediary may retain it.
