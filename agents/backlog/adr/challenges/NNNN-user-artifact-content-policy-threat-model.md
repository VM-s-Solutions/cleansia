# ADR-NNNN (`user-artifact-content-policy-no-decoder`) — Challenger (threat-model / subject-of-the-metadata lane)

Role: **CHALLENGER**, distinct instance from the author. Date: 2026-08-06.

Gate 0 discipline: nothing below is inherited from the draft, the living doc, or the tickets. Every
claim is traced to a `file:line` I opened at HEAD. Where a boundary held I say so in
§"What I attacked and could not break" rather than inflating it into a finding — **five of the
draft's load-bearing claims survived my attack**, including the central one.

**No shell in this invocation** (`Read`/`Glob`/`Grep`/`Write`). Nothing was compiled, executed or
measured. Where I dispute a runtime number I dispute its *derivation*, not a measurement.

---

## Headline

The author asked for the threat-model inversion to be pressed. **It lands — but not where he aimed
it, and not as "D10 makes the rest optional."**

The sentence *"for metadata the uploader is the victim"* is a claim about **whose metadata is in the
file**. It is true on exactly one of the four surfaces — the avatar — which is the one surface the
ADR exempts. On the other three the uploader is **not the subject**, and on one of them
(`UploadDisputeEvidence`) the uploader is an adversary with a direct financial motive, which the ADR
itself documents and then files under "secondary benefit." So:

- **The rulings mostly survive. The reasons mostly do not.** D4's "scrub order photos and dispute
  evidence" is right for reasons the ADR does not give; the audience table it calls *"the load-bearing
  table"* is materially wrong (CH-2c).
- **C-5's escape hatch must be narrowed.** "If the panel rules D10 sufficient, defer D2/D4 with a
  written trigger" is available for order photos and **not** for dispute evidence.
- **Separately and independently: over half the Decision section now rules on work that has already
  shipped** (CH-1). D5 and D6 are done, R1/R2/R3 are closed, and five citations point at files that no
  longer exist.

And one live design hole the author did not self-name: **the surface D4 elects as the scrub pilot has
no server-truth content type to dispatch a per-format scrub on** (CH-4).

---

## CH-1 — Gate 0: D5 and D6 are **shipped**, R1/R2/R3 are **closed**, and five citations are dead. The ADR must be re-based before it can be adjudicated

The draft's own §Context warns *"do not read those tickets' §Context as current."* The same warning
now applies to the draft. Verified at HEAD:

| Draft claims | HEAD | Evidence |
|---|---|---|
| **D5** — "delete BMP and both TIFF signatures; tighten WebP from `RIFF` to `RIFF`+`WEBP` at offset 8" | **Shipped, exactly as specified** | `src/Cleansia.Core.AppServices/Common/Validators/SniffedContentType.cs:66-78` — no BMP, no TIFF; WebP is two fragments, `RIFF`@0 + `WEBP`@8 (`:72`) |
| **D6** — "widen the roster from 10 rows to 14" | **Shipped**, and the test was renamed | `src/Cleansia.Tests/Common/Validators/UploadIntakeRosterTests.cs:39-55` — 14 rows, each annotated with its guarding rule; `:76-84` a second `[Theory]` naming the four non-`BlobFileDto` intakes so narrowing the predicate cannot silently pass |
| **R1** — "`UploadOrderPhoto` writes `contentType: command.ContentType`; `UploadDisputeEvidence` records nothing and derives from the client file name" | **Fixed, both** | `src/Cleansia.Core.AppServices/Features/Orders/UploadOrderPhoto.cs:102`; `src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs:104-105` — both `SniffedContentType.FromContent(bytes, …)`, extension minted from the table |
| **R2** — "`GetOrderPhotos` emits the raw stored string on the DTO (`:75`)" | **Fixed** | `src/Cleansia.Core.AppServices/Features/Orders/GetOrderPhotos.cs:105` is `ContentType: servedAs.Value`, with `:87-93` documenting the exact hazard R2 names |
| **R3** — "`Constants.ImageSignatures:95-104` admits BMP, TIFF ×2 and any RIFF" | **Fixed, and the symbol no longer exists** | `grep -r ImageSignatures src` → **zero files**. `Constants.cs` ends at `:94` |

`Base64UploadIntakeRosterTests` also no longer exists (renamed to `UploadIntakeRosterTests`), so the
draft's citations at `:31-43` and `:63-66` are dead, as are the five `Constants.ImageSignatures:95-104`
references and the two `GetOrderPhotos.cs:75` / `UploadOrderPhoto.cs:112` ones.

**Why this blocks rather than being an editorial nit.** An ADR that "decides" what already shipped
teaches its next reader that the ADR is where the work happened, and the §"How a reviewer verifies
compliance" list then reads as future work when items 2 and 3 are already green. It also mis-tiers
§D7's enforcement table in both directions:

- *"Q1 + Q3 (audience + scrub declared) — `(gate pending: T-0458)`"* — the **widening** is T1-CI today.
  Only the two extra columns are pending. Say that.
- *"Q2 (accept = serve) — `(gate pending: T-0458)`"* — still correctly pending, but for a **different
  reason** than the draft gives. Accept ⊆ serve is now true *by construction* (one `Signatures` table,
  `AcceptedByIntake` selecting from it, `SniffedContentType.cs:66-104`), and I checked
  `src/Cleansia.Tests/Infrastructure/ServedContentTypeTests.cs` — there is **no** assertion that every
  `Signatures` MIME resolves to a non-`Opaque` `ServedContentType`. The construction is unpinned, so a
  seventh row added to `Signatures` that `ServedContentType` cannot serve reintroduces R3 silently.

**Ask:** delete D5 and D6 or restate them as *ratifications with citations*, and let the ADR shrink to
the one decision it still owns — *metadata is scrubbed at intake, by audience, without a decoder*.
One decision per ADR.

> **Process note for the lead/PM, not a design challenge.** `T-0458` and `T-0460` both carry
> `status: done` / `updated: 2026-08-06` in their front matter, while every AC checkbox is unchecked,
> both status logs end 2026-08-05 with *"Still not `ready`"*, and this ADR is `proposed` with no panel
> run. The tickets and the ADR disagree about whether this work is finished. That has to be reconciled
> before an `accepted` stamp means anything.

---

## CH-2 — The uploader is the subject on **one** of four surfaces. On dispute evidence the uploader is a paying adversary, and the ADR says so itself

This is the challenge the author flagged. Pressed properly, it does not sustain "D10 is sufficient" —
it does the opposite on one surface and rewrites the reasons on another.

### (a) EXIF is written by the capturing device, not by the uploading account — and nothing establishes they are the same party

The premise is *"a cleaner has no motive to hand-craft an API call that re-attaches their own home
GPS."* True. It is also not the question. A cleaner may upload a photo a colleague sent them; a
customer may upload a photo forwarded to them; either may upload a photo of a document photographed
by someone else. No intake in this codebase establishes provenance — `UploadOrderPhoto` and
`SaveOrderPhotos` check that the caller is **assigned to the order**
(`UploadOrderPhoto.cs:97-100`, `SaveOrderPhotos.cs:114-117`), which is an authorization fact, not a
capture fact.

So the premise is not merely *false against an adversary*; it is **unknowable in the ordinary case**.
"The uploader is the victim" is a statement the platform is not in a position to make. That alone
demotes it from a load-bearing threat-model claim to an observation about the avatar.

### (b) `UploadDisputeEvidence`: adversarial uploader, money on the outcome — the ADR documents this and mis-files it

D4's own dispute row:

> *"Secondary benefit: EXIF timestamps on evidence are **client-forgeable**, so removing them removes
> a signal an adjudicator might otherwise trust."*

A forgeable signal, on an artifact submitted into a money-bearing adjudication, by the party who
benefits from the outcome, **is** an adversarial uploader. Evidence that the uploader is that party
and that the process is adversarial:

- `src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs:95-99` — the handler
  refuses unless `dispute.UserId == userId`. The uploader is the dispute's own customer.
- The dispute's counterparty is a cleaner and its outcome is a refund; `AdminDisputeController`
  exposes `resolve` / `update-status` (`src/Cleansia.Web.Admin/Controllers/AdminDisputeController.cs:42,55`).

Against that uploader, "the client strips it" is defeated by one `curl` with the caller's **own valid
token** — the exact structure T-0458 described and the ADR dismissed as an imported XSS frame. **For
this surface it is not imported; it is the actual model.** The server-side scrub here is
*enforceability*, not durability.

**Consequence for C-5 / A3, and this is the operative ask:** the deferral the author offers —
*"ship D10, ship D5 and D6, ship D7's rule, defer D2/D4 with a written trigger"* — is a legitimate
outcome for `SaveOrderPhotos`/`UploadOrderPhoto`. It is **not** available for
`UploadDisputeEvidence`. The panel should rule **per surface**, which the ADR's own audience framing
already supports and which is a better result than the binary the author framed.

**Honest bound on severity, stated so the lead can weigh it.** No surface reads EXIF today —
`DisputeEvidenceDto` carries `FileName` / `BlobUrl` / `UploadedOn` (server time), so a forged
`DateTimeOriginal` is only reachable by an adjudicator who downloads the file and inspects it. This is
**latent, not live**. It bounds the urgency; it does not restore the premise, and the premise is what
the ADR's sequencing rests on.

### (c) The order-photo audience is wider than "customer, cleaner and admin" — the load-bearing table is wrong

`GetOrderPhotos` gates on `CanBrowseOrderAsync`, **not** `CanAccessOrderAsync`:

- `src/Cleansia.Core.AppServices/Features/Orders/GetOrderPhotos.cs:59`
- `src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs:68-92` — after owner/admin/assigned
  fails, **any** caller with role `Employee` and a resolvable `employeeId` gets `true` when
  `order.HasAvailableSpots && OrderVisibility.NotHeldFrom(order, employeeId, now)`. The comment at
  `:84-87` is explicit that this branch is *"both browse surfaces at once — order detail and order
  photos."*

So the fetch set for an order photo is: the customer, admins, every assigned cleaner, **and every
cleaner in the tenant who can see the order in the available list while a seat remains open.** Seats
are `RequiredEmployees = ceil(EstimatedTime / 120)` capped by the 24 h span, i.e. up to 12 — so on a
multi-seat order cleaner A's "before" photos are fetchable by cleaner B who has not taken the job and
never will.

**What that discloses that the DTO does not.** I checked before claiming it: the customer's street
address and lat/long are **already** on `OrderListItem`
(`src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs:101-104,159-160`), so the *job's* GPS is not
new information to a browsing cleaner. What **is** new is the uploading cleaner's **device identity**
(`Make`/`Model`/body and lens serials/`MakerNote`) and, for any photo taken away from the job, **that
cleaner's own location** — handed to an arbitrary other cleaner.

That matters because the platform treats cleaner identity as protected *elsewhere, deliberately*:

- `GetOrderPhotos.cs:107-109` withholds `CapturedByEmployeeId` and the surname from a customer caller.
- `PreferredEmployeeId` is never on a partner-facing DTO (ADR-0036).

A device serial inside the bytes is a stable cross-order correlation key that walks straight through
both controls. **This is the S12 argument in its purest form** — a DTO-level control defeated by
content — and the ADR does not have it, because its table stops at "customer, cleaner and admin."

**Net effect: D4's ruling gets stronger, its stated reason gets replaced.** Order photos need the
scrub not because the audience is three known parties, but because **the audience is not enumerable at
upload time and includes parties with no relationship to the job**. Fix the table; keep the ruling.

### (d) "An operator recording a document" — checked, and it does not exist. Recording that, because D8 depends on it

I grepped `src/Cleansia.Web.Admin` for `[HttpPost]`/`[HttpPut]` actions taking `IFormFile`,
`BlobFileDto` or `byte[]` — **there are none**, and the roster confirms all four document intakes are
`Web.Partner` / `Web.Mobile.Partner` (`UploadIntakeRosterTests.cs:45-46,50-51`). So no operator
uploads into a cleaner's file today, and D8's *"the metadata discloses to the one party that already
has more"* holds **for the employee-document surface**. If an admin-side document upload is ever added,
that sentence inverts (the operator's scanner/workstation identity flows to the cleaner) — worth one
line in D8 as the expiry condition, in the same style as the avatar's.

---

## CH-3 — The no-decoder prohibition is right; its headline arithmetic is wrong, and its proposed enforcer cannot see its actual failure mode

### (i) "10 MiB × 30 attacker-chosen items" is not reachable, and the ADR cites the document that says so

`agents/architecture/decisions/request-intake-limits.md:26-42`: `MaxRequestBodySize` /
`RequestSizeLimit` / `MultipartBodyLengthLimit` appear **nowhere** in `src/**`, so the effective
ceiling is **Kestrel's 30,000,000 B (≈28.6 MiB)** on all five hosts; base64 is +33 % over the decoded
cap, so *"2 max-size files fit, the 3rd does not."* The real per-request decoder input is ≈21 MiB
decoded, not ≈300 MiB. The draft cites that document for the plan SKU and not for this.

**The conclusion survives on a smaller number** — a single ~300 KB single-colour PNG decodes to ≈3.6 GB,
so the `×30` was never doing the work. But the figure is repeated in the draft, in T-0458's Gate 0
block and in the living doc §3, and a number a reader can falsify from the ADR's own citation costs
credibility the ruling does not need to spend. Restate it as *"one bounded upload is already
sufficient; the array cap is irrelevant to the argument."*

### (ii) A fact the author did not have, which makes D2 stronger

`deploy/bicep/modules/appServicePlan.bicep:52-104` — the prod autoscale rule is **CPU-driven only**
(`metricName: 'CpuPercentage'`, +1 above 70 % over 10 min, 10-min cooldown). A decoder's failure mode
is **memory**, not CPU. So scale-out never fires, and the plan hosts *"the 5 APIs + SSR + Functions"*
(`:1-2,22`) — an OOM takes down all seven sites on the instance. Put this in D2; it is a better
argument than the one there.

### (iii) The denylist enforcer is scoped to the wrong artifact — and a complete image decoder is **already deployed**

I verified the direct claim: `SixLabors|SkiaSharp|System.Drawing|Magick` across `src/**/*.csproj`
returns exactly one line, `QuestPDF` (`src/Cleansia.Infra.Services/Cleansia.Infra.Services.csproj:14`,
pinned at `src/Directory.Packages.props:55` to 2024.12.1). **True.** But QuestPDF 2024.12.1 ships its
own native Skia as runtime assets:

- `src/Cleansia.Infra.Services/obj/project.assets.json:831-841` —
  `runtimes/linux-arm64/native/libQuestPdfSkia.so`, `libqpdf.so`, and the musl/x64 siblings.
- `:2352-2370` — the bundled external-dependency licences: `libjpeg-turbo.txt`, `libpng.txt`,
  `libwebp.txt`, `wuffs.txt`, `skia.txt`, `zlib.txt`.

**A full JPEG/PNG/WebP decoding stack is already on the Linux App Service image.** What is absent is a
**call site** — I checked: `.Image(` / `ImageDescriptor` / `Image.FromBinaryData` return **zero
matches** across `src/**/*.cs`, and `Cleansia.Infra.Services` contains no `Image`/`Skia` symbol at all.
So D2's *fact* holds at HEAD. Its *enforcer* does not:

> *"walk `src/**/*.csproj` for a package-reference denylist (`SixLabors.*`, `SkiaSharp*`,
> `System.Drawing.Common`, `Magick.NET*`) … per the `WebSdkContentGlobTests` shape"*

One `.Image(orderPhotoBytes)` inside a QuestPDF document — and *"attach the order photos to the
dispute pack / the invoice"* is an ordinary next feature on a codebase that already generates invoice
PDFs (`PayPeriodBackgroundService.cs:359`, `RegenerateInvoicePdf.cs:102`) — creates precisely the
primitive D2 refuses, on a request path, **and the denylist stays green.** It matches none of those
four strings and it is not a `PackageReference`.

The prohibition is a **reachability** property ("no request path decompresses user-supplied image
data"); a package-name denylist cannot express it. **The ADR is holding others to a standard it fails
here**: §D7 correctly insists *"do not label this rule `T1-CI` wholesale"* and the living doc §5
correctly notes a mechanism that cannot fail a build is advisory however labelled (ADR-0032 /
`enforcement.md`). Same test, applied inward: this gate cannot fail the build on the real failure mode.

**Minimum repair (author's choice of two):** keep the package denylist *and* add a symbol-level
assertion that no type in `Cleansia.Core.AppServices` / `Cleansia.Infra.Services` references
`QuestPDF.Fluent.ImageExtensions` / `QuestPDF.Infrastructure.Image`; **or** state plainly in the ADR
that the prohibition is enforced at **T1-CI for a direct package reference** and **T2-ADVISORY for the
transitive/call-site case**, and name the reviewer check.

**Secondary, and it needs an owner not an architect.** A1 is rejected partly on *"a licence question
that is legal rather than technical."* QuestPDF is distributed under a **revenue-threshold** licence
structure of the same shape as Six Labors' split licence. I have not verified the current terms of
either and am not asserting them — but if that structure is disqualifying for ImageSharp, the codebase
has already accepted one, and if it is manageable here it is not a rejection ground there. **Pick one,
or drop the licence limb** (A1's rejection is over-determined by the resource and PDF-generality
arguments and loses nothing without it).

---

## CH-4 — The scrub has no server-truth format to dispatch on, on the exact surface D4 elects as the pilot

D2 is a per-format design (JPEG segments / PNG chunks / RIFF chunks / GIF passthrough) and D1 rules
*"one helper per format, called by the two handlers that need it — not a seam."* That makes the
**handler** the dispatcher. On `SaveOrderPhotos`, the handler has no byte-derived type to dispatch on:

- `src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs:130` →
  `DetermineContentType(file.FileName!, file.Base64Content)`, defined at `:171-184`: the client's
  `data:` URI prefix first, then the client's **file extension**, then a hardcoded **`"image/jpeg"`
  default** (`:183`). No signature check anywhere on this path.
- The roster records it in writing: `"…OrderController.SavePhotos — BlobFileSize only"`
  (`UploadIntakeRosterTests.cs:47,52`), and the class doc at `:35-37` names it as the one entry with
  no content rule of its own.
- It is deliberate and pinned: `src/Cleansia.Tests/Features/Orders/SaveOrderPhotosContentTypeTests.cs:14-26`
  calls it *"the one upload pipeline with no content-type allowlist anywhere,"* and `:50` proves that a
  `data:image/png` prefix on arbitrary payload records `image/png`.
- `src/Cleansia.Core.Blobs.Abstractions/ServedContentType.cs:8-9` still names this as live, in the
  present tense.

So a scrub wired here dispatches on a **client-chosen string that is provably not the byte format**.
Declare `data:image/png`, send JPEG bytes: the PNG chunk walker runs over a JPEG. Best case it finds
no `IHDR`, bails, and the metadata survives — **a no-op the attacker selects, and a green
"scrub was applied" test**. Worst case it is a length-arithmetic fault in brand-new attacker-facing
parsing code, which is C-2's entire worry, reached by editing one word in a data URI.

**Note the inversion.** The draft's R1 reports this shape on `UploadOrderPhoto` and
`UploadDisputeEvidence`. Both are fixed (CH-1). **The sibling left behind is now the batch route — and
D4 makes it the pilot.**

**What the ADR owes, and it cannot be left to T-0459:** either (a) the scrub sniffs its own bytes and
D2 says so — which weakens D1's "nothing here is shareable," since the sniff *is* the shareable part
and `SniffedContentType` already exists with an `OrderPhoto` intake; or (b) `SaveOrderPhotos` adopts
`SniffedContentType.FromContent(…, UploadIntake.OrderPhoto)` **first**, and D2 is defined as running
only against a byte-derived type. (b) is roughly one line, closes a live inconsistency between the two
order-photo routes regardless of whether the scrub ever ships, and makes the roster's fourteenth
annotation stop being an exception.

---

## CH-5 — Generation loss disqualifies A1 and is then adopted, at a harsher setting, by D10 — which ships first

D2 rejects decode+re-encode partly on *"JPEG generation loss on evidentiary photos."* D10 mandates the
web clients re-encode on pick and prices it *"~30 lines per picker, zero server cost."* A canvas
re-encode **is** a decode + re-encode with generation loss — the identical cost, on the identical
evidentiary photos, relocated to a machine the platform does not control.

And the platform has already accepted a **strictly harsher** version on the majority of order-photo
uploads:

- `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift:20-34` — downscale to
  `maxDimension`, then `quality: CGFloat = 0.7`.
- `src/cleansia_android/core/src/main/java/cz/cleansia/core/media/ImageCompressor.kt:62,99-100` —
  `MAX_DIMENSION` 1920, `JPEG_QUALITY = 70`, full `BitmapFactory` decode + `Bitmap.compress`.

A server-side re-encode at q0.9 with no downscale would be **less** destructive than what ships today.
One of these must be true and the ADR must say which:

1. Generation loss is acceptable — then it is not an A1 rejection ground, and A1's rejection stands on
   the resource and PDF-generality limbs alone (which is enough); or
2. Generation loss is unacceptable on evidentiary photos — then D10 and both shipped mobile compressors
   are defects, and D10 cannot ship first.

The current draft holds both positions at once.

**And this is not pedantry, because D10-first starves D2's riskiest code path.** D2's
orientation-preserving minimal-EXIF emitter is called *"the fiddly part … the single most likely place
to ship a visible regression."* Once every client the platform controls re-encodes on pick — mobile
already does, web after D10 — new uploads arrive with orientation baked into the pixels and **no EXIF
at all**, so that emitter executes in production almost exclusively for the residual old-client /
third-party traffic it was built for. New, hand-rolled, attacker-facing code with near-zero production
exercise is the worst available combination. Either D2 carries a synthetic-corpus test burden
proportional to that, or A4 deserves the second look it did not get (CH-6).

**Also unpriced in D10:** there are at least four distinct web file-read call sites, not one per app —
`libs/cleansia-customer-features/profile/src/lib/profile/profile.models.ts:59-66` (customer avatar),
`libs/cleansia-partner-features/orders/src/lib/order-details/components/order-photos.component.ts:124-140`
(partner order photos, which feeds `SaveOrderPhotos` — confirming the unscrubbed client and the
untyped route are the same path),
`libs/cleansia-partner-features/profile/src/lib/profile/profile-documents.facade.ts:145-150`
(**documents** — canvas re-encode must *not* apply here), and the shared
`libs/shared/utils/src/file-transformation.utils.ts:38-142`, which sets
`contentType: file.type || 'application/octet-stream'` and `fileName: file.name` (`:127-129`). A canvas
re-encode changes both, and `SaveOrderPhotos.DetermineContentType` reads both. "~30 lines per picker"
has to become "which pickers, and what happens to the name and the declared type."

---

## CH-6 — A4's rejection does not survive its own reasoning, because D2 needs the same parser

A4 (strip specific EXIF tags) is rejected because *"it requires a full EXIF/TIFF IFD parser with offset
rewriting … Against an adversarial file, the smaller parser wins."*

But D2's JPEG rule is *"re-emit a minimal EXIF `APP1` carrying only the `Orientation` tag when the
original carried one in 2–8."* To know that, the scrub must **read** the original `APP1` payload: the
TIFF header byte order (`II`/`MM`), the IFD0 offset, the entry count, and the 12-byte entries, hunting
tag `0x0112` — that **is** an EXIF/TIFF IFD reader. Then it must **write** a well-formed TIFF header +
IFD + value.

The delta A4 was rejected over is the *offset rewriting on write-back*, not the parser. The honest
comparison is **"IFD reader + synthesizer" vs "IFD reader + rewriter"** — far closer than the ADR's
framing — and A4 additionally preserves ICC and `COM` and needs no orientation special case at all,
because it never removes the tag. **Re-score A4, or restate the rejection with the narrower reason that
actually distinguishes them.**

---

## CH-7 — D8's exclusion is evadable in one sentence, and its justification is imported from a surface it does not cover

D4 rules dispute evidence gets the scrub *"the image formats."* The dispute intake's accepted set is
`{image/jpeg, image/png, image/webp, application/pdf}` (`SniffedContentType.cs:92-95`).

D8 excludes PDF from the scrub and justifies it on the **employee-document** audience: *"an admin who
already holds the cleaner's legal name, tax id and payout details … the metadata discloses to the one
party that already has more."* That sentence is about a different surface. On the dispute path the
uploader is a **customer**, the adverse party is a **cleaner**, and the fetcher is **staff adjudicating
money** — "already has more" is not true of anyone in that triangle.

**And the evasion is free.** An uploader who wants metadata preserved wraps the photo in a PDF. Same
intake, same allowlist, scrub does not apply. Combined with CH-2(b) — where the uploader has a motive —
this is not a hypothetical shape.

**One supporting clause of D8 is also factually wrong for this surface.** D8 says the excluded artifacts
are *"served as an attachment, byte-typed, and never by URL."* Dispute-evidence PDFs are served **by
URL, inline**: `UploadDisputeEvidence.cs:121-126` and `src/Cleansia.Core.AppServices/Mappers/DisputeMappers.cs:74`
mint a SAS with `ServedContentType.ForRecordedType("application/pdf")`, and
`src/Cleansia.Infra.Azure.Storage.Blobs/BlobContainerClient.cs:100-104` sets `rsct` and `rscc` only —
**no `rscd`**, so no `Content-Disposition: attachment`.

*Flagged, explicitly not blocking, so the lead does not over-weight it:* an inline `application/pdf`
from the storage origin is not equivalent to stored XSS — the storage host carries no app session and
browser PDF viewers are sandboxed — and the ADR's "stored XSS: closed" verdict survives. But the
threat table's row reasons over `text/html`/`image/svg+xml` and never mentions that the closed set
admits a **scriptable container**; one sentence is owed either way.

**Minimum:** D8's exclusion must be scoped per surface, and the roster's `scrub: none` reason for
dispute-evidence PDFs written independently of the employee-document reason — **or** drop
`application/pdf` from `UploadIntake.DisputeEvidence`, which is the cheaper answer and worth pricing
(the flow is photo evidence; the PDF affordance there is not obviously earned, and unlike Q-ART-01 it
changes no five-locale promise about employee documents).

---

## What I attacked and could not break — sustained, with what I checked

Silence is not assent, so these are named:

1. **The avatar's "audience: self" holds.** Verified independently rather than inherited.
   `src/Cleansia.Core.AppServices/Features/Users/GetCurrentUser.cs:44,47-60` is the **only** SAS mint
   against `user-files` (grepped the symbol and the container constant across `src/**`).
   `UserMappers.cs:23` (`UserListItem`) and `:66` (`UserItem`) map the photo with **no** URL; only
   `MapToMyProfileDto:44` takes one. `EmployeeMappers.cs:37,63` likewise. I then went looking for the
   leak the table would miss — the admin subject-access dump — and
   `src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprExportDto.cs:85-90` carries file **names**,
   not bytes or URLs. **D4's avatar exemption is correct on the facts.** C-3's residual risk is the
   *expiry mechanism*, and I have nothing better than the author's own option (b): the roster asserts a
   route→rule **string** (`UploadIntakeRosterTests.cs:39-55`), and adding audience/scrub columns keeps
   it a string assertion. That concession is already in the draft and I could not improve on it.
2. **"Nothing here decodes a user image" is true at HEAD.** Zero `.Image(` / `ImageDescriptor` /
   `Image.FromBinaryData` in `src/**/*.cs`; zero `sharp`/`jimp`/`canvas` in
   `src/Cleansia.App/package.json`; `OrderPhoto.Width`/`Height` never populated by either writer. The
   property is real and worth protecting. CH-3(iii) attacks the **enforcer**, not the fact.
3. **D3's "not the read path" is right and is the reason D9 is a real migration.** The SAS hands the
   client the stored bytes; `rsct` retypes a response header
   (`BlobContainerClient.cs:100-104`, pinned by `SasResponseHeaderOverrideTests:42,75,79`) and cannot
   change a byte. Content can only be altered where we hold it.
4. **D7's audience-over-delivery hinge is right.** Employee documents are served by three API routes
   with `attachment`, not by URL; a rule keyed on *"served back by URL"* would exclude the surface
   carrying the most metadata. CH-7 attacks D8's **exclusion**, not this hinge.
5. **D1's refusal of an `IImageSanitizer` seam holds, and the refusal to decorate
   `IBlobContainerClient.UploadAsync` is clearly right** — that sink also writes our own invoice PDFs
   (`PayPeriodBackgroundService.cs:359`, `RegenerateInvoicePdf.cs:102`), so an unconditional decorator
   would rewrite our own documents and a conditional one has lost the property that made it
   attractive. I looked for a genuine shared abstraction at the transform layer and agree there is
   none. **But** the sniff *is* the shareable part, it already exists, and CH-4 turns that into a
   better argument for D1 than the one given.
6. **The central ruling — no decoder on a request path — survives everything I could throw at it**, and
   CH-3(ii) (CPU-only autoscale on a shared 7-site plan) strengthens it. My objections are to the
   arithmetic, the enforcer and the sequencing, **not to the ruling**.

---

## Verdict requested of the lead

| # | Challenge | Ask |
|---|---|---|
| CH-1 | D5/D6 shipped; R1/R2/R3 closed; five dead citations | **BLOCKS until re-based.** Restate as ratifications or delete; re-tier the two enforcement rows; shrink the ADR to its one remaining decision |
| CH-2(b) | Dispute evidence has an adversarial uploader; the ADR documents it as a benefit | **BLOCKS.** The threat-model sentence must be scoped to the avatar, and **C-5's deferral option must be restricted per surface** — not available for `UploadDisputeEvidence` |
| CH-2(c) | Order-photo audience includes any browsing cleaner (`OrderAccessService.cs:68-92`) | **BLOCKS.** The "load-bearing table" is wrong. D4's ruling survives; its reason must be replaced |
| CH-4 | The pilot surface dispatches a per-format scrub on a client string | **BLOCKS.** Rule (a) or (b) in the ADR, not in T-0459 |
| CH-3(iii) | Denylist enforcer cannot see the transitive/call-site failure mode; Skia is already deployed via QuestPDF | **BLOCKS.** Add the symbol-level assertion or declare the clause `T2-ADVISORY` for that case |
| CH-3(i) | "10 MiB × 30" refuted by the ADR's own cited companion | Stands unless corrected. Conclusion unaffected |
| CH-5 | Generation loss disqualifies A1, then is adopted by D10 at q0.7 | Stands unless the author picks a position |
| CH-6 | A4's rejection assumes a parser D2 also needs | Stands unless A4 is re-scored or the rejection restated |
| CH-7 | D8's PDF exclusion is evadable and imports its justification | Stands. Scope per surface, or drop PDF from the dispute accept set |
| CH-2(a) | Uploader ≠ capturer; provenance is unknowable | Stands. It is the general form of (b) and (c) |
| CH-2(d) | No admin-side upload exists | **No finding.** Recorded as checked; suggest one expiry line in D8 |

**Not manufactured:** the no-decoder ruling, the avatar exemption, D3, D7's hinge and D1 all held. My
position is that this ADR reaches the **right answers** on a **materially out-of-date map**, with one
live hole (CH-4) and one enforcement claim it cannot honour (CH-3iii). Re-based and with the reasons
replaced, I would not block it.

---

## Lead ruling — 2026-08-06

Recorded here so this challenge is not left dangling. **The full adjudication is the `## Verdict`
section of `../drafts/NNNN-user-artifact-content-policy-no-decoder.md`**; this is the index into it.

**Every finding above was re-verified by the lead at HEAD before ruling — none was taken on trust.**

| # | Ruling | Where the repair is decided |
|---|---|---|
| CH-1 | **STANDS** | Verdict §C.1 — D5/D6 demoted to ratifications, ~7 citations replaced |
| CH-2(a) | **STANDS** | §C.3 |
| CH-2(b) | **STANDS** | §B.5 — the deferral becomes per-surface; **not available** for `UploadDisputeEvidence` |
| CH-2(c) | **STANDS** | §C.2 — ruling survives, reason replaced with *"the audience is not enumerable at upload time"* |
| CH-2(d) | **NO FINDING**, recorded as checked | §C.10 — the expiry line lands in D8 |
| CH-3(i) | **STANDS** | §C.5 — restated as *"one bounded upload already suffices"* |
| CH-3(ii) | **ACCEPTED** (a strengthening, not a challenge) | §C.5 — folded into D2 |
| CH-3(iii) | **STANDS** | §B.6 — the prohibition is re-declared a **reachability** property; the package denylist is honestly narrowed and a call-site enforcer added, `T2-ADVISORY` if it cannot be built |
| CH-3(iii) sec. (licence) | **STANDS** | §B.2 — the licence limb is **deleted** from A1/A2; the decision does not depend on it |
| CH-4 | **STANDS** | §B.1 — ruled **in this ADR**, as you asked, and in a form stronger than either option you offered: **the scrub dispatches on the bytes it is holding**, never on a persisted `ContentType`. Consequence: T-0459 is **not** gated on §7.1's closing ticket |
| CH-5 | **STANDS** | §B.2 — position picked: generation loss is **not** an A1 rejection ground. Your D10 pricing correction (four web call sites, documents excluded, `file-transformation.utils.ts:127-129`) is folded into the living doc §8.3 |
| CH-6 | **STANDS** | §B.3 — rejection **restated**, not re-scored: allowlist-vs-denylist and *no attacker byte reaches the output* |
| CH-7 | **STANDS** (incl. the inline-PDF limb, independently verified at `BlobContainerClient.cs:93-110`) | §C.10 — D8 scoped per surface; the accept-set narrowing is escalated to the owner as product, not decided here |

**Verdict: REVISE, not blocked.** Your closing position — *"re-based and with the reasons replaced, I
would not block it"* — is adopted as the panel's. Rev N+1 is a **transcription pass against the closed
list**, so **no further challenge round is convened**: T-0459's surfaces are cross-user visible today and
another round would cost exposure time without changing an answer.

**One correction to your process note:** T-0458 and T-0460 were both reopened to `status: blocked` on
2026-08-06 (T-0458's front matter carries the reopen note at `:19-21`, citing that the closing commit
pointed at an unratified draft). The tickets and the ADR now agree. T-0459 is still `draft`.
