# Challenge — ADR-NNNN (draft) `The stored content type is derived from the bytes on every intake; the SaveOrderPhotos exception closes`

**Role: challenger (attack).** Independent round — the draft's `## Challenge` at `:422` is author-run and
says so, so nothing there is treated as discharged. I read it only to see which alternatives the author
already saw.

**Method, stated because the draft states its own.** No `Bash` in this invocation (`Read`/`Glob`/`Grep`/
`Write` only). **Nothing was compiled, executed or measured.** Every claim below is read from source at
HEAD and cited `file:line`; where I want a number I could not take, I say who should take it rather than
inventing one. No claim is inherited from the draft, the ticket (`T-0561`), or the living doc — I
re-opened every file the draft cites for the findings I raise.

**Headline.** The change is probably right and I am not asking for a revert of its direction. But the
document's own load-bearing chain does not hold:

- Its lead finding (**F1**) is stated as the reason the exception dies, and **D1 does not close F1** — not
  for a single row already written, and the draft says so approvingly two pages apart from saying the
  opposite (**CH-1**).
- The alternative that *does* close F1 is **not in the alternatives table at all**. A4 rejects a
  different proposal (CH-2). This is the strongest finding.
- The reason given for rejecting **A2** — the ADR's central trade-off, and the one the author names as
  the challenge he most wants pressed — **points the other way on its own facts**, and the same author
  rules the opposite way on the identical outcome in the sibling draft (CH-3).
- **D2's named enforcer cannot fail a build on the clause it is named for** (CH-4).
- **"No new key, therefore no i18n work"** is true of key *resolution* and false of what the cleaner is
  told (CH-5).

**Blocking: CH-1, CH-2, CH-3, CH-4, CH-5.** CH-6…CH-10 are corrections to a document about to become
immutable.

---

## CH-1 — BLOCKING. The clamp cannot be both "one set too wide" and "what makes the legacy rows safe." The draft asserts both, and the consequence is that F1 stays fully open on every row already written.

**The hole.** Two sentences, both load-bearing, both in this draft:

> `:83` — "**The clamp bounds to the SERVE set, not to the ACCEPT set, and the two differ.**"
> `:113-115` — "**F1 — the bound is one set too wide.** `application/pdf` and `image/gif` are storable and
> servable here and nowhere else on this container."

versus

> `:208-211` — "the catalog's 'a write-path rule retypes nothing that is already stored' bullet … has no
> read-path remedy to offer here — **the clamp is all the legacy rows will ever get, and that is fine,
> because the clamp is what makes them safe.**"
> `:252-255` — "the read-path clamp in `GetOrderPhotos.MapToDto` stays exactly as it is … it is **defence
> in depth** for the rows written from here on."

If the clamp makes a stored row safe, F1 is not a finding and the exception survives on its own
justification. If F1 stands, the clamp does **not** make a stored row safe — and then `:211` is false for
exactly the population D1 cannot reach.

**Why it matters, mechanically.** I verified the chain the draft relies on:
`GetOrderPhotos.cs:96` resolves `ServedContentType.ForRecordedType(photo.ContentType)`;
`ServedContentType.cs:34-42` admits **six** values including `image/gif` and `application/pdf`;
`GetOrderPhotos.cs:140` hands that to `GenerateSasUri(blobName, 1h, servedAs)`; and the builder
(`src/Cleansia.Infra.Azure.Storage.Blobs/BlobContainerClient.cs:93-110`) sets `ContentType` and
`CacheControl` and **no `ContentDisposition`** — I read the whole `BlobSasBuilder` initialiser and there
is no `rscd` in it. So a row already carrying `application/pdf` renders inline, today, tomorrow, and
after D1 ships. **D1 changes nothing about it.** The draft's `## Consequences` even lists this as a
feature: *"it does not retype a single row already stored"* (`:389-390`).

The draft does not say how many such rows exist. It cannot — no shell, same as me. That is fine; what is
not fine is that neither the ADR nor `T-0561` asks anyone to find out. Azure DEV is live (the deployment
memory), `SaveOrderPhotos` is the route both mobile clients call, and a caller who wants the capability
does not need a UI — the draft's own F1 says so.

**What I want.** One of:
1. D1 gains a limb that closes F1 on the stored population (CH-2 names two ways), **or**
2. F1 is demoted from "the finding that kills the exception" to "a capability that is closed
   going forward and knowingly left open on existing rows," **and** the ADR carries the query that
   measures it (`SELECT "ContentType", count(*) FROM "OrderPhotos" GROUP BY 1`) as a pre-`accepted`
   evidence item the way ADR-0040's stop conditions do — run by the owner, not asserted here.

Either is defensible. Asserting both `:83` and `:211` is not.

---

## CH-2 — BLOCKING. A4 rejects a proposal nobody made. Two read-path repairs exist, **neither needs the bytes**, and one of them is already an obligation in the catalog sentence this ADR quotes.

**The hole.** The alternatives table has one read-path row:

> `:365-367` — "**A4 — Fix it on the read path, as the document intakes were fixed.** **Rejected on a
> structural fact**: the server never sees an order photo's bytes after intake. The document technique
> does not exist on this surface."

That is true and it answers exactly one proposal: *re-derive the type from the payload* (`SniffedContentType.ForDownload`).
It answers **neither** of the two read-path repairs that would actually close F1, because neither reads a
byte:

**(a) Clamp the read path to the INTAKE's accepted set, not to the platform serve set.**
`GetOrderPhotos.Handler` statically knows which intake it serves — it is hard-coded to
`Constants.BlobContainers.OrderPhotos` at `:66` and `:135`. `AcceptedByIntake[UploadIntake.OrderPhoto]`
is `{image/jpeg, image/png, image/webp}` (`SniffedContentType.cs:91`). `GetOrderPhotos` lives in
`Cleansia.Core.AppServices.Features.Orders` and `SniffedContentType` is `internal` in
`Cleansia.Core.AppServices.Common.Validators` (`:44`) — **same assembly**, so there is no assembly-direction
problem of the kind that blocks the sibling draft's C-4. This is a one-line change at
`GetOrderPhotos.cs:96` plus one accessor, and it demotes a legacy `application/pdf` row to
`Opaque` → the browser downloads it instead of rendering it. **It closes F1 for the rows already stored
and for any future gap, and it needs no bytes at all.**

And it is not my invention. The catalog bullet this ADR leans on, read to its end:

> `patterns-backend.md:1358-1366` — "**A write-path rule retypes nothing that is already stored.** …
> Close the residue where it is a **closed set** — the handlers that serve the blob … and the same
> discipline: **the read path reads the intake's own signature table**, or the two answers drift and a
> document is one type on the way in and another on the way out."

`GetOrderPhotos.cs:96` does **not** read the intake's own set; it reads the platform-wide one. That is a
live violation of a shipped catalog sentence, on the exact call site this ADR declares off-limits
(`T-0561` §Out of scope: *"`GetOrderPhotos.MapToDto` is untouched"*, and §Implementation notes:
*"**Read-only, must not change:** `GetOrderPhotos.cs`"*). An ADR is the only sanctioned way to deviate
from the catalog; this one deviates from `:1364-1366` without naming it.

**(b) `rscd`.** `BlobSasBuilder.ContentDisposition` exists and is not set
(`BlobContainerClient.cs:93-110`). `ContentDisposition = "attachment"` on non-image served types closes
the *"renders inline"* half of F1 — which is the entire harm F1 describes — for every container that
uses this one mint, including dispute evidence, where the accepted set legitimately contains
`application/pdf` and therefore **(a) can never help**. The draft itself records that a sibling lane
reached this same call site (`:330-333`, CH-7 on the content-policy draft: *"a dispute-evidence PDF is
served inline with no `rscd`"*).

I concede the counter in advance, because it is real: `patterns-backend.md:1352-1357` says **"Do not lean
on `Content-Disposition`… The control is the byte-derived type; the disposition is luck."** That rules
out `rscd` as *the* control. It does not rule it out as defence in depth, and it does not touch (a) at
all.

**Why it matters.** The draft argues the exception dies because the clamp is one set too wide (F1). The
cheapest repair for "the clamp is one set too wide" is **to narrow the clamp** — a two-line change with
no client impact, no refusals, and coverage of the stored population — and it is not in the alternatives
table, is not in the living doc's trade-off table (`user-uploaded-artifacts.md:190-196`), and is
explicitly forbidden by the ticket. This is the shape the deliberation protocol exists to catch: *"the
alternative the author dismissed too fast."* Except it was not dismissed. It was never surfaced.

**What I want.** The alternatives table gains a row for (a) — **narrow the read clamp per intake** — and
one for (b), each answered on its merits. A4's why-not is rewritten to name what it actually rejects
(*re-derive from bytes on read*), so it stops reading as a refutation of every read-path option. If the
panel rules D1 **and** (a), say so, and `T-0561`'s "must not change `GetOrderPhotos.cs`" comes out. If the
panel rules D1 **instead of** (a), the ADR must say why a rule that governs 100% of future rows was
preferred to one that governs 100% of all rows, when the two are not exclusive.

---

## CH-3 — BLOCKING. The stated reason for rejecting A2 has its sign backwards on its own facts, and the same author rules the opposite way on the identical outcome in the sibling draft. And D1 does not deliver the symmetry that is carrying the rest of the rejection.

**The hole.** A2 (`:347-356`) is *"sniff, but store `application/octet-stream` instead of refusing."* It
is rejected on two grounds. The second:

> `:352-355` — "an upload that succeeds and can never render is the outcome `patterns-backend.md:1321-1328`
> already rules against, and it is *worse* than a refusal for the cleaner: the tile is broken with no
> diagnosis and **the photo is silently missing from a job record that a dispute may later turn on**."

Under A2 **the bytes are stored.** `GetOrderPhotos.cs:101` still mints a SAS; `servedAs.Value` is
`application/octet-stream`; `BlobContainerClient.cs:104` pins it as the response `Content-Type`, and with
no `rscd` the browser **downloads** the file. An adjudicator opens it. The evidence exists and is
retrievable.

Under **D1** the upload is **refused with a 400 and the bytes never exist.** If the cleaner — on site,
mid-job, on a phone — does not successfully retry, the photo is gone from the job record permanently.

So on the ADR's own axis (*evidence a dispute may later turn on*), **A2 preserves the evidence and D1
destroys it.** The argument as written supports A2.

**The same author already ruled this the other way, one file over.** The sibling draft
`NNNN-dispute-evidence-type-carrier-is-the-blob-name.md`, D3 (`:114-122`), characterises the *identical*
`Opaque` outcome:

> "an evidence file serves as `application/octet-stream`, so the customer's photo or PDF **downloads
> instead of previewing** … It is a **silent capability loss on a support-critical path, not a security
> failure** … Nobody would see a stack trace; someone would eventually file 'evidence previews are
> broken.'"

*Capability loss, recoverable* in one draft; *evidence silently lost on a money path* in the other. Both
drafts are dated 2026-08-06 by the same author instance. One of the two readings has to go.

**And the first ground — symmetry — is not delivered by D1.** `:350-352`: *"`UploadOrderPhoto` refuses on
the identical accepted set writing the identical table on the identical container; A2 gives one question
two answers."* After D1 the two endpoints still disagree about the *declared* type:
`UploadOrderPhoto.Validator` requires `Command.ContentType` to be one of four strings
(`UploadOrderPhoto.cs:38, 55-59`, `BusinessErrorMessage.InvalidFileType`), while D1 leaves
`SaveOrderPhotos` reading **no** declared type at all — `DetermineContentType` is deleted (`:241`) and
`BlobFileDto.ContentType` was never read. All three clients keep sending a field
(`PartnerOrderClient.swift:209`, `OrdersRepository.kt:283`, `order-photos.helpers.ts:100`) that nothing
on the server reads. So "one question, two answers" survives D1 in a different place, and the symmetry
argument is weaker than the sentence that rejects A2 on it.

**Why it matters.** The author's own C-5 (`:446-451`) concedes that **F4 does not choose D1 over A2**, and
C-3 (`:436-439`) flags the symmetry argument as interest-conflicted and asks a lead to weigh it. Strip F4
(conceded neutral) and strip F1 (CH-1: not closed by D1 anyway), and the *entire* case for D1 over A2 is
(i) symmetry, which D1 only partly delivers, and (ii) the evidence argument, which points the other way.

**What I want.** Either A2's why-not is re-argued on ground that survives — the honest one is *"a refusal
the cleaner can act on beats a silently broken tile they will not notice"*, which is a **usability**
claim, not an evidence-preservation claim, and it deserves to be stated as one — or the panel picks A2.
Whichever way it lands, the sibling draft's D3 and this draft's A2 must say the same thing about what an
`Opaque` blob costs, because the two are the same fact.

---

## CH-4 — BLOCKING. D2 names `UploadIntakeRosterTests` as the enforcer of a clause that test does not assert, and inserts a second `Enforced by:` — with a *narrower* enforcer list — into a section that already has one.

**The hole.** D2 (`:275-280`):

> **Enforced by:** `UploadIntakeRosterTests` — **every row's annotation is `SniffedContentType(<intake>)`
> or the named validator that calls it, and no row may read "…only"**; plus the per-intake pins … —
> **`T1-CI`**

I read the whole test file. The annotation is **split off and discarded**:

```
UploadIntakeRosterTests.cs:64      Assert.Equal(ExpectedIntakes.Length, intakes.Count);
UploadIntakeRosterTests.cs:66-68   Assert.Equal(
                                       ExpectedIntakes.Select(entry => entry.Split(" — ")[0]).ToList(),
                                       intakes);
```

`[0]` is the route name. **Nothing in the file reads `[1]`.** The second test (`:76-84`) asserts four
route names. There is no assertion anywhere on the text after `" — "`. So today a row reading
`"…SavePhotos — BlobFileSize only"` and a row reading `"…SavePhotos — SniffedContentType(OrderPhoto)"`
are the *same green build*, and tomorrow a fifteenth intake added with the annotation
`"— BlobFileSize only"` reddens nothing once its route name is on the list.

Per `conventions.md:232` a mechanism that cannot set the exit code on the offending change is
`T2-ADVISORY` "however it is labelled", and `:251-253` is explicit that *"a guard test that walks the
tree must fail when its corpus is empty or its anchor is missing. A test that passes because the files
were renamed away is not an enforcer."* The roster is a genuine `T1-CI` enforcer of **route enumeration**.
It is not, as written, an enforcer of **which rule guards each route** — which is the whole content of
D2's clause.

**Second defect in the same block.** D2 says the sentence goes in *"as the section's opening obligation"*
of §"The declared content type is a HINT; the bytes are the evidence". That section **already opens with
an `Enforced by:`** — `patterns-backend.md:1286-1290`, naming `SaveMyDocumentsHandlerTests`,
`UpdateEmployeeStoredContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`,
`UploadOrderPhotoContentTypeTests`, `UploadDisputeEvidenceContentTypeTests`,
`GetOrderPhotosServedTypeTests` **+ the two file validators' tests**. D2's list drops
`EmployeeDocumentDownloadContentTypeTests`, `GetOrderPhotosServedTypeTests` and the two file-validator
suites — i.e. it drops the enforcers for the **three avatar routes** (`UserController.UpdateCurrentUser`
on Customer, Mobile.Customer, Mobile.Partner) while writing a rule that is universally quantified over
"**Every** intake that puts a file into storage." So the section ends with two `Enforced by:` clauses of
different scope and no statement of which governs.

**Why it matters.** D2 is the deliverable — `:378` calls the general rule "the deliverable; the code
change is what makes it honest." A law whose named enforcer is green on the violation it forbids is the
ADR-0032 failure this codebase writes ADRs about.

**What I want.**
1. D2's `Enforced by:` names an enforcer that can go red on the clause. The honest minimum I can see is
   a companion assertion in the same file: for each row, resolve the annotation's named validator/type by
   reflection and assert it exists **and** that the route's command type is covered by a validator whose
   rule set references `SniffedContentType` — or, simpler and stronger, a per-intake theory that feeds
   each of the fourteen routes' validators one fixed non-image payload and asserts refusal. I am naming
   the shape, not costing it; the author or the closing ticket costs it.
2. If no such enforcer ships with the rule, the tier is `T2-ADVISORY` for the universal clause with
   `T1-CI` on the five named instances — stated as two clauses, not one.
3. The existing `Enforced by:` at `:1286-1290` is merged into D2's, not shadowed by it.

---

## CH-5 — BLOCKING. "No new key, therefore no i18n work" is true of key *resolution* and false of what the cleaner is told. The one client that can hit the new refusal is the one whose message names the wrong accepted set.

**The hole.** `:257-261`:

> "**No new `BusinessErrorMessage` key, therefore no i18n work.** `FileTypeNotAllowed`
> (`file.type_not_allowed`) and `InvalidFileType` are both live and already carry five-locale `api.*`
> entries on the partner app."

The resolution claim is **true** — I checked all five and both keys, and they exist on every client that
can reach the route:

| Client | `file.type_not_allowed` | `file.invalid_file_type` |
|---|---|---|
| partner web | `api.file.type_not_allowed`, en/cs/sk/uk/ru (`en.json:1223` + 4) | `en.json:1217` + 4 |
| Android partner | `error_file_type_not_allowed` ×5 (`values/strings.xml:1161` + 4) | `:1158` + 4 |
| iOS | `error.file.type_not_allowed` ×5 (`Localizable.xcstrings:1999-2032`) | `:1859` |

**What is wrong is the sentence.** The partner-web value, in all five locales, is the **document**
promise:

```
en.json:1223  "type_not_allowed": "File type is not allowed. Accepted: PDF, JPEG, PNG, DOC, DOCX"
en.json:1217  "invalid_file_type": "Invalid file type. Please upload valid document files."
```

`AcceptedByIntake[UploadIntake.OrderPhoto]` is `{image/jpeg, image/png, image/webp}`
(`SniffedContentType.cs:91`). So after D1, a cleaner whose photo is refused is told PDF and DOCX are
accepted (they are refused — that is **D3**, on purpose) and is not told WebP is (it is). The two mobile
clients say "This file type isn't allowed." — generic and correct — and **cannot hit this refusal at
all** per the draft's own Test 2. **Partner web is the only client that can reach it, and it is the only
one whose string is wrong.**

This is not a stylistic point. It is the catalog rule this ADR cites, in the direction the ADR did not
check:

> `patterns-backend.md:1344-1346` — "**Keep the accepted set equal to what the clients offer** … the web
> picker's accept list and **the five-locale `file.type_not_allowed` string ("Accepted: PDF, JPEG, PNG,
> DOC, DOCX") are the promise**, so a format missing from the table refuses an upload the UI invited."

and the code says the same in as many words: `SniffedContentType.cs:83-86` — *"the five-locale
`file.type_not_allowed` string … **are the promise for documents**; `AVATAR_ALLOWED_CONTENT_TYPES`,
`PHOTO_ALLOWED_TYPES` and `DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES` are the promise for the other three."*
`BusinessErrorMessage.cs:366` files the constant under `// Document Upload`.

**And the repair is not free, which is the point.** Re-wording the web string to something generic
weakens the document promise that `DocumentFileValidatorTests.cs:16-18` pins and that
`SniffedContentType.cs:83` cites. So the honest options are (i) a **new** key for the photo intake —
which the ADR itself prices at *"a three-app, fifteen-row change"* (`:260-261`) and rules out — or (ii)
knowingly shipping a wrong sentence to the only client that sees it. That is a real cost the ADR
currently books at zero, and it is the cost that made `T-0561` size `S`.

**Non-blocking rider, same evidence.** `PartnerErrorVoiceTests.swift:91,94` carries a **provenance**
roster mapping each key to its backend emitters — `"file.type_not_allowed": "DocumentFileValidator"`,
`"file.invalid_file_type": "DocumentFileValidator, UploadOrderPhoto"`. I read the assertion
(`:198-209`): the emitter strings are used only in the failure message, so this does **not** redden iOS
CI. It does become stale documentation in a suite whose stated purpose (`:53-56`) is that the roster
"comes from the backend side". Add `SaveOrderPhotos` to both rows in the same change.

**What I want.** The `## Consequences` bullet stops saying "no i18n work." The ADR rules explicitly:
new photo-intake key (and books the 15 rows), or reuse + reword (and books the document-promise
regression), or reuse + accept the wrong sentence (and says so, so a reviewer does not read it as an
oversight).

---

## CH-6 — F5 names the wrong actor, and "a storage host shared by every tenant" claims an audience the code does not give.

**(a) The actor.** `:151-155`, consecutive sentences:

> "**Writing** still requires assignment (`SaveOrderPhotos.cs:114-117`); it is the **fetch** side that is
> wide. So the inline-`application/pdf` capability in F1 … **is mintable by any cleaner in the tenant who
> can see the order while a seat remains open**"

Minting requires assignment; the sentence says the opposite of the sentence before it. I re-verified both
halves: `SaveOrderPhotos.cs:114-117` refuses a non-assigned caller with
`EmployeeNotAssignedToOrder`; `GetOrderPhotos.cs:59` gates on `CanBrowseOrderAsync`, and
`OrderAccessService.cs:68-92` returns `true` for any `Employee` with a resolvable id when
`order.HasAvailableSpots && OrderVisibility.NotHeldFrom(...)`. The living doc gets it right
(`user-uploaded-artifacts.md:224-226`: *"can **mint a SAS for its photos**"*); the ADR does not. Fix
before it becomes immutable.

**(b) The audience.** `:88-90` — *"those bytes render inline, as a PDF, **from a storage host shared by
every tenant**."* The host is shared; the *reach* is not. `BlobContainerClient.cs:151` creates containers
`PublicAccessType.None`, the blob is reachable only through a per-blob SAS, and that SAS is minted only
by `GetOrderPhotos` behind `CanBrowseOrderAsync`. No cross-tenant reader obtains one. And the served type
is `application/pdf`, which `ServedContentType.cs:31-33` deliberately keeps in a class that is not a
scripting context — the same reasoning that excludes `image/svg+xml` by name. So F1's harm is: *an
authenticated same-tenant cleaner, the order's customer, or an admin opens a PDF where a photo should be.*

That is a real defect. It is not "a shared host," and the difference matters because CH-3 shows the
choice between D1 and A2 rests on how much F1 is worth once F4 is conceded neutral.

**What I want.** Both sentences corrected. F1's harm stated at its actual reach.

---

## CH-7 — The "exhaustively" list is wrong on two of its five rows.

`:174-188` is headed *"**What would newly fail, exhaustively:**"*. Two rows are false:

> "**A payload shorter than 12 bytes** … newly refused. Today the first stores a 3-byte 'photo'."

`SniffedContentType.Matches` (`:152-163`) only requires `content.Length >= offset + bytes.Length` per
fragment. The JPEG signature is **3** bytes (`:69`) and the PNG signature is **8** (`:70`). So:

- base64 `"/9j/"` → `DecodeHead` (`:165-192`) takes 4 whole chars → 3 bytes `FF D8 FF` → matches
  `image/jpeg`, which is in `AcceptedByIntake[OrderPhoto]` → **accepted after D1, exactly as today.**
  `BlobFileSize.HasContentWithinLimit` (`:17-28`) passes it (3 ≤ 10 MB, non-blank).
- The draft's own shipped fixture is an 8-byte payload: `SaveOrderPhotosContentTypeTests.cs:96` sends
  `dataUriPrefix + "iVBORw0KGgo="`, which is the bare PNG signature and nothing else — and it is the
  fixture the rewritten test will be built from.

So a 3-byte "photo" survives D1 unchanged, and the row is not a newly-refused case.

The second row I could not falsify but is stated more strongly than the code supports: *"or one that is
not decodable base64: newly refused"* is true **only if** rule 2 (the decodability rule) ships;
`SniffedContentType.FromContent` reads at most 16 characters and asserts nothing about the remainder
(`:53, :165-192`). The draft knows this (F3) but the exhaustive list credits the refusal to the change
generally rather than to the one rule that produces it.

**Why it matters.** The mobile half of Test 2 is the claim the ADR stakes itself on, and **I re-verified
it independently and it holds** (see "found sound"). Precisely because that half holds, the two false
rows around it are corrosive — a lead who spot-checks the list and finds a wrong row has no way to know
the important row is right.

**What I want.** Drop or correct the size row; attribute the decodability row to rule 2; keep the mobile
rows, which are the ones that matter and which survive.

---

## CH-8 — D1 hand-rolls a fourth copy of the validator family's chain instead of joining the family, and drops the error code both existing members set.

**The hole.** D1 items 1–2 append `SniffedContentType` + decodability rules **inline** into
`SaveOrderPhotos.Validator`'s per-photo `ChildRules` block (`SaveOrderPhotos.cs:64-85`). The catalog says
there are exactly two members of this family:

> `patterns-backend.md:1231-1233` — "**Two** `AbstractValidator<BlobFileDto>` siblings now,
> `ImageFileValidator` and `DocumentFileValidator`: the third, `FileValidator`, is deleted."

`ImageFileValidator.cs:20-33` and `DocumentFileValidator.cs:20-36` are the *same* four-rule chain
differing only in the `UploadIntake` and the messages. D1 produces a third instance of that chain that is
not a member, so the next family-wide change — a new rule at the foot of the chain, a change to the head
size, the metadata scrub of T-0459 — has three shapes to find instead of one. The living doc already
flags the direction (`user-uploaded-artifacts.md:167-170`: *"whether the four `byte[]`/`IFormFile` routes
should become `BlobFileDto` so **one roster predicate and one validator family covers all fourteen**"*).
A `PhotoFileValidator : AbstractValidator<BlobFileDto>` used via `SetValidator` inside the `ChildRules`
would make the roster annotation read like every other row's, which is what D2's clause ("or the named
validator that calls it") assumes exists.

**The concrete bug this shape reintroduces.** Both siblings set `.WithErrorCode(nameof(BlobFileDto))` on
**every** rule (`ImageFileValidator.cs:25,28,31`; `DocumentFileValidator.cs:25,28,31,34`). D1's sketch
sets none. The error code is the **ProblemDetails dictionary key**:

```
CleansiaApiController.cs:93-99
    errors.Where(e => e.Code is not null).GroupBy(e => e.Code!)
          .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(e => e.Message)))
```

and every client reads the **first value** out of that bag (`http-error.interceptor.ts:14-20`, and the
comment at `CleansiaApiController.cs:48-51` names the iOS/Android readers). Un-coded `Must` failures
group under FluentValidation's default code together with any other un-coded failure in the same
response, and the value becomes `"file.type_not_allowed; file.size_exceeded"` — which resolves to
nothing and, on web, silently becomes `api.common.error_occurred`. This is the mechanism CH-M2 of the
ADR-0037 challenge documented; the fix is one `.WithErrorCode` per rule and it must be in D1, not
discovered by the implementer.

**What I want.** D1 states whether `SaveOrderPhotos` joins the `AbstractValidator<BlobFileDto>` family or
why it stays inline, and — either way — the sketch carries `.WithErrorCode(...)` on both new rules, with
a test asserting a single-entry `errors` bag for a payload that fails only the content rule.

---

## CH-9 — The two intake drafts should **not** merge, but as written they contradict each other, and this one leaves `OrderPhoto` with two carriers, one of them unread and unasserted.

**Should they be one ADR? No.** They answer different questions — *where does the stored type come from*
(this one) versus *where is it carried* (the sibling) — and folding them would produce one ADR with two
decisions, which the architect charter forbids. The sibling is right to say so (`:7-9`).

**But they disagree on the shared premise.** The sibling refuses a `DisputeEvidence.ContentType` column
on this ground:

> sibling `:74-81` — "A column would give this surface **two sources of truth for one fact** … a row whose
> name says `.png` and whose column says `application/pdf` becomes **representable**, and the next reader
> picks one."

This draft's D1 item 4 (`:245-247`) mints the blob-name extension from the sniff on `OrderPhoto` — which
**already has a column** — so after D1, `OrderPhoto` carries exactly the two-carrier state the sibling
refuses to create. And the sibling's own D2 round-trip test (`:93-97`) explicitly **exempts** intakes
"where the intake's read path does not use the name as its carrier," which `OrderPhoto` is
(`GetOrderPhotos.cs:96` reads the column). So the new minted extension is written by D1, read by nothing,
and asserted by nothing.

I am **not** arguing D1 item 4 is wrong — a server-minted name is strictly better than
`Path.GetExtension(file.FileName)` (`SaveOrderPhotos.cs:132`), and `UploadOrderPhoto.cs:103` already does
it. I am arguing the ADR must say which carrier is authoritative on `OrderPhoto` and that the other is
deliberately decorative, or the next reader resolves the ambiguity by guessing — which is the failure the
sibling's D1 is built to prevent.

**What I want.** One paragraph in this draft: on `OrderPhoto` the **column** is the carrier
(`GetOrderPhotos.cs:96`), the minted extension is a defence-in-depth artifact that no read path
consults, and the sibling's round-trip test names `OrderPhoto` in its exemption list with that reason.
Cross-referenced from the sibling so the two leads see the same interface.

---

## CH-10 — The pre-panel catalog edit does more than withdraw a blessing, and verification step 8 is vacuous today.

**(a) The edit already landed and it is normative.** `:393-396` justifies making the catalog edit outside
the panel: *"That edit ratifies nothing — it **withdraws** an implicit blessing and **imposes no
obligation on any call site**."* The text in the tree (`patterns-backend.md:1292-1313`) closes with:

> `:1310-1311` — "**The general form of the sentence below therefore cannot be written while this
> stands**, and that is a statement about the code, not about the rule."
> `:1312-1313` — "**Drafted, panel owed:** `backlog/adr/drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md`
> (D2 carries the exact sentence and its tier). **Do not copy this intake's shape into a new one.**"

"Do not copy this intake's shape into a new one" **is** an obligation on call sites — it is the D2 rule in
imperative form, minus its enforcer and its tier, which is precisely what `conventions.md:219-223`
requires a constraining entry to carry. And "the general form cannot be written while this stands"
pre-decides the question the panel convened to answer (A1 is *"keep the exception; write the rule with an
honest carve-out"* — a live option that this sentence declares impossible). It also routes a catalog
reader to an **unpanelled draft** by filename.

**What I want.** The two sentences at `:1311` and `:1313` are struck or restated as descriptive
(*"`SaveOrderPhotos` reads no byte of its payload; the general form of the sentence below is scoped to
exclude it; ruling drafted, panel owed"*) until the panel rules. The rest of the callout — which is pure
disclosure of an existing exclusion — earns its place and should stay; it is the best thing in this
lane.

**(b) Verification step 8 is a no-op today.** `:419-420` asks a reviewer to confirm *"`consistency.md` no
longer carries the `SaveOrderPhotos` deviation entry."* `consistency.md` contains **zero** occurrences of
`SaveOrderPhotos`, `ServedContentType` or `SniffedContentType` (searched). D2 creates that entry
(`:284-287`); until it exists, step 8 passes without anything having happened, which is the
"green for the wrong reason" shape `UploadIntakeRosterTests.cs:62-64` exists to refuse. State the
ordering: step 8 is checked **after** D2's entry lands, or it is not a check.

---

## What I checked and found sound

Silence is not assent, so — explicitly, this is what I opened and could not break.

- **The mobile empty-set claim.** Independently re-derived, not taken from the draft. iOS:
  `OrderPhotosViewModel.swift:52-78` is the **only** call site (`savePhotos`/`orderSavePhotos` has one
  hit per client under `src/cleansia_ios` and `src/cleansia_android`); it calls
  `ImageCompressor.encode` and **aborts on nil** (`:61-65`) — there is no raw-bytes fallback.
  `ImageCompressor.swift:75-86` encodes through `CGImageDestinationCreateWithData(…, UTType.jpeg, …)` and
  returns `contentType: "image/jpeg"`, `fileName: "photo.jpg"` (`:36-40`). Android:
  `OrderPhotosViewModel.kt:110-142` likewise aborts on null (`:115-122`);
  `ImageCompressor.kt:103,112` pin `OUTPUT_MIME = "image/jpeg"` / `OUTPUT_FILE_NAME = "photo.jpg"`,
  `:144-160` base64s with `Base64.NO_WRAP`. **Neither client can emit a non-JPEG, and neither has a
  fallback path.** The "it would break a live mobile path" objection is false, as the draft says.
- **The XSS ruling.** `ServedContentType.cs:34-42` has no `text/html` and no `image/svg+xml`; unknown
  input returns the `Opaque` singleton (`:64-66`), and the `!=` / `==` comparisons in
  `SaveOrderPhotos.cs:176,183` are reference comparisons against that singleton, which is correct because
  `ForRecordedType` returns `new ServedContentType(served)` only on a hit. `BlobContainerClient.cs:151`
  is `PublicAccessType.None`. `SaveOrderPhotosContentTypeTests.cs:32-47` pins it. **The draft is right
  that this closes no XSS hole, and right to say so three times.**
- **F3 is real.** `SaveOrderPhotos.cs:136` calls `Convert.FromBase64String(base64Data)` with nothing
  upstream decoding: the validator's only payload rules are presence (`:78`) and
  `BlobFileSize.HasContentWithinLimit` (`:80`), which derives size from the **encoded** length and never
  decodes (`BlobFileSize.cs:24-27`). Both siblings close with `HasDecodableContent`
  (`ImageFileValidator.cs:35-41`, `DocumentFileValidator.cs:43-49`) for exactly this reason. Whatever the
  panel does with the content-type question, **this one should not wait for it.**
- **The sniff and the store read the same bytes.** I looked for a validate/persist divergence and there
  is none: `FileExtensions.ExtractBase64Data` (`:17-26`) returns `Split(',')[1]` when there is a comma,
  and `SaveOrderPhotos.cs:126-128` does the same thing by hand. `,` is outside the base64 alphabet, so
  the two agree on every input. The command arrives `[FromBody]` (`Cleansia.Web.Partner/Controllers/OrderController.cs:152`),
  so `Photos` is a materialised collection and the multiple enumerations at `:61`, `:85` and `:122`
  cannot see different items.
- **`!` in D1 item 3 is safe.** The `ChildRules` block is gated `.When(x => x.Photos is not null &&
  x.Photos.Count() <= MaxPhotosPerRequest)` (`:85`), and both escape hatches are already refused by the
  `RuleFor(x => x.Photos)` chain at `:57-62`, so the handler cannot run with the child rules skipped.
  Same contract `UploadOrderPhoto.cs:102` relies on. No finding.
- **`(gate pending: <ticket>)` is sanctioned vocabulary**, not invented — `conventions.md:234`, and
  `:241-242` is explicit that a non-zero baseline *requires* it. D2's sequencing is right on this point,
  and `user-uploaded-artifacts.md:141-144` already uses the token four times.
- **The `Pending`-style "no writer" style of check.** `DetermineContentType` has one caller
  (`SaveOrderPhotos.cs:130`) and `OrderPhoto.Create`'s `contentType` argument has exactly two production
  writers (`SaveOrderPhotos.cs:148`, `UploadOrderPhoto.cs:119`). D1 reaches both halves of the surface it
  claims to.
- **A3's cost.** The 30-photo batch is real: `MaxPhotosPerRequest = 30` (`SaveOrderPhotos.cs:46`) with a
  documented reason, and the web picker stages a list. Rejecting A3 as out of scope is right.
- **A5's rejection.** Widening the accept set to follow the serve set really would be backwards, and
  `PHOTO_ALLOWED_TYPES` (`order-photos.helpers.ts:17-22`) offers neither GIF nor PDF. Sound.
- **The QuestPDF correction at `:213-224`.** The draft catching its own overclaim, in the draft, and
  narrowing the structural sentence to the `GetOrderPhotos` → SAS path, is the best-executed paragraph in
  the document. It is also the paragraph that makes CH-2 unavoidable: once you concede the read path
  *can* be changed, "the read path cannot solve this" needs to say **which** read-path change it means.

---

## Verdict requested of the lead

**Blocking — must be defended, conceded or escalated before `accepted`:**

| # | Finding | Why it blocks |
|---|---|---|
| **CH-1** | The clamp is asserted both insufficient (F1, `:83`) and sufficient (`:211`, `:252`) | The ADR's lead justification and its scope statement are mutually exclusive; one is wrong and which one changes the decision |
| **CH-2** | A4 rejects a proposal nobody made; the per-intake read clamp closes F1 for **all** rows, needs no bytes, and is already obliged by `patterns-backend.md:1364-1366` | The alternative that was never surfaced, and the cheaper one; and the ticket forbids the file it lives in |
| **CH-3** | A2's why-not has its sign backwards, and the sibling draft rules the opposite way on the identical outcome | This is the ADR's central trade-off; the author's own C-5 concedes F4 does not decide it |
| **CH-4** | D2's named enforcer does not assert the clause it is named for (`UploadIntakeRosterTests.cs:66-68`); and D2 adds a second, narrower `Enforced by:` to a section that has one | D2 is the deliverable, and ADR-0032 is the rule it breaks |
| **CH-5** | "No i18n work" is true of resolution, false of meaning; the only client that can hit the refusal is told "Accepted: PDF, JPEG, PNG, DOC, DOCX" (`en.json:1223` ×5) | It converts a booked-at-zero cost into either 15 rows or a knowingly wrong message, on the rule the ADR itself cites (`patterns-backend.md:1344-1346`) |

**Non-blocking — corrections to a document about to become immutable:** CH-6 (F5 names the wrong actor;
"shared by every tenant" overstates reach), CH-7 (two false rows in the "exhaustively" list), CH-8
(fourth copy of the validator chain; missing `.WithErrorCode`), CH-9 (name the authoritative carrier on
`OrderPhoto`; the two drafts should not merge but must agree), CH-10 (the pre-panel catalog edit is
normative; verification step 8 is vacuous today).

**The single strongest finding is CH-2.** CH-1 is its premise and CH-3 attacks the chosen option, but
CH-2 is the one that changes what gets built: there is a repair that costs less than D1, closes the
finding the ADR leads with on the rows D1 cannot reach, requires no client change and refuses no upload,
is already required by a shipped catalog sentence — and it appears in neither the ADR's alternatives
table nor the living doc's trade-off table nor the ticket, because A4's structural rejection reads as
having covered the whole read path when it covers one option within it.

**Not escalated by me, flagged for the lead:** whether a photo that fails the sniff should be **refused**
(D1) or **stored opaque** (A2) on a surface where the uploader is a cleaner mid-job and the artifact may
later be dispute evidence looks like a product call rather than an architecture one, once CH-3 removes
the evidence-preservation argument from D1's side. If the lead agrees, it goes to the owner rather than
being settled by sibling symmetry.

**Method limits, so the lead can weigh them.** No shell: nothing here was compiled or run, and two things
I would want measured before `accepted` I could not measure — (i) how many `OrderPhotos` rows on DEV
already carry `application/pdf` or `image/gif` (CH-1; owner query), and (ii) whether the rewritten
`SaveOrderPhotosContentTypeTests` actually reddens under each mutation the ADR's step 3 names (the
closing ticket's Gate 0.5, not this panel's).
