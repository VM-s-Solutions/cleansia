# User-uploaded artifacts — living decision doc

> **Status: §1–§3 are SHIPPED and verified at HEAD (2026-08-05). §4–§6 are DECIDED-BUT-UNPANELLED —
> nothing in them is implemented.** The immutable record for §4–§6 is
> `../../backlog/adr/drafts/NNNN-user-artifact-content-policy-no-decoder.md` (**`proposed`**, number not
> allocated, **defense panel owed**).
> **Tickets:** T-0458 (policy + seam), T-0459 (application), T-0460 (the S-series law) — **all three are
> re-scoped by that draft; do not read their `## Context` as current.**
> **Related:** T-0464 ✅ (`b9753e85`, the served-type clamp), T-0548 ✅ (`97bb7265`, the avatar size cap),
> T-0556 ✅ + follow-up (document intake, the roster), `request-intake-limits.md` (the host ceiling —
> the *transport* bound, a different guarantee).

---

## 1. The five questions an upload surface has to answer

Conflating them is the recurring mistake in this area — three tickets have each fixed one and been read
as having fixed another.

| # | Question | Where it is answered | What it CANNOT do |
|---|---|---|---|
| 1 | **How many bytes?** | `Common/Validators/BlobFileSize.cs` — 10 MiB decoded, derived from the *encoded* length, **first** in every chain | bound the *collection*; bound the *request* |
| 2 | **How many items?** | each command's `Validator` — 10 documents, 30 photos | bound bytes at all; it runs after buffering, so it is answer-correctness, not resource protection |
| 3 | **Is it the kind of thing we take?** | the two `AbstractValidator<BlobFileDto>` siblings + their signature tables | say anything about what is *inside* the container |
| 4 | **What is it served as?** | `ServedContentType` on the **read** path (closed set) + `DocumentContentType.ForDownload` | change the bytes |
| 5 | **What travels inside it?** | **nothing today** — §4 is the open decision | be answered on the read path (a SAS hands the client the stored bytes directly) |

The transport ceiling is a **sixth** bound and lives in `request-intake-limits.md`. It is a host
property; 1–5 are per-surface properties.

## 2. State at HEAD — the fourteen upload routes and who fetches them

**Exposure is a property of the audience, not of the pipeline.** This is the table the tickets lack.

| Surface | Routes | Uploaded by | **Fetched by** | Delivery | Served as | Metadata scrubbed |
|---|---|---|---|---|---|---|
| Avatar | 3 (`UserController.UpdateCurrentUser` × Customer / Mobile.Customer / Mobile.Partner / Partner — 4 rows on the roster) | the user | **the same user only** | 1 h SAS → `<img>` | `application/octet-stream` (opaque overload) | no |
| Order photos (batch) | 2 (`OrderController.SavePhotos`, Partner + Mobile.Partner) | a cleaner | **customer + cleaner + admin**, 5 read hosts | 1 h SAS | closed-set typed | no |
| Order photo (single) | 2 (`OrderController.UploadPhoto`) | a cleaner | same | 1 h SAS | closed-set typed at read; **raw client string stored** | no |
| Dispute evidence | 2 (`DisputeController.UploadEvidence`, `multipart/form-data`) | the customer | that customer + **staff** | 1 h SAS | typed from the **client's file name** | no |
| Employee documents | 4 (`EmployeeController.SaveMyDocuments` / `.UpdateEmployee`, Partner + Mobile.Partner) | a cleaner | that cleaner + **admin** | **never by URL** — API host, `File(bytes, type, name)` → `attachment` | **byte-derived** | no (PDF/Office) |

**Why the avatar row matters most for planning:** `GetCurrentUser.ResolveProfilePhotoUrl` is the *only*
SAS mint for `user-files`. `UserMappers.cs:23,66` and `EmployeeMappers.cs:37,63` map the photo **without**
a URL, so every list and employee DTO carries `BlobUrl = null`. **Cross-user avatar display is one line
away**, and the day it lands the avatar's "audience: self" exemption expires.

### What the four hardening tickets actually closed

- **Stored XSS from a served artifact: closed.** `ServedContentType` is a closed value type with a
  private constructor; `text/html` and `image/svg+xml` are excluded **by name**; unknown → `Opaque`
  (`application/octet-stream`, outside the MIME-sniffing standard's sniffable set). Applied via the SAS
  response-header override (`rsct`/`rscc`), so it governs blobs written **before** it existed.
- **Type confusion on documents: closed.** `DocumentContentType.FromContent` answers *may we accept*
  and *what is it* from the first 9 bytes; `ForDownload` re-derives from the same table on the read
  path, so legacy rows retype without a backfill.
- **Unbounded intake: closed.** One shared size predicate, ordered first; count caps on all three arrays.
- **"How many intakes are there": partly closed.** `Base64UploadIntakeRosterTests` enumerates 10 —
  every route whose request graph reaches `BlobFileDto`. **Four more exist** and it cannot see them.

### The residue, named so it is not rediscovered

- **R1** — `UploadOrderPhoto` stores `command.ContentType` verbatim (`:112`); `UploadDisputeEvidence`
  records nothing and derives the served type from the client's file name (`DisputeMappers.cs:65-77`).
  Both sit behind a *declared*-type allowlist, which `patterns-backend.md:1283-1287` already calls a
  client-affordance filter, not a control. **Not exploitable** — the read clamp holds — but it is the
  sibling-left-behind shape in the one form the roster cannot see.
- **R2** — `GetOrderPhotos.cs:75` emits the raw stored `ContentType` on the DTO while clamping only the
  SAS header (`:71`).
- **R3** — `Constants.ImageSignatures:95-104` admits BMP, TIFF ×2 and **any RIFF** (the signature is
  `"RIFF"`, not `RIFF????WEBP`). `ServedContentType` can never serve BMP or TIFF, so those uploads
  succeed and never render — and no client offers them.
- **R4** — no metadata is removed from anything, anywhere.

## 3. The property that decides everything — nothing here decodes an image

`SixLabors`, `SkiaSharp`, `System.Drawing`, `Magick` appear in **zero** `src/**/*.csproj`. The only
graphics package is `QuestPDF` (`Cleansia.Infra.Services.csproj:14`), which generates invoices and never
touches a user photo. `OrderPhoto.Width`/`Height` exist and are **never populated** — both writers omit
the optional arguments.

**So every decoder in this system belongs to a client rendering an `<img>`.** That is what makes a
decompression bomb a non-threat today, and it is what a "sanitizer" would destroy: 10 MiB × 30 items of
attacker-chosen input, on an **S1 / 1.75 GB plan shared by 5 APIs + SSR + Functions**. A single-colour
30 000 × 30 000 PNG is a few hundred KB on the wire and ≈3.6 GB decoded.

## 4. The decided shape (unpanelled)

1. **No shared sanitizer seam.** The shareable things already are shared (`BlobFileSize`,
   `ServedContentType`, the roster). A metadata transform is not shareable — JPEG segments, PNG chunks
   and PDF object graphs have nothing in common but the word. **The shareable part is the *obligation*,
   and its home is a roster column, not an interface.**
2. **No decoder, ever, on a request path.** Metadata is removed by **container rewrite**: drop JPEG
   `APP1`/`APP13` (re-emitting a minimal EXIF carrying only `Orientation`), PNG `eXIf`/`tEXt`/`iTXt`/
   `zTXt`/`tIME`, WebP `EXIF`/`XMP ` chunks + the `VP8X` flag bits. GIF passes through. **Call it a
   metadata scrub, not a sanitizer** — it does not remove ICC, JPEG `COM`, or anything inside the image
   data, and saying otherwise oversells it.
3. **Applied by audience:** order photos and dispute evidence **yes**; the avatar **no**, recorded with
   an expiry (the ticket that first emits an avatar URL on a cross-user DTO owes it); employee
   documents **no** (PDF/OOXML rewriting refused, exclusion written on the roster).
4. **Narrow the accept set to the serve set** — drop BMP + TIFF, tighten WebP to `RIFF`+`WEBP`. Existing
   error key, no new i18n. This deletes the TIFF metadata problem instead of solving it (TIFF *is* an
   IFD container).
5. **Widen the roster to 14 rows** (`byte[]` + `IFormFile` request graphs) and give each row
   `validator | audience | scrub`.
6. **The law is a new S12**, keyed on **audience**, not on "served back by URL" — because the surface
   carrying the most metadata (employee documents) is not served by URL at all. Not an S4 extension:
   same principle, but S4's check is "read the DTO's field list," and no reading of a field list reaches
   inside a byte array.
7. **The web clients re-encode on pick, and that ships FIRST.** Both mobile clients already do; no
   `canvas`/`createImageBitmap` exists anywhere in `src/Cleansia.App`. ~30 lines per picker, zero server
   cost, removes essentially all live volume.

### The threat-model inversion this rests on

T-0458 argues the server work is required because *"a client-side strip is unenforceable."* That is
decisive for XSS, where **the uploader is the adversary**. For metadata **the uploader is the victim** —
a cleaner has no motive to hand-craft an API call re-attaching their own home GPS. The residual after a
client-side strip is an old client, a future integration, and carelessness. The server-side scrub is
therefore a **durability** argument, not a **correctness** one, and it must be defended as such.

The one genuinely new disclosure: an order photo uploaded from **partner web** carries the cleaner's
device identity, capture timestamp and — if taken away from the job — the cleaner's own location, **to
the customer**. GPS taken at the job is the customer's own address, which all three parties already
hold. On the avatar the only fetcher is the subject, so **T-0446 disclosed nobody's EXIF to anyone.**

## 5. Enforcement (ADR-0032 tiers — one clause is live, four are pending)

| Clause | Enforcer | Tier |
|---|---|---|
| Served type is server-derived from a closed set | `ServedContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`, `SasResponseHeaderOverrideTests` (`Cleansia.Tests`, a named step of `backend-ci.yml:70-71`) | **T1-CI** |
| Accept set = serve set | new test over `Constants.ImageSignatures` × `ServedContentType` | `(gate pending: T-0458)` |
| Every intake declares audience + scrub | `Base64UploadIntakeRosterTests`, widened | `(gate pending: T-0458)` |
| The scrub actually removes metadata | per-pipeline tests reading metadata out of the bytes handed to the blob client | `(gate pending: T-0459)` |
| No image-decoding package reference | new csproj-graph denylist walk with a non-vacuity floor | `(gate pending: T-0458)` |

**The rule must not be labelled `T1-CI` wholesale.** `enforcement.md:177-179` provides
`(gate pending: <ticket>)`, and note that `check-consistency.mjs` is **T2-ADVISORY** (in zero
workflows) and the frontend lint step is `continue-on-error: true` — neither can carry a law here.

## 6. Open / owed

- **Panel.** T-0458 AC1 and T-0460's "Deliberation required" both need distinct author / challenger /
  lead instances. **Not run.** Nothing below the draft ADR is `ready`.
- **The challenge I most want pressed:** whether §4.7 (web re-encode) makes §4.2/§4.3 unnecessary. If
  the panel rules it does, the correct output is: ship §4.7, §4.4, §4.5 and the §4.6 rule, and **defer
  the scrub with a written trigger.**
- **The second:** §4.2 hand-rolls format parsers over attacker-controlled binary. Less parsing surface
  than a decoder, but new security-control code with an interest-conflicted author.
- **The avatar exemption** is one PR from being wrong and the roster row asserts a *string*, not a
  *fact*. Scrubbing it anyway costs one call site.
- **Escalation Q-ART-01 (owner):** keep accepting DOC/DOCX on employee documents? They carry author
  names and revision history, no scrub is proposed, and an OOXML rewriter is not worth building.
  Dropping them changes a five-locale promise. Product call.
- **Backfill.** Blobs uploaded before PR #154 (2026-07-26) plus every web upload since carry metadata.
  **It cannot be fixed on the read path** — the SAS hands the client the stored bytes — so unlike the
  content-type residue this needs a real migration. Own ticket, after the panel.
- **Unexamined, and worth someone's time:** whether `UploadOrderPhoto` should be **deleted** rather than
  hardened (it duplicates `SaveOrderPhotos` for one photo and R1 is entirely its fault), and whether the
  four `byte[]`/`IFormFile` routes should become `BlobFileDto` so one roster predicate and one validator
  family covers all fourteen.
- **Adjacent, out of scope, still open:** `X-Content-Type-Options: nosniff` is set by **no** host
  (`patterns-backend.md:1306`) and real Azure sends none on a SAS fetch (owner-verified on DEV,
  T-0464 status log). The closed served-type set is the control; a header would be defence in depth and
  is a Bicep change. Storage-account CORS is a separate live gap (T-0447 C2).
