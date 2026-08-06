# ADR-NNNN (DRAFT — number NOT allocated) — User-uploaded artifacts: the platform decodes no user image; metadata is removed by container rewrite, on the surfaces whose audience is not the uploader

- **Status:** `proposed`
- **Date:** 2026-08-05 (drafted)
- **Number:** **not allocated on purpose.** Highest on disk is **0042**; two other drafts
  (`NNNN-host-request-intake-ceiling`, `NNNN-client-price-display-…`) are also awaiting allocation.
  The PM allocates; the file is renamed then.
- **Tickets:** **T-0458** (policy + seam), **T-0459** (application), **T-0460** (the S-series law).
  All three are re-scoped by this ADR — see §Context "What changed under the tickets".
- **Applies to:** every upload surface on all five `Cleansia.Web.*` hosts
- **Consumes:** T-0464 / `ServedContentType` (the served-type clamp), T-0548 / T-0556 (`BlobFileSize`,
  `DocumentContentType`, the count caps, the intake roster), ADR-0032 (a constraining catalog entry
  names an enforcer and declares a tier), ADR-0033 (this edit narrows a governing sentence → Architect)
- **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md`

> ### ⚠️ Method declaration — read before relying on anything here
> **1. ~~No defense panel has run.~~ SUPERSEDED 2026-08-06 — the panel HAS run. See §Verdict.**
> An independent challenger round was run
> (`agents/backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md`, 2026-08-06) and a
> **lead has adjudicated** (§Verdict, 2026-08-06). Outcome: **REVISE — the rulings survive, the map and
> several reasons do not.** The body below is **rev N** and is superseded in the twelve places §Verdict
> §C enumerates. **Do not implement from this body**; implement from rev N+1. §Challenge below remains
> the author's *self*-challenge and is NOT the panel round.
>
> **2. No shell in this invocation** (`Read`/`Glob`/`Grep`/`Write`/`Edit`; no `Bash`). Nothing was
> compiled, executed or measured. Every fact is read from source at HEAD and cited at `file:line`.
> Claims about **runtime** cost carry **⚠ not measured** and are owed a measurement by the
> implementing ticket, not inherited from here.
>
> **3. Three of T-0458's and T-0460's premises are stale at HEAD.** They are restated below rather
> than inherited. **Do not read those tickets' §Context as current.**

---

## Context

### What changed under the tickets — Gate 0

T-0458 and T-0460 were filed 2026-07-30 from the T-0446 security gate. Four intake hardening tickets
have landed since (T-0464 `b9753e85`, T-0548 `97bb7265`, T-0556 + follow-up). The state at HEAD:

| T-0458 asked for | State at HEAD | Evidence |
|---|---|---|
| **A per-image size cap** ("there is no size limit anywhere") | **SHIPPED.** 10 MiB decoded, one shared predicate for every base64 intake, derived from the **encoded** length so a rejection never decodes, and **first** in every `Cascade.Stop` chain | `Common/Validators/BlobFileSize.cs:8-9,17-28`; `ImageFileValidator.cs:11-19`; `DocumentFileValidator.cs:22-36`; `SaveOrderPhotos.cs:76-81` |
| **A per-request bound** (not asked for; found by T-0556) | **SHIPPED.** 10 for both document arrays, **30** for `SaveOrderPhotos.Photos`, each gating its own `RuleForEach` so a refused list is not decoded item by item | `SaveOrderPhotos.cs:46,57-85` |
| **Server-truth content type** ("`ImageFileValidator` checks a 3–4 byte magic prefix and nothing else") | **SHIPPED for documents** — one function answers *may we accept this* and *what is it*, from the bytes; the client's declared type and the extension are both discarded. **NOT for images** — see the residue table | `Common/Validators/DocumentContentType.cs:43-62`; `patterns-backend.md:1274-1315` |
| **Nothing can be served as `text/html` / `image/svg+xml`** (the implicit goal behind "sanitize") | **SHIPPED.** A closed value type decides the served type on the read path, so it fixes rows already stored; SVG is excluded beside `text/html` by name | `Core.Blobs.Abstractions/ServedContentType.cs:27-52`; `IBlobContainerClient.cs:42`; legacy rows retype from the intake's own table (`DocumentContentType.ForDownload`, `:71-72`) |
| **A roster so the next intake is not forgotten** (not asked for) | **SHIPPED for base64 routes** — ten routes, each annotated with the validator that guards it | `Cleansia.Tests/Common/Validators/Base64UploadIntakeRosterTests.cs:31-43` |
| **EXIF / metadata removal** | **NOT SHIPPED.** Nothing server-side removes anything from inside any artifact | — |
| **Resize / dimension bound / re-encode** | **NOT SHIPPED — and this ADR refuses it.** See D2 | — |

**T-0460's premise moved too.** It says the rule set is silent on "bytes inside a stored artifact." Half
of that is no longer true: the *served type* half is now enforced by three T1-CI suites
(`ServedContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`,
`SasResponseHeaderOverrideTests`) and written into `patterns-backend.md:1274-1315`. What is still
unwritten is (a) that the pattern is a **law**, not a backend convention, and (b) the *content* half.

**So: T-0458 is ~70 % satisfied and its remaining 30 % is a different decision than the one it framed.
T-0460 is half-satisfied in practice and unwritten as law.** Neither is closed; neither is what it says.

### The residue, stated exactly

**R1 — Two order-photo/dispute intakes still take the client's declared type as the stored type.**
`UploadOrderPhoto.Handler` writes `contentType: command.ContentType` straight from the wire
(`:112`), and `UploadDisputeEvidence` records nothing and lets the read path derive from the
**client-supplied file name** (`DisputeMappers.cs:65-77`). Both are behind a declared-type allowlist
(`UploadOrderPhoto.cs:34`, `UploadDisputeEvidence.cs:17-24`) — which `patterns-backend.md:1283-1287`
already names as *"a client-affordance filter, not a control."* **Not exploitable today**, because
`ServedContentType` clamps the served header to a closed set — but it is the identical sibling-left-
behind shape the roster was built to end, in the one form the roster cannot see: these four routes take
`byte[]` / `IFormFile`, not `BlobFileDto`, so `Base64UploadIntakeRosterTests`' predicate
(`:63-66`) does not reach them. **The roster reports ten intakes; there are fourteen.**

**R2 — `GetOrderPhotos` emits the raw stored string on the DTO** (`:75`, `ContentType: p.ContentType`)
while clamping only the SAS header (`:71`). Any client that ever builds an element from that field
re-opens what the clamp closed. Low severity, named so it is not rediscovered.

**R3 — The image accept set is wider than the serve set.** `Constants.ImageSignatures:95-104` admits
BMP, TIFF (both endiannesses) and **any RIFF container** (the signature is `"RIFF"`, not
`RIFF????WEBP`, so a WAV or AVI passes). `ServedContentType:34-52` can never serve BMP or TIFF —
they resolve to `Opaque`. **A TIFF avatar therefore uploads successfully and never renders**, and no
client offers those formats anyway (`profile.models.ts:19-25`, `order-photos.helpers.ts:17-21`,
`profile-documents.component.html:15`, `cleansia-file.component.ts:35`). This violates an existing
catalog sentence — *"keep the accepted set equal to what the clients offer"*
(`patterns-backend.md:1298-1300`).

**R4 — Metadata.** No path removes EXIF/XMP/IPTC from an image or `/Author`,`/Producer` from a PDF.

### Who actually fetches what — the table the tickets do not have

This is the load-bearing table. **Exposure is a property of the audience, not of the pipeline.**

| Surface | Uploaded by | Fetched by | How | Served as |
|---|---|---|---|---|
| **Avatar** | the user | **the same user, and nobody else** — `GetCurrentUser.ResolveProfilePhotoUrl` (`:47-70`) is the **only** SAS mint for `user-files`; `UserMappers.cs:23,66` and `EmployeeMappers.cs:37,63` map the photo **without** a URL, so every list/employee DTO carries `BlobUrl = null` | 1 h SAS, `<img>` | `application/octet-stream` (the opaque overload) |
| **Order photo** | a cleaner (partner web **or** mobile) | **customer, cleaner and admin** — `GetOrderPhotos` is exposed on **five** hosts (`Web.Partner:158`, `Web.Mobile.Partner:149`, `Web.Customer:131`, `Web.Mobile.Customer:131`, `Web.Admin/AdminOrderController:48`) | 1 h SAS | closed-set typed (`GetOrderPhotos.cs:71`) |
| **Dispute evidence** | the customer (own dispute, `UploadDisputeEvidence.cs:90-94`) | that customer + **admin staff** | 1 h SAS | closed-set typed from the **file name** |
| **Employee document** | a cleaner | that cleaner + **admin** | **never by URL** — three API routes, all using `File(bytes, type, name)` → `Content-Disposition: attachment` (`Web.Partner/EmployeeController.cs:125`, `Web.Mobile.Partner/EmployeeController.cs:179`, `Web.Admin/AdminEmployeeDocumentController.cs:92`) | byte-derived, `attachment` |

Two consequences the tickets get backwards:

1. **The avatar is not the pilot. It is the surface with no exposure at all.** T-0458 AC6 picks it
   *because* it is lowest-blast-radius — which is exactly why piloting there delivers zero exposure
   reduction while making the work look done. D5 overturns it.
2. **T-0460's hinge — "served back by URL" — is the wrong hinge.** Employee documents are *not* served
   by URL and are the surface carrying the **most** metadata (PDFs and Office files carry author names
   and revision history). A rule keyed on the delivery mechanism excludes the worst case. D7 keys on
   audience instead.

### The fact that decides the whole thing: nothing here decodes an image

`SixLabors`, `SkiaSharp`, `System.Drawing` and `Magick` appear in **zero** `src/**/*.csproj`. The only
graphics package in the solution is `QuestPDF` (`Cleansia.Infra.Services.csproj:14`), which *generates*
invoices and never touches a user photo. `OrderPhoto.Width`/`Height` exist (`:39-40`) and **are never
populated** — both writers omit the optional arguments (`SaveOrderPhotos.cs:141-150`,
`UploadOrderPhoto.cs:105-114`).

So **no user-supplied image is decompressed anywhere on this platform's servers.** Every decoder in the
system belongs to a client rendering an `<img>`.

That is not an accident to be corrected; it is the property that makes the current design safe, and the
tickets propose to destroy it. A decoder converts a *bounded* input (10 MiB, enforced) into an
*unbounded* allocation chosen by the uploader: a single-colour 30 000 × 30 000 PNG compresses to a few
hundred KB and decodes to ≈3.6 GB of bitmap. `SaveOrderPhotos` accepts **30** items per request
(`:46`). The prod App Service plan is **S1, 1.75 GB, shared by 5 APIs + SSR + the Functions host**
(`agents/architecture/decisions/request-intake-limits.md` §3, from
`deploy/bicep/modules/appServicePlan.bicep:19-22`). **A naive `IImageSanitizer` is therefore a remote
OOM primitive on an authenticated-but-cheap path, delivered by the ticket written to make uploads
safer.** It is mitigable — a header-only `Identify` before any decode — but that mitigation must be a
decision, not an assumption, and it is the assumption both tickets make.

### What the residual threats actually are

| Threat | Live at HEAD? | Why |
|---|---|---|
| Stored XSS from a served artifact | **No** | Closed set on the read path; `text/html` and `image/svg+xml` absent by name; documents byte-typed and `attachment` |
| Polyglot (valid JPEG **and** valid HTML) | **No** | Same clamp. A polyglot only matters if something serves it with an executing type; nothing can |
| Type confusion / extension rename | **No** for documents (byte-derived); **contained** elsewhere by the clamp | R1 is a hygiene defect, not an exploit |
| Decompression bomb / pixel flood | **No — and adding a sanitizer creates it** | Nothing decodes |
| Malware inside a PDF/DOCX handed to an admin | **Yes, and out of scope** | No scanner exists; `DocumentContentType`'s own doc-comment says so (`:22-26`). Refusing markup/scripts/executables is what it does; it is not a scanner |
| **Metadata disclosure to a fetcher who is not the uploader** | **Yes — this is the whole residue** | R4 |

**And the metadata threat model is not the XSS one.** T-0458 says the reason to do the work server-side
is that *"a client-side strip is unenforceable — the server cannot distinguish a stripped upload from an
unstripped one."* That argument is decisive for XSS, where **the uploader is the adversary** and will
bypass any client check. For metadata it is much weaker, because **the uploader is the victim**: a
cleaner has no motive to hand-craft an API call that re-attaches their own home GPS. The residual after
a client-side strip is an *old or modified client*, a future integration, and carelessness — not an
attacker. Importing the XSS threat model into the metadata case is what makes a decoder look mandatory.

The one genuinely new disclosure, stated narrowly: **an order photo uploaded from partner web carries
the cleaner's device identity, capture timestamp and — if the photo was taken away from the job — the
cleaner's own location, to the customer.** GPS taken *at* the job is the customer's own address, which
the customer, the cleaner and the admin all already hold from the order. On dispute evidence, the
metadata is the customer's and the extra fetcher is staff. On the avatar the only fetcher is the
subject, so **T-0446 discloses nobody's EXIF to anyone.** The disclosure is real, asymmetric (the
platform deliberately withholds cleaner identity — `PreferredEmployeeId` is never on a partner-facing
DTO; cleaner first-name only, per S4), and **narrow**.

---

## Decision

### D1 — The tickets' "shared sanitizer seam" is refused. The shared seams already exist and a transform is not one of them

T-0458 assumes one `IImageSanitizer` for all uploads. **There is no such abstraction to build**, and the
codebase already demonstrates why: the intake lanes diverged, and they diverged *correctly*.

| Shared today | Why it is genuinely shareable |
|---|---|
| `BlobFileSize` | A byte count is the same fact for every artifact |
| `ServedContentType` | The set of types this platform may ever emit is one closed set |
| `Base64UploadIntakeRosterTests` | "How many intakes are there" is one question |
| Two `AbstractValidator<BlobFileDto>` **siblings** | The *accepted set* is a per-surface product promise: documents accept PDF/DOC/DOCX, avatars accept the browser-renderable image set. One validator would have to be the union, which is wrong for both |

A metadata transform sits with the siblings, not with the shared three: JPEG segments, PNG chunks,
RIFF chunks and PDF object dictionaries have nothing in common but the word "metadata." An interface
over them is a switch statement with a DI registration.

**What is shareable is the *obligation*, and its home is the roster, not an interface.** The roster
already annotates each intake with the validator guarding it; it gains a second annotation (D6).
A new upload route then cannot be added without stating its answer — which is the property the
codebase has twice failed to get from "remember to do it in each intake."

**Consequence:** T-0458's AC6 ("wire the sanitizer into exactly one pipeline as a pilot") and T-0459's
"mirror the pilot; do not invent a second integration style" are both re-scoped. There is one helper
per format, called by the two handlers that need it — not a seam.

### D2 — Nothing on a request path decodes user-supplied image data. Re-encoding is refused

This is the ADR's central ruling and the one a challenger should attack hardest.

**Refused: decode + re-encode** (`ImageSharp` / `SkiaSharp`). It buys metadata removal "by
construction," and it costs: a decoder on a request path fed 10 MiB × 30 attacker-chosen items on a
1.75 GB shared plan; a licence question that is legal rather than technical; native binaries to
validate against the Linux App Service and Functions images; JPEG generation loss on evidentiary
photos; an orientation regression that must be handled or photos ship rotated; and **it does not
generalise to PDF at all**, which is the format carrying the most metadata in this codebase.

**Adopted: removal by container rewrite** — walk the container's own segment/chunk structure, drop the
metadata containers, re-emit the rest byte-identically. No decoder, no bitmap, no library, no licence,
no quality loss, allocation bounded by the input which is already bounded.

- **JPEG** — drop every `APP1` (EXIF **and** XMP, which rides its own `APP1`) and `APP13` (IPTC/Photoshop)
  segment. **Orientation is preserved by re-emitting a minimal EXIF `APP1` carrying only the
  `Orientation` tag** when the original carried one in 2–8. This is the fiddly part of the design and
  the single most likely place to ship a visible regression — it is pinned by D4's test.
- **PNG** — drop the `eXIf`, `tEXt`, `iTXt`, `zTXt` and `tIME` ancillary chunks. Chunks carry their own
  CRC, so removal needs no recomputation.
- **WebP** — drop the `EXIF` and `XMP ` RIFF chunks, clear the corresponding `VP8X` flag bits, fix the
  RIFF size field. Simple (`VP8 `/`VP8L`-only) files have no such chunks and pass through untouched.
- **GIF** — pass through. GIF has no EXIF; the only metadata container is a comment extension, which no
  camera writes.
- **PDF / DOC / DOCX** — **not rewritten.** See D8.

**What this loses, said plainly.** It removes the metadata containers a camera and an editor write. It
does **not** remove an ICC profile (removing one changes rendered colour), a JPEG comment (`COM`), or
anything embedded inside the image data itself. It is a **metadata scrub**, and the ADR calls it that
rather than a "sanitizer" — a word that promises the thing D2 refuses to attempt.

**⚠ not measured.** The claim that a segment walk over a 10 MiB JPEG is negligible against a request
that already base64-decodes and uploads the same bytes is a reasonable expectation, not a measurement.
T-0458 AC7 stands and is owed **on the 30-item batch**, not on one avatar.

### D3 — Where the scrub runs, and why it cannot be on the read path

**Intake, in the handler, between the decode and `UploadAsync`.** Three placements were considered
(T-0458 lists them):

- **Not a FluentValidation validator** — validators reject; they do not transform. Correctly ruled out
  by the ticket.
- **Not a decorator on `IBlobContainerClient.UploadAsync`** — it is the most tamper-proof and it is
  wrong here: that sink also writes generated invoice PDFs, receipts and GDPR exports, which are
  *ours*. A decorator that must ask "is this stream a user artifact?" has lost the property that made
  it attractive, and an unconditional one would rewrite our own documents.
- **Not the read path.** This is worth stating because the read path is exactly where T-0464 solved the
  *type* problem, retro-fixing every stored blob with zero migration. **It does not generalise**: the
  type clamp works because we mint the SAS and can pin a response header on it, but the SAS then hands
  the client the blob **directly from storage** — we never touch those bytes again. Content can only be
  changed where we hold it, and that is intake. (This is also why R4 has no zero-migration answer and
  D9's backfill stays open.)

### D4 — The scrub is applied by audience: order photos and dispute evidence. **Not** the avatar

Per the audience table:

| Surface | Ruling |
|---|---|
| `SaveOrderPhotos`, `UploadOrderPhoto` | **Scrub.** Cross-user by construction, five read hosts, and the one surface with a genuinely new disclosure |
| `UploadDisputeEvidence` | **Scrub** the image formats. Same helper, near-zero marginal cost, audience includes staff. Secondary benefit: EXIF timestamps on evidence are **client-forgeable**, so removing them removes a signal an adjudicator might otherwise trust |
| `UpdateCurrentUser` (avatar) | **No scrub, recorded as a decision with an expiry.** The only fetcher is the subject; scrubbing discloses nothing to nobody. **The obligation attaches to the ticket that first emits an avatar URL on a cross-user DTO** — which today is a one-line change in `UserMappers`/`EmployeeMappers`, and that is precisely why it must be a written gate rather than a memory |
| `SaveMyDocuments`, `UpdateEmployee.Documents` | **No image scrub** (D8) |

**This overturns T-0458 AC6.** The pilot is `SaveOrderPhotos`, because it is simultaneously the exposed
surface and the batch shape whose cost must be measured (T-0459 AC6). Piloting on the avatar measures
the one-item case and reduces zero exposure.

### D5 — Narrow the image accept set to the serve set

Delete BMP and both TIFF signatures from `Constants.ImageSignatures`; tighten WebP from `"RIFF"` to
`RIFF` + `WEBP` at offset 8. Existing error key `BusinessErrorMessage.FileNotMatchContentType`
(`file.content_type_doesnt_match`) — **no new key, so no i18n work and no parity-guard churn.**

Three things this buys for near-zero cost:

1. It fixes R3: an upload that "succeeds" and can never render is a worse user outcome than a refusal.
2. **No client can send these** (all four accept lists cited in R3), so it breaks nobody.
3. **It removes the TIFF metadata problem entirely rather than solving it.** TIFF *is* an IFD
   container — its metadata is not a removable segment, it is the file format. This is the ADR's one
   use of "refuse is cheaper and worse for users," applied where it is not worse for users at all.

### D6 — The roster is widened, and gains the audience/scrub column

`Base64UploadIntakeRosterTests`' predicate matches request graphs reaching `BlobFileDto` (`:63-66`) and
therefore misses `UploadOrderPhoto` (`byte[]`) and `UploadDisputeEvidence` (`IFormFile`) on two hosts
each. Widen it to those two shapes — **the roster goes from 10 rows to 14, and the four new rows are
exactly the ones carrying R1.** Each row's annotation becomes
`<validator> | <audience: self | cross-user | staff> | <scrub: none | image-metadata | n/a>`.

A new upload route reddens the test; the fix is to add the row *after* answering all three columns.
This is D7's enforcer and the reason the obligation is a roster column rather than an interface.

### D7 — The law (T-0460): a new **S12**, keyed on audience, not on delivery mechanism

**S12, not an extension of S4.** S4's principle is the same — *do not hand a client something you did
not intend* — but a rule's identity is its **check**, and S4's check is "read the DTO's field list."
No reading of a field list reaches inside a byte array. A reviewer walking S1–S11 for an upload ticket
will not open "DTO leak prevention." Different check, different number. (T-0460 §Out of scope already
forbids renumbering, and this adds rather than renumbers.)

Proposed text — **T-0460 owns the final wording and is the sole writer of
`agents/knowledge/security-rules.md`; this ADR does not edit it**:

> ## S12 — What is inside a stored artifact is disclosed to everyone who can fetch it
>
> A file a user uploads is a **container**, not a value: pixels *plus* the capture coordinates, device
> identity, author names and revision history that travel with them. A magic-byte check is an
> accept/reject test — it bounds what the container **claims to be** and removes nothing from inside
> it. An allowlist of *declared* types is weaker still: it is a client-affordance filter, and arbitrary
> bytes under a permitted claim pass it unchanged.
>
> For every upload surface, answer three questions **in writing, on the intake roster**:
>
> 1. **Who fetches these bytes?** If the only fetcher is the uploader, the artifact discloses nothing
>    new — record *"audience: self"*. **That answer expires the moment a second audience is added**, and
>    the ticket that adds one owes the scrub.
> 2. **What is it served as?** Server-derived, from a **closed set**, decided on the **read** path so it
>    also governs rows written before the rule. Never the client's declared type; never the file
>    extension. The accepted set equals the servable set — accepting a format that can only ever be
>    served opaquely is an upload that succeeds and never renders.
> 3. **What travels inside it?** For an artifact whose audience is not its uploader, metadata containers
>    are removed at **intake** — the read path cannot do it, because a signed URL hands the client the
>    stored bytes directly. A surface that does not scrub records **why**, by name, on the roster.
>
> **And one prohibition: no request path decompresses user-supplied image data.** A decoder turns a
> bounded upload into an allocation the uploader chooses — a 300 KB PNG into gigabytes of bitmap, times
> the array cap. Nothing in this system needs pixels. Adding a decoder is an **ADR**, not a package
> reference, and it owes a header-derived dimension bound checked **before** any decode.
>
> **The incident.** `ImageFileValidator` was a 3–4 byte magic-prefix check over three shipped
> pipelines; `SaveOrderPhotos` read its stored type off the client's own `data:` URI prefix; every
> employee-document intake stored the string its uploader claimed. None of it was a violation of
> S1–S11 — **S4 governs DTO fields, S6 governs logs, S8/S10 govern query scoping, and none of them
> reaches inside a byte array.** The reviewers were not wrong against the rules; the rules were silent.

**Enforcement, per ADR-0032 — enforcer named, tier declared, per clause:**

| Clause | Enforcer | Tier |
|---|---|---|
| Q2 (served type) | `ServedContentTypeTests` (26 cases), `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`, `SasResponseHeaderOverrideTests` — all in `Cleansia.Tests`, a named step of `backend-ci.yml:70-71` | **T1-CI** — exists today |
| Q2 (accept = serve) | new: assert every `Constants.ImageSignatures` MIME resolves to a non-`Opaque` `ServedContentType` | **`(gate pending: T-0458)`** |
| Q1 + Q3 (audience + scrub declared) | `Base64UploadIntakeRosterTests`, widened per D6 | **`(gate pending: T-0458)`** — the un-widened roster exists and is T1-CI, but it does not yet assert the new columns |
| Q3 (the scrub works) | per-pipeline tests reading metadata back out of **the bytes handed to the blob client** | **`(gate pending: T-0459)`** |
| The no-decode prohibition | new: walk `src/**/*.csproj` for a package-reference denylist (`SixLabors.*`, `SkiaSharp*`, `System.Drawing.Common`, `Magick.NET*`) with a `>= 20` project-count non-vacuity floor, per the `WebSdkContentGlobTests` shape | **`(gate pending: T-0458)`** |

**Do not label this rule `T1-CI` wholesale.** One clause is enforced today; four are specified and
ticketed. `enforcement.md:177-179` provides `(gate pending: <ticket>)` for exactly this, and a rule
that claims a gate it does not have is the defect ADR-0032 exists to stop.

### D8 — Scope of "artifact": images are scrubbed, documents are **declared** — and the exclusion is named

T-0460 asks how wide "artifact" goes. Ruling: **the law covers every user-supplied artifact; the
*scrub* covers images only, and the exclusion is written into the rule rather than left to inference.**

- **PDF metadata is not stripped.** Doing it right is an object-graph rewrite of the document catalog
  (`/Info` plus XMP in a metadata stream, plus incremental-update history), which is the PDF equivalent
  of the decoder D2 just refused. Doing it wrong corrupts a cleaner's contract scan.
- **The exposure it leaves is small and asymmetric:** an employee document's audience is that cleaner
  and an admin who already holds the cleaner's legal name, tax id and payout details. The metadata
  discloses to the one party that already has more.
- **It is served as an attachment, byte-typed, and never by URL** — so it also cannot be rendered.
- **Recorded, not silent:** the roster row reads `scrub: none — PDF object-graph rewrite refused, see
  ADR-NNNN D8; audience: staff`.

**Office formats (DOC/DOCX) are accepted and carry revision history and author names.** Same ruling,
same reason, and the honest statement is that this is the weakest point of D8. If the panel wants that
closed, the cheap answer is **refusing DOC/DOCX** (they are a convenience on a document-scan path where
PDF/JPEG/PNG serve every real case), not building an OOXML rewriter — but that is a product decision,
so it is **escalated rather than decided here** (§Escalations).

### D9 — No backfill obligation, and the reason is structural

S12 binds **new uploads**. It does not oblige an audit or a rewrite of stored artifacts:

- The *type* half needed no backfill because it was fixable on the read path (T-0464's whole argument).
- The *content* half **cannot** be fixed on the read path (D3), so a backfill is a real data migration:
  enumerate two containers, download, rewrite, re-upload, with an owner-run step and its own risk.
- Its scope is bounded and shrinking — blobs uploaded before PR #154 (2026-07-26, both mobile clients
  re-encode on pick) plus web uploads since.

**File it as its own ticket after the panel; do not let the rule mandate it by implication.** Both
T-0458 and T-0459 already exclude it and this ADR agrees.

### D10 — The web clients re-encode on pick, and this ships **first**

`readFileAsDataUrl` (`profile.models.ts:59-66`) and the order-photo/dispute pickers hand the raw file
straight to base64; no `canvas`/`createImageBitmap` appears anywhere in `src/Cleansia.App`. Both mobile
clients already re-encode every pick (`CleansiaCore/.../Media/ImageCompressor.swift`,
`cz.cleansia.core.media.ImageCompressor`), which is why they cannot be a source.

Making web match mobile is **~30 lines per picker, zero server cost, and removes essentially all live
metadata volume**, because — per §Context — for metadata the uploader is the victim, not the
adversary, so a client-side strip protects the person it needs to protect.

**It is a complement, not a substitute** (an old client, a future integration and a careless
third-party caller remain), but it is where the exposure/effort ratio is best, and **it must not be
sequenced behind the server work.** T-0459's ordering assumes the opposite.

---

## Alternatives considered

**A1 — Decode + re-encode with `ImageSharp` (T-0458's presumed answer).** Rejected: §D2. It creates the
only user-driven decoder in the system on a 1.75 GB shared plan behind a 30-item array; it carries a
licence question that is legal rather than technical; it is lossy on evidentiary photos; and it does
not generalise to the format carrying the most metadata here. **What it gets right, conceded:** it is
the only option that removes *everything*, including containers D2's walk does not know about. D2 is
narrower and says so.

**A2 — `SkiaSharp` instead.** Same rejection as A1 plus native binaries to validate against the Linux
App Service image and the Functions host. The library question is downstream of the re-encode question
and never becomes live.

**A3 — Do nothing server-side; ship D10 alone.** The strongest alternative, and **partially adopted**:
D10 ships first and independently. Rejected as the *complete* answer on one ground only — the upload
surface has grown from 10 routes to 14 and each new one gets a new client, so a per-client obligation is
the shape that already failed twice here (which is why `Base64UploadIntakeRosterTests` exists). A
challenger who argues D10 is sufficient is arguing against the *durability* of the fix, not its
correctness, and that is a legitimate position the panel should force me to answer.

**A4 — Strip specific EXIF tags (GPS IFD, `Make`, `Model`, serials, `MakerNote`) rather than dropping
the whole segment.** Rejected, narrowly. It is precisely the threat and it preserves orientation for
free — but it requires a full EXIF/TIFF IFD parser with offset rewriting, which is more attacker-facing
parsing code than "drop segment, re-emit minimal orientation." Against an adversarial file, the smaller
parser wins. **Revisit if** D2's minimal-EXIF emitter proves harder than expected.

**A5 — Strip metadata in a background job after upload.** Rejected: the blob is fetchable via SAS
between upload and sweep, and the job would have to rewrite a blob other requests may hold URLs to.
A synchronous byte walk costs less than the coordination.

**A6 — A `nosniff` header / storage-account CORS instead of any of this.** Not an alternative to
metadata removal (different threat), and out of scope: it is a Bicep change, already noted on
T-0464 §Out of scope. Worth stating that **no host sets `X-Content-Type-Options: nosniff`**
(`patterns-backend.md:1306`) and real Azure does not send one on SAS fetches (T-0464 status log,
2026-08-01, owner-verified on DEV) — which is why the closed served-type set, not a header, is the
control.

**A7 — Extend S4 rather than add S12.** Rejected: §D7. Same principle, different check, and
discoverability decides it.

---

## Consequences

- **The platform keeps its strongest current property — no user image is ever decoded server-side —
  and that property becomes a written prohibition instead of an accident.**
- Order photos and dispute evidence stop carrying capture metadata to the counterparty. The avatar does
  not, **by a recorded decision with a named expiry**, not by omission.
- A word disappears from this area: there is **no sanitizer**. There is a bound, an accept set, a
  server-truth type, a served-type clamp, and — on two surfaces — a metadata scrub. T-0458's and
  T-0459's titles are now wrong and should be renamed by the PM.
- The intake roster becomes the single place that answers "how many upload surfaces are there, who
  fetches each, and what does each do about content" — 14 rows, three columns.
- **The residue this ADR knowingly leaves:** PDF and Office metadata (D8, declared); already-stored
  blobs (D9, ticketed separately); malware inside a permitted container (never in scope, and
  `DocumentContentType:22-26` already says so).
- If only part of this ships, **D10 is the part that reduces exposure** and D2/D4 are the part that
  makes it durable. Shipping D2/D4 without D10 leaves the live volume untouched for a sprint.

## How a reviewer verifies compliance

1. `grep -rE "SixLabors|SkiaSharp|System.Drawing|Magick" src --include=*.csproj` returns **nothing**,
   and the D7 denylist test reddens when a reference is added (mutate: add one to a test project).
2. `Base64UploadIntakeRosterTests` lists **14** rows, each with validator | audience | scrub. Adding a
   new upload action reddens it; the four `byte[]`/`IFormFile` rows are present.
3. Every MIME in `Constants.ImageSignatures` resolves to a non-`Opaque` `ServedContentType`; BMP and
   TIFF are absent; the WebP signature checks `WEBP` at offset 8.
4. For `SaveOrderPhotos`, `UploadOrderPhoto` and `UploadDisputeEvidence`, a test reads EXIF back out of
   **the bytes handed to the blob client** and finds none — **not** an assertion that a helper was
   called. Each goes red when its own call is removed (three distinct mutations, three named tests).
5. An input whose EXIF `Orientation` is 6 produces output that still renders rotated the same way —
   asserted on the emitted bytes, not visually.
6. `UpdateCurrentUser` does **not** call the scrub, and the roster row says `audience: self`. Any diff
   that adds an avatar `BlobUrl` to a non-self DTO must change that row in the same change.
7. `security-rules.md` carries S12, its header reads **S1–S12** (it reads "S1–S10" today while S11
   exists — already stale), the audit checklist gained an item, and a
   `grep -rE "S1[-–]S1[01]" agents/ .claude/` sweep is in the PR body with results (T-0460 AC4).

## Escalations (owner)

**Q-ART-01 — Do we keep accepting DOC/DOCX on employee documents?** They carry author names and
revision history, no scrub is proposed (D8), and an OOXML rewriter is not worth building. Dropping them
would leave PDF/JPEG/PNG, which cover every real document-scan case — but it narrows what a cleaner may
upload and changes a five-locale string that promises "Accepted: PDF, JPEG, PNG, DOC, DOCX"
(`DocumentContentType:36-42`). **Product decision, not an architecture one.** To be filed in
`questions/open.md` by the panel lead if D8 survives otherwise intact.

## Challenge (author-run — NOT a panel; an independent round is owed)

**C-1 — "You refused a decoder on a resource argument, and you did not measure anything."**
Sustained as a caveat and handled by construction rather than by measurement: D2's alternative has **no
decode step at all**, so the resource argument does not have to be quantified to be avoided. The claim
that *would* need measuring — "a segment walk is negligible" — is marked ⚠ and owed by T-0458 AC7 on
the 30-item batch. A challenger should still press whether ImageSharp's header-only `Identify` +
dimension gate would have been an adequate mitigation; my position is that it makes the decoder *safe*
without making it *necessary*, and D2 only has to show it is unnecessary.

**C-2 — "D2 hand-rolls format parsers. Parsing attacker-controlled binary is what libraries are for."**
The strongest challenge and only partly answered. A JPEG segment walk and a PNG chunk walk are
length-prefixed, ~100 lines each, and read forward only — categorically less parsing surface than a
decoder — but they *are* new attacker-facing code and a bug there is a bug in the security control. My
answer: bound the risk by construction (never seek backwards; treat any malformed length as "refuse,
do not repair"; fuzz-style table tests over truncated/garbage segments), and note that the alternative
is not "no parser" but "a much larger third-party parser plus a decompressor." **An independent
challenger should decide whether that answer is good enough, because I have an interest in it.**

**C-3 — "The avatar exemption is one PR from being wrong, and 'we wrote it on a roster' is how every
forgotten obligation was documented."**
Partly sustained. The mitigation is that the roster row is asserted by a T1-CI test, so adding an
avatar URL to a cross-user DTO cannot land without touching the row — but the test asserts the *string*,
not the *fact*, so a developer can update the annotation without doing the work. **The honest options
are: (a) accept it, (b) scrub the avatar anyway for ~one extra call site.** I chose (a) because the
avatar is the single-item path whose cost nobody has measured, and because scrubbing a surface with no
fetcher is exactly the "build machinery to be safe" the ticket brief warns against. A challenger may
reasonably rule (b) — it is cheap and it removes the whole class.

**C-4 — "T-0460 asked for a rule and you produced a four-clause rule with a table. That is four rules."**
Not conceded, but flagged for the lead. The four clauses share one check — *walk the roster row* — and
three of them already describe shipped behaviour that is currently written only in `patterns-backend.md`
as a backend convention. If the lead rules they are separable, the natural split is
**S12 = the disclosure law (Q1 + Q3)** and **the served-type clause promoted into S4 as a DTO-adjacent
sentence**, which I would accept; I would not accept splitting the no-decode prohibition out, because
it is the reason clause 3 takes the form it does.

**C-5 — "D10 makes the rest optional. You conceded the uploader is the victim, not the adversary."**
The challenge I most want an independent instance to press. My defense is durability, not correctness
(A3), and durability arguments are the easiest to inflate. If the panel rules D10 sufficient, the
correct output is: ship D10, ship D5 and D6 (both cheap and independent), ship D7's rule with the
avatar/photo audiences recorded, and **defer D2/D4 with a written trigger** — and that would be a
legitimate ADR, not a defeat.

**Not self-challenged; start here.** Whether `UploadOrderPhoto` should be **deleted** rather than
hardened — it duplicates `SaveOrderPhotos` for a single photo, has no web caller I found, and R1 is
entirely its fault; whether `DisputeEvidence` should record a byte-derived content type rather than
deriving from the client's file name at read time; and whether the four `byte[]`/`IFormFile` routes
should be converted to `BlobFileDto` so one roster predicate and one validator family covers everything
(which would delete R1 and half of D6 instead of enforcing them).

## Verdict — LEAD, 2026-08-06

**Adjudicated.** Panel: author (this document, 2026-08-05) · challenger (threat-model / subject-of-the-
metadata lane, `../challenges/NNNN-user-artifact-content-policy-threat-model.md`, 2026-08-06) · lead
(this section, architect instance distinct from both). `process/deliberation.md` step 5.

**Outcome: REVISE. Not `accepted`. No further challenge round is required.** Every finding is either
sustained-with-its-repair-ruled-here or defended; none is left open for another instance to decide. Rev
N+1 is a **transcription job against the closed list in §C**, not a re-deliberation — the author (or
whoever the PM assigns) applies §C, and the PM checks rev N+1 against §C **only**, then accepts. Sending
this round again would cost exposure time (T-0459's surfaces are cross-user visible today) and buy
nothing, because the challenger's own closing position is *"re-based and with the reasons replaced, I
would not block it"* and the lead independently re-verified every blocking fact at HEAD.

**The one-line summary of the panel: this ADR reached the right answers on a stale map, and defended
several of them with reasons that are false. The answers stand. The reasons are rewritten.**

### A. Per-finding ruling

Every "stands" below was **re-verified by the lead at HEAD**, not taken from the challenge document.

| # | Finding | Ruling | Reason (one line) |
|---|---|---|---|
| **CH-1** | D5/D6 shipped, R1/R2/R3 closed, ~7 dead citations | **STANDS** | Verified: `SniffedContentType.cs:66-78` carries no BMP/TIFF and matches WebP as `RIFF`@0 + `WEBP`@8; `UploadIntakeRosterTests.cs:39-55` is 14 rows; `UploadOrderPhoto.cs:102` and `UploadDisputeEvidence.cs:104` both sniff; `GetOrderPhotos.cs:96,105` resolves once; `grep ImageSignatures src/` → **zero files** |
| **CH-2(a)** | Uploader ≠ capturer; provenance is unknowable | **STANDS** | No intake establishes capture provenance — `SaveOrderPhotos.cs:114-117` proves *assignment*, which is an authorization fact; unrebutted |
| **CH-2(b)** | Dispute evidence has an adversarial uploader | **STANDS** | `UploadDisputeEvidence.cs:95-99` — the uploader **is** the dispute's own customer, and the outcome is money; D4 already called forgeable EXIF timestamps a "benefit" while the ADR's premise denies the adversary exists |
| **CH-2(c)** | Order-photo audience is wider than the table says | **STANDS** | `GetOrderPhotos.cs:59` gates on `CanBrowseOrderAsync`; `OrderAccessService.cs:68-92` returns `true` for **any** tenant `Employee` while `HasAvailableSpots && NotHeldFrom`. The "load-bearing table" is materially wrong |
| **CH-2(d)** | No admin-side document upload exists | **NO FINDING** | Confirmed by the roster: all four document intakes are Partner / Mobile.Partner (`UploadIntakeRosterTests.cs:45-46,50-51`). Recorded as checked; D8 gains the expiry line |
| **CH-3(i)** | "10 MiB × 30" is unreachable | **STANDS** | Kestrel's 30,000,000 B ceiling bounds a request to ≈21 MiB decoded (`request-intake-limits.md:26-42`, the ADR's own cited companion). The conclusion is unaffected — one ~300 KB PNG already suffices — so the figure is spending credibility for nothing |
| **CH-3(ii)** | CPU-only autoscale strengthens D2 | **ACCEPTED (not a challenge)** | Verified: `appServicePlan.bicep:70,88` are both `CpuPercentage`; a decoder fails on **memory**, so scale-out never fires, and `:22` states the plan carries the 5 APIs + SSR + Functions. DEV is B2 with autoscale **off** (`weu.dev.bicepparam:26`) — one fixed instance, and DEV is live |
| **CH-3(iii)** | The denylist enforcer cannot see the real failure mode; Skia is already deployed | **STANDS** | Verified: `Cleansia.Infra.Services/obj/project.assets.json:832-864` ships `libQuestPdfSkia.so` for all three Linux RIDs and `:2362-2368` the libjpeg-turbo/libpng/libwebp/skia licences. The prohibition is a **reachability** property; a `PackageReference` name-denylist cannot express it, and the ADR holds others to exactly this standard in its own §D7 |
| **CH-3(iii) sec.** | The licence limb is self-inconsistent | **STANDS** | The repo already ships one revenue-threshold-licensed graphics package. "Licence" cannot disqualify ImageSharp without an established legal finding this ADR does not have and may not make |
| **CH-4** | The pilot surface has no server-truth type to dispatch on | **STANDS — repair ruled in §B.1** | Verified: `SaveOrderPhotos.cs:171-184` reads the client `data:` prefix, else the client extension, else the literal `"image/jpeg"`; the roster records the exception in writing (`UploadIntakeRosterTests.cs:47,52`) |
| **CH-5** | Generation loss disqualifies A1, then D10 adopts it harsher | **STANDS — position picked in §B.2** | Verified: `ImageCompressor.swift:31-32` is `maxDimension: 1920, quality: 0.7`, and `:80-82` writes *only* the quality key. The draft holds both positions at once |
| **CH-6** | A4's rejection assumes a parser D2 also needs | **STANDS — rejection restated in §B.3** | D2's *"re-emit a minimal EXIF `APP1` carrying only `Orientation`"* requires reading the TIFF byte order, IFD0 offset and entry table to find tag `0x0112`. That **is** the IFD reader A4 was rejected for needing. The stated distinction is false |
| **CH-7** | D8's PDF exclusion is evadable and imports its justification | **STANDS** | `AcceptedByIntake[DisputeEvidence]` includes `application/pdf` (`SniffedContentType.cs:92-95`); D8's *"already has more"* is an employee-document sentence applied to a customer→cleaner→staff triangle where it is true of nobody |
| **CH-7 sec.** | Dispute PDFs are served **inline**, not `attachment` | **STANDS** | Verified: `BlobContainerClient.cs:93-110` sets `ContentType` and `CacheControl` and **no** `ContentDisposition`. D8's *"served as an attachment … never by URL"* is false for this surface |
| **C-1** (author self) | Refused a decoder without measuring | **DEFENDED** | D2's adopted option has no decode step, so the resource cost never has to be quantified to be avoided. The claim that *is* owed a measurement (the segment walk) is marked ⚠ and ticketed |
| **C-2** (author self) | D2 hand-rolls parsers over attacker binary | **DEFENDED, conditionally** | Sustained as a real cost, answered by construction — forward-only, length-prefixed, *refuse-never-repair*, and (new, §B.3) **no attacker byte reaches the output**. Condition: the §B.4 degradation rule and the synthetic-corpus burden are written into rev N+1 |
| **C-3** (author self) | The avatar exemption is one PR from wrong | **DEFENDED** | The challenger independently verified the exemption is correct on the facts (`GetCurrentUser.cs:44,47-60` is the only `user-files` SAS mint; `UserMappers.cs:23,66` / `EmployeeMappers.cs:37,63` carry no URL; `GdprExportDto.cs:85-90` carries names, not bytes) and **could not improve on the author's own mitigation**. Option (a) stands |
| **C-4** (author self) | Four clauses is four rules | **DEFENDED** | The clauses share one check — *walk the roster row*. The lead declines the proposed split: promoting the served-type clause into S4 would put a bytes-question under a rule whose check is "read the DTO's field list", which is the exact discoverability failure D7 exists to fix |
| **C-5** (author self) | D10 makes the rest optional | **CONCEDED IN PART — see §B.5** | Sustained for order photos, **refused for dispute evidence** (CH-2b): a client-side strip is not available against an uploader whose interest is to not strip. The deferral becomes per-surface |

**Zero blocking challenges remain.** Consensus is declared on the *rulings*; the revise verdict is about
the record, not about the answers.

### B. Rulings the lead makes (these are decisions, not suggestions — rev N+1 transcribes them)

**B.1 — CH-4: the scrub selects its parser from the bytes it is handed. Full stop.**
The challenger offered the author a choice of (a) the scrub sniffs its own bytes or (b) `SaveOrderPhotos`
adopts `SniffedContentType.FromContent(…, UploadIntake.OrderPhoto)` first. The sibling lane
(`drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md` D1) has independently decided (b)
and asks this ADR not to re-decide it. **Agreed — and the lead rules the strictly stronger form that is
robust to that lane's outcome:**

> The metadata scrub determines the container format **from the bytes it is holding**, at the moment it
> runs. It never reads a client-supplied string, and it never reads a persisted `ContentType` field —
> not even a correct one. A format it cannot identify is **passed through untouched and reported as
> "not scrubbed"**, never as "scrubbed".

This satisfies the sibling lane's constraint (*"T-0459 must not dispatch on `OrderPhoto.ContentType`"*)
by never dispatching on it at all, and it does **not** undercut D1: calling an existing intake helper is
not building a shared transform abstraction. **One decision per ADR is preserved** — this ADR rules what
the *scrub* dispatches on; the sibling rules what the *row* stores. The sibling's closing ticket is owed
on its own grounds (a stored type that disagrees with its bytes, the `"image/jpeg"` fabrication, and the
unguarded `Convert.FromBase64String` at `SaveOrderPhotos.cs:136`), and **T-0459 is not gated on it.**

**B.2 — CH-5: generation loss is NOT an A1 rejection ground. Drop that limb, and drop the licence limb.**
The platform already re-encodes the majority of order photos at q0.7 with a 1920 downscale, on the
clients, deliberately. A server-side re-encode at q0.9 with no downscale would be *less* destructive. An
ADR may not reject an option for a cost it is simultaneously shipping. **A1's rejection is
over-determined and loses nothing:** it stands on the resource limb (a user-driven decoder on a request
path, on a memory-blind autoscale, on a plan carrying seven sites) and the PDF-generality limb. Deleting
the two weak limbs makes the rejection *stronger*, because both are falsifiable from this repo.

**B.3 — CH-6: A4's rejection is restated, not re-scored.** The parser-size argument is conceded — D2 and
A4 both need an IFD reader. A4 is still rejected, on the two grounds that actually distinguish them:

1. **Allowlist vs denylist.** D2 emits only the one tag it chose. A4 removes only the tags it thought
   of, and that list ages every time a vendor invents a `MakerNote` variant. For a *disclosure* control
   the default must be "drop unless named", not "keep unless named".
2. **No attacker byte reaches the output.** D2's emitted `APP1` is server-synthesized end to end — fixed
   one-entry IFD, server-computed offsets, one value validated into 2–8. A4 re-emits attacker-chosen
   bytes with rewritten offsets, and offset arithmetic over attacker-chosen values is precisely where
   C-2's worry becomes a defect.

This also answers C-2 better than the draft does, and rev N+1 should say so there too.

**B.4 — EXIF `Orientation`: the policy is completed with its degradation direction.** The draft names the
mechanism and never says what happens when it fails, which is what makes it incomplete against the
INDEX's fourth item. Ruling:

> Orientation is preserved **if and only if** the source `APP1` can be read unambiguously and yields a
> value in 2–8. Anything else — a malformed IFD, an unexpected byte order, a value out of range, a
> truncated segment — emits **no EXIF at all** and accepts the rotation. Never guess, never repair.

The safe direction is deliberate: a photo that ships rotated is a visible cosmetic defect on a rare and
largely adversarial branch; a corrupted photo or a surviving GPS tag is not. CH-5's second half is
sustained as the reason this needs a **synthetic corpus**, not production exercise: once D10 lands, every
client the platform controls bakes rotation into pixels and emits no EXIF at all, so this branch will run
almost exclusively for residual old-client and third-party traffic. Rev N+1 carries that as an explicit
test burden on T-0458 (truncated segments, garbage lengths, both byte orders, orientation 1 / 2–8 / 9 /
absent), and it is the answer to *"new attacker-facing code with near-zero production exercise."*

**B.5 — C-5 / CH-2(b): the deferral option becomes per-surface.**

| Surface | May D2/D4 be deferred behind D10 with a written trigger? |
|---|---|
| `SaveOrderPhotos` / `UploadOrderPhoto` | **Yes** — the argument there is durability, and D10 genuinely removes the live volume |
| `UploadDisputeEvidence` | **No** — the uploader is the dispute's own customer with money on the outcome (`:95-99`). "The client strips it" is not a control against a party whose interest is to not strip. Here the server-side scrub is *enforceability*, not durability |

The challenger's own severity bound is recorded and does **not** change the ruling: the dispute exposure
is **latent** (no surface reads EXIF today — `DisputeEvidenceDto` carries `FileName` / `BlobUrl` /
`UploadedOn`, which is server time), so this bounds the *urgency*, not the *availability of the
deferral*.

**B.6 — CH-3(iii): the no-decode prohibition is re-tiered honestly, and the "nothing decodes" claim is
corrected.** The **fact** survives — there is no call site (`.Image(` / `ImageDescriptor` /
`Image.FromBinaryData` return zero across `src/**/*.cs`, independently checked by two instances). The
**framing** does not: a complete JPEG/PNG/WebP decoding stack is already on the Linux image via
QuestPDF's native assets, so §3's *"nothing here decodes an image"* must become *"nothing here **calls**
a decoder"*. Enforcement:

| Clause | Enforcer | Tier |
|---|---|---|
| No **direct package reference** to a decoder | `.csproj` denylist walk + non-vacuity floor | `(gate pending: T-0458)` → `T1-CI` when it lands |
| No **call site** reaching a decoder (incl. transitively-shipped QuestPDF image APIs) | source scan of `src/**/*.cs` for `.Image(` / `ImageDescriptor` / `Image.FromBinaryData`, non-vacuity floor | `(gate pending: T-0458)`; **if T-0458 cannot build it, the clause is declared `T2-ADVISORY` in the ADR with a named reviewer check** — not left labelled as a gate |
| Roster **widening** to 14 rows | `UploadIntakeRosterTests` | **`T1-CI` today** — only the two new columns are `(gate pending: T-0458)` |
| Accept ⊆ serve | true **by construction** today (one `Signatures` table + `AcceptedByIntake`) but **unpinned** — `ServedContentTypeTests` carries no such assertion | `(gate pending: T-0458)`, for that reason, not the draft's |

A mechanism that cannot fail a build is `T2-ADVISORY` however it is labelled (ADR-0032,
`enforcement.md`). The draft says this about others in §D7 and must apply it to itself.

### C. Closed list — what rev N+1 must change (nothing else is reopened)

1. **Re-base §Context.** Delete R1/R2/R3 (closed) and delete or demote **D5** and **D6** to
   *ratifications with HEAD citations*. Replace every dead citation: `Constants.ImageSignatures` →
   deleted; `DocumentContentType` → `SniffedContentType`; `Base64UploadIntakeRosterTests` →
   `UploadIntakeRosterTests`; `GetOrderPhotos.cs:75` → `:96,105`; `UploadOrderPhoto.cs:112` → `:102`;
   `DisputeMappers.cs:65-77` → `UploadDisputeEvidence.cs:104-105`.
2. **Fix the audience table** per CH-2(c), and **replace D4's stated reason**: order photos are scrubbed
   because the audience is **not enumerable at upload time**, not because it is three known parties.
   Name what is disclosed that the DTO withholds — device identity and off-site location, a stable
   cross-order correlation key that walks through `GetOrderPhotos.cs:107-109` and ADR-0036.
3. **Scope the threat-model premise to the avatar.** Add CH-2(a) (provenance is unknowable) and CH-2(b)
   (adversarial uploader on dispute evidence), and record the latency bound from §B.5.
4. **Apply §B.5** — the deferral table, per surface, replacing C-5's binary.
5. **Apply §B.6** — drop "10 MiB × 30" (restate as *"one bounded upload already suffices; the array cap
   is irrelevant to the argument"*), correct "nothing decodes" → "nothing calls a decoder", add CH-3(ii)
   (CPU-only autoscale, seven sites, DEV single-instance), and replace the §D7 enforcement table with
   §B.6's.
6. **Apply §B.1** as an explicit clause of D2/D3, citing the sibling draft and stating that T-0459 is
   **not** gated on the sibling's closing ticket.
7. **Apply §B.2** — delete the generation-loss limb **and** the licence limb from A1/A2; state in one
   sentence that the licence question is **not live because no library is adopted**, and that it becomes
   an owner/legal question in the ADR that ever overrules D2.
8. **Apply §B.3** — restate A4's rejection; carry the same two grounds into the C-2 answer.
9. **Apply §B.4** — the orientation degradation rule and the synthetic-corpus test burden.
10. **Scope D8 per surface.** The dispute-evidence PDF exclusion needs its own written reason (the
    employee-document one is false there), plus CH-7's correction that dispute PDFs are served **inline**
    with no `rscd`, plus CH-2(d)'s expiry line, plus one sentence in the threat table acknowledging that
    the closed served-type set admits a **scriptable container** even though stored XSS stays closed.
11. **Carry the deliberation trail.** `## Challenge` must cite
    `../challenges/NNNN-user-artifact-content-policy-threat-model.md` as the panel round (the existing
    §Challenge stays, relabelled as the author's self-challenge); add a `## Defense` recording
    rebut/concede per finding; keep this §Verdict.
12. **Do not re-title this ADR to cover shipped work.** Its one decision is *metadata is scrubbed at
    intake, by audience, without a decoder*. Everything else in it is ratification or context.

### D. The four things T-0458's INDEX row demanded the panel rule on

| # | Demanded | Answered? | By what |
|---|---|---|---|
| 1 | **The library** — ImageSharp licence is legal, not technical | **YES — by removing the dependency** | D2 adopts **no** library, so the licence question never becomes live. **Fallback, and rev N+1 must state it (§C.7):** if a future ADR overrules D2 and adopts a decoder, the licence is an **owner/legal** question filed at that time — the architect does not rule it. The draft's use of it as a *rejection ground* is deleted per §B.2 |
| 2 | **Strip vs re-encode** | **YES** | D2: **strip** by container rewrite; re-encode refused. Survived the challenge — the challenger's §"could not break" item 6 sustains the central ruling explicitly, and §B.6 strengthens it |
| 3 | **Seam location** (a validator cannot mutate) | **YES** | D3: **intake, in the handler, between the decode and `UploadAsync`.** Validator ruled out for exactly the INDEX's reason — validators reject, they do not transform. Also ruled out: a decorator on `IBlobContainerClient.UploadAsync` (that sink writes our **own** generated invoices/receipts/GDPR exports) and the read path (a SAS hands the client the stored bytes). D1 refuses the `IImageSanitizer` seam; §B.1 adds what the seam dispatches on |
| 4 | **EXIF `Orientation`** | **NOW yes — it was not, in rev N** | The draft named the mechanism and never named its failure behaviour, which is what left the policy incomplete. **§B.4 completes it** (preserve iff unambiguously readable, else emit no EXIF and accept the rotation; never guess, never repair) and **§B.3** replaces the reasoning that chose this mechanism over A4 |

### E. Ticket consequences (PM)

- **T-0458** — stays `blocked` until rev N+1 is accepted. **AC6 is overturned** (pilot moves off the
  avatar). **AC1 must be re-worded**: it asks for *"the library + its licence position"*; the ruling is
  *no library*, and the licence position is explicitly **not** the architect's to state. **AC5** cites
  `errors.*` — wrong namespace, it is `api.*` (`CLAUDE.md` §i18n). New AC owed for §B.4's corpus and
  §B.6's two enforcers.
- **T-0459** — **partially unblocked by this ruling. See §F.**
- **T-0460** — the S12 text survives (C-4 defended); its enforcement table is replaced by §B.6, and its
  Q3 clause gains §B.1's "from the bytes it is holding".
- **Titles.** T-0458/T-0459 both say "sanitizer". There is no sanitizer. Rename at the PM's discretion.

### F. Is T-0459 unblocked?

**Partially — and the split is worth having, because two of its three handlers can start immediately.**

| T-0459 handler | Blocked? | Why |
|---|---|---|
| `UploadDisputeEvidence.cs` | **NO — start now** | Already byte-derived (`:104`). §B.5 rules the scrub here is **not deferrable**, so it is the most decision-complete of the three |
| `UploadOrderPhoto.cs` | **NO — start now** | Already byte-derived (`:102`) |
| `SaveOrderPhotos.cs` | **NO, under §B.1** | The scrub sniffs the bytes it holds, so it does **not** depend on the sibling lane's closing ticket. That ticket is still owed on independent grounds and should land first if the PM can sequence it, but it is not a gate |

The only true gate on T-0459 is **rev N+1 being accepted**, which is a transcription pass against §C.

### G. Escalation

**Q-ART-01 survives D8 and is now two-part** (CH-7 added the dispute-evidence half). Filed by the PM;
proposed text handed over with this verdict. It does **not** block: D8 is scoped per surface either way,
and the roster records `scrub: none` with its reason regardless of the owner's answer.
