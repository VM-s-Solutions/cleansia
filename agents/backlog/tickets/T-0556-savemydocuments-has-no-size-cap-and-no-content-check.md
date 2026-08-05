---
id: T-0556
title: SaveMyDocuments accepts an unbounded upload with no content check — the validator asserts the same predicate twice and nothing else
status: ready
size: S
owner: backend
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
source: found by the backend lane while fixing the avatar path in `97bb7265` (T-0548) and reported as a
  second, unfixed instance of the same defect class. Filed by the PM 2026-08-05
---

## Context

`src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs` validates each uploaded
document's payload with **two rules that assert the same predicate**:

```csharp
document.RuleFor(d => d.File.Base64Content)
    .NotEmpty().WithMessage(BusinessErrorMessage.Required)
    .Must(content => !string.IsNullOrWhiteSpace(content))   // ← the same assertion again
    .WithMessage(BusinessErrorMessage.Required);
```

(`SaveMyDocuments.cs:74-77`.) `NotEmpty()` on a string already fails null, empty and whitespace, so the
second rule adds nothing. **That is the entire content validation.** What is missing:

1. **No size cap.** The base64 array is unbounded. The handler decodes it and uploads to blob storage.
2. **No magic-byte check.** Nothing inspects the decoded bytes, so the declared type and the real type
   are never compared.
3. **Content type is inferred from the file *extension***, not from the bytes.
4. **No cap on the number of documents** in one request — `RuleForEach` iterates whatever arrives.

**This is the same defect class fixed on the avatar path in `97bb7265`** ("the server now enforces the
10 MB the UI promises — and rejects before it decodes", T-0548). The avatar fix did not reach here, and
this path is arguably worse: avatars are one image, this accepts a *list*.

**Reachable on two hosts** — both partner surfaces:
- `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs`
- `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs`

It is filed as its own ticket, not a footnote on T-0548, because the fix is not a copy: documents are a
different content set (PDFs and images, not images alone), arrive as a collection, and the
extension-derived content type is a stored-content decision with its own consequences on retrieval.

## Acceptance criteria

- [ ] **AC1 — a size cap exists and rejects before decoding.** Given a request whose `Base64Content`
      exceeds the configured per-document limit, When it is validated, Then it is rejected with a
      business error **before** `Convert.FromBase64String` runs and before any blob call. Evidence: a
      test asserting the rejection *and* that no decode/upload occurred.
- [ ] **AC2 — the duplicate predicate is gone.** Given `SaveMyDocuments.cs:74-77`, When the validator is
      rewritten, Then `NotEmpty()` and the redundant `Must(!IsNullOrWhiteSpace)` are not both present,
      and the ordered `Cascade.Stop` chain reads: present → within size → decodable → permitted content.
- [ ] **AC3 — the content type is decided by the bytes, not the extension.** Given a file whose
      extension disagrees with its magic bytes, When it is uploaded, Then it is rejected (or stored with
      the type derived from the bytes — the ticket states which, and why). **A `.pdf` extension on a
      payload that is not a PDF must not produce a stored object served as PDF.**
- [ ] **AC4 — the collection itself is bounded.** Given a request carrying an unreasonable number of
      documents, When it is validated, Then it is rejected by a named cap. An unbounded list of bounded
      items is still unbounded.
- [ ] **AC5 — both hosts are covered by the same rule.** Given the partner web and partner mobile
      controllers, When the fix lands, Then the constraint lives in the validator (one place) and a test
      proves it fires on **both** routes. A per-controller attribute would be the shape that let this
      gap exist.
- [ ] **AC6 — the limits are named constants, not literals** (`conventions.md` — no magic numbers), and
      the ticket records what the client already promises so the server is not stricter than the UI
      claims without saying so.
- [ ] **AC7 — the security gate runs.** `security_touching: true`: the S-laws pass over the upload path
      (ownership of the employee row, tenancy, what a decode failure leaks) is recorded in `## Review`
      by the `security` charter, not by the implementer.

## Out of scope

- **The host-level request-body ceiling** — Kestrel's ~28.6 MB default is the real outer bound on every
  intake path and is **T-0557**, an architect decision. This ticket makes *this endpoint's* answer
  correct; it does not stop the allocation, because the body is fully buffered before validation runs.
- **`UploadEmployeeDocument` / `UploadNewDocumentVersion`** — dead commands, **T-0558**. Do not "fix"
  them here; they must not be revived as-is.
- The avatar path (T-0548, shipped in `97bb7265`).

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs` — validator `:62-85`,
  handler (blob upload) below it.
- `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs` and
  `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs` — the two reachable routes
  (tests, not necessarily edits).
- `BusinessErrorMessage` + the five-locale `api.*` keys in each app that can reach the endpoint, for any
  **new** error key (`CLAUDE.md` §i18n — a key written under `errors.*` alone is read by nothing).

**Read `97bb7265` first.** The avatar fix is the reference shape for "reject before you decode", and
reusing its constants/ helper where they genuinely apply is preferable to a second idiom — but the
content set differs, so do not assume the image rules transfer.

⚠️ **A new `BusinessErrorMessage` key means five locales × every app that can reach the route**, and the
parity guards (`apps/<app>/src/app/i18n/error-contract-parity.spec.ts`) assert against
`BusinessErrorMessage.cs` directly.

### Staleness detectability (sprint-15 §D3)

This ticket names **product paths under `src/`**, so the candidate-3 path rule will flag it the moment
`SaveMyDocuments.cs` or either controller is committed after this ticket's `updated:` date.

**No-decision note:** the *existence* of a cap and a content check is not a new decision — the avatar
ruling already settled it for this codebase. AC3's "reject vs. re-type" choice is an implementation
call the reviewer gates; if it turns out to change stored-object semantics, it routes to the architect.

## Status log
- 2026-08-05 — created `ready` by pm, `security_touching: true`. Filed first in this batch on the
  reporting lane's assessment that it is the most serious of the four. The duplicate-predicate line is
  quoted above verbatim so the finding survives line drift.
- 2026-08-05 — implemented by backend. 3053 unit / 144 integration, exit 0 (baselines 3037 / 144; +16
  is exactly the additions). No new `BusinessErrorMessage` key, therefore no i18n work — see below.

## Implementation decisions (backend)

**AC3 — reject *and* re-type, and the declared content type is discarded entirely.** The stored
content type is now derived from the payload's first bytes
(`Common/Validators/DocumentContentType.FromContent`); a payload matching no permitted signature is
refused (`file.type_not_allowed`). The client's `BlobFileDto.ContentType` and the file extension are
both ignored, so a JPEG named `payslip.pdf` and declared `application/pdf` is stored — and served —
as `image/jpeg`. **A cross-check of declared-vs-sniffed was deliberately NOT added**: the declared
value is discarded, so cross-checking it would only add a rejection path for a benign mismatch the
Android picker produces routinely (`application/octet-stream` when a content provider declares no
type), at zero security gain. `FileValidator.HaveValidFileType` reads the *declared* string and is a
client-affordance filter, not a control; it is not cited here as content validation.

**What the signature check does not protect against**, stated plainly: a signature bounds the
*container*, not its contents. `PK\x03\x04` accepts any ZIP (a `.docx`-named `.xlsx` or `.jar`
passes and is labelled as `.docx`); `D0CF11E0` accepts any OLE2 compound file; a well-formed PDF with
embedded JavaScript or a `.doc` with macros passes. **There is no malware scanning on this path and
this change does not add any.** It removes the classes that have no business here — markup, scripts,
executables, arbitrary binary — and makes the stored type server-truth.

**AC1 order deviates from the AC's literal wording, on purpose.** The chain is present → size →
**signature → decodable**, not size → decodable → signature. The signature rule reads 9 bytes (12
base64 characters decode independently of the rest), while the decodability rule must materialize the
whole payload — so running the signature first applies the AC's own principle one step further down:
the payloads that were never documents are refused without being decoded at all. Correctness is
identical under either order (an undecodable payload fails both); only cost and the reported message
differ. Both orders are pinned: `Oversized_Payload_Is_Refused_Without_Being_Decoded` reddens if the
size rule moves after the decode (mutation M2).

**A decodability rule is genuinely needed and is not a second duplicate predicate.** Because the
signature rule reads the head only, a payload can begin with `%PDF-` and be undecodable further in —
which reaches the handler's `Convert.FromBase64String` as an unhandled `FormatException`, i.e. a 500
today. The two rules are independently killable (M3 / M4), which is the test that they are not the
same assertion twice.

**No new error key, therefore no i18n and no iOS lane contact.** All four messages already exist in
`BusinessErrorMessage` **and** are already translated in all five locales in the partner web app,
the Android partner `strings.xml`, and `CleansiaCore/Localizable.xcstrings`:
`file.size_exceeded`, `file.type_not_allowed` (whose shipped English is literally "Accepted: PDF,
JPEG, PNG, DOC, DOCX"), `file.invalid_file_type`, `file.count_exceeded` — the last had **zero**
usages before this ticket. That translated string is also the evidence for the accepted-format set.

**AC6 — what the clients already promise**, so the server is not stricter than the UI claims: web
`maxSizeInMB: 10` + accept list `.pdf .doc .docx .jpg .jpeg .png`
(`profile-documents.facade.ts`); iOS `DocumentPresentation.maxDocumentBytes = 10 * 1024 * 1024` with
an `[.pdf, .image]` picker; Android has no client-side cap and no MIME filter at all
(`ActivityResultContracts.GetContent()`), so for Android this endpoint is the only bound there has
ever been. The size limit reuses the shared `BlobFileSize` (no second copy); the collection cap is a
named `MaxDocumentsPerRequest = 10`.

**AC4 — the collection is now capped, and the item rules are gated on the cap.** Ten is above any real
batch (there are ten `DocumentType` values). The cap matters more than the per-item one: the ~28.6 MB
host body limit divided by a *small* document is tens of thousands of blob uploads and rows in a
single request, which the per-item size cap does nothing about.

### Findings this ticket did NOT fix (scope), for the PM

1. **`UpdateEmployee.UploadDocuments` is a fourth intake into the same container and the same
   `EmployeeDocument` table**, on the same two partner hosts, storing
   `document.ContentType ?? "application/octet-stream"` — a client string — which
   `DownloadEmployeeDocument` serves back. Its `FileValidator` does cap size (post-`97bb7265`) and does
   constrain the declared type to seven values, so it cannot store `text/html`; it can still store a
   *wrong* one, and its `Documents` list has no count cap either. Same class, smaller blast radius.
2. **`SaveOrderPhotos.Validator` still owns a private `MaxFileSizeBytes = 10 * 1024 * 1024`** and its
   own `* 0.75` derivation — the "one limit, one place" rule's remaining violation.
3. **Legacy rows keep whatever content type was stored before today**, and this fix does not retype
   them. Per `ServedContentType`'s own argument (a write-path validator fixes the rows written after
   it; a closed set on the read path fixes the rows already there), closing that residue means a closed
   set on `DownloadMyDocument`/`DownloadEmployeeDocument`. Verified by execution that it is not
   currently exploitable: `File(bytes, type, name)` emits
   `Content-Disposition: attachment; filename=…`, so a stored `text/html` downloads rather than
   renders — but the two-argument `File(bytes, type)` overload emits **no** disposition header at all,
   so that mitigation is one call-site edit from gone. Recommend a follow-up ticket on the read path.
4. **Mixed per-file failures in one request degrade to the generic client message.** `HandleResult`
   groups ProblemDetails errors by `Code` and joins the values with `"; "`, and every rule here uses
   `WithErrorCode(nameof(BlobFileDto))` (mirroring `ImageFileValidator`). Identical failures collapse
   via `Distinct()`, so N oversized files still read correctly; two files failing *differently* produce
   `"file.size_exceeded; file.type_not_allowed"`, which no catalog matches. Pre-existing shape, not
   introduced here; fixing it means per-rule error codes across all `BlobFileDto` validators.

### Catalog harvest

`agents/knowledge/patterns-backend.md`: the existing "A rule that REJECTS cheaply…" roster now names
the third sibling and records the two intake paths still uncovered; added the collection-cap corollary
and a new short section, "The declared content type is a HINT; the bytes are the evidence".

### Mutation table (Gate 0.5)

Every test was proven to fail under a mutation of the rule it guards, and every file restored
byte-exact (sha256 verified against a pre-mutation snapshot after each run). No test survived every
mutation, so none was deleted for decoration.

| # | Mutation | Tests that FAILED |
|---|---|---|
| M1 | `DocumentFileValidator`: delete the size rule | `Document_Over_TenMebibytes_Fails_With_FileSizeExceeded`, `Oversized_Payload_Is_Refused_Without_Being_Decoded`, `Document_Over_TenMebibytes_Fails_The_Whole_Command` |
| M2 | move the size rule *after* the decodability rule | `Oversized_Payload_Is_Refused_Without_Being_Decoded` (only — the others still reject, which is the point) |
| M3 | delete the signature rule | `Payload_That_Is_Not_A_Permitted_Document_Fails_With_FileTypeNotAllowed` |
| M4 | delete the decodability rule | `Document_Header_With_Undecodable_Content_Fails_With_InvalidFileType` |
| M5a | drop the `%PDF-` row from `Signatures` | `Every_Format…(".pdf")`, `Document_Header_With_Undecodable_Content…`, `DataUriPrefixed_Document_Is_Sniffed…`, `Valid_Documents_Pass` |
| M5b | drop the `PK\x03\x04` row | `Every_Format…(".docx")` |
| M5c | drop the `FF D8 FF` row | `Every_Format…(".jpg")`, `Stored_ContentType_Comes_From_The_Bytes…` |
| M5d | drop the PNG row | `Every_Format…(".png")` |
| M5e | drop the OLE2 row | `Every_Format…(".doc")` |
| M6 | sniff `Base64Content` instead of `ExtractBase64Data()` | `DataUriPrefixed_Document_Is_Sniffed_On_The_Extracted_Data` |
| M7 | delete the count cap | `More_Documents_Than_The_Cap_Fails_With_FileCountExceeded`, `Over_Long_List_Is_Refused_Without_Validating_Its_Items` |
| M8 | delete the `.When(count <= Max)` gate on `RuleForEach` | `Over_Long_List_Is_Refused_Without_Validating_Its_Items` |
| M9 | unwire `.SetValidator(new DocumentFileValidator())` | `Document_Over_TenMebibytes_Fails_The_Whole_Command` |
| M10 | handler: `doc.File.ContentType ?? …` wins again | `Stored_ContentType_Comes_From_The_Bytes_Not_The_Declared_Type_Or_The_Extension` |
| M11 | delete the mobile host's `SaveMyDocuments` action | `Every_Host_Action_Taking_A_Document_Upload_Dispatches_The_Validated_Command` |
| M12 | `MaxDocumentsPerRequest` 10 → 1 | `Valid_Documents_Pass`, `Document_Over_TenMebibytes_Fails_The_Whole_Command` |

**Honest note on process:** production was written before the tests on this ticket rather than after,
because the design question (what a document content check can even assert) had to be settled against
the three clients' wire forms first. The mutation table above is the per-rule red evidence, which is a
stronger claim than an initial red run — but it is not test-first, and it is recorded as a deviation
rather than presented as one.

## Review
<!-- reviewer + security write verdicts here; PM reconciles before advancing state -->
