# ADR-0043 — User-artifact metadata is scrubbed at intake, by audience, without a decoder

- **Status:** `accepted` — PM, 2026-08-06, per §Verdict §E: rev N+1 checked against §C only, no
  re-deliberation. Six citations spot-checked independently against HEAD before accepting
  (`OrderController.cs:164`, `UploadOrderPhoto.cs:112-121`, `UploadDisputeEvidence.cs:95-99`,
  `patterns-backend.md`, `GetOrderPhotos.cs:107-109`, `OrderMappers.cs:101-104`); all six hold,
  including the last, which is the one that *narrows* this ADR's own severity claim — the customer's
  full street address is already on `OrderListItem`, so job-site GPS is not new disclosure to a cleaner
  who can already read it. **T-0459 is unblocked by this acceptance.**
  > **One correction to this note, recorded rather than quietly fixed.** The catalog citation I checked
  > was `patterns-backend.md:1344-1347`, and a sibling lane edited that file *while I was checking* —
  > inserting seven lines at `:1308`. So the text I read at `:1344` was the paragraph that had moved
  > *into* those numbers, not the one this ADR cites. The ADR's citation was correct against the file
  > its author read; both it and this note now say `:1351-1354`, which is where that sentence lives
  > today. Five of the six spot-checks stand as taken; this one was re-taken.
- **Date:** 2026-08-05 (rev N, author) · 2026-08-06 (panel: challenge + verdict) · 2026-08-06 (**rev N+1**, this body)
- **Supersedes:** nothing. **Superseded by:** nothing.
- **Tickets:** **T-0458** (policy + seam), **T-0459** (application), **T-0460** (the S-series law).
  All three are re-scoped by this ADR — see §Context.
- **Applies to:** every upload surface on all five `Cleansia.Web.*` hosts
- **Consumes:** T-0464 / `ServedContentType` (the served-type clamp), T-0548 / T-0556 + follow-up
  (`BlobFileSize`, `SniffedContentType`, the count caps, the intake roster), ADR-0032 (a constraining
  catalog entry names an enforcer and declares a tier), ADR-0033 (this edit narrows a governing
  sentence → Architect), ADR-0036 (the preferred-cleaner hold, which the browse gate reads)
- **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md`

> ### Method declaration — read before relying on anything here
>
> **1. This is rev N+1.** A defense panel ran on 2026-08-06: author (rev N, 2026-08-05) ·
> independent challenger (`challenges/NNNN-user-artifact-content-policy-threat-model.md`) · lead
> (§Verdict), all distinct instances. Outcome **REVISE — the rulings survive, the map and several
> reasons do not.** This body applies §Verdict §C's closed list of twelve changes and **nothing else**;
> §Verdict is carried verbatim as the record of what was ruled and why. The lead ruled **no further
> challenge round is convened**.
>
> **2. Acceptance is the PM's step.** Per §Verdict, the PM checks this body against §C **only**, then
> accepts. Until that stamp lands the status above is `proposed`, and **rev N+1 being accepted is the
> only true gate on T-0459** (§Verdict §F).
>
> **3. No shell in either authoring invocation** (`Read`/`Glob`/`Grep`/`Write`/`Edit`; no `Bash`).
> Nothing was compiled, executed or measured. Every `file:line` below was **re-opened at HEAD for this
> revision**; citations that could not be re-verified were deleted rather than carried forward. Claims
> about **runtime** cost carry **⚠ not measured** and name who owes the measurement.
>
> **4. Three of T-0458's and T-0460's premises are stale at HEAD, and so were several of rev N's.**
> Both are restated below rather than inherited. **Do not read those tickets' §Context as current, and
> do not read rev N's citations as current** — roughly seven of them pointed at symbols that no longer
> exist.

---

## Context

### Gate 0 — what shipped under the tickets, and what this ADR therefore does *not* decide

T-0458 and T-0460 were filed 2026-07-30 from the T-0446 security gate. Four intake-hardening tickets
have landed since (T-0464, T-0548, T-0556 + follow-up). **Over half of what rev N framed as decisions
is now shipped code.** This ADR ratifies it and rules on the one thing that is still open.

| T-0458 asked for | State at HEAD | Evidence (re-verified 2026-08-06) |
|---|---|---|
| **A per-image size cap** ("there is no size limit anywhere") | **SHIPPED.** 10 MiB decoded, one shared predicate, derived from the **encoded** length so a rejection never decodes, and **first** in every `Cascade.Stop` chain | `Common/Validators/BlobFileSize.cs:8-9,17-28`; `SaveOrderPhotos.cs:76-81`; `UploadOrderPhoto.cs:61-68`; `UploadDisputeEvidence.cs:66-73` |
| **A per-request bound** (not asked for; found by T-0556) | **SHIPPED.** 10 for both document arrays, **30** for `SaveOrderPhotos.Photos`, each gating its own `RuleForEach` so a refused list is not decoded item by item | `SaveOrderPhotos.cs:46,57-85` |
| **Server-truth content type** ("a 3–4 byte magic prefix and nothing else") | **SHIPPED on 13 of 14 intakes.** One function answers *may we accept this* and *what is it*, from the bytes, off **one** signature table with a per-intake accepted set; the client's declared type and the extension are both discarded. **The fourteenth is `SaveOrderPhotos`** — see below | `Common/Validators/SniffedContentType.cs:66-78,88-104,106-150`; `UploadOrderPhoto.cs:102`; `UploadDisputeEvidence.cs:104-105`; `patterns-backend.md:1284-1322` |
| **Nothing can be served as `text/html` / `image/svg+xml`** | **SHIPPED.** A closed value type decides the served type on the **read** path, so it also fixes rows already stored; SVG is excluded beside `text/html` **by name** | `Core.Blobs.Abstractions/ServedContentType.cs:27,31-42,58-67`; applied via the SAS response-header override, `BlobContainerClient.cs:89-110`; legacy rows retype from the intake's own table, `SniffedContentType.cs:127-128` |
| **A roster so the next intake is not forgotten** (not asked for) | **SHIPPED, and wider than rev N knew** — **14** rows, each annotated with the rule guarding it, plus a second `[Theory]` naming the four non-`BlobFileDto` intakes so narrowing the predicate cannot silently pass | `Cleansia.Tests/Common/Validators/UploadIntakeRosterTests.cs:39-55,76-84,97-107` |
| **EXIF / metadata removal** | **NOT SHIPPED.** Nothing server-side removes anything from inside any artifact | — |
| **Resize / dimension bound / re-encode** | **NOT SHIPPED — and this ADR refuses it.** See D2 | — |

**Rev N's three residues R1, R2 and R3 are all CLOSED at HEAD**, and the symbol R3 was about no longer
exists (`grep -r ImageSignatures src/` returns zero files). They are not restated here; the living doc
keeps them struck rather than deleted, because the wrong half of a closed finding is what gets
re-derived from an old checkout (`agents/architecture/decisions/user-uploaded-artifacts.md` §2).

**Two residues survive and this ADR is about the first:**

- **R4 — Metadata.** No path removes EXIF/XMP/IPTC from an image, or `/Info`/XMP from a PDF. Nothing.
- **R5 — `SaveOrderPhotos` is the fourteenth intake and reads no byte of its payload.**
  `DetermineContentType` (`SaveOrderPhotos.cs:171-184`) takes the caller's `data:` URI prefix, else the
  caller's file extension, else the string literal `"image/jpeg"` (`:183`); the blob name's extension is
  `Path.GetExtension(file.FileName)` (`:132`), the caller's string. It is deliberate, recorded on the
  roster in writing (`UploadIntakeRosterTests.cs:35-38,47,52`) and pinned by a test
  (`SaveOrderPhotosContentTypeTests.cs:14-26,49-59`). **It is owned by a sibling lane**
  (`drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md`), not by this ADR — and D2's
  §B.1 clause below is written so that this ADR does not depend on that lane's outcome either way.

**T-0460's premise moved too.** It says the rule set is silent on "bytes inside a stored artifact."
Half of that is no longer true: the *served type* half is enforced by four `T1-CI` suites
(`ServedContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`,
`EmployeeDocumentDownloadDispositionTests`, `SasResponseHeaderOverrideTests` — all in `Cleansia.Tests`,
a named step of `backend-ci.yml:69-71`) and written into `patterns-backend.md:1284-1322`. What is still
unwritten is (a) that the pattern is a **law**, not a backend convention, and (b) the *content* half.

**So: T-0458 is largely satisfied and its remainder is a different decision than the one it framed;
T-0460 is half-satisfied in practice and unwritten as law.** Neither is closed; neither is what it says.

### Who actually fetches what — corrected

**Exposure is a property of the audience, not of the pipeline.** Rev N called this "the load-bearing
table" and it was **materially wrong on the order-photo row**. Corrected and re-verified:

| Surface | Uploaded by | **Fetched by** | How | Served as |
|---|---|---|---|---|
| **Avatar** (`UpdateCurrentUser`, 4 roster rows) | the user | **the same user, and nobody else.** `GetCurrentUser.ResolveProfilePhotoUrl` (`:44,47-61`) is the **only** SAS mint against `user-files` in production code (`UpdateCurrentUser.cs:160` writes; `GdprDeletionService.cs:134` deletes). `UserMappers.cs:23,66` and `EmployeeMappers.cs:37,63` map the photo **without** a URL, so every list/employee DTO carries no avatar URL. The GDPR subject-access dump carries file **names**, not bytes or URLs (`GdprExportDto.cs:85-90`) | 1 h SAS, `<img>` | `application/octet-stream` (the opaque overload) |
| **Order photos** (`SavePhotos` batch + `UploadPhoto` single, 4 roster rows) | a cleaner assigned to the order (`SaveOrderPhotos.cs:114-117`, `UploadOrderPhoto.cs:97-100`) | **customer + admin + assigned cleaners + ANY tenant cleaner who can browse the order while a seat is open.** `GetOrderPhotos.cs:59` gates on **`CanBrowseOrderAsync`**, not `CanAccessOrderAsync`: after owner/admin/assigned fails, `Authentication/OrderAccessService.cs:68-92` returns `true` for any caller with role `Employee` and a resolvable `employeeId` when `order.HasAvailableSpots && OrderVisibility.NotHeldFrom(order, employeeId, now)` — the comment at `:84-87` says that branch is *"both browse surfaces at once — order detail and order photos."* Exposed on **five** hosts (`Web.Partner/OrderController.cs:164`, `Web.Mobile.Partner:155`, `Web.Customer:137`, `Web.Mobile.Customer:137`, `Web.Admin/AdminOrderController.cs:48`) | 1 h SAS | closed-set typed (`GetOrderPhotos.cs:96,105`) |
| **Dispute evidence** (2 roster rows) | **the dispute's own customer** — the handler refuses unless `dispute.UserId == userId` (`UploadDisputeEvidence.cs:95-99`) | that customer + **admin staff adjudicating a refund** (`AdminDisputeController.cs:42,55`) | 1 h SAS, **inline** — the mint sets `rsct`/`rscc` and **no `rscd`** (`BlobContainerClient.cs:93-110`) | closed-set typed from the server-minted blob-name extension (`UploadDisputeEvidence.cs:104-105`, `DisputeMappers.cs:73-78`) |
| **Employee documents** (4 roster rows) | a cleaner | that cleaner + **admin** | **never by URL** — three API routes, all `File(bytes, type, name)` → `Content-Disposition: attachment` (`Web.Partner/EmployeeController.cs:125`, `Web.Mobile.Partner/EmployeeController.cs:179`, `Web.Admin/AdminEmployeeDocumentController.cs:92`) | byte-derived, `attachment` |

Three consequences, two of which the tickets get backwards:

1. **The avatar is not the pilot. It is the surface with no exposure at all.** T-0458 AC6 picks it
   *because* it is lowest-blast-radius — which is exactly why piloting there delivers zero exposure
   reduction while making the work look done. D4 overturns it.
2. **T-0460's hinge — "served back by URL" — is the wrong hinge.** Employee documents are *not* served
   by URL and carry the **most** metadata (PDFs and Office files carry author names and revision
   history). A rule keyed on the delivery mechanism excludes the worst case. D7 keys on audience.
3. **The order-photo audience is not enumerable at upload time.** Seats are
   `ceil(EstimatedTime / 120)` under a 24 h span cap — up to twelve (`CLAUDE.md` §"Seats and
   duration") — and *writing* requires assignment while *fetching* does not. Cleaner A's "before"
   photos are fetchable by cleaner B, who has not taken the job and never will. This, and not "three
   known parties", is why D4 scrubs them.

### The threat model, scoped to the surface it is true on

T-0458 argues the work must be server-side because *"a client-side strip is unenforceable — the server
cannot distinguish a stripped upload from an unstripped one."* That is decisive for XSS, where **the
uploader is the adversary**. Rev N generalised the inverse — *"for metadata the uploader is the
victim"* — across all four surfaces. **That generalisation does not hold, and this revision scopes it
to the avatar.** Three reasons, in increasing severity:

- **(a) The uploader is not the capturer, and nothing establishes that they are.** EXIF is written by
  the capturing device, not by the uploading account. A cleaner may upload a photo a colleague sent
  them; a customer may upload a photo forwarded to them. No intake in this codebase establishes
  provenance — `SaveOrderPhotos.cs:114-117` and `UploadOrderPhoto.cs:97-100` prove **assignment**,
  which is an authorization fact, not a capture fact. So "the uploader is the victim" is not merely
  false against an adversary; it is **unknowable in the ordinary case**, and the platform is not in a
  position to assert it.
- **(b) On dispute evidence the uploader is an adversary with money on the outcome.**
  `UploadDisputeEvidence.cs:95-99` refuses unless the uploader **is the dispute's own customer**; the
  counterparty is a cleaner and the outcome is a refund (`AdminDisputeController.cs:42,55`). Against
  that uploader, "the client strips it" is defeated by one request carrying the caller's own valid
  token. **Here the server-side scrub is *enforceability*, not durability** — which is why D4's
  ruling on this surface is not deferrable (see the deferral table under D10).
  *Severity bound, recorded so it is weighed correctly:* no surface reads EXIF today —
  `DisputeEvidenceDto` carries `Id`/`FileName`/`FilePath`/`BlobUrl`/`UploadedBy`/`UploadedOn`
  (`DisputeEvidenceDto.cs:3-10`), and `UploadedOn` is server time — so a forged `DateTimeOriginal` is
  only reachable by an adjudicator who downloads the file and inspects it. **This is latent, not live.
  It bounds the urgency; it does not restore the premise, and the premise is what the sequencing rests
  on.**
- **(c) On order photos the disclosure is to parties with no relationship to the job.** Stated
  precisely, because the obvious version of it is wrong: the customer's street address and lat/long
  are **already** on `OrderListItem` (`OrderMappers.cs:101-104,159-160`), so GPS taken *at* the job is
  not new information to a browsing cleaner. What **is** new is **the uploading cleaner's device
  identity** (`Make`/`Model`, body and lens serials, `MakerNote`) and, for any photo taken away from
  the job, **that cleaner's own location** — handed to an arbitrary other cleaner. A device serial is
  a **stable cross-order correlation key**, and it walks straight through the two controls that
  deliberately withhold cleaner identity: `GetOrderPhotos.cs:107-109` withholds
  `CapturedByEmployeeId` and the surname from a customer caller, and `PreferredEmployeeId` is never on
  a partner-facing DTO (ADR-0036). **This is the S12 argument in its purest form — a DTO-level control
  defeated by content.**
- **(d) It holds on the avatar, which is the surface D4 exempts.** The only fetcher is the subject, so
  **T-0446 disclosed nobody's EXIF to anyone.**

### The residual threats

| Threat | Live at HEAD? | Why |
|---|---|---|
| Stored XSS from a served artifact | **No** | Closed set on the read path; `text/html` and `image/svg+xml` absent **by name** (`ServedContentType.cs:31-42`); documents byte-typed and `attachment`. Pinned for the untyped intake too (`SaveOrderPhotosContentTypeTests.cs:32-47`) |
| **A scriptable container inside the closed set** | **Yes, and accepted** | The closed set admits `application/pdf` (`ServedContentType.cs:41`), and on the dispute path it is served **inline** — the SAS mint sets `rsct`/`rscc` and **no `rscd`** (`BlobContainerClient.cs:93-110`). This is *not* equivalent to stored XSS: the storage host carries no app session and browser PDF viewers are sandboxed, so the "stored XSS: closed" verdict survives. But the row above reasons only over `text/html` and `image/svg+xml`, and that is not the whole of the served set. Stated here rather than left to inference |
| Polyglot (valid JPEG **and** valid HTML) | **No** | Same clamp. A polyglot only matters if something serves it with an executing type; nothing can |
| Type confusion / extension rename | **No** on 13 of 14 intakes (byte-derived); the fourteenth is R5, owned by the sibling lane and contained by the read-path clamp | See R5 |
| Decompression bomb / pixel flood | **No — and adding a sanitizer creates it** | Nothing *calls* a decoder — see the next section |
| Malware inside a PDF/DOCX handed to an admin | **Yes, and out of scope** | No scanner exists; `SniffedContentType`'s own doc-comment says so (`:38-42`). Refusing markup/scripts/executables is what it does; it is not a scanner |
| **Metadata disclosure to a fetcher who is not the uploader** | **Yes — this is the whole residue** | R4, and the audience table above |

### The fact that decides the whole thing: nothing here **calls** a decoder

Rev N wrote *"nothing here decodes an image."* That is wrong in one direction and overstated in
another, and the correction matters because it changes what can enforce the rule.

**What is true:** `SixLabors`, `SkiaSharp`, `System.Drawing` and `Magick` appear in **zero**
`src/**/*.csproj` — the only graphics package in the solution is `QuestPDF`
(`Cleansia.Infra.Services.csproj:14`, pinned at `Directory.Packages.props:55` to 2024.12.1), which
*generates* invoices and never touches a user photo. `OrderPhoto.Width`/`Height` exist (`:39-40`) and
**are never populated** — both writers omit the optional arguments (`SaveOrderPhotos.cs:141-150`,
`UploadOrderPhoto.cs:112-121`).

**What is also true, and rev N did not know:** a complete JPEG/PNG/WebP decoding stack is **already
deployed**. QuestPDF ships its own native Skia as runtime assets —
`runtimes/{linux-x64,linux-arm64,linux-musl-x64}/native/libQuestPdfSkia.so`
(`Cleansia.Infra.Services/obj/project.assets.json:832-864`) with bundled `libjpeg-turbo` / `libpng` /
`libwebp` / `skia` licences (`:2362-2368`).

**So the property is about the call site, not the package inventory:** `.Image(` / `ImageDescriptor` /
`Image.FromBinaryData` return **zero** matches across `src/**/*.cs` (independently checked by two
instances and re-checked for this revision). **No user-supplied image is decompressed anywhere on this
platform's servers.** Every decoder in the system belongs to a client rendering an `<img>`.

That is not an accident to be corrected; it is the property that makes the current design safe, and
the tickets propose to destroy it. A decoder converts a *bounded* input into an *unbounded* allocation
chosen by the uploader:

- **One bounded upload already suffices.** A single-colour 30 000 × 30 000 PNG is a few hundred KB on
  the wire and ≈3.6 GB decoded. **The array cap is irrelevant to the argument** — rev N's "10 MiB × 30"
  was never reachable and was never needed: with no `MaxRequestBodySize` / `RequestSizeLimit` /
  `MultipartBodyLengthLimit` anywhere in `src/**`, the effective ceiling is Kestrel's 30,000,000 B, so
  base64 at +33 % puts the real per-request decoder input at ≈21 MiB
  (`agents/architecture/decisions/request-intake-limits.md:26-42`, this ADR's own cited companion).
  A figure a reader can falsify from the ADR's own citation spends credibility the ruling does not need.
- **The plan is small and shared.** Prod is **S1** (`weu.prod.bicepparam:34`) and the plan carries
  *"the 5 APIs + SSR + Functions"* (`appServicePlan.bicep:22`). DEV is **B2 with autoscale off**
  (`weu.dev.bicepparam:26` — the `autoscaleEnabled` param defaults to `false`,
  `appServicePlan.bicep:23`), i.e. one fixed instance, **and DEV is live**.
- **Autoscale is memory-blind.** Both prod rules trigger on `CpuPercentage`
  (`appServicePlan.bicep:70,88`; enabled at `weu.prod.bicepparam:50`). A decoder's failure mode is
  **memory**, so scale-out never fires and an OOM takes every site on the instance.

**A naive `IImageSanitizer` is therefore a remote OOM primitive on an authenticated-but-cheap path,
delivered by the ticket written to make uploads safer.** It is mitigable — a header-only `Identify`
before any decode — but that mitigation must be a decision, not an assumption, and it is the
assumption both tickets make.

---

## Decision

> **The one decision this ADR makes:** *metadata is scrubbed at intake, by audience, without a
> decoder.* D5 and D6 are **ratifications** of shipped behaviour, kept at their original numbers
> because the verdict, the living doc and the tickets reference them by number. Everything else is
> context or consequence.

### D1 — The tickets' "shared sanitizer seam" is refused. The shared seams already exist and a transform is not one of them

T-0458 assumes one `IImageSanitizer` for all uploads. **There is no such abstraction to build**, and
the codebase already demonstrates why: the intake lanes diverged, and they diverged *correctly*.

| Shared today | Why it is genuinely shareable |
|---|---|
| `BlobFileSize` | A byte count is the same fact for every artifact |
| `ServedContentType` | The set of types this platform may ever emit is one closed set |
| `SniffedContentType` | *What is this byte sequence* is one fact — one table, a per-intake accepted set beside it (`:66-78,88-104`) |
| `UploadIntakeRosterTests` | "How many intakes are there" is one question |
| Two `AbstractValidator<BlobFileDto>` **siblings** | The *accepted set* is a per-surface product promise. One validator would have to be the union, which is wrong for both |

A metadata transform sits with the siblings, not with the shared four: JPEG segments, PNG chunks,
RIFF chunks and PDF object dictionaries have nothing in common but the word "metadata." An interface
over them is a switch statement with a DI registration.

**What is shareable is the *obligation*, and its home is the roster, not an interface.** The roster
already annotates each intake with the rule guarding it; it gains two more annotations (D6).
A new upload route then cannot be added without stating its answers — which is the property the
codebase has twice failed to get from "remember to do it in each intake"
(`patterns-backend.md:1269-1274`).

**Consequence:** T-0458's AC6 ("wire the sanitizer into exactly one pipeline as a pilot") and T-0459's
"mirror the pilot; do not invent a second integration style" are both re-scoped. There is one helper
per format, called by the handlers that need it — not a seam.

### D2 — Nothing on a request path decodes user-supplied image data. Re-encoding is refused

This is the ADR's central ruling. It survived the panel intact; the challenger's closing position was
that it *"survives everything I could throw at it"*, and CH-3(ii) strengthened it.

**Refused: decode + re-encode** (`ImageSharp` / `SkiaSharp`). It buys metadata removal "by
construction," and it costs: a decoder on a request path fed attacker-chosen input on a memory-blind
autoscale over a plan carrying seven sites; native binaries to validate against the Linux App Service
and Functions images; and **it does not generalise to PDF at all**, which is the format carrying the
most metadata in this codebase. *(Rev N also rejected it on generation loss and on an ImageSharp
licence question. Both limbs are deleted — see §Alternatives A1 and §Verdict §B.2. The rejection is
over-determined without them and stronger for their absence, because both were falsifiable from this
repo.)*

**Adopted: removal by container rewrite** — walk the container's own segment/chunk structure, drop the
metadata containers, re-emit the rest byte-identically. No decoder, no bitmap, no library, no licence
question, no quality loss; allocation bounded by the input, which is already bounded.

- **JPEG** — drop every `APP1` (EXIF **and** XMP, which rides its own `APP1`) and `APP13`
  (IPTC/Photoshop) segment. **Orientation is preserved by re-emitting a minimal, server-synthesized
  EXIF `APP1` carrying only the `Orientation` tag** — see D2.1 for its degradation rule, which is part
  of the policy and not an implementation detail.
- **PNG** — drop the `eXIf`, `tEXt`, `iTXt`, `zTXt` and `tIME` ancillary chunks. Chunks carry their own
  CRC, so removal needs no recomputation.
- **WebP** — drop the `EXIF` and `XMP ` RIFF chunks, clear the corresponding `VP8X` flag bits, fix the
  RIFF size field. Simple (`VP8 `/`VP8L`-only) files have no such chunks and pass through untouched.
- **GIF** — pass through. GIF has no EXIF; the only metadata container is a comment extension, which no
  camera writes.
- **PDF / DOC / DOCX** — **not rewritten.** See D8.

**What this loses, said plainly.** It removes the metadata containers a camera and an editor write. It
does **not** remove an ICC profile (removing one changes rendered colour), a JPEG comment (`COM`), or
anything embedded inside the image data itself. It is a **metadata scrub**, and this ADR calls it that
rather than a "sanitizer" — a word that promises the thing D2 refuses to attempt.

**⚠ not measured.** That a segment walk over a 10 MiB JPEG is negligible against a request that
already base64-decodes and uploads the same bytes is a reasonable expectation, not a measurement.
**Owed by T-0458 (AC7), on the 30-item batch, not on one avatar** — the batch is the shape whose cost
matters, and no instance authoring this ADR had a shell.

#### D2.1 — Orientation: the degradation direction is part of the policy

Rev N named the mechanism and never said what happens when it fails, which is what left the policy
incomplete against T-0458's fourth demanded ruling.

> Orientation is preserved **if and only if** the source `APP1` can be read unambiguously and yields a
> value in 2–8. Anything else — a malformed IFD, an unexpected byte order, a value out of range, a
> truncated segment — emits **no EXIF at all** and accepts the rotation. **Never guess, never repair.**

The safe direction is deliberate: a photo that ships rotated is a visible cosmetic defect on a rare and
largely adversarial branch; a corrupted photo, or a surviving GPS tag, is not.

**And this branch carries a synthetic-corpus test burden, because production will barely exercise it.**
Once D10 lands, every client the platform controls bakes rotation into pixels and emits no EXIF at all
— both mobile clients already do (`ImageCompressor.swift:24-27,31-32`; `ImageCompressor.kt:97,100,248`)
— so the emitter runs almost exclusively for residual old-client and third-party traffic. New,
hand-rolled, attacker-facing code with near-zero production exercise is the worst available
combination, and the answer is a corpus, not production. **T-0458 owes tests over: truncated segments,
garbage lengths, both TIFF byte orders, and orientation 1 / 2–8 / 9 / absent.**

#### D2.2 — What the scrub dispatches on: the bytes it is holding. Full stop

> The metadata scrub determines the container format **from the bytes it is holding**, at the moment it
> runs. It never reads a client-supplied string, and it never reads a persisted `ContentType` field —
> **not even a correct one**. A format it cannot identify is **passed through untouched and reported as
> "not scrubbed"**, never as "scrubbed".

This exists because a per-format scrub dispatching on a client string is a control whose no-op path its
own uploader selects: declare `data:image/png`, send JPEG bytes, and the PNG chunk walker runs over a
JPEG — best case it finds no `IHDR`, bails, and the metadata survives **under a green "scrub applied"
test**; worst case it is a length-arithmetic fault in brand-new attacker-facing parsing code, reached
by editing one word in a data URI. R5 makes that concrete on `SaveOrderPhotos`, which is exactly the
surface D4 elects as the pilot (`SaveOrderPhotos.cs:130,171-184`;
`SaveOrderPhotosContentTypeTests.cs:49-59`).

**The sibling lane** (`drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md` D1) has
independently decided that `SaveOrderPhotos` adopts `SniffedContentType.FromContent(…,
UploadIntake.OrderPhoto)`, and asks this ADR not to re-decide it. This clause is the strictly stronger
form and is **robust to that lane's outcome**: by never dispatching on a persisted `ContentType` at
all, it satisfies that lane's constraint whichever way the lane resolves.

**One decision per ADR is preserved:** this ADR rules what the *scrub* dispatches on; the sibling rules
what the *row* stores. The sibling's closing ticket is owed on its own independent grounds (a stored
type that disagrees with its bytes, the `"image/jpeg"` fabrication at `SaveOrderPhotos.cs:183`, and the
unguarded `Convert.FromBase64String` at `:136`, which is a live 500) and **should land first if the PM
can sequence it — but T-0459 is not gated on it.**

Calling an existing intake helper to answer *what are these bytes* is not building a shared transform
abstraction, so this does not undercut D1. The sniff **is** the shareable part, it already exists, and
saying so is a better argument for D1 than rev N's.

### D3 — Where the scrub runs, and why it cannot be on the read path

**Intake, in the handler, between the decode and `UploadAsync`.** Three placements were considered
(T-0458 lists them):

- **Not a FluentValidation validator** — validators reject; they do not transform. Correctly ruled out
  by the ticket, and it is T-0458's own stated reason.
- **Not a decorator on `IBlobContainerClient.UploadAsync`** — the most tamper-proof option, and wrong
  here: that same sink writes **our own** generated PDFs — receipts (`ReceiptService.cs:136,321`),
  employee invoices (`PayPeriodBackgroundService.cs:467`, `RegenerateInvoicePdf.cs:141`). A decorator
  that must ask "is this stream a user artifact?" has lost the property that made it attractive, and an
  unconditional one would rewrite our own documents.
- **Not the read path.** Worth stating because the read path is exactly where T-0464 solved the *type*
  problem, retro-fixing every stored blob with zero migration. **It does not generalise**: the type
  clamp works because we mint the SAS and can pin a response header on it (`BlobContainerClient.cs:89-110`),
  but the SAS then hands the client the blob **directly from storage** — we never touch those bytes
  again. `rsct` retypes a response header; it cannot change a byte. **Content can only be changed where
  we hold it, and that is intake.** (This is also why R4 has no zero-migration answer and D9's backfill
  stays open.)

### D4 — The scrub is applied by audience: order photos and dispute evidence. **Not** the avatar

| Surface | Ruling | Reason |
|---|---|---|
| `SaveOrderPhotos`, `UploadOrderPhoto` | **Scrub** | **The audience is not enumerable at upload time.** `CanBrowseOrderAsync` admits any tenant cleaner while a seat is open (`OrderAccessService.cs:68-92`), so the fetch set includes parties with no relationship to the job. What is disclosed is the uploading cleaner's **device identity and off-site location** — a stable cross-order correlation key that walks straight through `GetOrderPhotos.cs:107-109` and ADR-0036, the two controls that deliberately withhold cleaner identity |
| `UploadDisputeEvidence` | **Scrub** the image formats | The uploader is an adversary with money on the outcome (`:95-99`), so a client-side strip is not a control here at all. Same helper, near-zero marginal cost. Secondary: EXIF timestamps on evidence are **client-forgeable**, so removing them removes a signal an adjudicator might otherwise trust. PDFs are **excluded** — D8 |
| `UpdateCurrentUser` (avatar) | **No scrub, recorded as a decision with an expiry** | The only fetcher is the subject; scrubbing discloses nothing to nobody. **The obligation attaches to the ticket that first emits an avatar URL on a cross-user DTO** — today a one-line change in `UserMappers`/`EmployeeMappers`, which is precisely why it must be a written gate rather than a memory |
| `SaveMyDocuments`, `UpdateEmployee.Documents` | **No image scrub** | D8 |

**This overturns T-0458 AC6.** The pilot is `SaveOrderPhotos`, because it is simultaneously the exposed
surface and the batch shape whose cost must be measured (T-0459 AC6). Piloting on the avatar measures
the one-item case and reduces zero exposure.

**On the avatar exemption's residual risk, honestly:** the roster asserts a route→rule **string**, so a
developer can update the annotation without doing the work. The challenger independently verified the
exemption is correct on the facts and **could not improve on that mitigation**; it stands as the best
available, not as airtight.

### D5 — *(Ratification, not a decision)* The image accept set already equals the serve set

**Shipped.** `SniffedContentType.Signatures` (`:66-78`) carries **no BMP and no TIFF**, and matches
WebP as two fragments — `RIFF` at offset 0 **and** `WEBP` at offset 8 (`:72`) — so a WAV or AVI no
longer passes as an image. The per-intake accepted sets (`:88-104`) select from that one table, so
accept ⊆ serve holds **by construction**. The catalog sentence this satisfies —
*"keep the accepted set equal to what the clients offer"* — is `patterns-backend.md:1351-1354`, and the
reasoning is recorded at `SniffedContentType.cs:30-36,80-87`.

Rev N proposed this as a decision (delete BMP/TIFF, tighten WebP). **It had already shipped.** It is
restated here only so a reader of this ADR does not implement it twice, and because one thing about it
is *not* closed: **the construction is unpinned.** `ServedContentTypeTests` carries no assertion that
every `Signatures` MIME resolves to a non-`Opaque` `ServedContentType` (grep for `SniffedContentType`
or `Signatures` in that file returns nothing), so a seventh row added to `Signatures` that
`ServedContentType` cannot serve reintroduces the defect silently. That gap — **not** the deletion — is
what T-0458 owes here, and it is tiered accordingly in D7.

### D6 — *(Ratification + one decision)* The roster is already 14 rows; it gains the audience/scrub columns

**Shipped:** `UploadIntakeRosterTests.cs:39-55` enumerates all **14** intakes, each annotated with the
rule guarding it, plus a `[Theory]` at `:76-84` naming the four `byte[]`/`IFormFile` routes by name so
narrowing the predicate cannot silently pass, and a count assertion ahead of the set comparison
(`:64`) so an empty walk cannot agree with an empty roster. The predicate now asks *does a file reach
storage from here* (`:97-107`). **This is `T1-CI` today.**

**The decision this ADR makes:** each row's annotation gains two columns —

```
<rule> | <audience: self | cross-user | staff> | <scrub: none | image-metadata | n/a>
```

— and a `scrub: none` row must carry its reason **by name**. A new upload route reddens the test; the
fix is to add the row *after* answering all three columns. This is D7's enforcer and the reason the
obligation is a roster column rather than an interface (D1).

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
>    the ticket that adds one owes the scrub. Read the **authorization gate the fetch actually uses**,
>    not the one you expect: a browse gate that admits any cleaner with an open seat makes the audience
>    unenumerable at upload time.
> 2. **What is it served as?** Server-derived, from a **closed set**, decided on the **read** path so it
>    also governs rows written before the rule. Never the client's declared type; never the file
>    extension. The accepted set equals the servable set — accepting a format that can only ever be
>    served opaquely is an upload that succeeds and never renders.
> 3. **What travels inside it?** For an artifact whose audience is not its uploader, metadata containers
>    are removed at **intake** — the read path cannot do it, because a signed URL hands the client the
>    stored bytes directly. **The removal dispatches on the bytes in hand, never on a stored or declared
>    type**, or the uploader selects the no-op. A surface that does not scrub records **why**, by name,
>    on the roster.
>
> **And one prohibition: no request path decompresses user-supplied image data.** A decoder turns a
> bounded upload into an allocation the uploader chooses — a 300 KB PNG into gigabytes of bitmap.
> Nothing in this system needs pixels. **This is a reachability property, not a package-inventory one:**
> a decoding stack can already be on the image as a transitive native asset, so the thing forbidden is
> the **call site**. Adding one is an **ADR**, not a package reference, and it owes a header-derived
> dimension bound checked **before** any decode.
>
> **The incident.** `ImageFileValidator` was a 3–4 byte magic-prefix check over three shipped
> pipelines; `SaveOrderPhotos` read its stored type off the client's own `data:` URI prefix; every
> employee-document intake stored the string its uploader claimed. None of it was a violation of
> S1–S11 — **S4 governs DTO fields, S6 governs logs, S8/S10 govern query scoping, and none of them
> reaches inside a byte array.** The reviewers were not wrong against the rules; the rules were silent.

**Enforcement, per ADR-0032 — enforcer named, tier declared, per clause. This table replaces rev N's,
which mis-tiered two rows in opposite directions and named an enforcer that cannot see its clause's
real failure mode.**

| Clause | Enforcer | Tier |
|---|---|---|
| Q2 — served type is server-derived from a closed set | `ServedContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`, `SasResponseHeaderOverrideTests` (`Cleansia.Tests`, a named step of `backend-ci.yml:69-71`) | **`T1-CI`** — exists today |
| Q2 — accept set ⊆ serve set | **True by construction today** (one `Signatures` table + `AcceptedByIntake`, `SniffedContentType.cs:66-104`) but **unpinned**: `ServedContentTypeTests` carries no such assertion, so a seventh row reintroduces the defect silently. New: assert every `Signatures` MIME resolves to a non-`Opaque` `ServedContentType` | **`(gate pending: T-0458)`** — for that reason, not rev N's |
| Q1 + Q3 — the roster **enumerates** all 14 intakes | `UploadIntakeRosterTests` (`:39-55` + `:64` + `:76-84`) | **`T1-CI` — shipped today** |
| Q1 + Q3 — every intake **declares** audience + scrub | the same test, with D6's two added columns | **`(gate pending: T-0458)`** |
| Q3 — the scrub actually removes metadata | per-pipeline tests reading metadata back out of **the bytes handed to the blob client** | **`(gate pending: T-0459)`** |
| Prohibition — no **direct package reference** to a decoder | `.csproj` denylist walk (`SixLabors.*`, `SkiaSharp*`, `System.Drawing.Common`, `Magick.NET*`) + a project-count non-vacuity floor, per the `WebSdkContentGlobTests` shape | **`(gate pending: T-0458)`** → `T1-CI` when it lands |
| Prohibition — no **call site** reaching a decoder, including QuestPDF's transitively-shipped Skia | source scan of `src/**/*.cs` for `.Image(` / `ImageDescriptor` / `Image.FromBinaryData`, with a non-vacuity floor. **If T-0458 cannot build it, this clause is declared `T2-ADVISORY` in the catalog with a named reviewer check — it is not left labelled as a gate** | **`(gate pending: T-0458)`** |

**Why the last row exists, and why rev N's single denylist row was not honest.** One
`.Image(orderPhotoBytes)` inside a QuestPDF document — and *"attach the order photos to the dispute
pack / the invoice"* is an ordinary next feature on a codebase that already generates invoice PDFs —
creates exactly the primitive D2 refuses, on a request path, **and a package-name denylist stays
green**: it matches none of those four strings and it is not a `PackageReference`. The prohibition is a
reachability property; a package-name denylist cannot express it. This ADR holds others to that
standard in this very table and must apply it to itself.

**Do not label this rule `T1-CI` wholesale.** Two clauses are enforced today; five are specified and
ticketed. `enforcement.md:177-178` provides `(gate pending: <ticket>)` for exactly this, and **a
mechanism that cannot fail a build is `T2-ADVISORY` however it is labelled** (ADR-0032). Note in
particular that `check-consistency.mjs` is `T2-ADVISORY` (it runs in zero workflows) and the frontend
lint step is `continue-on-error: true` — neither can carry a law here.

### D8 — Scope of "artifact": images are scrubbed, documents are **declared** — and the exclusion is scoped per surface

T-0460 asks how wide "artifact" goes. Ruling: **the law covers every user-supplied artifact; the
*scrub* covers images only, and the exclusion is written into the rule per surface rather than left to
inference.** Rev N gave one reason for both surfaces and that reason is false on one of them.

**Employee documents (PDF / DOC / DOCX) — excluded.**

- **Mechanism cost.** Stripping PDF metadata properly is an object-graph rewrite of the document
  catalog (`/Info` plus XMP in a metadata stream, plus incremental-update history) — the PDF
  equivalent of the decoder D2 just refused. Doing it wrong corrupts a cleaner's contract scan. OOXML
  is the same shape with a different container.
- **Audience.** The exposure it leaves is small and asymmetric: an employee document's audience is that
  cleaner and an admin who already holds the cleaner's legal name, tax id and payout details. The
  metadata discloses to the one party that already has more.
- **Delivery.** It is served as an `attachment`, byte-typed, and **never by URL** — three API routes,
  all `File(bytes, type, name)` (`Web.Partner/EmployeeController.cs:125`,
  `Web.Mobile.Partner/EmployeeController.cs:179`, `Web.Admin/AdminEmployeeDocumentController.cs:92`) —
  so it also cannot be rendered inline.
- **Expiry, in the same style as the avatar's.** The audience limb depends on there being **no
  admin-side document upload**, and there is none today: all four document intakes are `Web.Partner` /
  `Web.Mobile.Partner` (`UploadIntakeRosterTests.cs:45-46,50-51`), and a walk of `Web.Admin` for
  `[HttpPost]`/`[HttpPut]` actions taking `IFormFile` / `BlobFileDto` / `byte[]` finds none. **If an
  admin-side document upload is ever added, this limb inverts** — the operator's scanner/workstation
  identity would flow to the cleaner — and the ticket that adds it owes the scrub decision again.
- **Recorded, not silent:** the roster row reads `scrub: none — PDF/OOXML object-graph rewrite refused,
  see ADR-0043 D8; audience: staff`.

**Dispute-evidence PDFs — also excluded, and on the mechanism limb alone.** `application/pdf` is in the
dispute intake's accepted set (`SniffedContentType.cs:92-95`), so D4's *"scrub the image formats"*
leaves a hole here, and **the employee-document justification does not transfer**:

- **The audience limb is false here.** On this path the uploader is a **customer**, the adverse party
  is a **cleaner**, and the fetcher is **staff adjudicating money**. *"Already has more"* is not true of
  anyone in that triangle.
- **The delivery limb is false here.** Dispute evidence is served **by URL, inline** — the SAS mint sets
  `rsct` and `rscc` and **no `rscd`**, so no `Content-Disposition: attachment`
  (`BlobContainerClient.cs:93-110`; the mint at `UploadDisputeEvidence.cs:121-126` and
  `DisputeMappers.cs:73-78`).
- **So only the mechanism limb survives, and it survives unchanged:** a PDF object-graph rewriter is the
  thing D2 refuses, and this ADR will not build one on a support path.
- **The residue this leaves is named, not hidden: the exclusion is evadable in one sentence.** An
  uploader who wants metadata preserved wraps the photo in a PDF — same intake, same allowlist, scrub
  does not apply. Combined with the adversarial uploader on this surface, that is not a hypothetical
  shape. **The cheaper answer is to drop `application/pdf` from `UploadIntake.DisputeEvidence`** (the
  flow is photo evidence; unlike DOC/DOCX on employee documents it changes no five-locale promise) —
  **but narrowing what a customer may submit as evidence is a product decision, so it is escalated, not
  decided here** (§Escalations, Q-ART-01 part (b)).
- **Recorded, not silent:** the roster row reads `scrub: image-metadata; PDF excluded — object-graph
  rewrite refused (ADR-0043 D8), evadable, accepted pending Q-ART-01(b); audience: staff`.

### D9 — No backfill obligation, and the reason is structural

S12 binds **new uploads**. It does not oblige an audit or a rewrite of stored artifacts:

- The *type* half needed no backfill because it was fixable on the read path (T-0464's whole argument,
  and `SniffedContentType.ForDownload` at `:127-128` is that fix).
- The *content* half **cannot** be fixed on the read path (D3), so a backfill is a real data migration:
  enumerate two containers, download, rewrite, re-upload, with an owner-run step and its own risk.
- Its scope is bounded and shrinking — blobs uploaded before PR #154 (2026-07-26, after which both
  mobile clients re-encode on pick) plus web uploads since.

**File it as its own ticket; do not let the rule mandate it by implication.** Both T-0458 and T-0459
already exclude it and this ADR agrees.

### D10 — The web clients re-encode on pick, and this ships **first** — as a complement, per surface

No `canvas` / `createImageBitmap` image re-encode exists anywhere in `src/Cleansia.App` (a grep for
those and `getContext('2d')` returns only the address-autocomplete component and three dashboard chart
templates). Both mobile clients already re-encode every pick into a fresh bitmap, dropping metadata by
construction rather than by erasure (`ImageCompressor.swift:24-27,31-32`;
`ImageCompressor.kt:97,100,248`), which is why they cannot be a source.

Making web match mobile has the best exposure/effort ratio available and **removes essentially all
live metadata volume** — because for the *ordinary* uploader (a) holds: they are careless, not
adversarial, so a client-side strip protects the person it needs to protect. **It must not be
sequenced behind the server work**; T-0459's ordering assumes the opposite.

**It is a complement, not a substitute**, and rev N under-priced it. There are **four** distinct web
file-read call sites, not one per app, and one of them must **not** re-encode:

| Call site | Feeds | Re-encode? |
|---|---|---|
| `libs/cleansia-customer-features/profile/…/profile.models.ts:59-66` | customer avatar | yes |
| `libs/cleansia-partner-features/orders/…/order-photos.component.ts:125,139` | `SaveOrderPhotos` — the unscrubbed client and the untyped route are the same path | yes |
| `libs/cleansia-partner-features/profile/…/profile-documents.facade.ts:145-150` | employee documents | **no** — re-encoding a contract scan is destruction |
| `libs/shared/utils/src/file-transformation.utils.ts:126-130` (shared) | sets `contentType: file.type \|\| 'application/octet-stream'` and `fileName: file.name`, **both of which a canvas re-encode changes and `SaveOrderPhotos.DetermineContentType` reads** | depends on caller |

So *"~30 lines per picker"* must become *"which pickers, and what happens to the name and the declared
type."*

**The deferral question, ruled per surface.** Rev N framed it as a binary — either D10 is sufficient or
it is not. It is neither:

| Surface | May D2/D4 be deferred behind D10 with a written trigger? |
|---|---|
| `SaveOrderPhotos` / `UploadOrderPhoto` | **Yes.** The argument there is **durability** — a per-client obligation is the shape that already failed twice here — and D10 genuinely removes the live volume |
| `UploadDisputeEvidence` | **No.** The uploader is the dispute's own customer with money on the outcome (`:95-99`). *"The client strips it"* is not a control against a party whose interest is to not strip. Here the scrub is **enforceability**, not durability |

The severity bound recorded under (b) — the exposure is latent, because no surface reads EXIF today —
bounds the *urgency* of the dispute-evidence work. It does **not** restore the deferral.

---

## Alternatives considered

**A1 — Decode + re-encode with `ImageSharp` (T-0458's presumed answer).** Rejected on two grounds, both
in §D2: **(i) resource** — it creates the only user-driven decoder in the system, on a request path, on
a memory-blind autoscale (`appServicePlan.bicep:70,88`) over a plan carrying seven sites
(`:22`), with DEV a single fixed B2 instance (`weu.dev.bicepparam:26`); **(ii) generality** — it does
not generalise to PDF, the format carrying the most metadata here. **What it gets right, conceded:** it
is the only option that removes *everything*, including containers D2's walk does not know about. D2 is
narrower and says so.

> **Two limbs rev N used are deleted, and the rejection is stronger without them** (§Verdict §B.2):
> - **Generation loss is NOT a rejection ground.** The platform already re-encodes the majority of
>   order photos at q0.7 with a 1920 downscale, on the clients, deliberately
>   (`ImageCompressor.swift:31-32`, `ImageCompressor.kt:97,100`), and D10 extends that to web. A
>   server-side re-encode at q0.9 with no downscale would be *less* destructive. **An ADR may not reject
>   an option for a cost it is simultaneously shipping.**
> - **The licence question is NOT a rejection ground, and is not this ADR's to rule.** The repo already
>   ships a revenue-threshold-licensed graphics package (QuestPDF, `Directory.Packages.props:55`). More
>   importantly the question **is not live, because no library is adopted** — D2 needs none. If a future
>   ADR ever overrules D2 and adopts a decoder, **the licence is an owner/legal question filed at that
>   time; the architect does not rule it.** T-0458 AC1 asks for "the library + its licence position" and
>   must be re-worded accordingly.

**A2 — `SkiaSharp` instead.** Same rejection as A1, plus native binaries to validate against the Linux
App Service image and the Functions host. The library question is downstream of the re-encode question
and never becomes live.

**A3 — Do nothing server-side; ship D10 alone.** The strongest alternative, and **partially adopted**:
D10 ships first and independently, and the deferral it enables is **granted for order photos**. Rejected
as the *complete* answer on two grounds: (i) **durability** — the upload surface has grown to 14 routes
and each new one gets a new client, so a per-client obligation is the shape that already failed twice
here (which is why the roster exists); and (ii) **it is not available at all on dispute evidence**,
where the uploader is the adversary. See the D10 deferral table.

**A4 — Strip specific EXIF tags (GPS IFD, `Make`, `Model`, serials, `MakerNote`) rather than dropping
the whole segment.** Rejected — and the rejection is **restated**, not re-scored, because rev N's stated
reason was false. Rev N rejected A4 for *"requiring a full EXIF/TIFF IFD parser"*; **D2 needs one too**
— re-emitting a minimal `APP1` carrying only `Orientation` requires reading the TIFF byte order, the
IFD0 offset and the entry table to find tag `0x0112`. The parser-size argument is conceded. The two
grounds that actually distinguish them:

1. **Allowlist vs denylist.** D2 emits only the one tag it chose. A4 removes only the tags it thought
   of, and that list ages every time a vendor invents a `MakerNote` variant. For a *disclosure* control
   the default must be **"drop unless named"**, not "keep unless named".
2. **No attacker byte reaches the output.** D2's emitted `APP1` is server-synthesized end to end — a
   fixed one-entry IFD, server-computed offsets, one value validated into 2–8. A4 re-emits
   attacker-chosen bytes with rewritten offsets, and **offset arithmetic over attacker-chosen values is
   precisely where the hand-rolled-parser worry becomes a defect**.

What A4 genuinely keeps that D2 loses is conceded: ICC, `COM`, and no orientation special case at all.
**Revisit if** D2's minimal-EXIF emitter proves harder than expected — but revisit against these two
grounds, not against parser size.

**A5 — Strip metadata in a background job after upload.** Rejected: the blob is fetchable via SAS
between upload and sweep, and the job would have to rewrite a blob other requests may hold URLs to.
A synchronous byte walk costs less than the coordination.

**A6 — A `nosniff` header / storage-account CORS instead of any of this.** Not an alternative to
metadata removal (different threat), and out of scope: it is a Bicep change, already noted on T-0464
§Out of scope. Worth stating that **no host sets `X-Content-Type-Options: nosniff`**
(`patterns-backend.md:1364`) and real Azure does not send one on SAS fetches (T-0464 status log,
2026-08-01, owner-verified on DEV) — which is why the closed served-type set, not a header, is the
control.

**A7 — Extend S4 rather than add S12.** Rejected: §D7. Same principle, different check, and
discoverability decides it. The panel additionally declined the narrower variant — promoting only the
served-type clause into S4 — because it would file a bytes question under a rule whose check is "read
the DTO's field list," which is the exact discoverability failure D7 exists to fix.

---

## Consequences

- **The platform keeps its strongest current property — no request path calls an image decoder — and
  that property becomes a written prohibition instead of an accident.** Stated as a **reachability**
  property, so its enforcer is a call-site scan and not a package inventory.
- Order photos and dispute evidence stop carrying capture metadata to a fetch set that, on order
  photos, is not enumerable at upload time. The avatar does not, **by a recorded decision with a named
  expiry**, not by omission.
- A word disappears from this area: there is **no sanitizer**. There is a bound, an accept set, a
  server-truth type, a served-type clamp, and — on two surfaces — a metadata scrub. **T-0458's and
  T-0459's titles are now wrong and should be renamed by the PM.**
- The intake roster becomes the single place that answers "how many upload surfaces are there, who
  fetches each, and what does each do about content" — 14 rows, three columns.
- **The residue this ADR knowingly leaves:** PDF and Office metadata (D8, declared per surface, with
  the dispute-evidence evasion named and escalated); already-stored blobs (D9, ticketed separately);
  malware inside a permitted container (never in scope — `SniffedContentType.cs:38-42` already says
  so); and a scriptable container (`application/pdf`) inside the closed served-type set, served inline
  on the dispute path (threat table).
- **If only part of this ships, D10 is the part that reduces exposure** and D2/D4 are the part that
  makes it durable — *except on dispute evidence, where D10 is not a control at all*. Shipping D2/D4
  without D10 leaves the live volume untouched for a sprint.
- **What is owed and not answerable from a keyboard without a shell:** the runtime cost of a segment
  walk on the 30-item batch (T-0458 AC7). This ADR asserts no number for it.

## How a reviewer verifies compliance

1. `grep -rE "SixLabors|SkiaSharp|System.Drawing|Magick" src --include=*.csproj` returns **nothing**,
   and the D7 denylist test reddens when a reference is added (mutate: add one to a test project).
2. `grep -rE "\.Image\(|ImageDescriptor|Image\.FromBinaryData" src --include=*.cs` returns **nothing**,
   and the call-site scan reddens when one is added. **If that scan was not built, the catalog entry
   for this clause reads `T2-ADVISORY` with a named reviewer check — not `(gate pending: …)` and not
   `T1-CI`.**
3. `UploadIntakeRosterTests` lists **14** rows, each with `rule | audience | scrub`, count-asserted
   before the set comparison. Adding a new upload action reddens it. Every `scrub: none` row names its
   reason.
4. Every MIME in `SniffedContentType.Signatures` resolves to a non-`Opaque` `ServedContentType`,
   asserted in a test (it is true by construction today and **unasserted**); BMP and TIFF are absent;
   the WebP signature checks `WEBP` at offset 8.
5. For `SaveOrderPhotos`, `UploadOrderPhoto` and `UploadDisputeEvidence`, a test reads EXIF back out of
   **the bytes handed to the blob client** and finds none — **not** an assertion that a helper was
   called. Each goes red when its own call is removed (three distinct mutations, three named tests).
6. The scrub takes **bytes**, not a content-type string: its signature has no `contentType` parameter
   and no call site passes `photo.ContentType` or `command.ContentType` into it. An unidentifiable
   payload is reported as *not scrubbed* and the test asserts that reporting, not a silent pass.
7. An input whose EXIF `Orientation` is 6 produces output that still renders rotated the same way —
   asserted on the emitted bytes, not visually. The synthetic corpus covers truncated segments, garbage
   lengths, both byte orders, and orientation 1 / 2–8 / 9 / absent; every malformed case emits **no
   EXIF** and never a repaired one.
8. `UpdateCurrentUser` does **not** call the scrub, and its roster rows say `audience: self`. Any diff
   that adds an avatar URL to a non-self DTO must change those rows in the same change.
9. `security-rules.md` carries S12, its header reads **S1–S12** (it reads **"S1–S10"** today at `:1`
   while S11 exists at `:319` — already stale), the audit checklist gained an item, and a
   `grep -rE "S1[-–]S1[01]" agents/ .claude/` sweep is in the PR body with results (T-0460 AC4).

## Escalations (owner)

**Q-ART-01 is already filed by the PM in `agents/backlog/questions/open.md`. It is not re-filed here.**
It survives D8 and is **two-part**:

- **(a)** Do we keep accepting **DOC/DOCX on employee documents**? They carry author names and revision
  history, no scrub is proposed (D8), and an OOXML rewriter is not worth building. Dropping them would
  leave PDF/JPEG/PNG, which cover every real document-scan case — but it narrows what a cleaner may
  upload and changes a five-locale string that promises *"Accepted: PDF, JPEG, PNG, DOC, DOCX"*
  (`SniffedContentType.cs:80-87`).
- **(b)** Do we keep accepting **`application/pdf` on dispute evidence**? It will not be scrubbed (D8),
  and it is a one-sentence evasion of the scrub on the one surface with an adversarial uploader.
  Dropping it changes no five-locale promise — the flow is photo evidence — but it narrows what a
  customer may submit into an adjudication that decides their money.

Both are **product** narrowings, not architecture. **Neither blocks:** D8 is scoped per surface either
way, and the roster records `scrub: none` with its reason regardless of the owner's answer.

---

## Challenge

This section carries two rounds. **The panel round is the second.**

### Round A — the author's self-challenge (rev N, 2026-08-05). NOT a panel

Kept because its five items were carried into the panel and adjudicated as `C-1`…`C-5`.

**C-1 — "You refused a decoder on a resource argument, and you did not measure anything."**
Sustained as a caveat and handled by construction rather than by measurement: D2's adopted option has
**no decode step at all**, so the resource argument does not have to be quantified to be avoided. The
claim that *would* need measuring — "a segment walk is negligible" — is marked ⚠ and owed by T-0458 AC7
on the 30-item batch. A challenger should still press whether a header-only `Identify` + dimension gate
would have been an adequate mitigation; my position is that it makes the decoder *safe* without making
it *necessary*, and D2 only has to show it is unnecessary.

**C-2 — "D2 hand-rolls format parsers. Parsing attacker-controlled binary is what libraries are for."**
The strongest self-challenge and only partly answered. A JPEG segment walk and a PNG chunk walk are
length-prefixed, ~100 lines each, and read forward only — categorically less parsing surface than a
decoder — but they *are* new attacker-facing code and a bug there is a bug in the security control. My
answer: bound the risk by construction (never seek backwards; treat any malformed length as "refuse, do
not repair"; fuzz-style table tests over truncated/garbage segments), and note that the alternative is
not "no parser" but "a much larger third-party parser plus a decompressor." **An independent challenger
should decide whether that answer is good enough, because I have an interest in it.**

**C-3 — "The avatar exemption is one PR from being wrong, and 'we wrote it on a roster' is how every
forgotten obligation was documented."** Partly sustained. The mitigation is that the roster row is
asserted by a `T1-CI` test, so adding an avatar URL to a cross-user DTO cannot land without touching the
row — but the test asserts the *string*, not the *fact*, so a developer can update the annotation
without doing the work. **The honest options are: (a) accept it, (b) scrub the avatar anyway for ~one
extra call site.** I chose (a) because the avatar is the single-item path whose cost nobody has
measured, and because scrubbing a surface with no fetcher is exactly the "build machinery to be safe"
the ticket brief warns against.

**C-4 — "T-0460 asked for a rule and you produced a four-clause rule with a table. That is four rules."**
Not conceded, but flagged for the lead. The four clauses share one check — *walk the roster row*. If the
lead rules they are separable, the natural split is **S12 = the disclosure law (Q1 + Q3)** and the
served-type clause promoted into S4, which I would accept; I would not accept splitting the no-decode
prohibition out, because it is the reason clause 3 takes the form it does.

**C-5 — "D10 makes the rest optional. You conceded the uploader is the victim, not the adversary."**
The challenge I most want an independent instance to press. My defense is durability, not correctness
(A3), and durability arguments are the easiest to inflate.

### Round B — the independent panel challenge (2026-08-06)

**`agents/backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md`** — challenger in
the threat-model / subject-of-the-metadata lane, a distinct instance from both the author and the lead.
It carries its own `## Lead ruling` index back into §Verdict. Its findings, in one line each:

| # | Finding |
|---|---|
| **CH-1** | Gate 0: D5 and D6 are **shipped**, R1/R2/R3 are **closed**, and ~7 citations point at symbols that no longer exist. The ADR must be re-based before it can be adjudicated |
| **CH-2(a)** | EXIF is written by the capturing device, not the uploading account, and no intake establishes provenance — *"the uploader is the victim"* is **unknowable**, not merely false |
| **CH-2(b)** | On `UploadDisputeEvidence` the uploader is the dispute's own customer with money on the outcome. The ADR documents this and files it under "secondary benefit" |
| **CH-2(c)** | The order-photo audience is wider than "customer, cleaner and admin": `CanBrowseOrderAsync` admits **any** tenant cleaner while a seat is open. The "load-bearing table" is materially wrong |
| **CH-2(d)** | Checked and **no finding**: no admin-side document upload exists. Recorded, with an expiry line owed in D8 |
| **CH-3(i)** | *"10 MiB × 30"* is unreachable — Kestrel's ceiling bounds a request to ≈21 MiB decoded, per the ADR's own cited companion. The conclusion is unaffected; the figure spends credibility for nothing |
| **CH-3(ii)** | Not a challenge but a strengthening: prod autoscale is **CPU-only**, a decoder fails on **memory**, so scale-out never fires and the plan carries seven sites |
| **CH-3(iii)** | The `.csproj` denylist cannot see the real failure mode, and a complete Skia decoding stack is **already deployed** via QuestPDF's native assets. The ADR holds others to exactly this standard in its own D7 |
| **CH-3(iii) sec.** | The licence limb is self-inconsistent — the repo already ships one revenue-threshold-licensed graphics package |
| **CH-4** | The surface D4 elects as the **pilot** has no server-truth content type to dispatch a per-format scrub on: declare `data:image/png`, send JPEG, and the scrub is **a no-op the attacker selects, under a green test** |
| **CH-5** | Generation loss disqualifies A1 and is then adopted, harsher (q0.7 / 1920), by D10 and both shipped mobile compressors. Pick a position |
| **CH-6** | A4's rejection assumes a parser D2 also needs — the honest comparison is "IFD reader + synthesizer" vs "IFD reader + rewriter" |
| **CH-7** | D8's PDF exclusion is evadable in one sentence and imports its justification from a surface it does not cover; dispute PDFs are served **inline**, not `attachment` |

The challenger also named five load-bearing claims it **attacked and could not break** — the avatar's
"audience: self", "nothing calls a decoder", D3's "not the read path", D7's audience-over-delivery
hinge, and D1's refusal of an `IImageSanitizer` seam — because silence is not assent.

## Defense

Rev N+1's disposition per finding. **Every "concede" below is a change in this body, not an
acknowledgement**; every "rebut" cites evidence. The lead's adjudication of each is §Verdict §A.

| # | Disposition | Where it lands in this body, and the substance |
|---|---|---|
| **CH-1** | **CONCEDE + REVISE** | §Context is re-based against HEAD; R1/R2/R3 are gone; **D5 and D6 are restated as ratifications** with current citations; ~7 dead citations replaced (`Constants.ImageSignatures` → deleted, `DocumentContentType` → `SniffedContentType`, `Base64UploadIntakeRosterTests` → `UploadIntakeRosterTests`, `GetOrderPhotos.cs:75` → `:96,105`, `UploadOrderPhoto.cs:112` → `:102`, `DisputeMappers.cs:65-77` → `UploadDisputeEvidence.cs:104-105`). Every surviving `file:line` was re-opened. The ADR is now titled and scoped to its **one** decision |
| **CH-2(a)** | **CONCEDE** | §Context "The threat model, scoped to the surface it is true on" (a). The premise is demoted from a load-bearing claim to an observation about the avatar, on the ground the challenger gives: provenance is an authorization fact away from what the premise needs (`SaveOrderPhotos.cs:114-117`) |
| **CH-2(b)** | **CONCEDE** | (b), plus the **per-surface deferral table** under D10: the deferral is available for order photos and **not** for dispute evidence. The challenger's own severity bound is recorded verbatim in effect — the exposure is *latent*, which bounds urgency and not availability |
| **CH-2(c)** | **CONCEDE** | The audience table is corrected, and **D4's stated reason is replaced**: order photos are scrubbed because the audience is **not enumerable at upload time**, not because it is three known parties. What the content discloses that the DTO withholds — device identity, off-site location, a stable cross-order correlation key — is now named against `GetOrderPhotos.cs:107-109` and ADR-0036 |
| **CH-2(d)** | **ACCEPT (no finding)** | Recorded as checked, and D8 gains the expiry line in the avatar's style |
| **CH-3(i)** | **CONCEDE** | *"10 MiB × 30"* is deleted throughout and restated as **"one bounded upload already suffices; the array cap is irrelevant to the argument"**, with `request-intake-limits.md:26-42` cited for the ceiling that refutes it |
| **CH-3(ii)** | **ADOPT** | Folded into §Context and A1: memory-blind autoscale (`appServicePlan.bicep:70,88`), seven sites on the plan (`:22`), DEV a single fixed instance (`weu.dev.bicepparam:26`). It is a better argument than the one rev N had |
| **CH-3(iii)** | **CONCEDE** | *"nothing decodes an image"* → **"nothing calls a decoder"**, with QuestPDF's shipped native Skia cited (`project.assets.json:832-864,2362-2368`). The prohibition is re-declared a **reachability** property, the package denylist is narrowed to what it can actually see, and a **call-site** enforcer is added — declared `T2-ADVISORY` with a named reviewer check if T-0458 cannot build it. The ADR now applies to itself the standard it applies to others in D7 |
| **CH-3(iii) sec.** | **CONCEDE** | The licence limb is **deleted** from A1/A2. The question is not live because no library is adopted; if an ADR ever overrules D2 it is an **owner/legal** question filed then, and T-0458 AC1 is re-worded |
| **CH-4** | **CONCEDE, and ruled here rather than deferred to T-0459** | **D2.2**: the scrub dispatches on **the bytes it is holding**, never a client string and never a persisted `ContentType` — stronger than either option the challenger offered, and robust to the sibling lane's outcome. Unidentifiable → passed through and reported *not scrubbed*. Consequence: **T-0459 is not gated on the sibling's closing ticket** |
| **CH-5** | **CONCEDE (position picked)** | Generation loss is **not** an A1 rejection ground — the platform ships a harsher version of it (`ImageCompressor.swift:31-32`). A1 stands on resource + PDF-generality. The second half is sustained too: **D2.1 carries the synthetic-corpus burden** precisely because D10 leaves the orientation branch with near-zero production exercise. D10's pricing is corrected to four call sites, documents excluded |
| **CH-6** | **CONCEDE the argument, REBUT the conclusion** | The parser-size claim was false and is deleted — D2 and A4 both need an IFD reader. A4 is still rejected, on **allowlist-vs-denylist** and **no attacker byte reaches the output** (A4, restated). The same two grounds now answer **C-2** better than rev N did: the worry is offset arithmetic over attacker-chosen values, and D2's output contains none |
| **CH-7** | **CONCEDE** | **D8 is scoped per surface.** The employee-document reason is retained only where it is true; the dispute-evidence exclusion rests on the **mechanism limb alone**, with the audience and delivery limbs explicitly withdrawn (`BlobContainerClient.cs:93-110` — no `rscd`). The evasion is named, the accept-set narrowing is escalated as Q-ART-01(b), and the **threat table gains the scriptable-container row** while the "stored XSS: closed" verdict survives |
| **C-1** (self) | **DEFENDED** | The adopted option has no decode step, so the resource cost never has to be quantified to be avoided. The claim that *is* owed a measurement is marked ⚠ and ticketed to T-0458 AC7 on the batch |
| **C-2** (self) | **DEFENDED, conditionally** | Sustained as a real cost, answered by construction — forward-only, length-prefixed, *refuse-never-repair* — **plus the stronger property CH-6 forced out: no attacker byte reaches the output.** The condition is met: D2.1's degradation rule and the synthetic-corpus burden are written into this body |
| **C-3** (self) | **DEFENDED** | The challenger independently verified the exemption is correct on the facts and **could not improve on the mitigation**. Option (a) stands, with its residual risk stated in D4 rather than hidden |
| **C-4** (self) | **DEFENDED** | The clauses share one check — *walk the roster row*. The proposed split is declined: promoting the served-type clause into S4 would file a bytes question under a rule whose check is "read the DTO's field list", the exact discoverability failure D7 exists to fix |
| **C-5** (self) | **CONCEDED IN PART** | Sustained for order photos, **refused for dispute evidence**. The deferral is now per-surface (D10) rather than the binary rev N framed |

## Verdict — LEAD, 2026-08-06

*(Carried **verbatim** as written into rev N by the lead. This is the record of what was adjudicated;
the body above is its transcription, and §C is the closed list that body was written against.
**Its `file:line` citations are frozen as of the lead's 2026-08-06 verification and are not maintained
— the body above is authoritative for every citation.** A few differ deliberately, e.g. §A cites
`GetCurrentUser.cs:44,47-60` and `UploadIntakeRosterTests.cs:47,52`; the re-verified forms are
`:44,47-61` and `:35-38,47,52`.)*

**Adjudicated.** Panel: author (rev N, 2026-08-05) · challenger (threat-model / subject-of-the-
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

---

## Transcription note — rev N+1, 2026-08-06 (author)

Recorded so the next reader can tell what this revision did from what the panel decided.

- **Scope.** §Verdict §C's twelve items and nothing else. No alternative was added or removed beyond
  §C.7's two deleted limbs; no ruling was reopened.
- **Citations.** Every `file:line` in the body above was re-opened at HEAD for this revision. Roughly a
  dozen were corrected beyond §C.1's named six — including `GetOrderPhotos`'s five host routes,
  `UploadOrderPhoto.cs:105-114` → `:112-121`, `UploadDisputeEvidence.cs:17-24` / `:90-94` →
  `:20-27` / `:95-99`, `ImageCompressor.kt` (now `:97,100,248`), and four `patterns-backend.md` ranges
  that moved with the T-0556 follow-up. `IBlobContainerClient.cs:42` was **deleted** rather than
  re-pointed: it was not re-opened.
- **D-numbers are stable.** D5 and D6 keep their numbers while being restated as ratifications, because
  §Verdict, the living doc and the tickets all reference them by number. **D5 and D6 are not pending
  work.**
- **One composition, flagged rather than decided.** §C.10 required the dispute-evidence PDF exclusion to
  carry "its own written reason" without supplying that reason verbatim. It is composed from rulings the
  lead already made — the mechanism limb of D8 survives, the audience and delivery limbs are withdrawn
  as false on that surface (CH-7, CH-7 sec.), the residual evasion is named, and the accept-set
  narrowing is escalated as Q-ART-01(b) per §G. **No new decision was taken.**
- **No shell.** Nothing was compiled, executed or measured in this revision either. The single runtime
  claim in the body carries **⚠ not measured** and names T-0458 AC7 on the 30-item batch as its owner.
