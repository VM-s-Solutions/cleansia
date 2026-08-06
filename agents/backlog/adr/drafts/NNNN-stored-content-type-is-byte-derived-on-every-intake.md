# ADR-NNNN (DRAFT — number NOT allocated) — The stored content type is derived from the bytes on **every** intake; the `SaveOrderPhotos` exception closes

- **Status:** `proposed` — **rev 2** (2026-08-06), rewritten under an independent challenge
- **Date:** 2026-08-06 (drafted), 2026-08-06 (rev 2, defense)
- **Mode:** **author**. An independent challenger has run
  (`backlog/adr/challenges/NNNN-stored-content-type-byte-derived.md`); a **lead is still owed**
  (`process/deliberation.md`).
- **Number:** not allocated. Highest on disk is **0042**; the content-policy lane is landing **0043**.
  The PM allocates.
- **Supersedes:** nothing. **Amends in practice:** the unwritten carve-out currently recorded only in
  `Cleansia.Tests/Common/Validators/UploadIntakeRosterTests.cs:34-38` and
  `Cleansia.Core.Blobs.Abstractions/ServedContentType.cs:7-14`.
- **Consumes:** T-0464 (`ServedContentType`, the read clamp), T-0548, T-0556 + follow-up
  (`SniffedContentType`, `BlobFileSize`, the 14-row roster), ADR-0032 (a constraining entry names an
  enforcer and declares a tier), ADR-0033 (routing test 1 fires → Architect).
- **Escalated to the owner:** **Q-ART-02** (`backlog/questions/open.md:1480-1505`) — refuse-vs-store-
  un-previewable on a failed byte check. **Already filed; this ADR does not re-file it.**
- **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md` §7.1
- **Tickets it creates:** the closing ticket (`T-0561`), whose **scope changes in rev 2** — see D4.

> ### ⚠️ Method declaration
> **1. Panel state.** Rev 1 was written by one architect instance with an author-run self-challenge.
> An **independent challenger has now run** and filed five blocking + five non-blocking findings; this
> revision answers each in `## Defense` and folds every concession into the body. **A lead has not
> ruled. This ADR may not be `accepted` until it does.**
>
> **2. No shell in this invocation** (`Read`/`Glob`/`Grep`/`Write`/`Edit`; no `Bash`, no `git`). Nothing
> was compiled, executed or measured. Every fact below is read from source at HEAD and cited at
> `file:line`. **No claim is inherited** — including from the challenge: CH-3 and CH-4 were re-verified
> against source before being conceded, and CH-7's arithmetic was re-derived from
> `SniffedContentType.Matches` rather than accepted. Where a number is owed and I could not take it,
> the measurement and its owner are named, never a figure.
>
> **3. One citation offset, so a lead reading both documents is not misled.** This revision restates two
> sentences of the `patterns-backend.md` disclosure callout (CH-10a), which lengthens it and shifts every
> line **after 1311 by +7**. The `patterns-backend.md:NNNN` citations **in this ADR are post-edit**; the
> ones **in the challenge file are pre-edit and were correct when written** — e.g. the challenge's
> `:1364-1366` ("the read path reads the intake's own signature table") is this ADR's `:1371-1373`. Same
> sentence, same file, one edit apart.

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

### Test 1 — what *can* a client store, and what does a reader get?

| | `SaveOrderPhotos` today | `UploadOrderPhoto` (same table, same container, same read path) |
|---|---|---|
| Stored type derived from | client `data:` prefix → else file name → else literal `"image/jpeg"` | the bytes (`:102`) |
| Types a caller can store | `image/jpeg`, `image/png`, `image/webp`, **`image/gif`**, **`application/pdf`** | `image/jpeg`, `image/png`, `image/webp` |
| Bytes constrained | **not at all** | `SniffedContentType.FromContent(…, OrderPhoto)` (`:67`) |
| Blob-name extension | caller's `FileName` (`:132`) | server-minted from the sniff (`:103`) |
| Undecodable base64 | **unguarded** → `Convert.FromBase64String` at `:136` → 500 | decodability covered by the byte rule |

**The clamp bounds to the SERVE set, not to this intake's ACCEPT set, and the two differ.**
`ServedContentType.ServableTypes` (`ServedContentType.cs:34-42`) holds **six** values;
`AcceptedByIntake[UploadIntake.OrderPhoto]` (`SniffedContentType.cs:91`) holds **three**. So a caller
sending `data:application/pdf;base64,<arbitrary bytes>` stores `application/pdf` and
`GetOrderPhotos.cs:140` mints a SAS with `rsct=application/pdf`. `BlobContainerClient.GenerateSasUri`
(`:89-110`) sets `ContentType` and `CacheControl` and **no `ContentDisposition`** — verified by reading
the builder — so those bytes render **inline, as a PDF, to any reader that can already fetch the
photo**. Same for `image/gif`. Neither format is offered by any order-photo client
(`order-photos.helpers.ts:17-22`: `image/jpeg`, `image/jpg`, `image/png`, `image/webp`), and the
sibling intake on the same container refuses both.

> **Reach, stated precisely (rev 2, CH-6b).** Rev 1 said *"from a storage host shared by every tenant."*
> The **host** is shared; the **reach** is not, and the difference matters because it is what F1 is
> worth. Containers are created `PublicAccessType.None` (`BlobContainerClient.cs:151`); the only mint
> for this container is `GetOrderPhotos`, behind `CanBrowseOrderAsync` (`:59`). No cross-tenant reader
> obtains a URL. F1's harm is therefore: **an authenticated same-tenant reader who can already browse
> the order opens a PDF where a photo should be.** The "shared host" phrasing belongs to
> `ServedContentType.cs:7-14`, where it is correct — that argument is about a *scripting* origin, and
> `application/pdf` is deliberately in the non-scripting class (`ServedContentType.cs:29-33`). Do not
> "fix" that doc comment to match this paragraph; they are about different types.

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

### What the clamp is, exactly — and the equivocation rev 1 committed (CH-1)

Rev 1 wrote both *"the bound is one set too wide"* and *"the clamp is what makes them safe."* The
challenger is right that both cannot stand. They were equivocating on **safe**, and the resolution is
one sentence, checkable at `ServedContentType.cs:29-33`:

> **The platform-wide clamp makes a stored row *inert*. It does not make it *right*.**
> *Inert* — the served type is drawn from a set whose renderers cannot reach script or the DOM;
> `image/svg+xml` and `text/html` are excluded **by name**, and that is the property `ServedContentType`
> was built for and still delivers, on every row, past and future.
> *Right* — the served type is one **this intake** accepts. The platform-wide clamp never claimed this
> and does not deliver it: six values against this intake's three.

Everything rev 1 called "safe" is the first; F1 is entirely about the second. With that separated, the
finding and the scope statement stop contradicting each other — **and the repair for the second half
is a read-path repair, which rev 1 never surfaced. That is D4.**

### The findings

- **F1 — the served set is one set too wide.** `application/pdf` and `image/gif` are storable and
  servable here and nowhere else on this container. The catalog already forbids exactly this shape:
  *"a format the server accepts that no client offers and `ServedContentType` cannot serve is an upload
  that succeeds and an image that never renders"* (`patterns-backend.md:1354-1356`). This is that rule's
  other direction — the format is one `ServedContentType` **can** serve, which is worse, not better.
  **F1 is closed by D4, on every row; D1 closes only the ability to create new ones.** Rev 1 led with F1
  as the finding that kills the exception and then scoped the fix so that it could not reach it. That
  was the document's worst error.
- **F2 — tier 3 does not promote a weak hint; it manufactures a fact.** When neither the declared type
  nor the extension resolves, the path stores the literal `"image/jpeg"` (`:183`). Every downstream
  reader receives that as server truth. **What "downstream" means today, measured rather than implied:**
  `GetOrderPhotos.cs:96` is the **only** reader of `OrderPhoto.ContentType` in the codebase — `Features/
  Orders` has no other (`grep` over the folder), `GetOrderDetails` reads a photo **count** only
  (`:55-57`), and the GDPR feature reads no content type at all. So F2's harm today is not a live
  mis-render; it is that **the column is false, and the one reader that exists compensates for it.** A
  compensating read is a discipline every future reader must repeat — see D4's rationale.
- **F3 — a live 500.** No rule on this chain decodes or probes the payload; `:136` calls
  `Convert.FromBase64String(base64Data)` inside the handler. `BlobFileSize.HasContentWithinLimit`
  derives size from the **encoded** length and never decodes (`BlobFileSize.cs:24-27`). Both hardened
  base64 chains close with a decodability rule for exactly this reason (`ImageFileValidator.cs:35-41`,
  `DocumentFileValidator.cs:43-49`; `patterns-backend.md:1343-1347`). A payload that is well-formed
  base64 for its first characters and garbage after reaches the handler as an unhandled
  `FormatException`. **This is independent of the content-type question, is untouched by every
  alternative in this ADR, and should not wait for the panel.**
- **F4 — the stored type is not a safe dispatch basis. ⚠️ Rev 2: the sibling lane has ruled, and this
  finding no longer supports D1.** Rev 1 recorded that a per-format control built on this row runs the
  wrong parser and the attacker picks which (declare `data:image/png`, send JPEG; the PNG chunk walker
  finds no `IHDR` and bails — a no-op the uploader selected, under a green "scrub applied" test; premise
  pinned by `SaveOrderPhotosContentTypeTests.cs:49-59`). **The content-policy panel has since ruled that
  the scrub dispatches "from the bytes it is holding, at the moment it runs — never a client string,
  never a persisted `ContentType`, not even a correct one"** (`user-uploaded-artifacts.md` §8.2), i.e.
  CH-4's repair **(a)**, not (b). That is the stricter rule and it is right. **Consequences, both
  against me:** (i) T-0459 is **not** blocked by the closing ticket — rev 1's `blocks:` claim is
  withdrawn (see §Cross-lane); (ii) F4 no longer argues for D1 at all, because the general rule it
  produced ("never dispatch on a persisted content type") makes the persisted value's accuracy
  irrelevant to any future control. **F4 is retained as context and struck as a justification.**
- **F5 — the audience for this surface is not enumerable at upload time.** `GetOrderPhotos.cs:59` gates
  on `CanBrowseOrderAsync`, not `CanAccessOrderAsync`. `OrderAccessService.cs:68-92` — after
  owner/admin/assigned fails, **any** caller with role `Employee` and a resolvable `employeeId` returns
  `true` when `order.HasAvailableSpots && OrderVisibility.NotHeldFrom(order, employeeId, now)`, and the
  comment at `:84-87` states that branch is *"both browse surfaces at once — order detail and order
  photos."*
  **Rev 2 correction (CH-6a): rev 1 named the wrong actor in the following sentence.** Minting a row
  requires **assignment** (`SaveOrderPhotos.cs:114-117` refuses a non-assigned caller with
  `EmployeeNotAssignedToOrder`). The wide gate is on the **fetch** side. So the correct statement is:
  *the `application/pdf` capability is **planted** by an assigned cleaner and **reachable** by any
  cleaner in the tenant who can browse the order while a seat remains open* (up to 12 seats on a 24 h
  order), plus the customer and admin. That is what makes the audience non-enumerable at upload time; it
  does not change the ruling, it raises what F1 is worth — which is why D4 exists.

### Test 2 — if the intake closes, what breaks? **The mobile framing is false; the set is empty.**

The lane's objection names *"a live mobile path."* I checked both clients rather than accepting it, and
the independent challenger re-derived the same result from the same call sites without inheriting it.

| Client | Call site | What it sends | First bytes | Accepted by `UploadIntake.OrderPhoto`? |
|---|---|---|---|---|
| **iOS Partner** | `OrderPhotosViewModel.swift:57-58` → `PartnerOrderClient.swift:212-227` | `ImageCompressor.encode` → `CGImageDestinationCreateWithData(…, UTType.jpeg, …)` (`ImageCompressor.swift:77-85`), `contentType: "image/jpeg"`, `fileName: "photo.jpg"`, **bare** base64 (`:37`) | `FF D8 FF` | **yes** |
| **Android Partner** | `OrderPhotosViewModel.kt:114-129` → `OrdersRepository.kt:286-306` | `ImageCompressor.compressToBase64` → `Bitmap.compress(Bitmap.CompressFormat.JPEG, …)` (`ImageCompressor.kt:248`), `OUTPUT_MIME = "image/jpeg"`, `OUTPUT_FILE_NAME = "photo.jpg"` (`:103,112`), `Base64.NO_WRAP` (`:154`) | `FF D8 FF` | **yes** |
| **Partner web** | `order-photos.component.ts:124-140` → `helpers.ts:92-107` | the **raw picked file**, `FileReader.readAsDataURL` → full `data:` URI, `contentType = file.type` | whatever the file is | **only if the bytes really are JPEG/PNG/WebP** |

Both mobile clients **re-encode every pick to JPEG and cannot emit anything else**, and neither has a
raw-bytes fallback (both abort on a nil/null compressor result). They do not forward the source format,
they do not forward the source file name, and they do not send a `data:` prefix — so today they land on
tier 2 (`.jpg` → `image/jpeg`) and after the change they land on the sniff (`FF D8 FF` → `image/jpeg`).
**Same answer, both before and after.** The set of mobile uploads that would newly fail is **empty**.

**What would newly fail — corrected in rev 2 (CH-7), because two of five rows were wrong:**

- **Nothing from iOS.** Verified above.
- **Nothing from Android.** Verified above.
- **From partner web: a picked file whose browser-derived `File.type` disagrees with its bytes.**
  `validatePhotoFile` (`helpers.ts:29-45`) filters on `File.type`, which browsers derive from the
  extension/UTI, not from content. The intersection of *"passes that filter"* and *"fails a
  JPEG/PNG/WebP signature"* is exactly a mislabelled or renamed file — a HEIC saved as `.jpg` being the
  realistic instance. **Today that upload succeeds, stores `image/jpeg` over non-JPEG bytes, and the
  tile never renders.** After the change it is a 400 carrying an existing translated key (D1 item 5).
- **GIF and PDF from partner web: already refused client-side** (`PHOTO_ALLOWED_TYPES`). Only a
  hand-crafted call reaches them, which is the capability F1 describes, not a use.
- ~~*A payload shorter than 12 bytes.*~~ **WRONG — struck.** `SniffedContentType.Matches` (`:152-163`)
  requires only `content.Length >= offset + bytes.Length` **per fragment**, and the JPEG fragment is
  **3** bytes at offset 0 (`:69`). Base64 `"/9j/"` is four characters → one whole group → `FF D8 FF` →
  `image/jpeg`, which is in the accepted set, and `BlobFileSize.HasContentWithinLimit` passes it
  (non-blank, ≤ 10 MiB). **A 3-byte "photo" survives D1 exactly as it survives today.** The accurate
  row is: *a payload whose first 16 base64 characters do not decode to any signature this intake
  accepts* — which includes a payload of fewer than 4 base64 characters, since `DecodeHead` (`:165-192`)
  yields an empty span when there is no whole group.
- **A payload that is not decodable base64: newly refused — by rule 2 specifically (F3), not by the
  content rule.** `SniffedContentType.FromContent` reads at most 16 characters and asserts nothing about
  the remainder. Rev 1 credited this refusal to "the change" generally; it is produced by exactly one
  rule and dies if that rule is dropped.

**So: the set of uploads that succeed today, render correctly for their reader, and would newly fail is
empty.** That is a falsifiable claim and it is the acceptance criterion the closing ticket must prove.

**The residual, stated rather than waved away.** A partner mobile build older than PR #154
(2026-07-26, when both compressors landed) forwarded raw picks. Per
`agents/architecture/decisions/request-intake-limits.md` and the deployment memory, there is no
production mobile channel — iOS is TestFlight, Android is unreleased — so the field population is
internal. And even for such a build the change is an improvement, not a regression: a raw HEIC through
this path **today** is stored as `image/jpeg` and does not render. The change converts a silent broken
photo into a legible refusal.

### The read path: which repairs exist, and which one this ADR takes (rewritten in rev 2 — CH-2)

Rev 1 had one paragraph here that rejected *the read path* and in fact rejected only one option within
it. There are **three** distinct read-path proposals and they need three answers.

**(i) Re-derive the served type from the bytes on read** (`SniffedContentType.ForDownload`, the
technique that fixed employee documents). **Structurally unavailable here.** It works for documents
because the server **holds the bytes at read time** — `DownloadMyDocument.cs:88` /
`DownloadEmployeeDocument.cs:52` call `blobClient.DownloadAsync(...)`. The `GetOrderPhotos` → SAS path
never holds them: `GetOrderPhotos.cs:118-141` mints a URL and storage serves the client directly.
Adopting it would mean downloading every photo on every gallery render. **Rejected** — this, and only
this, is what rev 1's A4 disposed of.

> **Narrowing kept from rev 1, and it still matters.** An earlier revision said *"there is no moment
> after intake at which this platform sees an order photo's bytes."* That is a HEAD fact, not a
> structural invariant: QuestPDF 2024.12.1 ships native Skia plus libjpeg-turbo, libpng and libwebp as
> runtime assets (`src/Cleansia.Infra.Services/obj/project.assets.json:832-864,2362-2364`), so one
> `.Image(orderPhotoBytes)` in a generated dispute pack would give the server the bytes at a later
> moment. The structural claim is narrower and is the one used: **the `GetOrderPhotos` → SAS path**
> cannot re-derive a type, because it never holds the bytes.

**(ii) Narrow the clamp to the INTAKE's accepted set instead of the platform serve set.** **ADOPTED —
D4.** It reads no bytes, refuses no upload, changes no client, needs no migration, and it closes F1 on
**every row, including every row already written**, which D1 cannot reach. It is also already an
obligation of a shipped catalog sentence that `GetOrderPhotos` violates today:

> `patterns-backend.md:1365-1373` — *"A write-path rule retypes nothing that is already stored… Close
> the residue where it is a **closed set** — the handlers that serve the blob — … and the same
> discipline: **the read path reads the intake's own signature table**."*

`GetOrderPhotos.cs:96` reads the platform-wide table, not this intake's. An ADR is the only sanctioned
way to deviate from the catalog, and rev 1 deviated from that sentence without naming it. Rev 2 does not
deviate: it complies.

**(iii) Set `rscd` (`Content-Disposition: attachment`) on non-image served types.** **Not rejected on
merit; not this ADR's call site.** `BlobSasBuilder.ContentDisposition` is unset
(`BlobContainerClient.cs:93-110`), and setting it would close the *"renders inline"* half of F1. Two
reasons it is not decided here. First, `patterns-backend.md:1359-1364` — *"Do not lean on
`Content-Disposition`… The control is the byte-derived type; the disposition is luck"* — rules it out as
*the* control, which is what F1 would be asking of it. Second and decisive: `GenerateSasUri` is **one
shared mint** serving `order-photos`, `dispute-evidence` and `user-files`, and on dispute evidence
`application/pdf` is a **legitimately accepted** type whose inline preview is a capability the support
flow uses. Changing the shared mint is a product change on a surface this ADR does not own; it belongs
with the dispute/content-policy lane, where CH-7 on that draft already raised the identical call site.
**Routed, not dismissed** — and note (ii) achieves F1's harm reduction on *this* container without
touching it.

---

## Decision

### D1 — `SaveOrderPhotos` derives its stored type from the bytes, refuses what it cannot identify, and mints its own blob-name extension

Five changes. **D1's disposition of a failed sniff (refuse) is the escalated half — see D6.**

1. **A third member of the `AbstractValidator<BlobFileDto>` family, not a fourth copy of its chain**
   (rev 2, CH-8). `PhotoFileValidator : AbstractValidator<BlobFileDto>`, beside `ImageFileValidator` and
   `DocumentFileValidator` in `Common/Validators/`, carrying the family's one ordered
   `Cascade(CascadeMode.Stop)` chain in the family's order — presence → size → **sniff** → **decodability**
   — with the intake fixed to `UploadIntake.OrderPhoto`. `SaveOrderPhotos.Validator` consumes it via
   `photo.RuleFor(p => p.File).SetValidator(new PhotoFileValidator())` inside the existing `ChildRules`
   block (`:64-85`), replacing the inline presence + size rules (`:76-81`) rather than sitting beside
   them. The `.When(x => x.Photos is not null && x.Photos.Count() <= MaxPhotosPerRequest)` gate at
   `:85` is unchanged and still bounds the walk.
   *Why a member and not an inline chain:* the next family-wide change — a new foot rule, a change to
   the head size, T-0459's scrub obligation — must have **one shape per intake in one folder**, not two
   shapes plus a chain buried in a feature file. It also makes the roster annotation read
   `PhotoFileValidator`, the same shape as every other row, which is what D2's clause presumes exists.
2. **Every rule carries `.WithErrorCode(nameof(BlobFileDto))`** (rev 2, CH-8), as both siblings do
   (`ImageFileValidator.cs:25,28,31`; `DocumentFileValidator.cs:25,28,31,34`). The error code is the
   ProblemDetails dictionary **key**; un-coded failures group under FluentValidation's default and the
   value becomes a `"; "`-joined string that resolves to no translation key at all, which every client
   turns into `api.common.error_occurred`. This is not a style point and it must not be left to the
   implementer.
3. **The messages are the IMAGE family's, not the document family's** (rev 2, CH-5). Presence keeps
   `BusinessErrorMessage.FileRequired` and size keeps `FileSizeExceeded` — the keys this route already
   emits, so no client-visible message changes on rules that exist today. The **sniff** and
   **decodability** rules both use `BusinessErrorMessage.FileNotMatchContentType`
   (`file.content_type_doesnt_match`, `BusinessErrorMessage.cs:229`) — exactly what `ImageFileValidator`
   uses for both (`:29,32`). See D5 for why this and not `FileTypeNotAllowed`.
4. **Handler:** `DetermineContentType` is **deleted**; the stored type is
   `SniffedContentType.FromContent(file.Base64Content, UploadIntake.OrderPhoto)!` — the `!` is
   load-bearing and safe only because the validator ran, which is the same contract
   `UploadOrderPhoto.cs:102` already relies on and which the challenger independently verified against
   the `ChildRules` gate.
5. **Blob name:** `SniffedContentType.ExtensionFor(contentType)` replaces
   `Path.GetExtension(file.FileName)` (`:132`). `OriginalFileName` (`:146`) keeps the caller's string —
   it is a display value and must stay one. See D7 for which carrier is authoritative.

The roster row becomes `… OrderController.SavePhotos — PhotoFileValidator` on both hosts, and
`UploadIntakeRosterTests`' class doc loses the paragraph blessing the exception.

**Deliberately not part of D1:** a declared-content-type allowlist mirroring `UploadOrderPhoto.cs:55-59`.
`BlobFileDto.ContentType` is read by nothing on this path after D1, and `UploadOrderPhoto`'s own comment
(`:36-37`) calls that rule *"a client-affordance filter, not a control."* Adding one would add a second
refusal reason with no control value. **This leaves a residual asymmetry between the two endpoints and
rev 2 concedes it (CH-3):** they now agree exactly on the **control** (same accepted set, same table,
same refusal) and still differ on the **affordance**. The residue belongs to A3 (consolidation), not
here.

### D2 — The general sentence is written, with no carve-out, and its enforcer is one that can go red

Into `patterns-backend.md`, §"The declared content type is a HINT; the bytes are the evidence", as the
section's opening obligation:

> **Every intake that puts a file into storage derives that file's recorded type — and the extension of
> the name it is stored under — from the file's own bytes. There is no exemption for an intake whose
> served type is clamped on the read path: a platform-wide clamp makes a row *inert*, not *right* — it
> bounds the answer to the set this platform may *ever* serve, which is strictly wider than the set
> *that* intake accepts, and it says nothing at all about whether the recorded type matches the bytes.
> A recorded type that disagrees with its payload is a fact the system invented; every reader downstream
> receives it as server truth.**

**Enforcement, rewritten in rev 2 after CH-4 was verified against the test file.**

The clause's enforcer **must be able to fail a build on a new intake that skips the sniff.**
`UploadIntakeRosterTests` as written **cannot**: `:66-68` compares
`ExpectedIntakes.Select(entry => entry.Split(" — ")[0])` against the walk, so **the annotation after
`" — "` is split off and discarded** and nothing in the file reads `[1]`. It is a genuine `T1-CI`
enforcer of **route enumeration** and no enforcer at all of **which rule guards each route**
(`conventions.md:250-253`). Naming it for that clause is precisely the ADR-0032 failure this ADR is
written to stop. So:

- **One `Enforced by:` clause for the section, not two** (CH-4's second defect). The existing clause at
  `patterns-backend.md:1286-1290` is **merged**, not shadowed: the enforcer list is the union — the six
  it already names (including `EmployeeDocumentDownloadContentTypeTests` and
  `GetOrderPhotosServedTypeTests`, which rev 1 dropped, and which cover the three avatar routes and the
  read path) **plus** the two below.
- **The gate the closing ticket must build:** a per-intake `[Theory]` that, for each of the fourteen
  roster rows, constructs that route's validator and asserts that one fixed payload which no intake
  accepts (a payload whose head matches no signature) is **refused**. Non-vacuity floor: the theory is
  driven off `UploadIntakeRosterTests.ExpectedIntakes` itself and asserts the case count against
  `ExpectedIntakes.Length` before any per-case assertion, the same count-first discipline as `:62-64`,
  so the two cannot drift.
- **A cheap companion in the roster file itself:** assert that every row's annotation is drawn from a
  closed vocabulary and that **no row's annotation ends in `only`**. This proves the *vocabulary*, not
  the *fact* — say so in the entry rather than letting it read as coverage — but it does fail the build
  on the edit that introduces a new unguarded intake, which is the edit the rule exists to catch.
- **Tier:** **`(gate pending: T-0561)`**, for **two** stated reasons: `SaveOrderPhotos` violates the
  sentence today (non-zero baseline — `conventions.md:237-242` forbids `T1-CI`), and the gate above does
  not exist yet. It promotes to `T1-CI` when the closing ticket lands **both** the code change and the
  theory. **If the theory turns out not to be buildable within that ticket, the universal clause is
  declared `T2-ADVISORY` with the five named per-intake pins carried as `T1-CI` — two clauses, stated
  separately. It is not left labelled as a gate.** (Same fallback the living doc §5 already uses for the
  call-site clause.)
- `consistency.md` carries the deviation naming **two** violating call sites until the ticket lands:
  `SaveOrderPhotos` (D1) and `GetOrderPhotos.MapToDto` (D4 — a live violation of
  `patterns-backend.md:1371-1373`, found by the challenger). **The ticket is the canonicalization
  ticket**; both entries are deleted in the same change that promotes the token.

**Until then, the carve-out stays written down where the rule lives** — the dated disclosure callout at
`patterns-backend.md:1292-1320`, which is made independently of the panel because naming an existing
exclusion imposes nothing on anybody. **Rev 2 restates two of its sentences as descriptive** (CH-10a):
they read as normative and pre-decided A1, which a panel-owed callout may not do.

### D3 — `image/gif` and `application/pdf` become unreachable on the order-photo intake, and that is the point

`AcceptedByIntake[UploadIntake.OrderPhoto]` is already `{image/jpeg, image/png, image/webp}`
(`SniffedContentType.cs:91`) and is **not widened**. Consequences, stated so nobody reads them as an
oversight: a GIF order photo becomes impossible (no client offers one — `PHOTO_ALLOWED_TYPES`, and both
compressors emit JPEG); a PDF order photo becomes impossible (same, and `UploadOrderPhoto` already
refuses it). **Rows already carrying either stop rendering inline and download instead — that is D4, and
it is intended.**

### D4 — **NEW (rev 2).** The read clamp is narrowed to the intake's accepted set, in one function, so a second read path cannot get half of it

The finding D1 leads with (F1) is a property of **rows**, and D1 governs only rows written after it.
D4 governs all of them. The two are **complements, not substitutes**, and the ADR rules both.

**Shape** (interface sketch — the ticket writes the code):

```csharp
// Cleansia.Core.AppServices.Common.Validators — same assembly as GetOrderPhotos,
// and SniffedContentType already references Cleansia.Core.Blobs.Abstractions (:2).
internal static ServedContentType ServedFor(string? recordedContentType, UploadIntake intake);
//   = ServedContentType.ForRecordedType(recorded), IF that value is in AcceptedByIntake[intake];
//     otherwise ServedContentType.Opaque.
```

`GetOrderPhotos.MapToDto:96` becomes `SniffedContentType.ServedFor(photo.ContentType,
UploadIntake.OrderPhoto)` and everything downstream of it — the DTO field (`:105`) and the SAS header
(`:101,140`) — is unchanged, because it already resolves **once** and uses the one value for both.
`ServedContentType.cs` is **not modified**: its private constructor and closed sets stay exactly as they
are, and the composition lives on the side that knows about intakes.

**Why one named function and not two lines at the call site.** The clamp is a compensating read for a
column that may be false (F2). A compensating read is a discipline **every** reader must repeat, and
this codebase has already paid for that once in this exact method: `GetOrderPhotos` clamped the SAS
header and emitted the raw column beside it, so a legacy row told the client one type about a blob that
arrived as another (`patterns-backend.md:1374-1377`; R2 in the living doc). One method, two halves, one
of them forgotten. Today `GetOrderPhotos.cs:96` is the **only** reader of `OrderPhoto.ContentType` — I
checked `Features/Orders`, `GetOrderDetails` (count only, `:55-57`) and the GDPR feature — so the cost
of getting this right now is one function, and the cost of getting it wrong is paid by whoever adds the
second reader.

**What it costs, stated as a cost.** A legacy row that genuinely holds a GIF stops rendering inline and
downloads instead — the same *"downloads instead of previewing"* capability loss the sibling draft's D3
names for dispute evidence, and it is recoverable, not a loss of bytes. **How many rows: not asserted
here.** The measurement is already owed under Q-ART-02's rider (`open.md:1501-1505`) — an owner query,
`SELECT "ContentType", count(*) FROM "OrderPhotos" GROUP BY 1`. The number does not decide whether D4 is
right; it decides how many photos change behaviour on the day it ships, and it belongs in the ticket's
evidence section before the change is merged.

**A property worth stating, because it is what makes D1 and D4 compose cleanly:** after D1, every stored
type on this intake is a member of `AcceptedByIntake[OrderPhoto]`, so **D4 is the identity function on
every row D1 wrote.** Its only effect is on rows written before D1 — exactly the population D1 cannot
reach — plus its standing value as defence in depth against a future write-path regression.

**Enforcer:** `GetOrderPhotosServedTypeTests` (already named in the section's `Enforced by:`) gains a
case: a row recorded `application/pdf` resolves to `application/octet-stream` on **both** the DTO field
and the SAS header. `T1-CI` once it lands; until then the same `(gate pending: T-0561)` token as D2,
and the `consistency.md` deviation names this call site (D2).

**Ticket consequence, and it is a real scope change:** `T-0561`'s §Out of scope bullet *"The read-path
clamp — `GetOrderPhotos.MapToDto` is untouched"* (`:113-115`) and its §Implementation-notes line
*"Read-only, must not change: `GetOrderPhotos.cs` … `SniffedContentType.cs`"* (`:135`) are **struck**.
`ServedContentType.cs` stays read-only. The PM re-scopes; the ticket does not become large (one accessor,
one call site, one test case) but it does become two-sided, and the AC list gains D4's case.

### D5 — **NEW (rev 2).** The refusal message is the image family's key, and it is not a new key

`BusinessErrorMessage.FileTypeNotAllowed` (`file.type_not_allowed`) is declared under the
`// Document Upload` header (`BusinessErrorMessage.cs:366-368` — one of the two `file.*` keys filed
there rather than under `// File` at `:228-233`), and its partner-web value in all five locales is the
**document** promise — `en.json:1223`,
*"File type is not allowed. Accepted: PDF, JPEG, PNG, DOC, DOCX"* — which names two formats this intake
refuses on purpose (D3) and omits WebP, which it accepts. `SniffedContentType.cs:83-86` and
`patterns-backend.md:1351-1353` both say in as many words that this string **is the promise for
documents**. Sending it to a cleaner whose photo was refused is a wrong sentence to the only client that
can see it (partner web — both mobile clients cannot produce the refusal at all, per Test 2).

**The repair costs nothing and is not one of the three options the challenge listed.** Use
`BusinessErrorMessage.FileNotMatchContentType` — `file.content_type_doesnt_match`
(`BusinessErrorMessage.cs:229`) — which is what the **image** sibling `ImageFileValidator` already uses
for both its sniff and its decodability rule (`:29,32`), and which resolves, verified locale by locale:

| Client | Key | Value |
|---|---|---|
| partner web | `api.file.content_type_doesnt_match` — `en/cs/sk/uk/ru.json:1216` | en: *"File type does not match content"* |
| Android partner | `error_file_content_type_doesnt_match` — `values:1157`, `values-cs:1147`, `values-sk:1127`, `values-uk:1130`, `values-ru:1130` | *"File type doesn't match."* |
| iOS | `error.file.content_type_doesnt_match` — `Localizable.xcstrings:1754` | present |

**Zero new keys, zero new locale rows, no weakening of the document promise, and the sentence is exact
for the only case a real client produces** (a mislabelled or renamed file). It is approximate for a
hand-crafted GIF/PDF call, where "not accepted here" would be more precise than "does not match" — said
plainly so a reviewer reads it as a decision, not an oversight. `error-contract-parity.spec.ts` is
unaffected on all three apps: it asserts against `BusinessErrorMessage.cs`, and no constant is added.

**One documentation row changes:** `PartnerErrorVoiceTests.swift:89` maps
`"file.content_type_doesnt_match": "ImageFileValidator"`, a provenance roster whose stated purpose
(`:53-56`) is that it comes from the backend side. It gains `PhotoFileValidator`. (The rows the
challenger named — `:91,94` — are the ones that would have needed it under rev 1's key choice; under
D5 they do not. Rows `:90,92,93` already list `SaveOrderPhotos`.) The emitter strings are used only in
the failure message (`:198-209`), so this reddens no CI job; it is a doc row and it is in the ticket.

### D6 — **ESCALATED (rev 2).** Refuse-vs-store-opaque on a failed sniff is a product call, and it is already with the owner as Q-ART-02

Rev 1 rejected A2 (sniff, but store `application/octet-stream` instead of refusing) on two grounds, and
**one of them was backwards.** Rev 2 withdraws it — see the Defense on CH-3. What survives is not enough
for an architect to settle:

- **Architecture is complete either way.** Both options make the stored type byte-derived; both make it
  true; both leave D4 as the identity on new rows (`application/octet-stream` resolves to `Opaque`, so
  the clamp is a no-op on A2's rows too); both close F3 and both mint the extension from the sniff
  (`ExtensionFor` yields `string.Empty` for an unrecognised type — `:134-135` — which is the documented
  "nameless is recoverable, mislabelled is not" behaviour).
- **The trade is entirely about what a cleaner mid-job experiences:** a legible refusal they can act on
  (and may not retry, in which case the photo never exists) versus an upload that succeeds and a tile
  that never renders (bytes kept, nobody told why).

**Therefore: the ADR decides the seam; the owner decides the branch.** `Q-ART-02`
(`open.md:1480-1505`) is filed and states both options in these terms. **This ADR does not re-file it.**

- **Default if the owner has not answered when the ticket is picked up: (A) refuse** — because it is
  what the sibling endpoint on the same table, container and accepted set already does
  (`UploadOrderPhoto.cs:67-68`), and one question with two answers across two endpoints is the divergence
  that produced this whole area's last two tickets. That is a tie-breaker, not a ruling.
- **What changes if the owner picks (B):** exactly one branch. `PhotoFileValidator`'s sniff rule is
  removed from the chain (decodability stays — F3 is unconditional), and the handler stores
  `SniffedContentType.FromContent(…) ?? ServedContentType.Opaque.Value`. D2's sentence is unaffected —
  the stored type is byte-derived under both. D3 changes meaning: GIF/PDF become *storable but never
  previewable* rather than unreachable. The ticket carries both AC sets and deletes one.

### D7 — **NEW (rev 2).** On `OrderPhoto` the **column** is the carrier; the minted extension is defence in depth (CH-9)

`OrderPhoto` will carry the answer twice: in `ContentType` and in the blob name's extension (D1 item 5).
The sibling draft (`NNNN-dispute-evidence-type-carrier-is-the-blob-name.md`, D1) refuses to add a
content-type column to `DisputeEvidence` on the ground that *"a column would give this surface two
sources of truth for one fact."* Both cannot be left standing without a reader having to guess which
applies here. The two drafts **do not merge** — they answer different questions (*where does the stored
type come from* vs *where is it carried*) and folding them would be one ADR with two decisions — but
they must agree, and this is the agreement:

- **Authoritative on `OrderPhoto`: the `ContentType` column**, read at `GetOrderPhotos.cs:96` (through
  D4's clamp). It is the only carrier any read path on this surface consults.
- **Defence in depth: the minted extension.** No read path resolves it. It is minted anyway because it
  removes a caller-controlled string from a server-managed blob name, it makes an orphaned blob
  self-describing, and `UploadOrderPhoto.cs:103` already does it — a divergence between the two
  endpoints on the *name* would be the same defect this ADR closes on the *type*.
- **Why this is not the two-sources-of-truth defect the sibling refuses.** Both carriers are derived
  from **one expression, in one statement, at one moment** — the sniff result feeds the column and
  `ExtensionFor` feeds the name. They cannot disagree for any row written after D1. What the sibling
  refuses is a second carrier written from a *second* derivation (a column set independently of the
  name), which is representable-wrong. **Two carriers from one derivation is redundancy; two carriers
  from two derivations is ambiguity.** That distinction is the shared premise, and it is stated here
  because this is the draft that creates the second carrier.
- **What the sibling's D2 round-trip test should record**, so its lead sees the same interface:
  `OrderPhoto` takes the **named exemption** — carrier: the `ContentType` column, read at
  `GetOrderPhotos.cs:96`. *(Recorded here; the sibling draft is its author's to edit and is not touched
  by this revision.)*

---

## Cross-lane — what this discharges, and what it no longer blocks

Two ADR lanes met on this intake within a day. Recording the interface so neither lead has to
reconstruct it. **Rev 2 corrects this section: the sibling lane has ruled, and it ruled the other way
from what rev 1 assumed.**

- **The content-policy panel ruled CH-4's option (a), not (b).** Its verdict
  (`user-uploaded-artifacts.md` §8.2, *"What the scrub dispatches on"*): **from the bytes it is holding,
  at the moment it runs — never a client string, never a persisted `ContentType`, not even a correct
  one.** That is stricter than this ADR and it is right.
- **Therefore: ~~the closing ticket blocks T-0459~~ — WITHDRAWN.** Rev 1 recorded a `depends_on` in the
  ticket and in the living doc §7.1 on the strength of F4. The sibling lane's ruling removes it: a scrub
  that sniffs its own bytes is decision-complete on this surface today, and §8.3 says so explicitly.
  **The `blocks: T-0459` field on `T-0561` should be removed by the PM.** Sequencing the closing ticket
  first is still *preferable* (fewer moving parts on one file) but it is not a dependency, and leaving a
  false one in the backlog is worse than none.
- **F4 is retained as context and struck as a justification.** It killed the status quo for a control
  that no longer needs it. Rev 1's own C-5 already conceded F4 does not pick D1 over A2; rev 2 goes
  further and does not count it for D1 at all.
- **What this ADR does NOT depend on.** Nothing here rests on whether an image decoder exists, is
  referenced, or is reachable — not the absence of the *library* (false: Skia is deployed transitively)
  and not the absence of the *call site* (true at HEAD). D1/D4 would be identical on a codebase full of
  decoders.
- **What this ADR does not touch, from the same challenge:** CH-2(b) (adversarial uploader on dispute
  evidence), CH-5, CH-6, CH-7 and the D8 PDF exclusion are the content-policy lane's to answer. CH-7's
  observation that a dispute-evidence PDF is served **inline** with no `rscd` is the same
  `GenerateSasUri` fact this ADR relies on for F1 (`BlobContainerClient.cs:93-110`) and is the reason
  read-path option (iii) is routed there rather than decided here.

---

## Alternatives considered

**A1 — Keep the exception; write the rule with an honest carve-out.** The lane's implicit position, and
the one the brief explicitly licenses. **Rejected**, on the evidence rather than on principle: its own
justification (the clamp bounds it) bounds to *inert*, not to *right* (F1), its fallback manufactures a
fact (F2), it hides a live 500 (F3), and the cost of removing it is an empty set of broken uploads
(Test 2). **What A1 gets right, conceded:** it is the correct default when the cost is unknown, and the
lane was right to refuse to decide it unilaterally.

**A2 — Sniff, but store `application/octet-stream` instead of refusing when the sniff fails.** The
strongest alternative. It ends the lie (F2) with **zero** refusals — no upload that succeeds today stops
succeeding, so even the mislabelled-HEIC case survives — and the clamp then serves it opaquely.
**Rev 2: not rejected. ESCALATED — see D6 and `Q-ART-02`.** Rev 1 rejected it on two grounds and one has
been withdrawn:

- ~~*"a photo that uploads and can never render is evidence silently lost on a path a dispute may later
  turn on."*~~ **Withdrawn — it points the other way.** Under A2 the bytes are stored, the SAS is minted
  with `application/octet-stream`, no `rscd` is set, and the browser **downloads** the file; an
  adjudicator can open it. Under D1 the upload is refused and, if the cleaner does not retry, the photo
  does not exist. On the evidence-preservation axis **A2 preserves and D1 destroys.** The honest form of
  the argument is a **usability** claim — a legible refusal at the moment of upload beats a tile that
  silently never renders — and it is a product judgement, not an architectural one.
- *Symmetry with `UploadOrderPhoto`* — survives as to the **control** (same accepted set, same table,
  same container, same refusal) and is conceded as **partial**: the declared-type affordance filter at
  `UploadOrderPhoto.cs:55-59` still has no counterpart (D1, "deliberately not part of"). It is a
  tie-breaker for D6's default, and it is recorded as one.
- *A third ground, offered but not decisive:* under D1 the container carries an invariant — every blob
  written to `order-photos` from here on is a JPEG, PNG or WebP — that future work may rely on. Under A2
  the invariant is "…or arbitrary bytes." That is a real architectural difference and it is honestly
  weaker than it sounds, because A2's `application/octet-stream` is a **true** statement, and truth is
  what F2 asked for.

**A3 — Delete `SaveOrderPhotos`; route every client to `UploadOrderPhoto`.** Ends the duplication that
caused this. **Rejected as out of scope, not as wrong:** it is a wire change across three generated
clients and two shipped mobile apps, and it drops a genuine capability — the web picker stages up to 30
photos and sends them in one command (`:46`, `order-photos.facade.ts:38-61`), which the single-photo
endpoint cannot express without 30 round trips. D1 makes A3 *cheaper* than before by removing the last
behavioural difference except batching and the declared-type affordance. The consolidation question stays
open in the living doc where the prior draft parked it.

**A4 — Fix it on the read path.** **Split in rev 2, because rev 1's single row rejected one proposal and
read as rejecting three.** See §"The read path: which repairs exist":
**A4(i) re-derive from the bytes on read — rejected** on a structural fact (the SAS path never holds the
bytes); **A4(ii) narrow the clamp to the intake's accepted set — ADOPTED as D4**, and it was never an
alternative to D1 because the two govern disjoint populations; **A4(iii) `rscd` — routed to the
dispute/content-policy lane**, not decided here, because `GenerateSasUri` is a shared mint whose other
users this ADR does not own.

**A5 — Widen the accepted set to `{jpeg, png, webp, gif, pdf}` so the change refuses nothing at all.**
Rejected: it makes the accept set follow the serve set, which is backwards — the serve set is "what this
platform may ever emit", the accept set is "what this surface's clients offer" — and it would oblige
`UploadOrderPhoto` to widen with it, for two formats nobody sends.

---

## Consequences

- **The general rule becomes writable, and it is written** (D2) — with an enforcer that can fail a build
  on the clause, or an honestly split tier if that enforcer cannot be built.
- **F1 closes on every row, not only on new ones** (D4). That is the change rev 2 makes to what this ADR
  is worth, and it came from the challenge.
- **The last stored-type lie on the platform ends** (D1). After this, every recorded content type on
  every intake is a statement about bytes the server read.
- **One live 500 closes** (F3) — worth more in practice than the type question, and unconditional on D6.
- **Two capabilities disappear from the order-photo container:** storing `application/pdf` or
  `image/gif`, and putting a caller-chosen extension on a server-managed blob name.
- **Some existing photos change behaviour:** rows recorded `image/gif` or `application/pdf` download
  instead of rendering inline (D4). Count owed as an owner query before merge; not asserted here.
- **i18n: no new key and no new locale row — but the claim is now specific** (D5). Rev 1's *"therefore no
  i18n work"* was true of key resolution and false of meaning. The work is choosing the **image**
  family's existing key over the **document** family's, verified in five locales on all three clients
  that can reach the route, plus one iOS provenance-roster row.
- **T-0459 is NOT blocked by this ticket.** Rev 1 said it was, in three places. The sibling lane ruled
  that the scrub sniffs its own bytes; the PM should remove `blocks: T-0459` from `T-0561`.
- **`T-0561` gains a second side** (D4): `GetOrderPhotos.cs` and `SniffedContentType.cs` come off the
  read-only list; `ServedContentType.cs` stays on it.
- **What this does NOT do, said plainly:** it closes no XSS hole (there is none here), it is not a
  malware scan, it does not retype a single row already stored, and it removes no metadata from
  anything. The metadata question is a different decision and stays in the content-policy ADR.
- **Immediately, before any panel:** the dated `patterns-backend.md` callout naming `SaveOrderPhotos` as
  the one intake outside the section's `Enforced by:` scope stays — it *withdraws* an implicit blessing
  and imposes nothing — with **two sentences restated as descriptive** (CH-10a) so it stops pre-deciding
  A1 and stops carrying an unenforced imperative.

## How a reviewer verifies compliance

1. `UploadIntakeRosterTests.ExpectedIntakes` contains **no** row whose annotation ends in `only`, and
   both `OrderController.SavePhotos` rows read `PhotoFileValidator`. The count assertion (`:64`) still
   runs **before** the set comparison.
2. `grep -n "DetermineContentType\|Path.GetExtension" src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs`
   returns nothing.
3. `SaveOrderPhotosContentTypeTests` asserts, on the row handed to `IOrderPhotoRepository.Add`:
   a `data:application/pdf` payload carrying PNG bytes stores **`image/png`**; a `data:image/png`
   payload carrying JPEG bytes stores **`image/jpeg`**; and the blob name passed to `UploadAsync` ends
   in the extension of the **sniffed** type, not of `FileName`. Each goes red under a distinct mutation
   (drop the handler's sniff; restore `Path.GetExtension`), and the mutation table records which.
4. A payload that is not JPEG/PNG/WebP fails **`PhotoFileValidator`** with
   `file.content_type_doesnt_match` — asserted on the validator, not on the handler, or the rule is
   untested where it runs. *(If the owner answers Q-ART-02 with (B), this step is replaced by its
   counterpart: the handler stores `application/octet-stream` and the upload succeeds.)*
5. A payload whose first 16 base64 characters decode to a JPEG signature and whose remainder is garbage
   fails validation and **does not reach** `Convert.FromBase64String` (F3). Mutate: delete the
   decodability rule → the test must go red with an unhandled `FormatException`, not a 400. **This step
   is unconditional on Q-ART-02.**
6. **A single-entry `errors` bag.** A payload that fails only the content rule produces one key
   (`nameof(BlobFileDto)`) with one message — not a `"; "`-joined value. Mutate: drop a
   `.WithErrorCode` → red.
7. **`GetOrderPhotosServedTypeTests`** asserts that a row recorded `application/pdf` resolves to
   `application/octet-stream` on **both** the DTO's `ContentType` and the SAS header. Mutate: revert
   `ServedFor` to `ForRecordedType` → red. `ServedContentType.cs` is unmodified.
8. **The empty-set claim is proven, not asserted:** the ticket records that `ImageCompressor.swift:77`
   emits `UTType.jpeg` and `ImageCompressor.kt:248` emits `Bitmap.CompressFormat.JPEG`, with a fixture
   whose bytes are a real JPEG head passing the new chain. A 3-byte JPEG-head payload still **passes**
   (CH-7) and there is a test saying so, or the next reader re-derives the wrong rule.
9. No new `BusinessErrorMessage` constant; `error-contract-parity.spec.ts` untouched on all three apps;
   `PartnerErrorVoiceTests.swift:89` names `PhotoFileValidator`.
10. **Checked only after D2's catalog edit lands** (CH-10b — before that this step passes vacuously):
    `consistency.md` no longer carries the `SaveOrderPhotos` or `GetOrderPhotos.MapToDto` deviation
    entries, and `patterns-backend.md`'s tier token reads `T1-CI` rather than `(gate pending: T-0561)`,
    **or** the split-tier fallback of D2 is in force and says so.

---

## Challenge (author-run, rev 1 — superseded by the independent round below)

*Kept for the trail. C-1 (this is not a security fix) and C-4 (the roster's predicate is the roster's own
assertion) stand. C-2 (empty set) was independently re-derived and held. C-3 and C-5 are absorbed into
CH-3's answer: the author-run round flagged the symmetry argument as interest-conflicted and conceded
that F4 does not decide between D1 and A2 — the independent round showed the remaining ground was
backwards, which is why D6 escalates rather than rules.*

**C-6 — Not self-challenged; start here.** Whether D1 should carry `UploadOrderPhoto`'s deletion (A3)
after all, and whether `GetOrderPhotos` should gate on `CanAccessOrderAsync` rather than
`CanBrowseOrderAsync`. **Neither is mine** — the second is an authorization ruling on ADR-0036/ADR-0037
territory, would change what a browsing cleaner sees before taking a job, and wants its own panel. Named
here only because F5 is the first place the consequence has been written down.

---

## Defense (rev 2, 2026-08-06) — answering `challenges/NNNN-stored-content-type-byte-derived.md`

Three moves only: **REBUT** with evidence, **CONCEDE + REVISE**, **ESCALATE**. Verified independently
before answering; where the challenge's own reasoning had a defect I say so even when conceding the
finding.

### CH-1 — the clamp cannot be both insufficient and sufficient. **CONCEDED + REVISED.**

Sustained. It was an equivocation on *safe*, not a disagreement about the code, and a document about to
become immutable may not contain both sentences. **The draft now says it once** (§"What the clamp is,
exactly"): the platform-wide clamp makes a row **inert** — no scripting context, `image/svg+xml` and
`text/html` excluded by name, which is the property `ServedContentType.cs:29-33` was built for and does
deliver on every row — and it does **not** make a row **right**, i.e. bounded to what this intake
accepts. F1 is entirely the second. Every "the clamp is what makes them safe" sentence is gone
(`:208-211` and `:252-255` of rev 1 are rewritten).

The challenger offered two ways out and I have taken **neither of them as written** — I have taken the
first one's stronger form: **D1 gains a limb that closes F1 on the stored population** (D4), so F1 does
not have to be demoted, and the row-count query stays owed as **evidence about impact** rather than as
the thing that decides the finding. It is already filed under Q-ART-02's rider (`open.md:1501-1505`) as
an owner query, and D4 now names it as a pre-merge item on the ticket.

### CH-2 — A4 rejects a proposal nobody made; the per-intake read clamp is the alternative that was never surfaced. **CONCEDED + REVISED. This is the finding that changes what gets built.**

Sustained in full, and I verified every load-bearing fact before conceding rather than taking it on the
challenger's say-so:

- `GetOrderPhotos.cs:96` resolves `ServedContentType.ForRecordedType(photo.ContentType)` — the
  **six**-value platform set (`ServedContentType.cs:34-42`) — while `AcceptedByIntake[OrderPhoto]` is
  **three** (`SniffedContentType.cs:91`).
- The assembly direction is clear: `GetOrderPhotos` is in `Cleansia.Core.AppServices.Features.Orders`,
  `SniffedContentType` is `internal` in `Cleansia.Core.AppServices.Common.Validators` (`:44`) — **same
  assembly** — and `SniffedContentType` already references `Cleansia.Core.Blobs.Abstractions` (`:2`), so
  the composition can return a `ServedContentType` without touching that type at all.
- The catalog obligation is real and `GetOrderPhotos` violates it today:
  `patterns-backend.md:1371-1373` — *"the read path reads the intake's own signature table."* Rev 1
  marked that file *"must not change"* while quoting the sentence it breaks.

**The PM's question, answered directly: the read clamp is a COMPLEMENT, and byte-derived intake still
earns its place — but not for the reason rev 1 gave.** In one paragraph a reader can check: the two
govern **disjoint populations**. D4 (narrow the clamp) is the *only* thing that can reach the rows
already written, because a write-path rule retypes nothing already stored and this surface has no
byte-holding read path (A4(i)); D1 (sniff at intake) is the *only* thing that can make the stored column
**true**, because a clamp is a compensating read performed by one call site and truth is a property of
the row. Concretely: after D1 the clamp is the **identity function** on every row D1 writes
(stored type ∈ accepted set ⊆ servable set), so it is not doing D1's work; and D4 closes
`application/pdf`-renders-inline on rows D1 will never see, so it is not doing D4's work. Two more
things only D1 reaches: the container's invariant — *every blob written here is a JPEG/PNG/WebP*, which
governs the **bytes**, where the clamp governs only a **header** — and F3's 500, which no read path
touches. And the structural reason to prefer a true column over a compensating read is on the record in
this very method: `GetOrderPhotos` once clamped the SAS header and emitted the raw column beside it
(`patterns-backend.md:1374-1377`), one method with two halves and one of them forgotten. Today
`GetOrderPhotos.cs:96` is the only reader of `OrderPhoto.ContentType` — verified across `Features/Orders`,
`GetOrderDetails` (count only) and the GDPR feature — so a discipline that must be repeated has exactly
one site today and an unknown number tomorrow. **D4 puts it in one named function for that reason.**

Both are now in the decision, A4 is split into (i)/(ii)/(iii) so it stops reading as a refutation of the
whole read path, and the ticket's *"must not change `GetOrderPhotos.cs`"* is struck. CH-2(b) (`rscd`) is
**routed, not dismissed**: the challenger's own concession (`patterns-backend.md:1359-1364`) rules it out
as *the* control, and the decisive point is that `GenerateSasUri` is one shared mint whose dispute-evidence
user legitimately previews PDFs — changing it is a product change on a surface this ADR does not own.

### CH-3 — A2's why-not has its sign backwards, and the sibling draft rules the opposite way. **CONCEDED on the argument; ESCALATED on the decision.**

Verified against source before conceding. Under A2 the bytes are stored, `GetOrderPhotos.cs:101` still
mints a SAS, `application/octet-stream` is pinned as the response type, and with no `rscd` the browser
**downloads** the file — an adjudicator can open it. Under D1 the refusal means the bytes never exist.
**On the evidence-preservation axis the challenger is right and rev 1 was backwards.** That sentence is
struck from A2's why-not.

I also accept the consistency point: the sibling draft's D3 calls the identical `Opaque` outcome *"a
silent capability loss on a support-critical path, not a security failure,"* and rev 1 called it
"evidence silently lost." **One reading had to go and it is mine.** The draft now uses the sibling's
reading everywhere (D3, D4, D6, A2): `Opaque` costs *previewing*, never *bytes*.

What I do **not** concede: that the remaining ground is empty. The symmetry argument survives as to the
**control** — `UploadOrderPhoto.cs:67-68` refuses on the identical accepted set from the identical table
writing the identical container — and the challenger's third limb, that the two endpoints still disagree
about the **declared** type, is correct but is about an affordance filter that `UploadOrderPhoto.cs:36-37`
itself calls *"not a control"*. Conceded as a partial delivery of symmetry and recorded in D1.

But symmetry is a tie-breaker, not a reason, and with the evidence argument withdrawn and F4 conceded
neutral (and, since the sibling lane ruled, no longer even pointing at D1), what remains is a judgement
about a cleaner mid-job. **That is the challenger's own suggestion to the lead and it is right: it goes
to the owner.** It is already filed as **Q-ART-02** (`open.md:1480-1505`) in exactly these terms —
I have **not** re-filed it. D6 records the escalation, the default and the one branch that changes.

### CH-4 — D2's named enforcer cannot fail a build on its clause. **CONCEDED + REVISED. Verified myself: yes, the annotation is discarded.**

I read the file rather than accepting the quote. `UploadIntakeRosterTests.cs:66-68`:

```csharp
Assert.Equal(
    ExpectedIntakes.Select(entry => entry.Split(" — ")[0]).ToList(),
    intakes);
```

`[0]` is the route name and **nothing in the file reads `[1]`**. The count assertion at `:64` counts
routes. The second test (`:76-84`) asserts four route names. **There is no assertion anywhere on the text
after `" — "`**, so `"…SavePhotos — BlobFileSize only"` and `"…SavePhotos — SniffedContentType(OrderPhoto)"`
are the same green build. The challenger's claim is exactly true, and `conventions.md:250-253` names this
failure by name — *"an entry claiming … named a test that asserts two sentences."* A law whose enforcer is
green on the violation it forbids is worse than an unenforced law, because it stops people looking.

D2 is rewritten: the gate the closing ticket must build is a **per-intake refusal theory** driven off
`ExpectedIntakes` with a count-first non-vacuity floor; the roster gains a **vocabulary** assertion
(closed set of annotations, no row ending in `only`) that is honest about proving the vocabulary and not
the fact; the tier is `(gate pending: T-0561)` for two named reasons; and if the theory cannot be built
in that ticket, the universal clause is **declared `T2-ADVISORY`** with the named per-intake pins as
`T1-CI`, stated as two clauses — never left labelled as a gate.

**Second defect conceded in full.** The section already opens with an `Enforced by:` at
`patterns-backend.md:1286-1290` and rev 1 would have added a second, narrower one that dropped
`EmployeeDocumentDownloadContentTypeTests`, `GetOrderPhotosServedTypeTests` and the two file validators'
suites — i.e. dropped the enforcers for the three avatar routes while writing a rule universally
quantified over *every* intake. **One clause, merged, union of enforcers.** D2 says so.

### CH-5 — "no i18n work" is true of resolution and false of meaning. **CONCEDED + REVISED, with a repair the challenge did not list.**

Verified: `en.json:1223` is *"File type is not allowed. Accepted: PDF, JPEG, PNG, DOC, DOCX"*;
`SniffedContentType.cs:83-86` and `patterns-backend.md:1351-1353` both say that string **is the promise
for documents**; `BusinessErrorMessage.cs` files the constant under document upload; and partner web is
the only client that can reach the new refusal (Test 2). Sending it a cleaner is a wrong sentence, it
names two formats D3 deliberately refuses, and it omits WebP.

The challenge priced three options — new key (15 rows), reword (breaks the document promise), or ship a
wrong sentence. **There is a fourth and it costs nothing:** use the **image** family's key,
`BusinessErrorMessage.FileNotMatchContentType` / `file.content_type_doesnt_match` — which is what
`ImageFileValidator` already uses for both its sniff and decodability rules (`:29,32`), which is the
correct family for a photo intake, and which I verified resolves in **all five partner-web locales**
(`en/cs/sk/uk/ru.json:1216`, en = *"File type does not match content"*), **all five Android partner
locales** (`values:1157`, `values-cs:1147`, `values-sk:1127`, `values-uk:1130`, `values-ru:1130`) and
**iOS** (`Localizable.xcstrings:1754`). Zero new keys, zero new rows, the document promise untouched, and
the sentence is **exact** for the only case a real client produces. D5 records it, including where it is
approximate (a hand-crafted GIF/PDF call, where "not accepted here" would be better than "does not
match") so a reviewer reads a decision rather than an oversight. The `## Consequences` bullet no longer
says "no i18n work."

**Rider accepted, redirected to the right row.** `PartnerErrorVoiceTests.swift:89` maps
`file.content_type_doesnt_match → "ImageFileValidator"` and gains `PhotoFileValidator`. Under D5 the rows
the challenger named (`:91,94`) do not change; `:90,92,93` already list `SaveOrderPhotos`. **I re-read
the assertion rather than inheriting the claim:** `testEveryPartnerReachableKeyResolvesInAllFiveLocales`
(`:198-209`) iterates `partnerReachable` and appends to `gaps` only when the **key** fails to resolve;
`emitters` appears solely inside the interpolated failure message. So this reddens no CI — it is stale
documentation in a roster whose stated purpose is backend provenance, and it is in the ticket.

### CH-6 — F5 names the wrong actor; "shared by every tenant" overstates reach. **CONCEDED + REVISED, both.**

(a) Rev 1's consecutive sentences contradicted each other. Minting requires assignment
(`SaveOrderPhotos.cs:114-117`); the wide `CanBrowseOrderAsync` gate is on the **fetch** side. F5 now
reads: **planted** by an assigned cleaner, **reachable** by any tenant cleaner who can browse the order
while a seat remains open, plus customer and admin. The living doc had it right and the ADR did not.

(b) Corrected. `PublicAccessType.None` (`BlobContainerClient.cs:151`), the only mint is `GetOrderPhotos`
behind `CanBrowseOrderAsync`, no cross-tenant reader obtains a URL, and `application/pdf` is deliberately
in the non-scripting class. F1's harm is now stated at its actual reach. I have added one sentence the
challenge did not ask for: the "shared host" phrasing is **correct** where it lives
(`ServedContentType.cs:7-14`, about a scripting origin) and must not be "fixed" to match this ADR.

### CH-7 — two false rows in the "exhaustively" list. **CONCEDED on the size row (re-derived myself); PARTLY REBUTTED on the framing.**

I re-derived rather than conceding on the quote. `Matches` (`:152-163`) requires only
`content.Length >= offset + bytes.Length` per fragment; the JPEG fragment is **3 bytes** at offset 0
(`:69`); `"/9j/"` is four base64 characters → one whole group → `FF D8 FF` → `image/jpeg` ∈ accepted;
`BlobFileSize.HasContentWithinLimit` (`:17-28`) passes it. **The row is wrong and is struck.** The
accurate row is now stated in terms of the head decode: *a payload whose first 16 base64 characters do
not decode to any signature this intake accepts*, which does newly refuse a payload of fewer than four
base64 characters (`DecodeHead` yields an empty span when `wholeGroups == 0`).

The decodability row is **not** false, and the challenger says as much — it is mis-attributed, which I
concede: the refusal is produced by rule 2 alone and dies with it. Now attributed.

I keep the challenger's framing point rather than arguing it: the mobile rows are the ones that carry the
ADR, they were independently re-derived and they held, and a wrong row beside them is corrosive precisely
because it teaches a spot-checking lead to distrust the list.

### CH-8 — a fourth copy of the validator chain, and the missing `.WithErrorCode`. **CONCEDED + REVISED.**

Both halves. The family is two `AbstractValidator<BlobFileDto>` siblings running the same ordered chain
differing only in the intake and the messages (`ImageFileValidator.cs:20-33`,
`DocumentFileValidator.cs:20-36`); an inline third instance means the next family-wide change has three
shapes to find. D1 item 1 now creates **`PhotoFileValidator`** as a member, consumed via `SetValidator`
inside the existing `ChildRules` block, which also makes the roster annotation read like every other
row's — which is what D2's clause presumes. `patterns-backend.md:1231-1233`'s *"Two siblings now"* becomes
three in the same catalog edit as D2 (a factual update, tied to the ticket landing).

The `.WithErrorCode(nameof(BlobFileDto))` omission is a real bug and is now in D1 item 2 with a
verification step of its own (step 6), not left to the implementer. **One correction to the challenge's
mechanism, which does not change the conclusion:** the value the client resolves is the *message*
(`file.…`), and the error code is the dictionary *key*; the failure is that un-coded failures group with
other un-coded failures and the joined `"a; b"` value resolves to no key, which every client turns into
`api.common.error_occurred`. Same outcome, one step over from where the challenge put it.

I also fixed a message-key hazard the challenge did not raise: mirroring `DocumentFileValidator`
wholesale would have swapped this route's presence message from `file.required` to `common.required`, a
client-visible change to a rule that is not being changed. `PhotoFileValidator` keeps `FileRequired` and
`FileSizeExceeded` (D1 item 3).

**Not conceded:** collapsing the family into one parameterized validator over `UploadIntake`. It is
probably right and it is a refactor of shipped, tested code across four intakes — a separate ticket, and
it is named in the living doc's open list.

### CH-9 — the two drafts must not merge but currently contradict each other. **CONCEDED + REVISED — resolved in this draft, per the panel's instruction.**

Agreed they do not merge: *where does the stored type come from* and *where is it carried* are two
questions and one ADR may hold one decision. **D7 is new and states the interface:** on `OrderPhoto` the
**`ContentType` column is authoritative** (read at `GetOrderPhotos.cs:96`, through D4's clamp); the
minted extension is **defence in depth** that no read path consults; and the sibling's D2 round-trip test
should name `OrderPhoto` in its exemption list with that carrier. **The sibling draft is not edited** —
the cross-reference is written here, from this side.

D7 also states the premise that makes the two drafts consistent rather than merely coexisting, because
without it the next reader has to guess: **two carriers minted from one derivation is redundancy; two
carriers written from two derivations is ambiguity.** The sibling refuses a `DisputeEvidence.ContentType`
column because it would be a *second derivation* — a value that can be set independently of the name and
therefore disagree with it. On `OrderPhoto` after D1 the column and the extension come out of one sniff
in one statement and cannot disagree for any row written after it. That is why D1 item 5 is right and the
sibling's D1 is also right.

### CH-10 — the pre-panel catalog edit is normative; step 8 is vacuous. **CONCEDED + REVISED, both.**

(a) Sustained. *"The general form of the sentence below therefore cannot be written while this stands"*
pre-decides A1, which is a live option the panel convened to weigh, and *"Do not copy this intake's shape
into a new one"* is an imperative on call sites carrying neither enforcer nor tier — the exact thing
`conventions.md:219-223` forbids. Both are restated as descriptive in the catalog, in this change. The
rest of the callout — pure disclosure of an existing exclusion, which is the part that imposes nothing —
stays, and I am glad it is the part the challenger wanted kept.

(b) Sustained. `consistency.md` carries no such entry today, so a reviewer running step 8 before D2 lands
passes for the reason `UploadIntakeRosterTests.cs:62-64` exists to refuse. The step is now **numbered 10
and explicitly ordered** — checked only after D2's catalog edit lands — and it now names **two** call
sites, because D4 adds `GetOrderPhotos.MapToDto` as a live deviation from
`patterns-backend.md:1371-1373` until the ticket closes it.

### Not raised by the challenger, self-reported in rev 2

The challenge could not have caught this because it is in a sibling lane's verdict, not in source: the
content-policy panel ruled that the scrub dispatches **from the bytes it is holding** (§8.2), i.e.
CH-4-on-that-draft's option **(a)**. Rev 1 asserted the opposite in three places and recorded a
`blocks: T-0459` on the ticket. **Withdrawn** — see §Cross-lane and the Consequences bullet. It costs
this ADR its F4 justification, which is the correct outcome: a justification that a sibling panel has
just made moot should not be carried forward because it was persuasive when written.

## Verdict — LEAD, 2026-08-06

Author (rev 1 + rev 2) · independent challenger
(`../challenges/NNNN-stored-content-type-byte-derived.md`, ten findings, five blocking) · lead
(2026-08-06), all distinct instances. `process/deliberation.md` step 5.

**Outcome: REVISE — every ruling survives; the enforcement specification, four statements and eight
citations do not. No further challenge round.** §E below is a **closed list**: the next pass is
transcription, not deliberation. On its completion this lands as
**`agents/backlog/adr/0044-stored-content-type-is-byte-derived-on-every-intake.md`**, status
`proposed`, PM checks it against §E only and stamps `accepted` — the ADR-0043 sequence.

> **Method (lead).** No `Bash` in this invocation — `Read`/`Glob`/`Grep`/`Write`/`Edit` only. Nothing
> compiled, executed or measured. **No claim below is inherited**, from the draft or from the challenge:
> every finding was re-opened at source before it was ruled on, and three of the rulings (§A, §B, §E-6)
> rest on facts neither document contains. Where a number is owed and I could not take it, the
> measurement and its owner are named.

### §A — RULING: the composition argument HOLDS. Here is the check, and here is the row it misses.

The author's central claim — D1 (sniff at intake) and D4 (clamp narrowed to the intake's accepted set)
are **complements, not substitutes**, governing disjoint populations — survives, and it survives for a
better reason than "disjoint". A reviewer verifies it in three steps by reading, no execution:

1. **D4 is the identity on every row D1 writes.** `AcceptedByIntake[OrderPhoto]` =
   `{image/jpeg, image/png, image/webp}` (`SniffedContentType.cs:91`). Each of the three is a *value* of
   `ServedContentType.ServableTypes` (`ServedContentType.cs:36-42`) and `ForRecordedType` maps each to
   itself. So for any `t` D1 can write, `ServedFor(t, OrderPhoto) == ForRecordedType(t) == t`. **D4 adds
   nothing on D1's rows — it is not doing D1's work.** (Also true under Q-ART-02 branch (B):
   `ForRecordedType("application/octet-stream")` is already `Opaque`.)
2. **Every row D4 acts on non-trivially is a row D1 cannot reach.** D4 acts only when the recorded value
   resolves outside the accepted set — `image/gif`, `application/pdf`, or anything already demoting to
   `Opaque`. D1 makes those unwritable from here on. So the set D4 changes is, by construction, the
   pre-D1 set. Disjoint by construction, not by argument.
3. **Each limb reaches something the other cannot, and neither is decorative.** D1 alone: F2 (the column
   becomes true), F3 (the 500), the container-bytes invariant, and the truth of the column for any
   *future* reader that does not route through the clamp. D4 alone: F1 on rows already written — which
   no write-path rule reaches, and which `SniffedContentType.ForDownload` cannot reach either, because
   `GetOrderPhotos.cs:118-141` mints a URL and never holds the bytes (contrast `DownloadMyDocument`,
   which downloads).

**Strengthen the statement, because "disjoint populations" alone invites a future reader to delete
whichever limb they meet first.** The stronger and equally checkable form: **each limb is the other's
backstop.** D4 is defence in depth against a write-path regression; **D1 is defence in depth against a
read path that skips the clamp** — which is not hypothetical, it is the defect this exact method already
shipped once (`patterns-backend.md:1374-1377`: one method, two halves, one of them forgotten). Put that
sentence in D4. It is the answer to "why both", and it does not depend on either population.

**The population NEITHER limb reaches, which the ADR must name.** A pre-D1 row recorded `"image/jpeg"`
by the tier-3 literal (`SaveOrderPhotos.cs:186`) over bytes that are not JPEG. D1 is too late for it;
D4 is the **identity** on it, because `image/jpeg` is in the accepted set. That row keeps asserting a
fact the system invented and keeps being served as `image/jpeg`. Harm is unchanged from today — a broken
tile — and it is **inert**, `image/jpeg` being in the non-scripting class. It is the F2 residue, it is
uncloseable on this surface for the same structural reason as A4(i), and its existence is exactly why
§E-2 strikes the Consequences over-claim. **Naming this row is the honest completion of the composition
argument; the argument does not need it to be empty, only to be stated.**

**One implementation hazard the composition depends on and neither document flags — see §E-3.** The
order inside `ServedFor` is load-bearing.

### §B — RULING: the tier token is honest; the gate as specified is NOT yet an enforcer. Two corrections, then it is.

**The token.** `(gate pending: T-0561)` is correct and sanctioned. `conventions.md:234` defines it;
`:237-242` *requires* it when the baseline is non-zero, and the baseline here is non-zero twice over —
`SaveOrderPhotos` violates D2's sentence and `GetOrderPhotos.MapToDto` violates
`patterns-backend.md:1371-1373`. The `T2-ADVISORY`-with-named-pins fallback, stated in advance rather
than discovered later, is the right shape and is the thing ADR-0032 asks for. **Accepted as written.**

**The gate.** CH-4's diagnosis is settled: `UploadIntakeRosterTests.cs:66-68` splits on `" — "` and keeps
`[0]`; nothing in the file reads `[1]`. The replacement — a per-intake refusal `[Theory]` driven off
`ExpectedIntakes` with a count-first floor — **can** fail a build on the edit it exists to catch, and
the closing chain is real: new route → roster red → add row → theory case missing → red → the case must
assert a refusal → to make it green you must add a content rule. **But as specified it also passes
vacuously, per case, for a reason one level below the floor it declares**, and this project has now
shipped two enforcers that enforced less than their description. It does not get a third.

- **The floor counts CASES, not REASONS.** The fourteen roster rows collapse to six command validators,
  each with heterogeneous constructor dependencies — `IOrderRepository` (`SaveOrderPhotos.cs:49`,
  `UploadOrderPhoto.cs:41`), `IDisputeRepository` (`UploadDisputeEvidence.cs:44`),
  `IUserRepository`+`IUserSessionProvider`+`ILanguageRepository` (`UpdateCurrentUser.cs:25-28`),
  `ICountryRepository`+`IEmployeeRepository`+`IUserSessionProvider`+`ITaxIdValidator`
  (`UpdateEmployee.cs:36-39`), and `SaveMyDocuments.cs:58-61` — and several run `MustAsync` existence
  checks. **`Assert.False(result.IsValid)` is green on every un-stubbed dependency**: the command is
  invalid because the order does not exist, not because the payload was refused. That is CH-4's own
  failure mode, moved down one level, in the gate written to close CH-4.
- **Two corrections make it real, and both are cheap.** (i) **Assert the reason, not the verdict**: the
  failure collection contains a failure on the file property whose `ErrorCode` is `nameof(BlobFileDto)`
  (or that route's coded equivalent) and whose message is that intake's content key. (ii) **Carry a
  positive control per case**: the *same* command with an accepted payload validates clean. The positive
  control is what proves every other rule was stubbed to pass — without it the case asserts nothing
  about content, and no count of cases can tell you so.
- **The vocabulary companion is over-claimed by exactly one clause.** *"it does fail the build on the
  edit that introduces a new unguarded intake"* is **false**: it fails only on an edit that introduces a
  new unguarded intake **and annotates it honestly as unguarded**. A developer who writes a validator
  name in the annotation gets a green build. The mechanism is a genuine `T1-CI` assertion over a
  **narrower clause** — *"every roster annotation is drawn from the closed vocabulary and no row reads
  `only`"* — and `conventions.md:246-250` requires the entry to say that boundary out loud, which the
  draft's own next sentence nearly does. Strike the false clause; state the narrow one. §E-5.

**Ruling: with §E-4 and §E-5 applied, D2's enforcer can fail a build on its clause and the token is
honest. Without them, it is a third test described as an enforcer.**

### §C — RULINGS per finding

| # | Author's answer | Lead |
|---|---|---|
| **CH-1** | conceded; *inert* vs *right* separated once, F1 closed rather than demoted | **Accepted.** `ServedContentType.cs:29-33` carries the *inert* property verbatim; the equivocating sentences are gone. Residue: the Consequences bullet re-commits the over-claim — §E-2 |
| **CH-2** | conceded in full; A4 split (i)/(ii)/(iii); (ii) adopted as D4 | **Accepted, and it is the finding that changed the build.** Every load-bearing fact re-verified by me: `GetOrderPhotos.cs:96` = `ForRecordedType`; six values at `ServedContentType.cs:36-42` vs three at `SniffedContentType.cs:91`; same assembly (`…Features.Orders` / `…Common.Validators`), `internal` reachable, `SniffedContentType.cs:2` and `GetOrderPhotos.cs:4` both already reference `Core.Blobs.Abstractions`. D4 is mechanically buildable as sketched |
| **CH-3** | evidence argument withdrawn; sibling's `Opaque` reading adopted; symmetry kept as a labelled tie-breaker; escalated | **Accepted.** Q-ART-02 verified filed at `open.md:1480-1499` with both branches in these terms and the two measurements at `:1501-1505`. Not re-filed, correctly |
| **CH-4** | conceded; roster annotation is discarded; new theory + vocabulary companion + split-tier fallback | **Accepted on the diagnosis; INSUFFICIENT on the replacement.** §B — the floor counts cases, not reasons, and the companion is over-claimed by one clause. §E-4, §E-5 |
| **CH-5** | conceded; a fourth repair the challenge did not price — the **image** family's existing key | **Accepted, and it is better than all three options the challenger costed.** Verified at source: `en.json:1216` = *"File type does not match content"*, `:1223` = the document promise; `BusinessErrorMessage.cs:229` / `:368`; `ImageFileValidator.cs:29,32` uses it for both rules. Zero new keys, zero locale rows. Residue is in the **ticket**, not the ADR — §F |
| **CH-6** | conceded both; actor corrected, reach corrected | **Accepted.** `SaveOrderPhotos.cs:115-118` refuses the non-assigned writer; `GetOrderPhotos.cs:59` is `CanBrowseOrderAsync`; `BlobContainerClient.cs:151` is `PublicAccessType.None` and `:93-110` sets `ContentType`+`CacheControl` and no `ContentDisposition` — I read the whole initialiser. The added sentence protecting `ServedContentType.cs:7-14`'s "shared host" phrasing from a well-meaning "fix" earns its place |
| **CH-7** | size row struck after independent re-derivation; decodability row re-attributed to rule 2 | **Accepted.** Re-derived a third time: `Matches` (`:152-163`) requires `content.Length >= offset + bytes.Length` **per fragment**; the JPEG fragment is 3 bytes at offset 0 (`:69`); `DecodeHead` (`:165-192`) yields empty only when `wholeGroups == 0`. `"/9j/"` → `FF D8 FF` → accepted, before and after |
| **CH-8** | conceded both halves; `PhotoFileValidator` as a family member; `.WithErrorCode` in the decision, not left to the implementer | **Accepted, and the author found a defect the challenger missed** — `DocumentFileValidator.cs:26` uses `BusinessErrorMessage.Required`, not `FileRequired`, so mirroring it wholesale would have silently changed this route's presence message. Verified. One phrasing correction — §E-10 |
| **CH-9** | conceded; D7 states the carrier and the redundancy/ambiguity premise | **Accepted on the premise — I adopt it as the shared rule — and WRONG in its instruction to the sibling.** §D-4, §E-8 |
| **CH-10** | conceded both; two sentences restated descriptively; step renumbered and ordered | **Accepted, and discharged at HEAD.** `patterns-backend.md:1292-1320` now reads *"Scope, descriptively"*; the imperative is gone; `consistency.md` confirmed to contain zero occurrences of `SaveOrderPhotos` / `GetOrderPhotos` / `SniffedContentType` / `ServedContentType`, so step 10's ordering clause is the right fix |

### §D — RULINGS on the four new decisions, and on the two questions the author put to the lead

1. **D4 — SOUND.** §A. It is buildable exactly as sketched, it complies with
   `patterns-backend.md:1371-1373` rather than deviating from it, and `ServedContentType.cs` stays
   unmodified, which keeps the closed set closed. **One addition is mandatory** — §E-3.
2. **D5 — SOUND, and fully verified at source.** The image family's key is the correct family for a
   photo intake, the document promise is untouched, and the place where the sentence is *approximate*
   (a hand-crafted GIF/PDF call) is stated as a decision rather than hidden. `PartnerErrorVoiceTests`
   is a provenance roster whose emitter strings appear only in a failure message, so it reddens no job —
   the author re-read the assertion rather than inheriting the claim, which is the standard.
3. **D6 — SOUND; the escalation STANDS.** This is a product call and the ADR is right to refuse it: the
   architecture is complete under both branches, the seam is identical, and the only surviving
   architectural input (sibling symmetry) is labelled a tie-breaker rather than dressed as a finding.
   **One sequencing note for the PM, which strengthens it** — everything in D1 except *one rule and one
   null-coalesce* is unconditional on the answer (D4, the decodability rule, the byte-derived store, the
   minted extension). The ticket should not be held hostage to Q-ART-02: land the unconditional side,
   and let the sniff-refusal rule be the one thing that waits.
4. **D7 — SOUND in its premise, WRONG in one word of its instruction.** *"Two carriers minted from one
   derivation is redundancy; two carriers written from two derivations is ambiguity"* is the correct
   shared rule and I adopt it for both drafts. But D7 asks the sibling's D2 to put `OrderPhoto` on the
   **exemption** list, and `OrderPhoto`'s three accepted types all round-trip today —
   `.jpg`/`.png`/`.webp` (`SniffedContentType.cs:69,70,72`) resolve back through
   `ServedContentType.ServableExtensions` (`:44-52`) to `image/jpeg`/`image/png`/`image/webp`. Exempting
   an intake whose pairs **pass** deletes the only assertion that would ever catch the minted extension
   drifting — leaving it *"written by D1, read by nothing, asserted by nothing"*, which is precisely
   CH-9's objection. **`OrderPhoto` is COVERED, not exempt**, with a note that its read path uses the
   column so the round-trip is defence in depth rather than the carrier. `EmployeeDocument` stays the
   only exemption, for the reason the sibling gives (`.doc`/`.docx` are unknown to `ServedContentType`).
   §E-8. *(The sibling draft is not edited by this verdict; its own lead applies this.)*
5. **One ADR or two — ONE. The author's question 1 is answered: keep them together.** "One decision per
   ADR" forbids bundling *unrelated* decisions; D1 and D4 are two limbs of one ruling — *on this intake,
   the type a reader is given is derived from the bytes: at the write path for the rows it can reach, at
   the read path for the rows it cannot.* The catalog sentence they implement is itself one bullet
   spanning both directions (`patterns-backend.md:1365-1373`). Splitting would produce two ADRs that must
   be read together to understand either, and would put F1 in an ADR that cannot close it. Precedent:
   ADR-0037 holds `OrderAvailability`'s SQL and in-memory forms as **one** decision pinned by an
   equivalence test, for the same reason. **The title must carry both limbs** — §E-1.
6. **The author's question 2 is answered by §D-3: the escalation stands, deliberately, on the record.**

### §E — CLOSED LIST (transcription only; nothing here re-opens a decision)

1. **Title gains the read limb.** e.g. *"The stored content type is derived from the bytes on every
   intake, and the served type is clamped to the intake's accepted set; the `SaveOrderPhotos` exception
   closes."* As it stands D4 is invisible to anyone searching for the read-path ruling.
2. **Strike the universal Consequences over-claim.** *"After this, every recorded content type on every
   intake is a statement about bytes the server read"* is false for the population §A names. Replace
   with: *"Every recorded content type written **from here on** is a statement about bytes the server
   read. The rows written before it keep what their uploader claimed; D4 demotes the ones that gained a
   capability, and a pre-D1 row recorded `image/jpeg` over non-JPEG bytes is reached by neither limb — it
   stays a broken tile, inert, exactly as today."* Same sentence goes in §A of D4's rationale.
3. **D4 gains the composition-order paragraph, and it is load-bearing.** `ServedFor` must resolve
   `ForRecordedType(recorded)` **first** and membership-test the **result's `.Value`** — never
   membership-test the raw recorded string. The natural implementation is the wrong order and it
   demotes real photos: `ServedContentType.ServableTypes` maps `image/jpg → image/jpeg`
   (`ServedContentType.cs:37`), and rows recorded `image/jpg` exist — `UploadOrderPhoto`'s declared-type
   allowlist contains `"image/jpg"` (`:39`) and that endpoint stored the client's string until the
   T-0556 follow-up (living doc §2, R1). Add a verification case: **a row recorded `image/jpg` resolves
   to `image/jpeg`, not `Opaque`**; mutate the order → red.
4. **D2's theory asserts the REASON and carries a POSITIVE CONTROL** (§B). Per case: (a) the failure
   collection contains a failure on the file property with that route's error code and that intake's
   content key — not merely `IsValid == false`; (b) the same command with an accepted payload validates
   clean. State that the count-first floor bounds the *case set* and the positive control bounds the
   *reason*, because the two failure modes are different and only one of them is what CH-4 caught.
5. **D2's vocabulary companion: strike the false clause.** Delete *"but it does fail the build on the
   edit that introduces a new unguarded intake, which is the edit the rule exists to catch."* Replace
   with: *"It enforces a narrower clause — every annotation is drawn from the closed vocabulary and no
   row reads `only` — and it is green on a new unguarded intake whose author writes a validator name.
   `T1-CI` over that narrower clause (`conventions.md:246-250`); the refusal theory is the enforcer of
   the clause itself."*
6. **NEW, and neither document could have caught it — the scrub landed on this handler mid-panel, and
   the ADR's central claim now depends on an unpinned invariant.** At HEAD,
   `SaveOrderPhotos.cs:137` reads `var storedBytes = ImageMetadata.Scrub(Convert.FromBase64String(base64Data)).Bytes;`
   and `UploadOrderPhoto.cs:107` the same — ADR-0043 D2/D3 has shipped on both. So the recorded type
   describes the **submitted** bytes while the blob holds the **scrubbed** bytes. It holds today by
   construction (`ImageMetadata.Rewrite:42-57` dispatches only to a walker whose own `Identifies`
   matched and returns the input unchanged otherwise — `ImageMetadataDispatchTests:34` asserts
   `Assert.Same`), so the container class is preserved on every branch. **It is not asserted anywhere,
   and a future walker change breaks D1's claim silently.** D1 gains an item stating the invariant and
   naming its pin: for every intake and every type in its accepted set,
   `SniffedContentType.FromContent(ImageMetadata.Scrub(p).Bytes, intake) == SniffedContentType.FromContent(p, intake)`
   — `T1-CI`, zero baseline. It spans `UploadOrderPhoto` and `UploadDisputeEvidence` too, so the
   generalized pin is a **separate small ticket** the PM files; this ADR states the invariant and cites
   it, and adds a verification step for `SaveOrderPhotos`.
7. **Verification step 6 is scoped to a ONE-photo command.** `RuleForEach` over up to 30 photos with one
   shared `.WithErrorCode(nameof(BlobFileDto))` groups every per-photo failure under one key and joins
   them with `"; "` — correctly, and by design. Two bad photos in one command therefore produce exactly
   the joined value the step forbids. Say *"a one-photo command that fails only the content rule"*, or
   the step asserts something false about the batch route it guards.
8. **D7's last bullet: `OrderPhoto` is COVERED by the sibling's round-trip test, not exempt** (§D-4).
   Restate as: *"the sibling's D2 test **covers** `OrderPhoto`'s three pairs — they round-trip today —
   and records that its read path resolves the **column**, so the round-trip is defence in depth rather
   than the carrier. `EmployeeDocument` remains the only named exemption."*
9. **Refresh every `SaveOrderPhotos.cs` citation and record the scrub as a HEAD fact.** The file moved
   under a live lane during the panel: the validator region is **+1** and the handler region **+3**
   against the draft. Verified at HEAD — `MaxPhotosPerRequest` `:47`; `ChildRules` `:65-86`; the inline
   presence+size rules `:77-82`; the `.When` gate `:86`; the assignment refusal `:115-118`; the comma
   split `:127-129`; `Path.GetExtension` `:133`; `Convert.FromBase64String` `:137`; `OriginalFileName`
   `:149`; `contentType:` `:151`; `DetermineContentType` `:174-187` (the tier-3 literal at `:186`).
   In the same pass, rewrite the §Cross-lane T-0459 bullet in the **past tense** — it is no longer a
   prediction that the scrub would not depend on this ticket; the scrub has shipped on this exact
   handler and does not depend on it. The **`patterns-backend.md` callout carries the same two stale
   citations** (`:171-184`, `:132`) and is fixed in the catalog edit — text in §G.
10. **D1 item 1's "family's order" phrasing.** `ImageFileValidator` has **three** rules
    (`:22-32`: size → sniff → decodability) and no presence rule; `DocumentFileValidator` has four
    (`:22-35`). Say *"the family's ordered `Cascade(CascadeMode.Stop)` chain, with the presence rule this
    route already has at its head — four rules, `DocumentFileValidator`'s shape with `ImageFileValidator`'s
    messages"*, or the next reader reads a uniformity the family does not have.
11. **Method declaration gains one line** recording that `SaveOrderPhotos.cs` was re-verified at HEAD on
    2026-08-06 after the ADR-0043 scrub landed on it, and that the citations in the body are post-scrub
    — the same courtesy the draft already extends for the `patterns-backend.md` +7 offset.
12. **Landing.** Rename to `agents/backlog/adr/0044-stored-content-type-is-byte-derived-on-every-intake.md`,
    status `proposed`, `Number:` header replaced by the allocation (0043 is `accepted`; 0044 is free).
    Rename the challenge file to `challenges/0044-stored-content-type-byte-derived.md` — the numbered
    form is the majority convention in that folder (`0034-` … `0042-`) — and update its two references
    (this ADR, living doc §7.1) in the same change. Carry `## Challenge` (the author-run rev-1 round, as
    labelled) + `## Defense` + this `## Verdict` verbatim. Update the `(gate pending: T-0561)` token if
    the PM reassigns the ticket id.

### §F — Not this ADR's to fix, handed to the PM (do not absorb into the transcription pass)

- **`T-0561:141` still forbids the file D4 must change.** *"Read-only, must not change:
  `ServedContentType.cs`, `SniffedContentType.cs`"* — but D4 puts `ServedFor` **on**
  `SniffedContentType`. Only `ServedContentType.cs` stays read-only. (Distinct from the two lines the PM
  has already fixed.)
- **`T-0561` is stale against rev 2 in five more places:** AC2/AC3/AC7 name `FileTypeNotAllowed` /
  `InvalidFileType`, superseded by D5's `FileNotMatchContentType`; AC5's roster annotation should read
  `PhotoFileValidator`, not `SniffedContentType(OrderPhoto)` (D1 item 1); AC8 names one `consistency.md`
  deviation entry where D2 creates two; §Context `:53-60` still asserts *"It blocks T-0459"* and *"Do not
  ship a scrub … before this ticket lands"*, both now false in **fact** as well as in law — the scrub has
  shipped on this handler (`SaveOrderPhotos.cs:137`); §Implementation-notes line numbers carry the same
  +1/+3 drift as §E-9. Add the §E-4/§E-6 gates to the AC list.
- **The living doc is stale against HEAD on the scrub**, and it is not this lane's field: §2's
  order-photo rows read *"Metadata scrubbed: no"*, §5's row is `(gate pending: T-0459)`, and §8.3 lists
  T-0459 as pending — while `SaveOrderPhotos.cs:137` and `UploadOrderPhoto.cs:107` both call
  `ImageMetadata.Scrub`. The T-0459 lane owns that correction. **Re-verify before acting: I read the
  working tree, and `Features/Orders` is a live lane.**

### §G — Catalog edit owed with this ruling (routed; not applied by the lead)

`patterns-backend.md` is being written by another lane, so the text is handed over rather than applied.
Two edits, both inside the dated `SaveOrderPhotos` callout at `:1292-1320`, both **descriptive**:

- Replace `` `SaveOrderPhotos.DetermineContentType` (`:171-184`) `` with
  `` `SaveOrderPhotos.DetermineContentType` (`:174-187`, the literal at `:186`) ``, and
  `` `Path.GetExtension(file.FileName)` (`:132`) `` with `` (`:133`) `` — the file moved +1/+3 when
  ADR-0043's scrub landed on it (`SaveOrderPhotos.cs:137`).
- Append one sentence to the callout: *"Cross-lane note (descriptive — not a rule): ADR-0043's metadata
  scrub now runs on this handler between the decode and the upload (`SaveOrderPhotos.cs:137`,
  `UploadOrderPhoto.cs:107`), so a recorded type describes the SUBMITTED bytes while the blob holds the
  SCRUBBED ones. The two agree only because `ImageMetadata.Rewrite` (`:42-57`) dispatches to a walker
  whose own signature matched and returns the input unchanged otherwise; that agreement is unpinned."*

The **general sentence (D2) and the deviation entries (`consistency.md`) land with the closing ticket**,
not now — the token is `(gate pending: T-0561)` for the two reasons D2 states, and both remain true.

### §H — Owner: no new question. Q-ART-02 stands, and one measurement is now dated.

`Q-ART-02` (`open.md:1480-1505`) covers the refuse-vs-store-un-previewable branch in exactly the terms
this panel needs and is **not re-filed**. Its rider already owns the one number this ADR declines to
assert — `SELECT "ContentType", count(*) FROM "OrderPhotos" GROUP BY 1`, owner-run, **before merge**,
because it sizes how many photos change from previewing to downloading on the day D4 ships. It does not
decide D4. The second rider measurement (does the rewritten pin redden under each named mutation) is the
closing ticket's Gate 0.5 and §E-4's positive control is now part of it.

**Consensus: reached, conditional on §E.** Zero blocking challenges survive. Nothing in §E requires a
further round; a lead re-read is not owed once it is transcribed.
