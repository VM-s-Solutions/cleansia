# User-uploaded artifacts — living decision doc

> **Status: §1–§3 are SHIPPED and verified at HEAD (2026-08-05). §4–§6 are DECIDED-BUT-UNPANELLED —
> nothing in them is implemented. §7 is new (2026-08-06) and is the content-type ruling.** The
> immutable record for §4–§6 is
> `../../backlog/adr/drafts/NNNN-user-artifact-content-policy-no-decoder.md` (**`proposed`**, number not
> allocated, **defense panel owed**).
> **Tickets:** T-0458 (policy + seam), T-0459 (application), T-0460 (the S-series law) — **all three are
> re-scoped by that draft; do not read their `## Context` as current.**
> **Related:** T-0464 ✅ (`b9753e85`, the served-type clamp), T-0548 ✅ (`97bb7265`, the avatar size cap),
> T-0556 ✅ + follow-up (document intake, the roster), `request-intake-limits.md` (the host ceiling —
> the *transport* bound, a different guarantee).
>
> ⚠️ **§2's residue table (R1–R3) was written before the T-0556 follow-up and is STALE. Corrected
> in-place below; read §7 for the current content-type state.** Three names in §2/§5 no longer exist:
> `DocumentContentType` → `SniffedContentType` (one table, all four intakes),
> `Base64UploadIntakeRosterTests` → `UploadIntakeRosterTests` (**14 rows, not 10**), and
> `Constants.ImageSignatures` is **deleted** (`grep` over `src/` at 2026-08-06 returns nothing).

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

*(R1–R3 as written 2026-08-05. **Re-checked at HEAD 2026-08-06 — all three are CLOSED.** Struck rather
than deleted, because the wrong half of a closed finding is what gets re-derived from an old checkout.)*

- ~~**R1**~~ — **CLOSED.** `UploadOrderPhoto.cs:102` and `UploadDisputeEvidence.cs:104` both sniff;
  the dispute blob name's extension is minted from the bytes (`:105`) and the read resolves the stored
  **path**, not `FileName` (`DisputeMappers.cs:75`).
- ~~**R2**~~ — **CLOSED.** `GetOrderPhotos.MapToDto` resolves `ServedContentType` once and uses it for
  both the DTO field and the SAS header (`:96,101,105`).
- ~~**R3**~~ — **CLOSED.** `Constants.ImageSignatures` is deleted; `SniffedContentType.Signatures`
  (`:66-78`) carries no BMP or TIFF and matches WebP as `RIFF` + `WEBP` at offset 8.
- **R4** — no metadata is removed from anything, anywhere. **Still open** (§4).
- **R5 — new, 2026-08-06.** `SaveOrderPhotos` is the fourteenth intake and reads no byte of its
  payload. **See §7 — it is the subject of a ruling, not an unowned residue.**

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

---

## 7. The content-type question — ruled 2026-08-06 (two drafts, panel owed)

The T-0556 follow-up brought thirteen of the fourteen intakes onto a byte-derived stored type and routed
two calls here rather than deciding them. Both are now drafted. **Neither is `accepted`; both are
author-mode drafts awaiting a challenger and a lead.**

### 7.1 `SaveOrderPhotos` — the exception closes

`backlog/adr/drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md`

**The trade-off space, so the next reader does not re-derive it:**

| Option | Refusals it introduces | Why not (or why) |
|---|---|---|
| **Keep the exception, document it honestly** | none | Its justification is one set too wide, its fallback invents a fact, and its cost is zero — a carve-out with no cost behind it buys nothing and forbids stating the rule |
| **Sniff → refuse on failure** ← **chosen** | web-only, and only for a file whose browser-derived type disagrees with its bytes | Matches `UploadOrderPhoto` on the same container/table/accept set; the refused set is exactly the set that stores a lie today |
| **Sniff → store `Opaque` on failure** | **none at all** | The strongest alternative. Rejected because it gives one question two answers across two sibling endpoints, and a photo that uploads and can never render is evidence silently lost on a path a dispute may later turn on |
| Delete `SaveOrderPhotos`, route everyone to `UploadOrderPhoto` | none | Right direction, wrong ticket — wire change across 3 generated clients + 2 shipped apps, and it drops the 30-photo batch the web picker uses |
| Fix it on the read path (the document technique) | none | **Structurally impossible.** The server never sees an order photo's bytes after intake — `GetOrderPhotos` mints a SAS and storage serves the client directly |

**The fact that decided it, and the one to re-check if this is ever revisited:** *both mobile clients
re-encode every pick to JPEG and cannot emit anything else* — iOS `ImageCompressor.swift:77`
(`UTType.jpeg`), Android `ImageCompressor.kt:248` (`Bitmap.CompressFormat.JPEG`), both emitting
`photo.jpg` and bare base64. **The "it would break a live mobile path" objection is false**, and the set
of uploads that succeed today, render correctly, and would newly fail is **empty**.

**A second, independent lane reached the same verdict from the other direction.** The challenge on the
content-policy draft (`backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md` CH-4,
`c6370115`) attacks that draft for electing this surface as its metadata-scrub **pilot**: a per-format
scrub dispatching on the client's `data:` prefix runs the PNG chunk walker over JPEG bytes when the
uploader says so — **a no-op the attacker selects, under a green "scrub applied" test**
(premise pinned by `SaveOrderPhotosContentTypeTests.cs:49-59`). That is a stronger argument than "the
stored type is a lie", because it is about a future control being **unbuildable** rather than a fact
being wrong. It does **not** decide between the chosen option and the `Opaque` alternative — a
byte-derived `octet-stream` would make the scrub a *declared* no-op, which is fine — so F4 kills the
status quo and the sibling-symmetry reasons still carry the rest.

**Sequencing this creates, and it is the practical output of the two lanes meeting:**
**the closing ticket blocks T-0459** (apply the scrub). A scrub shipped first is a control whose no-op
path its own uploader chooses. The content-policy ADR should not re-decide the mechanism — it should
state that its D2 runs only against a byte-derived type and cite §7.1.

**Two audience facts re-verified here, because they change what the ruling is worth (CH-2c, CH-3iii):**

- `GetOrderPhotos.cs:59` gates on **`CanBrowseOrderAsync`**, not `CanAccessOrderAsync`
  (`OrderAccessService.cs:68-92`, comment at `:84-87`). Writing still requires assignment
  (`SaveOrderPhotos.cs:114-117`); **fetching does not.** Any tenant cleaner who can see the order while
  a seat remains open can mint a SAS for its photos — so the `application/pdf`-over-arbitrary-bytes
  capability is planted for an audience that is not enumerable at upload time.
- **A decoder is already deployed, and this ruling does not depend on it either way.** QuestPDF
  2024.12.1 ships native Skia + libjpeg-turbo/libpng/libwebp as runtime assets
  (`Cleansia.Infra.Services/obj/project.assets.json:832-864,2362-2364`, verified); the *call site* is
  what is absent. §3's "nothing decodes" is therefore a statement about **call sites**, not about the
  image on the box — correct §3 accordingly when that ADR is re-based. §7.1 leans on **neither** limb.

**Current shape (what the closing ticket implements):** sniff rule appended after the size rule; a
decodability rule closing the chain (there is none today — `SaveOrderPhotos.cs:136` calls
`Convert.FromBase64String` unguarded, which is a **live 500**); `DetermineContentType` deleted; blob-name
extension minted via `SniffedContentType.ExtensionFor`. The read-path clamp is **not** touched — it is
what governs rows already stored.

**Blocked on:** the panel, then the ticket. The catalog sentence is written in that ADR's D2 with tier
`(gate pending: <closing ticket>)`, promoting to `T1-CI` when it lands. Until then
`patterns-backend.md` carries the exclusion **as a named, dated deviation at the rule** rather than only
in two doc comments — that disclosure edit is made and is not gated on the panel, because withdrawing an
implicit blessing obliges nobody.

### 7.2 `DisputeEvidence` — the column is refused, the round-trip is pinned

`backlog/adr/drafts/NNNN-dispute-evidence-type-carrier-is-the-blob-name.md`

**Ruling: the server-minted blob-name extension is sufficient. No migration.** The name is
content-addressed — minted from the bytes in the same statement that reads them — which makes
`DisputeEvidence` the only upload surface with exactly **one** source of truth for its served type. A
column would give it two, and the sibling that has two (`OrderPhoto`) is the one that shipped the
"client believes the wrong one" defect the follow-up had to fix.

**The real gap is elsewhere, and it is cheap:** the carrier depends on `SniffedContentType.ExtensionFor`
and `ServedContentType.ForFileName` agreeing in both directions, across an assembly boundary
(`Core.AppServices` vs `Core.Blobs.Abstractions`, which cannot reference it), with **no test**. It holds
for all four accepted types today; one of four is exercised. `.doc`/`.docx` are already in the signature
table and unknown to `ServedContentType`, so a one-line widening of an accepted set silently demotes a
whole surface. Pin it with a property test over every `(intake, accepted type)` pair, count-asserted
first, exemptions named — `T1-CI`, zero baseline.

**Failure mode if this ruling is wrong:** evidence downloads instead of previewing for the customer and
the adjudicating staff member. Silent capability loss on a support path; never a security failure (the
demotion direction is `Opaque`-ward by construction).

### 7.3 Found while verifying 7.2 — not a content-type problem, and larger

**GDPR erasure orphans every dispute-evidence blob and destroys the only pointer to it.**
`GdprDeletionService` deletes blobs for `user-files` (`:134-135`), `employee-documents` (`:146-157`) and
`order-photos` (`:164-180`), then calls `dispute.Anonymize()` (`:210-212`) → `evidence.Anonymize()`
(`Dispute.cs:160-163`) → `FilePath = AnonymizationMarker.Value` (`DisputeEvidence.cs:37-42`). The
`dispute-evidence` container is **never touched**, and after the marker is written nothing in the
database can name the blob to delete it later. **Ordering matters: any deletion sweep must run before
`Anonymize()`.** Needs its own ticket against `GdprDeletionService`, `security_touching: true`. Recorded
here so it is not lost between two ADRs that are not about it.
