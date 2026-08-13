---
id: T-0561
title: "`SaveOrderPhotos` is the last intake that does not read its payload — sniff the bytes, refuse what it cannot identify, and mint the blob-name extension server-side"
status: draft
size: S
owner: backend
created: 2026-08-06
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: ["0044", "0043"]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
source: routed to the architect by the T-0556 follow-up lane, which declined to close it and named its
  reason. Ruled 2026-08-06. **`draft`, not `ready`: the ADR has not been panelled.**
---

> ⚠️ **Ticket id is PROPOSED.** Highest on disk at filing was T-0560. The PM confirms or reassigns the
> id; the ADR's `(gate pending: T-0561)` token must be updated in the same change if it moves.

> ⚠️ **`security_touching: false` on purpose, and it is a deliberate claim.** This closes **no** XSS
> hole. `text/html` and `image/svg+xml` are already unreachable through this path — `ServedContentType`
> excludes them by name (`:34-42`) and `SaveOrderPhotosContentTypeTests.cs:32-47` pins it. This is a
> correctness and consistency change. Do not let it inherit a severity it does not have.

## Context

Fourteen upload routes; thirteen derive the stored content type from the payload's bytes. This one does
not. `SaveOrderPhotos.DetermineContentType` (`src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs:171-184`)
reads the caller's `data:` URI prefix, else the caller's file-name extension, else returns **the string
literal `"image/jpeg"`**. That value is stored on `OrderPhoto.ContentType` (`:148`) and
`GetOrderPhotos.MapToDto` uses it for both the DTO field and the SAS `rsct` header (`:96,101,105`).

The clamp that was cited as making this safe bounds the answer to `ServedContentType`'s **six-value
serve set**, not to this intake's **three-value accept set** (`SniffedContentType.cs:91`). So a caller
can store and serve `application/pdf` or `image/gif` over arbitrary bytes here, and on no other
order-photo path — `UploadOrderPhoto`, same container, same table, same read path, refuses both.
`GenerateSasUri` sets no `ContentDisposition` (`BlobContainerClient.cs:93-110`), so a stored
`application/pdf` renders inline.

Two more defects on the same 14 lines:

- The blob name's extension is `Path.GetExtension(file.FileName)` (`:132`) — the caller's string — where
  `UploadOrderPhoto.cs:103` mints it from the sniff.
- **A live 500.** No rule on this chain touches the payload, and the handler calls
  `Convert.FromBase64String(base64Data)` at `:136`. A payload that is valid base64 for its first
  characters and garbage after reaches an unhandled `FormatException`. Both hardened base64 chains close
  with a decodability rule for exactly this reason.

**~~It blocks T-0459.~~ WITHDRAWN 2026-08-06 — this whole paragraph is now false in fact, not
only in law.** ADR-0043 ruled the scrub dispatches **from the bytes it is holding**, never from a
client string or a persisted `ContentType`, and that scrub has since landed on all three handlers
(`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`, `UploadDisputeEvidence.cs:108`). The
selected-no-op attack the paragraph below describes is closed by the dispatch rule, not by this
ticket. **A NEW invariant now rests on this ticket and nothing pins it:** the recorded type describes
the SUBMITTED bytes while the blob holds the SCRUBBED ones. That holds today only because the rewrite
dispatches to a walker whose own signature matched and returns the input unchanged otherwise -- see
AC-NEW below. Retained for provenance:

> **It blocks T-0459.** The challenge on the content-policy draft
(`agents/archive/2026-08/adr-deliberation/challenges/NNNN-user-artifact-content-policy-threat-model.md` CH-4, `c6370115`)
found the same defect from the other side: that draft elects **this** surface as its metadata-scrub
pilot, and a per-format scrub dispatching on the client's `data:` prefix runs the **PNG chunk walker
over JPEG bytes** whenever the uploader says `data:image/png` — a no-op the attacker selects, under a
green "scrub applied" test. `SaveOrderPhotosContentTypeTests.cs:49-59` proves the premise today. **Do
not ship a scrub, a thumbnailer or a PDF embed that dispatches on `OrderPhoto.ContentType` before this
ticket lands.**

**What breaks: nothing that works.** Both mobile clients re-encode every pick to JPEG and cannot emit
anything else — iOS `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift:77` (`UTType.jpeg`),
Android `core/src/main/java/cz/cleansia/core/media/ImageCompressor.kt:248`
(`Bitmap.CompressFormat.JPEG`), both `photo.jpg`, both bare base64. Partner web sends the raw picked
file filtered on `File.type` (`order-photos.helpers.ts:29-45`), so the only newly-refused case is a file
whose browser-derived type disagrees with its bytes — which today stores a lie and never renders.

## Acceptance criteria

- [ ] **AC1 — the stored type comes from the bytes.** Given a `data:application/pdf;base64,` payload
      whose bytes are a PNG, When the command is handled, Then the row handed to
      `IOrderPhotoRepository.Add` records **`image/png`**. And given a `data:image/png` payload whose
      bytes are a JPEG, Then it records **`image/jpeg`**. `DetermineContentType` is deleted; the handler
      calls `SniffedContentType.FromContent(file.Base64Content, UploadIntake.OrderPhoto)!`.
- [ ] **AC2 — the refusal is on the VALIDATOR, in the right position.** Given a payload that is not
      JPEG/PNG/WebP, When `SaveOrderPhotos.Validator` runs, Then it fails with
      `BusinessErrorMessage.FileTypeNotAllowed` — asserted on the validator, not on the handler. The new
      rule sits **after** `BlobFileSize.HasContentWithinLimit` (`:80`), because the size rule must stay
      ahead of anything that touches the payload. Prove the order the way the catalog requires: a
      payload failing **both** reports only the size failure, and swapping the rules reddens it.
- [ ] **AC3 — the chain closes with a decodability rule.** Given a payload whose first 16 base64
      characters decode to a JPEG signature and whose remainder is not decodable, When the command is
      validated, Then it is refused with `BusinessErrorMessage.InvalidFileType` and **never reaches**
      `Convert.FromBase64String`. Mutation: delete the rule → the test goes red with an unhandled
      `FormatException`, not a 400.
- [ ] **AC4 — the blob name is server-minted.** Given any accepted payload, When it is uploaded, Then
      the name passed to `IBlobContainerClient.UploadAsync` ends in
      `SniffedContentType.ExtensionFor(<sniffed type>)`, regardless of `File.FileName`.
      `OriginalFileName` (`:146`) still carries the caller's string — it is a display value.
- [ ] **AC5 — the roster stops blessing the exception.** `UploadIntakeRosterTests.ExpectedIntakes`
      reads `… OrderController.SavePhotos — SniffedContentType(OrderPhoto)` on **both** hosts, no row's
      annotation ends in `only`, and the class doc's blessing paragraph (`:34-38`) is removed. The count
      assertion (`:64`) still runs **before** the set comparison.
- [ ] **AC6 — the empty-set claim is proven, not asserted.** The ticket records, with `file:line`, that
      both mobile compressors emit JPEG, and carries a fixture whose bytes are a real JPEG head passing
      the new chain. **If any shipped client is found that sends a non-JPEG through this endpoint, STOP
      and re-open the ADR** — the ruling depends on this being empty and the alternative (store
      `application/octet-stream` instead of refusing) is written up in the ADR as A2.
- [ ] **AC7 — no new error key, verified rather than assumed.** `BusinessErrorMessage` gains nothing;
      `FileTypeNotAllowed` and `InvalidFileType` already resolve under `api.*` in all five partner
      locales. Confirm by running the three `error-contract-parity.spec.ts` suites, not by reading.
      A new key makes this a three-app, fifteen-row change and is out of scope.
- [ ] **AC8 — the catalog rule lands with the code.** `patterns-backend.md`'s dated exclusion callout
      is replaced by the ADR's D2 sentence; its tier moves from `(gate pending: T-0561)` to **`T1-CI`**;
      the `consistency.md` deviation entry naming `SaveOrderPhotos` is deleted in the same change.
- [ ] **AC9 — mutation table (Gate 0.5).** Each of AC1–AC5 goes red under a distinct named mutation
      (drop the handler's sniff; restore `Path.GetExtension`; delete the sniff rule; delete the
      decodability rule; swap size/sniff order), files restored byte-exact.

### Panel corrections — 2026-08-06, lead ruling on draft rev 2 (these override the ACs above)

The ACs above predate the panel. Where they disagree with this block, **this block wins**.

- **AC2 / AC3 / AC7 — the refusal message.** Do **not** mint a new key. Reuse
  `BusinessErrorMessage.FileNotMatchContentType` (`file.content_type_doesnt_match`), which
  `ImageFileValidator` already uses and which is verified present in all five partner-web locales, all
  five Android partner locales and iOS. **Zero new keys, zero new locale rows**, and the document
  promise string stays untouched.
- **AC5 — the roster annotation is not an enforcer.** `UploadIntakeRosterTests.cs:66-68` asserts
  `entry.Split(" — ")[0]` and nothing reads index 1, so the text after the em-dash is checked by
  nothing. Add a per-intake refusal `[Theory]` instead — and note the lead's finding that the obvious
  form **passes vacuously**: `Assert.False(result.IsValid)` is green on any un-stubbed constructor
  dependency, because the command is invalid for the wrong reason. Two corrections make it real:
  (a) assert the failure's **identity** (the file property, that route's error code, that intake's
  content key), and (b) a **positive control** per case — the same command with an accepted payload
  validates clean, which is the only thing proving the other rules were stubbed to pass.
- **AC8 — two deviation entries, not one.**
- **New AC — pin the scrub/sniff invariant.** ADR-0043's metadata scrub landed on this handler while
  the panel was sitting (`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`,
  `UploadDisputeEvidence.cs:108`). The recorded type now describes the **submitted** bytes while the
  blob holds the **scrubbed** ones. Those agree today only because the rewrite dispatches to a walker
  whose own signature matched and returns the input unchanged otherwise — a future walker change breaks
  the ticket's central claim **silently**. Assert
  `FromContent(Scrub(p).Bytes, intake) == FromContent(p, intake)` over every `(intake, accepted type)`
  pair. `T1-CI`, zero baseline.
- **Verification step 6 is wrong on the batch route.** `RuleForEach` over 30 photos with one shared
  error code groups every per-photo failure under a single key joined with `"; "` — by design. Scope
  that step to a **one-photo** command.
- **Line numbers in §Implementation notes are stale by +1 (validator region) / +3 (handler region)**
  since the scrub landed. Re-derive rather than trusting them.


## Out of scope

- ~~**The read-path clamp.**~~ **WITHDRAWN 2026-08-06 by the ADR panel (draft rev 2, new D4).** The
  clamp is no longer out of scope and `GetOrderPhotos.MapToDto` is no longer read-only: it resolves
  through the SIX-value `ServedContentType` table while this intake accepts THREE, and the catalog
  already obliges the narrowing — *"the read path reads the intake's own signature table"*. D4 is the
  only half of the pair that reaches rows already written; D1 is the only half that makes the stored
  column true. They compose rather than substitute: after D1 the clamp is the identity on every row D1
  writes, so its whole effect is on pre-D1 rows — exactly D1's blind spot.
- **Retyping existing rows.** A write-path rule retypes nothing already stored, and this surface has no
  read-path remedy — the server never sees an order photo's bytes after intake.
- **Metadata / EXIF.** Different decision, `ADR-0043`.
- **Deleting `UploadOrderPhoto` / merging the two endpoints** — ADR alternative A3, still open.
- **`GetOrderPhotos`' browse gate.** It uses `CanBrowseOrderAsync`, not `CanAccessOrderAsync`
  (`GetOrderPhotos.cs:59`, `OrderAccessService.cs:68-92`), so any tenant cleaner who can see the order
  while a seat remains open can fetch its photos. **Verified, and deliberately not touched here** — it is
  an authorization ruling on ADR-0036/0037 territory and wants its own panel. It raises what this ticket
  is worth (the audience for a planted `application/pdf` is not enumerable); it changes nothing about
  what this ticket does.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs` — validator `:64-86`, handler
  `:122-159`, delete `:164-184`
- `src/Cleansia.Tests/Features/Orders/SaveOrderPhotosContentTypeTests.cs` — rewritten
- `src/Cleansia.Tests/Common/Validators/UploadIntakeRosterTests.cs` — `:34-38`, `:47`, `:52`
- `agents/knowledge/patterns-backend.md`, `agents/knowledge/consistency.md` (AC8)
- `src/Cleansia.Core.AppServices/Features/Orders/GetOrderPhotos.cs` — the D4 clamp (was listed here as
  read-only; the panel withdrew that)
- `src/Cleansia.Core.AppServices/Common/SniffedContentType.cs` — `ServedFor` goes HERE (the lead
  corrected this: it was listed read-only, and D4 cannot be built without it). **Resolve
  `ForRecordedType` FIRST, then membership-test the RESULT's value** — the natural order is the wrong
  one and demotes real photos, because `ServableTypes` maps `image/jpg` to `image/jpeg` and `image/jpg`
  rows exist (`UploadOrderPhoto.cs:39` allows the alias and stored the client string until the T-0556
  follow-up)
- **Read-only, must not change:** `ServedContentType.cs`

**Do not widen `AcceptedByIntake[UploadIntake.OrderPhoto]`.** `image/gif` and `application/pdf` becoming
unreachable on this intake is the intended outcome, not a regression: no client offers either
(`PHOTO_ALLOWED_TYPES`), both compressors emit JPEG, and `UploadOrderPhoto` already refuses both.

### Staleness detectability (sprint-15 §D3)

Names product paths under `src/`; the candidate-3 path rule covers it. Manual check at dispatch:
`grep -n "DetermineContentType" src/Cleansia.Core.AppServices/Features/Orders/SaveOrderPhotos.cs`.

**Decision note:** this ticket implements a **drafted, unpanelled** ADR. It stays `draft` until the
panel rules. If the panel adopts alternative A2 (store `Opaque` rather than refuse), AC2 and AC6 change
and AC1/AC3/AC4/AC5 do not.

## Status log
- 2026-08-06 — created `draft` by the architect (author mode) alongside the ADR. Not `ready`: no
  challenger and no lead have run.
- 2026-08-06 — `blocks: T-0459` added after the content-policy challenge (`c6370115`) landed. Its CH-4
  is an independent argument for the same change and its requested repair (b) is this ticket. The
  browse-gate finding (CH-2c) was re-verified at `OrderAccessService.cs:68-92` and recorded in
  §Out of scope rather than absorbed.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->
