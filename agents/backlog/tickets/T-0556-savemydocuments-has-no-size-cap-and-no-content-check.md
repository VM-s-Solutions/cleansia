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

## Review
<!-- reviewer + security write verdicts here; PM reconciles before advancing state -->
