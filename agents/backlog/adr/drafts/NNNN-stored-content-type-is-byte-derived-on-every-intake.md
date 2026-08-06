# ADR-NNNN (DRAFT — number NOT allocated) — The stored content type is derived from the bytes on **every** intake; the `SaveOrderPhotos` exception closes

- **Status:** `proposed`
- **Date:** 2026-08-06 (drafted)
- **Mode:** **author**. A challenger and a lead are owed (`process/deliberation.md`).
- **Number:** not allocated. Highest on disk is **0042**; five drafts await allocation. The PM allocates.
- **Supersedes:** nothing. **Amends in practice:** the unwritten carve-out currently recorded only in
  `Cleansia.Tests/Common/Validators/UploadIntakeRosterTests.cs:34-38` and
  `Cleansia.Core.Blobs.Abstractions/ServedContentType.cs:7-14`.
- **Consumes:** T-0464 (`ServedContentType`, the read clamp), T-0548, T-0556 + follow-up
  (`SniffedContentType`, `BlobFileSize`, the 14-row roster), ADR-0032 (a constraining entry names an
  enforcer and declares a tier), ADR-0033 (routing test 1 fires → Architect).
- **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md` §7
- **Tickets it creates:** the closing ticket (`T-0561` proposed; PM confirms the id at filing)

> ### ⚠️ Method declaration
> **1. No panel has run.** One architect instance wrote this. `## Challenge` below is an author-run
> self-challenge and is weaker by construction. **This ADR may not be `accepted` until distinct
> author / challenger / lead instances have run.**
>
> **2. No shell in this invocation** (`Read`/`Glob`/`Grep`/`Write`/`Edit`; no `Bash`, no `git`). Nothing
> was compiled, executed or measured. Every fact below is read from source at HEAD and cited at
> `file:line`. **No claim in this ADR is inherited from a prior ticket or ADR** — three statements in
> `user-uploaded-artifacts.md` §2 were stale when I checked them (R1 and R3 are closed;
> `Constants.ImageSignatures` no longer exists — `grep` over `src/` returns nothing), so nothing was
> taken on trust.

---

## Context

### The question routed here

Fourteen upload routes are enumerated by `UploadIntakeRosterTests.cs:39-55`. Thirteen derive their
stored content type from the payload's own bytes. One does not: **`SaveOrderPhotos`**, on
`Web.Partner` and `Web.Mobile.Partner`. The lane that consolidated the other thirteen declined to close
it and gave two reasons: closing it *"would refuse uploads that succeed today on a live mobile path"*,
and generalising the catalog sentence *"puts a blessed exception in violation."* It routed the call here
rather than smuggling either edit in — correctly, under ADR-0033 routing test 1.

### What `SaveOrderPhotos` actually does — verbatim, at HEAD

`src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs:171-184`:

```csharp
private static string DetermineContentType(string fileName, string? base64Content)
{
    if (!string.IsNullOrEmpty(base64Content) && base64Content.StartsWith("data:"))
    {
        var declared = ServedContentType.ForRecordedType(base64Content.Split(';')[0].Replace("data:", ""));
        if (declared != ServedContentType.Opaque) { return declared.Value; }
    }

    var byExtension = ServedContentType.ForFileName(fileName);
    return byExtension == ServedContentType.Opaque ? "image/jpeg" : byExtension.Value;
}
```

Three tiers, none of which reads a byte of the payload:

1. the caller's `data:` URI prefix, canonicalised through `ServedContentType`;
2. else the caller's file **name** extension, through the same set;
3. **else the string literal `"image/jpeg"`.**

The result is written to `OrderPhoto.ContentType` (`:148`), and `GetOrderPhotos.MapToDto` (`:96,101,105`)
uses it for **both** the DTO's `ContentType` and the SAS `rsct` response-header override. The blob name's
extension is `Path.GetExtension(file.FileName)` (`:132-133`) — the caller's string again — where the
sibling `UploadOrderPhoto.cs:103` mints it from the sniff.

### Test 1 — does the exception survive? What *can* a client store, and what does a reader get?

The exception rests on one claim: **the read-path clamp bounds the damage.** The clamp is real and I
verified it end to end. It bounds the damage to the wrong set.

| | `SaveOrderPhotos` today | `UploadOrderPhoto` (same table, same container, same read path) |
|---|---|---|
| Stored type derived from | client `data:` prefix → else file name → else literal `"image/jpeg"` | the bytes (`:102`) |
| Types a caller can store | `image/jpeg`, `image/png`, `image/webp`, **`image/gif`**, **`application/pdf`** | `image/jpeg`, `image/png`, `image/webp` |
| Bytes constrained | **not at all** | `SniffedContentType.FromContent(…, OrderPhoto)` (`:67`) |
| Blob-name extension | caller's `FileName` (`:132`) | server-minted from the sniff (`:103`) |
| Undecodable base64 | **unguarded** → `Convert.FromBase64String` at `:136` → 500 | decodability covered by the byte rule |

**The clamp bounds to the SERVE set, not to the ACCEPT set, and the two differ.**
`ServedContentType.ServableTypes` (`ServedContentType.cs:34-42`) holds **six** values;
`AcceptedByIntake[UploadIntake.OrderPhoto]` (`SniffedContentType.cs:91`) holds **three**. So a caller
sending `data:application/pdf;base64,<arbitrary bytes>` stores `application/pdf` and
`GetOrderPhotos.cs:140` mints a SAS with `rsct=application/pdf`. `BlobContainerClient.GenerateSasUri`
(`:89-110`) sets `ContentType` and `CacheControl` and **no `ContentDisposition`** — verified by reading
the builder — so those bytes render inline, as a PDF, from a storage host shared by every tenant. Same
for `image/gif`. Neither format is offered by any order-photo client
(`order-photos.helpers.ts:17-22`: `image/jpeg`, `image/jpg`, `image/png`, `image/webp`), and the
sibling intake on the same container refuses both.

**What the reader actually gets, in three cases** (all read out of `GetOrderPhotos.MapToDto`):

| Uploaded as | Stored `ContentType` | DTO `ContentType` + SAS `rsct` | What a client sees |
|---|---|---|---|
| `data:text/html` / `data:image/svg+xml` + HTML bytes | `"image/jpeg"` (tier 3 fallback) | `image/jpeg` | broken tile. **No XSS** — confirmed |
| `data:application/pdf` + arbitrary bytes | `application/pdf` | `application/pdf`, no disposition | renders inline in the browser's PDF viewer |
| a HEIC named `photo.jpg`, bytes ≠ JPEG | `"image/jpeg"` | `image/jpeg` | broken tile, **silently**, with the row asserting a format the bytes do not have |

**Ruling on the XSS half: the exception's central claim is TRUE.** `text/html` and `image/svg+xml`
are absent from `ServableTypes` by name (`:34-42`), unknown input resolves to `Opaque`, `Opaque` is
outside the MIME-sniffing standard's sniffable set, the containers are `PublicAccessType.None`
(`BlobContainerClient.cs:151`) so the unsigned `BlobUrl` in the command's own response is not
fetchable, and `SaveOrderPhotosContentTypeTests.cs:32-47` pins it. **Stored XSS is not reachable
through this path and closing it does not close an XSS hole.** Any framing of this ADR as a
vulnerability fix is wrong and I am not making one.

**Ruling on the rest: the exception does not survive.** Three findings, none of which the "bounded"
argument covers:

- **F1 — the bound is one set too wide.** `application/pdf` and `image/gif` are storable and servable
  here and nowhere else on this container. The catalog already forbids exactly this shape:
  *"a format the server accepts that no client offers and `ServedContentType` cannot serve is an upload
  that succeeds and an image that never renders"* (`patterns-backend.md:1324-1326`). This is that rule's
  other direction — the format is one `ServedContentType` **can** serve, which is worse, not better.
- **F2 — tier 3 does not promote a weak hint; it manufactures a fact.** When neither the declared type
  nor the extension resolves, the path stores the literal `"image/jpeg"` (`:183`). Every downstream
  reader receives that as server truth. A stored type that disagrees with its bytes is a lie the system
  tells itself, and this tier tells it deliberately.
- **F3 — a live 500.** No rule on this chain decodes or probes the payload; `:136` calls
  `Convert.FromBase64String(base64Data)` inside the handler. Both hardened base64 chains close with a
  decodability rule for exactly this reason (`patterns-backend.md:1315-1317`). A payload that is
  well-formed base64 for its first characters and garbage after reaches the handler as an unhandled
  `FormatException`. This is independent of the content-type question and is the cheapest thing in this
  ADR to fix.

- **F4 — nothing downstream can dispatch on this type, and a control that tries is attacker-steerable.**
  *Independent finding, from a different lane:* the challenge on the content-policy draft
  (`backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md` **CH-4**, 2026-08-06,
  `c6370115`) attacks that draft's D4 for electing **this** surface as its metadata-scrub pilot. Its
  mechanism is not "the stored type is a lie" — it is *"a per-format control built on top of it runs the
  wrong parser, and the attacker picks which."* Declare `data:image/png`, send JPEG bytes: the PNG chunk
  walker runs over a JPEG, finds no `IHDR`, bails, the metadata survives — **a no-op the uploader
  selected, under a green "scrub was applied" test.** The shipped test that proves the premise is
  `SaveOrderPhotosContentTypeTests.cs:49-59`. **I verified the mechanism and I concede it outright: it
  is a stronger argument than F1 or F2**, because F1/F2 are about a fact being wrong while F4 is about a
  future control being *unbuildable* on it. Two lanes reached "close the exception" from opposite
  directions, which is the outcome a panel is for.
  **CH-4 offers two repairs — (a) the scrub sniffs its own bytes, or (b) `SaveOrderPhotos` adopts
  `SniffedContentType.FromContent(…, UploadIntake.OrderPhoto)` first. D1 below IS (b)**, decided here
  independently and before the challenge was read. See §Cross-lane for what that discharges.

- **F5 — the audience for this surface is not enumerable at upload time.** *Also from CH-2(c), and I
  re-verified it rather than inheriting it:* `GetOrderPhotos.cs:59` gates on `CanBrowseOrderAsync`, not
  `CanAccessOrderAsync`. `OrderAccessService.cs:68-92` — after owner/admin/assigned fails, **any** caller
  with role `Employee` and a resolvable `employeeId` returns `true` when `order.HasAvailableSpots &&
  OrderVisibility.NotHeldFrom(order, employeeId, now)`, and the comment at `:84-87` states that branch is
  *"both browse surfaces at once — order detail and order photos."*
  **Writing** still requires assignment (`SaveOrderPhotos.cs:114-117`); it is the **fetch** side that is
  wide. So the inline-`application/pdf` capability in F1 is not planted for a known triangle of
  customer + assigned cleaners + admin — it is mintable by **any cleaner in the tenant who can see the
  order while a seat remains open**, up to 12 seats on a 24 h order. That does not change the ruling; it
  raises what F1 is worth.

### Test 2 — if it closes, what breaks? **The mobile framing is false; the set is empty.**

The lane's objection names *"a live mobile path."* I checked both clients rather than accepting it.

| Client | Call site | What it sends | First bytes | Accepted by `UploadIntake.OrderPhoto`? |
|---|---|---|---|---|
| **iOS Partner** | `OrderPhotosViewModel.swift:57-58` → `PartnerOrderClient.swift:212-227` | `ImageCompressor.encode` → `CGImageDestinationCreateWithData(…, UTType.jpeg, …)` (`ImageCompressor.swift:77-85`), `contentType: "image/jpeg"`, `fileName: "photo.jpg"`, **bare** base64 (`:37`) | `FF D8 FF` | **yes** |
| **Android Partner** | `OrderPhotosViewModel.kt:114-129` → `OrdersRepository.kt:286-306` | `ImageCompressor.compressToBase64` → `Bitmap.compress(Bitmap.CompressFormat.JPEG, …)` (`ImageCompressor.kt:248`), `OUTPUT_MIME = "image/jpeg"`, `OUTPUT_FILE_NAME = "photo.jpg"` (`:103,112`), `Base64.NO_WRAP` (`:154`) | `FF D8 FF` | **yes** |
| **Partner web** | `order-photos.component.ts:124-140` → `helpers.ts:92-107` | the **raw picked file**, `FileReader.readAsDataURL` → full `data:` URI, `contentType = file.type` | whatever the file is | **only if the bytes really are JPEG/PNG/WebP** |

Both mobile clients **re-encode every pick to JPEG and cannot emit anything else.** They do not
forward the source format, they do not forward the source file name, and they do not send a `data:`
prefix at all — which means today they land on tier 2 (`.jpg` → `image/jpeg`) and after the change they
land on the sniff (`FF D8 FF` → `image/jpeg`). **Same answer, both before and after.** The set of mobile
uploads that would newly fail is **empty**, and the objection as stated is factually wrong on the path
it names.

**What would newly fail, exhaustively:**

- **Nothing from iOS.** Verified above.
- **Nothing from Android.** Verified above.
- **From partner web: a picked file whose browser-derived `File.type` disagrees with its bytes.**
  `validatePhotoFile` (`helpers.ts:29-45`) filters on `File.type`, which browsers derive from the
  extension/UTI, not from content. The intersection of *"passes that filter"* and *"fails a
  JPEG/PNG/WebP signature"* is exactly a mislabelled or renamed file — a HEIC saved as `.jpg` being the
  realistic instance. **Today that upload succeeds, stores `image/jpeg` over non-JPEG bytes, and the
  tile never renders.** After the change it is a 400 carrying an existing translated key.
- **GIF and PDF from partner web: already refused client-side** (`PHOTO_ALLOWED_TYPES`). Only a
  hand-crafted call reaches them, which is the capability F1 describes, not a use.
- **A payload shorter than 12 bytes, or one that is not decodable base64:** newly refused. Today the
  first stores a 3-byte "photo" and the second is a 500 (F3).

**So: the set of uploads that succeed today, render correctly for their reader, and would newly fail is
empty.** That is a falsifiable claim and it is the acceptance criterion the closing ticket must prove.

**The residual, stated rather than waved away.** A partner mobile build older than PR #154
(2026-07-26, when both compressors landed) forwarded raw picks. Per
`agents/architecture/decisions/request-intake-limits.md` and the deployment memory, there is no
production mobile channel — iOS is TestFlight, Android is unreleased — so the field population is
internal. And even for such a build the change is an improvement, not a regression: a raw HEIC through
this path **today** is stored as `image/jpeg` and does not render. The change converts a silent broken
photo into a legible refusal.

### Why the read path cannot solve this, unlike documents

Worth stating because the document fix went the other way and the analogy is tempting.
`SniffedContentType.ForDownload` works for employee documents because the server **holds the bytes at
read time** — `DownloadMyDocument.cs:88` / `DownloadEmployeeDocument.cs:52` call
`blobClient.DownloadAsync(...)` and can re-derive the type from what they are about to return. Order
photos are never read by the server: `GetOrderPhotos.cs:118-141` mints a SAS and the storage service
hands the bytes to the client directly. **The path that serves a photo to a client never holds that
photo's bytes.** The write path is therefore the only place, which is also why the catalog's
"a write-path rule retypes nothing that is already stored" bullet (`:1335-1343`) has no read-path
remedy to offer here — the clamp is all the legacy rows will ever get, and that is fine, because the
clamp is what makes them safe.

> **Correction to my own draft, made after reading CH-3(iii).** An earlier revision of this paragraph
> said *"there is no moment after intake at which this platform sees an order photo's bytes."* **That is
> a HEAD fact, not a structural invariant, and stating it as one was the exact overclaim this ADR's
> method declaration exists to prevent.** QuestPDF 2024.12.1 ships native Skia plus libjpeg-turbo,
> libpng and libwebp as runtime assets — I verified it in
> `src/Cleansia.Infra.Services/obj/project.assets.json:832-864,2362-2364` — so one
> `.Image(orderPhotoBytes)` in a generated dispute pack or invoice would give the server the bytes at a
> later moment. The claim is narrowed above to the one that is structural: **the `GetOrderPhotos` → SAS
> path** cannot re-derive a type, because it never holds the bytes. **This does not weaken D1 — it
> strengthens it:** the day a PDF job consumes an order photo, the thing it dispatches on is the stored
> content type, which is F4 one step worse (a *decoder* selected by a client string rather than a
> chunk walker).

---

## Decision

### D1 — `SaveOrderPhotos` derives its stored type from the bytes, refuses what it cannot identify, and mints its own blob-name extension

Four changes, all inside `SaveOrderPhotos.cs`, none of them new machinery:

1. **Validator, appended to the existing per-photo chain** (`:76-81`), after the size rule and in this
   order, because the size rule must stay ahead of anything that touches the payload
   (`patterns-backend.md:1244`):
   `.Must(file => SniffedContentType.FromContent(file.Base64Content, UploadIntake.OrderPhoto) is not null)`
   `.WithMessage(BusinessErrorMessage.FileTypeNotAllowed)`.
2. **A decodability rule closing the chain** — F3. The existing `DocumentFileValidator` shape is the
   reference; the message is `BusinessErrorMessage.InvalidFileType`, matching its sibling.
3. **Handler:** `DetermineContentType` is **deleted**; the stored type is
   `SniffedContentType.FromContent(file.Base64Content, UploadIntake.OrderPhoto)!` — the `!` is
   load-bearing and safe only because the validator ran, which is the same contract
   `UploadOrderPhoto.cs:102` already relies on.
4. **Blob name:** `SniffedContentType.ExtensionFor(contentType)` replaces
   `Path.GetExtension(file.FileName)` (`:132`). `OriginalFileName` (`:146`) keeps the caller's string —
   it is a display value and must stay one.

The roster row becomes `… OrderController.SavePhotos — SniffedContentType(OrderPhoto)` on both hosts,
and `UploadIntakeRosterTests`' class doc loses the paragraph blessing the exception.

**Not changed, on purpose:** the read-path clamp in `GetOrderPhotos.MapToDto` stays exactly as it is.
It is the only thing that governs rows already stored, it is defence in depth for the rows written from
here on, and removing it because the write path is now correct would be the "the bug was preventing the
vulnerability" inversion in reverse.

**No new `BusinessErrorMessage` key, therefore no i18n work.** `FileTypeNotAllowed`
(`file.type_not_allowed`) and `InvalidFileType` are both live and already carry five-locale `api.*`
entries on the partner app; the error-contract parity specs assert against `BusinessErrorMessage.cs`
directly and are unaffected. **The closing ticket must verify this rather than inherit it** — a new key
would make this a three-app, fifteen-row change.

### D2 — The general sentence is written, with no carve-out, and it is a law

Into `patterns-backend.md`, §"The declared content type is a HINT; the bytes are the evidence", as the
section's opening obligation:

> **Every intake that puts a file into storage derives that file's recorded type — and the extension of
> the name it is stored under — from the file's own bytes. There is no exemption for an intake whose
> served type is clamped on the read path: the clamp bounds the answer to the set this platform may
> *ever* serve, which is strictly wider than the set *that* intake accepts, and it says nothing at all
> about whether the recorded type matches the bytes. A recorded type that disagrees with its payload is
> a fact the system invented; every reader downstream receives it as server truth.**
>
> **Enforced by:** `UploadIntakeRosterTests` — every row's annotation is `SniffedContentType(<intake>)`
> or the named validator that calls it, and no row may read "…only"; plus the per-intake pins
> `SaveOrderPhotosContentTypeTests`, `UploadOrderPhotoContentTypeTests`,
> `UploadDisputeEvidenceContentTypeTests`, `SaveMyDocumentsHandlerTests`,
> `UpdateEmployeeStoredContentTypeTests` — **`T1-CI`** (`Cleansia.Tests`, the *"Unit tests
> (Cleansia.Tests)"* step of `.github/workflows/backend-ci.yml:69-74`, which sets the job's exit code).

**Sequencing, and it is not optional.** The sentence puts `SaveOrderPhotos` in violation the moment it
is written, so its baseline is not zero and ADR-0032 forbids labelling it `T1-CI` today. Until the
closing ticket lands the tier token is **`(gate pending: T-0561)`**, and `consistency.md` carries the
deviation naming `SaveOrderPhotos` as the single violating call site. **The ticket is the
canonicalization ticket** — one call site, so there is no migration beyond it. When it lands, the token
promotes to `T1-CI` and the deviation entry is deleted in the same change.

**Until then, the carve-out is written down where the rule lives.** It is recorded today only in a test
file's doc comment and a value type's doc comment — both code, neither read by anyone consulting the
catalog — while `patterns-backend.md:1284-1290`'s `Enforced by:` scope clause silently omits the
intake. A rule whose exception is invisible at the rule is the shape this sprint has repeatedly found
worthless, and that edit is made **now**, independent of the panel, because naming an existing
exclusion imposes nothing on anybody (see `## Consequences`).

### D3 — `image/gif` and `application/pdf` become unreachable on the order-photo intake, and that is the point

`AcceptedByIntake[UploadIntake.OrderPhoto]` is already `{image/jpeg, image/png, image/webp}`
(`SniffedContentType.cs:91`) and is **not widened**. Consequences, stated so nobody reads them as an
oversight: a GIF order photo becomes impossible (no client offers one — `PHOTO_ALLOWED_TYPES`, and both
compressors emit JPEG); a PDF order photo becomes impossible (same, and `UploadOrderPhoto` already
refuses it). Rows already carrying either keep rendering — the read clamp is untouched (D1).

---

## Cross-lane — what this discharges, and what it now blocks

Two ADR lanes met on this intake within a day. Recording the interface so neither lead has to
reconstruct it, and so the dependency is not discovered by a developer.

- **This ADR's D1 is CH-4's option (b), and discharges CH-4's `BLOCKS`.** The challenger asked the
  content-policy author to rule **in that ADR** on how a per-format scrub gets a trustworthy format,
  offering (a) the scrub sniffs its own bytes or (b) this intake sniffs first. D1 is (b), decided in
  this lane on independent grounds. **The content-policy ADR should therefore not re-decide it** — it
  should state that its D2 runs **only** against a byte-derived type, and cite this one. That is a
  narrower and better outcome than (a), which would have put the sniff inside the scrub and undercut
  that ADR's own D1 ("nothing at the transform layer is shareable") — the sniff *is* shareable, it
  already exists, and it belongs at intake, not inside a transform.
- **Consequence: the closing ticket becomes a prerequisite for the scrub ticket.** T-0459 (apply the
  scrub to order photos and dispute evidence) must not dispatch on `OrderPhoto.ContentType` until the
  closing ticket lands. This is recorded as a `depends_on` in the ticket and in the living doc §7.1; a
  scrub shipped first is a control whose no-op path the uploader selects.
- **What this ADR does NOT depend on.** Nothing here rests on whether an image decoder exists, is
  referenced, or is reachable — not the absence of the *library* (false: Skia is deployed transitively)
  and not the absence of the *call site* (true at HEAD: zero `.Image(`/`ImageDescriptor` hits, which I
  spot-checked). D1 is a statement about where a stored fact comes from and would be identical on a
  codebase full of decoders. Said explicitly so a lead does not have to work out which limb of CH-3(iii)
  this lane is exposed to: **neither.**
- **What this ADR does not touch, from the same challenge:** CH-2(b) (adversarial uploader on dispute
  evidence), CH-5, CH-6, CH-7 and the D8 PDF exclusion are the content-policy lane's to answer. CH-7's
  observation that a dispute-evidence PDF is served **inline** with no `rscd` is the same
  `GenerateSasUri` fact this ADR relies on for F1 (`BlobContainerClient.cs:93-110`), independently
  reached — two lanes, one call site, same reading.

---

## Alternatives considered

**A1 — Keep the exception; write the rule with an honest carve-out.** The lane's implicit position, and
the one the brief explicitly licenses. **Rejected**, on the evidence rather than on principle: the
carve-out's own justification (the clamp bounds it) is one set too wide (F1), its fallback manufactures
a fact (F2), and the cost of removing it is an empty set of broken uploads (Test 2). A carve-out with a
real cost behind it earns its place; this one's cost is zero and its price is a rule that cannot be
stated. **What A1 gets right, conceded:** it is the correct default when the cost is unknown, and the
lane was right to refuse to decide it unilaterally.

**A2 — Sniff, but store `application/octet-stream` instead of refusing when the sniff fails.** The
strongest alternative and the one a challenger should press. It ends the lie (F2) with **zero**
refusals — no upload that succeeds today stops succeeding, so even the mislabelled-HEIC case survives —
and the clamp then serves it opaquely. **Rejected on two grounds.** First, `UploadOrderPhoto` refuses
on the identical accepted set writing the identical table on the identical container; A2 gives one
question two answers and re-creates the sibling divergence that cost this area two tickets. Second, an
upload that succeeds and can never render is the outcome `patterns-backend.md:1321-1328` already rules
against, and it is *worse* than a refusal for the cleaner: the tile is broken with no diagnosis and the
photo is silently missing from a job record that a dispute may later turn on. **If the panel prefers
A2, it must also rule on `UploadOrderPhoto`** — the two must not diverge again.

**A3 — Delete `SaveOrderPhotos`; route every client to `UploadOrderPhoto`.** Ends the duplication that
caused this. **Rejected as out of scope, not as wrong:** it is a wire change across three generated
clients and two shipped mobile apps, and it drops a genuine capability — the web picker stages up to 30
photos and sends them in one command (`:46`, `order-photos.facade.ts:38-61`), which the single-photo
endpoint cannot express without 30 round trips. The consolidation question stays open in the living doc
where the prior draft already parked it.

**A4 — Fix it on the read path, as the document intakes were fixed.** **Rejected on a structural
fact**, not a preference: the server never sees an order photo's bytes after intake (see §Context). The
document technique does not exist on this surface.

**A5 — Widen the accepted set to `{jpeg, png, webp, gif, pdf}` so the change refuses nothing at all.**
Rejected: it makes the accept set follow the serve set, which is backwards — the serve set is "what this
platform may ever emit", the accept set is "what this surface's clients offer" — and it would oblige
`UploadOrderPhoto` to widen with it, for two formats nobody sends.

---

## Consequences

- **The general rule becomes writable, and it is written.** That is the deliverable; the code change is
  what makes it honest.
- **A per-format control becomes buildable on this surface** (F4). Until it lands, any scrub, thumbnailer
  or PDF-embed that dispatches on `OrderPhoto.ContentType` is dispatching on a client string — so the
  closing ticket **blocks T-0459**, and that dependency is now written in three places rather than
  discovered by whoever picks up the scrub.
- **The last stored-type lie on the platform ends.** After this, every recorded content type on every
  intake is a statement about bytes the server read.
- **One live 500 closes** (F3) — worth more in practice than the type question.
- **Two capabilities disappear from the order-photo container:** storing `application/pdf` or
  `image/gif`, and putting a caller-chosen extension on a server-managed blob name.
- **What this does NOT do, said plainly:** it closes no XSS hole (there is none here), it is not a
  malware scan, it does not retype a single row already stored, and it removes no metadata from
  anything. The metadata question is a different decision and stays where it is
  (`NNNN-user-artifact-content-policy-no-decoder.md`).
- **Immediately, before any panel:** `patterns-backend.md` gains a dated bullet naming
  `SaveOrderPhotos` as the one intake outside the section's `Enforced by:` scope, what it does instead,
  and what that admits. That edit ratifies nothing — it *withdraws* an implicit blessing and imposes no
  obligation on any call site — so it is not gated on this ADR. The **rule** in D2 is gated on it.

## How a reviewer verifies compliance

1. `UploadIntakeRosterTests.ExpectedIntakes` contains **no** row whose annotation ends in `only`, and
   both `OrderController.SavePhotos` rows read `SniffedContentType(OrderPhoto)`. The count assertion
   (`:64`) still runs **before** the set comparison.
2. `grep -n "DetermineContentType\|Path.GetExtension" src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs`
   returns nothing.
3. `SaveOrderPhotosContentTypeTests` asserts, on the row handed to `IOrderPhotoRepository.Add`:
   a `data:application/pdf` payload carrying PNG bytes stores **`image/png`**; a `data:image/png`
   payload carrying JPEG bytes stores **`image/jpeg`**; and the blob name passed to `UploadAsync` ends
   in the extension of the **sniffed** type, not of `FileName`. Each goes red under a distinct mutation
   (drop the handler's sniff; restore `Path.GetExtension`), and the mutation table records which.
4. A payload that is not JPEG/PNG/WebP fails the **validator** with `file.type_not_allowed` — asserted
   on the validator, not on the handler, or the rule is untested where it runs.
5. A payload whose first 16 base64 characters decode to a JPEG signature and whose remainder is garbage
   fails validation and **does not reach** `Convert.FromBase64String` (F3). Mutate: delete the
   decodability rule → the test must go red with an unhandled `FormatException`, not a 400.
6. **The empty-set claim is proven, not asserted:** the ticket records that `ImageCompressor.swift:77`
   emits `UTType.jpeg` and `ImageCompressor.kt:248` emits `Bitmap.CompressFormat.JPEG`, with a fixture
   whose bytes are a real JPEG head passing the new chain.
7. No new `BusinessErrorMessage` constant; `error-contract-parity.spec.ts` untouched on all three apps.
8. `consistency.md` no longer carries the `SaveOrderPhotos` deviation entry, and
   `patterns-backend.md`'s tier token reads `T1-CI` rather than `(gate pending: T-0561)`.

## Challenge (author-run — an independent round is owed)

**C-1 — "You are closing a hole that is not a hole, and you said so yourself."** Sustained as framing,
not as a rejection. This is a correctness and consistency change, not a security fix, and the ADR says
so three times so no ticket inherits a severity it does not have. The justification is F1 + F2 + the
empty cost, not a threat.

**C-2 — "Empty set" is a claim about two clients at HEAD, not about the field.** Partly sustained; see
the residual paragraph. My defence is that even for an old client the change strictly improves the
outcome, and that a refusal with a translated message is a supportable failure mode where a silently
broken photo is not. **A challenger who can name a shipped build that sends a non-JPEG through this
endpoint defeats the empty-set claim**, and the ruling would then need A2 instead of D1. That is the
single fact most worth attacking.

**C-3 — "A2 is better and you rejected it on symmetry."** The challenge I most want pressed. My
rejection rests on the dispute-record argument (a silently missing before/after photo is evidence lost
on a path that money later moves along), not on aesthetics — but I hold the pen on both this and the
sibling, so the symmetry argument is interest-conflicted and a lead should weigh it independently.

**C-4 — "You are writing the rule for a fourteen-row roster whose predicate you did not re-derive."**
Conceded as a limit. I read `UploadIntakeRosterTests`' predicate (`:97-107`) and it asks *does a file
reach storage from here* over three shapes; I did not execute the walk, so "fourteen" is the roster's
own assertion, not mine. The count assertion at `:64` is what makes that safe, and it is CI-enforced.

**C-5 — "A2 (store `Opaque`, refuse nothing) survives F4 too, so F4 does not decide between D1 and
A2."** Sustained, and it narrows what F4 buys. A scrub dispatching on a byte-derived
`application/octet-stream` would simply not run — a *declared* no-op, not an attacker-selected one, and
that is materially better than today. **F4 therefore kills the status quo and does not by itself pick
D1 over A2**; the reasons in A2 above (one question, two answers across sibling endpoints; evidence
silently lost on a money path) still have to carry that. Recorded so the lead does not over-read F4.

**C-6 — Not self-challenged; start here.** Whether D1 should carry `UploadOrderPhoto`'s deletion (A3)
after all, given that this ADR removes the last behavioural difference between the two endpoints except
batching — which makes A3 cheaper *after* this than before it. And, from CH-2(c)/F5: whether
`GetOrderPhotos` should gate on `CanAccessOrderAsync` rather than `CanBrowseOrderAsync`. **That is not
mine** — it is an authorization ruling on ADR-0036/ADR-0037 territory, it would change what a browsing
cleaner sees before taking a job, and it wants its own panel. It is named here only because F5 is the
first place the consequence has been written down.

## Verdict

**Not adjudicated.** No independent challenger has run and no lead has ruled. The rulings above are the
author's position going into the panel. The one edit made outside the panel is the catalog's
carve-out disclosure, for the reason given in `## Consequences`.
