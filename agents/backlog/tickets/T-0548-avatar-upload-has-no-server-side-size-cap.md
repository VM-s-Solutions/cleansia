---
id: T-0548
title: Avatar upload has no server-side size cap — the client promises 10 MB and the server enforces nothing
status: in_review
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
source: owner-reported gap on the profile-avatar path; scoped and verified by backend, which refuted
  two of the reported premises and found two further uncapped intake paths during the sweep
---

## Context

`ImageFileValidator` had exactly **one** rule — a magic-byte match against `Constants.ImageSignatures` —
and **no size bound of any kind**. Its only caller is the profile avatar
(`Features/Users/UpdateCurrentUser.cs:63`), reachable on four hosts (`Web.Customer`, `Web.Partner`,
`Web.Mobile.Customer`, `Web.Mobile.Partner`), so every one of them accepted an avatar of any size the
transport would carry.

The sibling `FileValidator` — same folder, same `AbstractValidator<BlobFileDto>` shape, used by
`UpdateEmployee`'s documents — has had a 10 MB cap since it was written. The avatar path simply never
got one.

**The product already promises the bound the server did not keep.** The customer web app enforces
10 MB *client-side only* (`AVATAR_MAX_SIZE_BYTES` in
`libs/cleansia-customer-features/profile/src/lib/profile/profile.models.ts`) and prints it:
`pages.profile.avatar.hint` = *"Use a square image up to 10 MB"*. A client-side check is a UX
affordance, never a control — any caller that is not the web app was unbounded.

**Verified: nothing else bounds it.** There is no `MaxRequestBodySize`, `MultipartBodyLengthLimit`,
`RequestSizeLimit`, or `maxAllowedContentLength` anywhere in the solution (searched `*.cs`/`*.json`/
`*.config`/`*.xml` outside `bin`/`obj`; also no Kestrel `Limits` section in any `appsettings*.json`).
The effective ceiling was therefore Kestrel's ~28.6 MB default — roughly a **21 MB image** after
base64 inflation.

### The ordering is the substance of the fix, not a detail

`FileMatchesImageContentType` allocates `new byte[base64Data.Length * 3 / 4]` and then copies into a
second array. A size rule placed *after* it would return the right answer having already paid the
entire cost it exists to avoid. Measured against the unfixed validator, rejecting a single ~10 MB
payload allocated **20,979,352 bytes** — twice the payload — before any verdict was reached.

So the rule is placed **first** in the existing `Cascade(CascadeMode.Stop)` chain, and that placement
is pinned by two tests, not by a comment.

### Premises checked before writing anything

| Claim | Verdict |
|---|---|
| `ImageFileValidator` has one rule and no size cap | **confirmed** |
| Its only caller is `UpdateCurrentUser.cs:63`, guarded by `.When(…Base64Content)` | **confirmed** — solution-wide grep returns that one `SetValidator` site |
| `FileValidator` has a 10 MB cap the avatar path does not use | **confirmed** |
| Web customer enforces 10 MB client-side only and the copy promises it | **confirmed** |
| No request-size limit configured anywhere | **confirmed** |
| `BusinessErrorMessage.FileSizeExceeded` exists and `api.file.size_exceeded` is in customer `en.json` | **confirmed — and it is already in all five locales of all three web apps.** No i18n work was owed |
| `UploadEmployeeDocument` / `UploadNewDocumentVersion` "look capped" | **refuted, in a way that matters** — they are capped, but on a **client-declared `FileSizeBytes`** with a **client-supplied `FilePath`**; no bytes flow through them. Both are also **unreachable** — no controller, no dispatcher (see Sweep) |
| The other upload paths are capped | **mostly** — `SaveMyDocuments` has **no size cap at all** (see Sweep) |

## Acceptance criteria

- [x] **AC1 — an oversized avatar is refused by the server.** Given a valid PNG whose decoded size
      exceeds 10 MiB, When it is submitted to `UpdateCurrentUser`, Then validation fails with
      `BusinessErrorMessage.FileSizeExceeded`. **Evidence:** `Image_Over_TenMebibytes_Fails_With_FileSizeExceeded`,
      `Avatar_Over_TenMebibytes_Fails_FileSizeExceeded` (through the real command validator, so the
      `.When(…)` guard is proven not to disable the rule).
- [x] **AC2 — the limit equals the promise, in both directions.** Given an image just under 10 MiB,
      When submitted, Then it passes. A cap tighter than the UI copy recreates the same class of bug
      from the other side. **Evidence:** `Image_Under_TenMebibytes_Passes`, `Avatar_Under_TenMebibytes_Passes`
      — both killed by mutation M3 (limit → 5 MB).
- [x] **AC3 — the size rule runs BEFORE the payload is decoded.** Given a payload that fails **both**
      rules, When validated, Then the single reported error is the size one. **Evidence:**
      `Payload_That_Is_Neither_An_Image_Nor_Within_The_Limit_Reports_Size_First` plus
      `Oversized_Payload_Is_Refused_Without_Being_Decoded`, which measures
      `GC.GetAllocatedBytesForCurrentThread()` across the call and fails if more than 1 MB is
      allocated. Both are killed by M2 (swap the rules) and M7 (drop `Cascade.Stop`).
- [x] **AC4 — the two validators cannot disagree about what "10 MB" means.** The constant and the
      derivation live in **one** place, `Common/Validators/BlobFileSize.cs`; `FileValidator`'s private
      duplicate is deleted and it now calls the shared predicate.
- [x] **AC5 — all three wire forms are pinned.** The web customer client sends a full `data:` URI;
      both mobile clients send bare base64. Size is measured on the **extracted** data, so the number
      means the same thing on all three. **Evidence:** `DataUriPrefixed_Image_Is_Measured_On_The_Extracted_Data`
      is deliberately sized one byte under the limit so that the 22-char prefix pushes it over — it is
      killed by M5 (measure the raw string). A smaller fixture would pass either way and pin nothing.
- [x] **AC6 — `FileValidator`'s existing behavior is preserved.** Characterized first, then refactored.
      Blank content failing the **size** rule rather than the type rule is part of the pinned contract,
      not a wart to tidy in this ticket — it is the error code `UpdateEmployee` returns today for the
      empty-file placeholder its clients send.
- [x] **AC7 — the three suites run green.** See Status log for exact counts and exit codes.

## Decisions taken (and why), rather than left implicit

**The limit is 10 MiB.** It matches the client promise, `FileValidator`, `UploadOrderPhoto`,
`SaveOrderPhotos` and `UploadDisputeEvidence`. A photo-specific number was considered and rejected: a
tighter server cap than the UI advertises reproduces this exact defect with the sign flipped, and a
looser one makes the printed number a lie.

**The constant is shared, not duplicated.** Four features already carry their own
`private const long MaxFileSizeBytes = 10 * 1024 * 1024`, so a fifth copy would have been the
"consistent" move. It was rejected for these two specifically: they are siblings over the same DTO in
the same folder, and the whole point of the change is that they agree. The seam is deliberately small
— one internal static class holding the constant and the predicate — rather than a new home in
`Constants` or `FileExtensions`, so it does not become a general-purpose attractor.

**The error key is `file.size_exceeded`, not `file.size_exceeded_10mb`.** `FileValidator` is the
precedent, and `size_exceeded_10mb` bakes the number into the key name, so the key starts lying the
day the constant moves. Both are already translated in all five locales of all three web apps, so
neither choice owed i18n work.

**The size is derived from the encoded length, never by decoding.** `(base64Data.Length * 3) / 4` is
`FileValidator`'s existing convention and is kept verbatim. It rounds **up** by at most two bytes, so
it never under-reports — the safe direction for a limit. Being exact would have cost the boundary an
edge case worth ~2 bytes in 10 MB and gained nothing a caller can perceive; the reason it is an
estimate at all (decoding is the cost being prevented) is the one thing a reader cannot recover from
the code, so that is what the comment says.

**A request-size limit is NOT added here, deliberately.** A validator cap makes the API's *answer*
correct; it does not stop the body being buffered, so it is not a resource-exhaustion control and must
not be read as one. That control was considered and scoped out for reasons that are about the control,
not about effort:

1. It is a different class of control at a different layer, and the correct number is **not derivable
   from the avatar path**. `SaveOrderPhotos`, `SaveMyDocuments` and `UpdateEmployee.Documents` accept
   **arrays** of files with **no count cap on any of them** (verified). A legitimate multi-document
   save can therefore exceed any single-file bound, so a host-wide body limit silently caps those
   features too, and the number depends on per-request bounds that do not exist yet.
2. The right shape is host-level and config-driven in `CleansiaStartupBase` — one place, five hosts —
   **not** a per-endpoint `[RequestSizeLimit]`, because a per-endpoint attribute is exactly the thing
   the next endpoint forgets. That is the failure mode this ticket is about.
3. A change to shared startup across five hosts affecting every endpoint is an Architect seam, not a
   rider on a validator fix.

**This is filed as a follow-up below rather than left as an oversight.** Today's bound is Kestrel's
~28.6 MB default, which is *a* bound; it is simply not a chosen one.

**No `manual_steps`.** No schema change, so no `ef-migration`. The OpenAPI surface is byte-identical —
no DTO, no command, no response, no route changed; only a validation rule was added — so **no
`nswag-regen` either**.

## Mutation table (Gate 0.5)

Every mutation was applied to production code, the suite re-run, then the file restored from a
byte-identical copy and re-run (MD5-verified; no `git restore`/`checkout`/`reset` was used on anything).
Baseline for every row: **23 tests in the three affected classes, 0 failing.**

| # | Mutation | Failed | Tests killed |
|---|---|---|---|
| **M1** | `ImageFileValidator`: delete the size rule | 5 | `Image_Over_TenMebibytes_Fails_With_FileSizeExceeded`, `DataUriPrefixed_Image_Over_TenMebibytes_Fails_With_FileSizeExceeded`, `Oversized_Payload_Is_Refused_Without_Being_Decoded`, `Payload_That_Is_Neither_An_Image_Nor_Within_The_Limit_Reports_Size_First`, `Avatar_Over_TenMebibytes_Fails_FileSizeExceeded` |
| **M2** | `ImageFileValidator`: run the **signature rule first** | 2 | `Oversized_Payload_Is_Refused_Without_Being_Decoded`, `Payload_…_Reports_Size_First` |
| **M3** | `BlobFileSize`: limit 10 MB → 5 MB | 4 | `Image_Under_TenMebibytes_Passes`, `DataUriPrefixed_Image_Is_Measured_On_The_Extracted_Data`, `Avatar_Under_TenMebibytes_Passes`, `Document_Under_TenMebibytes_Passes` |
| **M4** | `BlobFileSize`: limit 10 MB → 20 MB | 7 | all four over-limit cases + `Document_Over_TenMebibytes_Fails_With_FileSizeExceeded`, `Oversized_Document_Reports_Size_Before_Type`, `Payload_…_Reports_Size_First` |
| **M5** | `BlobFileSize`: measure the raw string (drop `ExtractBase64Data`) | 1 | `DataUriPrefixed_Image_Is_Measured_On_The_Extracted_Data` |
| **M6** | `FileValidator`: delete the size rule | 3 | `Document_Over_TenMebibytes_Fails_With_FileSizeExceeded`, `Oversized_Document_Reports_Size_Before_Type`, `Blank_Content_Fails_With_FileSizeExceeded` |
| **M7** | `ImageFileValidator`: drop `Cascade(CascadeMode.Stop)` | 2 | `Oversized_Payload_Is_Refused_Without_Being_Decoded`, `Payload_…_Reports_Size_First` |
| **M8** | `ImageFileValidator`: delete the signature rule | 1 | `Within_Limit_Payload_That_Is_Not_An_Image_Fails_With_ContentTypeMismatch` |
| **M10** | `FileValidator`: delete the content-type rule | 1 | `Disallowed_ContentType_Fails_With_InvalidFileType` |

**All 14 added tests are killed by at least one mutation.** Two candidate tests were written and then
**deleted for failing exactly this bar**: `Small_Image_Passes` (a 64-byte PNG) and `Blank_Content_Is_Rejected`
were unkillable by any single mutation — the first because no tightening mutation reaches 64 bytes, the
second because it guards a *conjunction* (both rules reject blank content independently, so removing
either leaves it green). Both were proof of nothing and were removed rather than left to look like
coverage. The blank-content semantic is still pinned, once, where a single mutation does kill it:
`FileValidatorTests.Blank_Content_Fails_With_FileSizeExceeded` (M6).

**Red before green, not reconstructed after.** The tests were written against the unfixed validator and
run first: 4 failed, 21 passed. `Assert.False() Failure` on the oversized-image case is the literal
statement of the gap — the server said a 10 MiB avatar was fine.

## Sweep — other base64 intake paths

Enumerated by grepping every `BlobFileDto` / `Base64Content` / `byte[]` member reachable from a command
across `Core.AppServices` and the five hosts. **Nothing below was changed.**

| Path | Wire form | Size cap | Verdict |
|---|---|---|---|
| `UpdateCurrentUser` (avatar) | `BlobFileDto` | **was none** | fixed by this ticket |
| `UpdateEmployee.Documents` | `BlobFileDto[]` | 10 MB/file via `FileValidator` | capped per file; **no count cap** |
| `SaveOrderPhotos` | `BlobFileDto[]` | 10 MB/file | capped per file; **no count cap**; uses a **third** size convention, `(long)(data.Length * 0.75)`, and re-implements the `data:` split inline instead of `ExtractBase64Data` |
| `UploadOrderPhoto` | `byte[]` | 10 MB | capped |
| `UploadDisputeEvidence` | `byte[]` | 10 MB | capped |
| **`SaveMyDocuments`** | `BlobFileDto[]` | **NONE** | 🚩 **genuine gap — the same defect, a different feature** |
| `UploadEmployeeDocument` | metadata only | 10 MB on a client-declared number | not an intake path, and **unreachable** |
| `UploadNewDocumentVersion` | metadata only | 10 MB on a client-declared number | not an intake path, and **unreachable** |

### Follow-ups owed (reported, not fixed — for the PM to file)

1. **`SaveMyDocuments` has no size cap and no content check.**
   `Features/EmployeeDocuments/SaveMyDocuments.cs:74-77` validates `Base64Content` with `NotEmpty()`
   and then `.Must(content => !string.IsNullOrWhiteSpace(content))` — the same predicate twice, so it
   asserts nothing beyond `NotEmpty`. The handler base64-decodes straight to a blob upload
   (`:128`). It is reachable on **two** hosts (`Web.Partner` and `Web.Mobile.Partner`,
   `EmployeeController.SaveMyDocuments`, `[Permission(Policy.CanUploadEmployeeDocument)]`), takes an
   **unbounded array**, and infers content type from a file extension with a base64-prefix fallback
   rather than from magic bytes. The fix is the same one-line seam this ticket built
   (`BlobFileSize.HasContentWithinLimit`), but it is a different feature on a different surface and was
   left alone deliberately.
2. **The chosen request-body limit** — see *Decisions taken*. Architect-owned; depends on deciding
   per-request bounds (count caps) for the three array-shaped intake paths first.
3. **`UploadEmployeeDocument` / `UploadNewDocumentVersion` are dead code** — no controller, no
   dispatcher, no test references either command; the only `UploadEmployeeDocument` hits in the
   solution are the identically-named **policy** constant. Both accept a client-supplied `FilePath`
   that is persisted as the document's blob path, so if either is ever wired up that is the first
   thing to look at. Delete or wire, do not leave.
4. **`SaveOrderPhotos`' third size convention** could converge on `BlobFileSize`. Behaviour-equivalent
   in practice, and trivial — but it is an order-photos change and this was an avatar ticket.

## Out of scope

- Frontend/i18n: none was owed. `api.file.size_exceeded` is already present in **all five locales of
  all three web apps**. Observation only, not touched (another agent is live in customer profile): the
  customer copy reads *"The file is too large."* while the client-side copy names 10 MB — accurate but
  less specific. The key is shared with order photos and dispute evidence, all of which are also 10 MB.
- `FileValidator`'s blank-content-fails-the-size-rule wart: pinned, not changed. See AC6.
- Everything in *Follow-ups owed*.

## Implementation notes

**Files changed**
- `src/Cleansia.Core.AppServices/Common/Validators/BlobFileSize.cs` — **new.** The shared limit +
  predicate.
- `src/Cleansia.Core.AppServices/Common/Validators/ImageFileValidator.cs` — size rule added **first**
  in the existing `Cascade(CascadeMode.Stop)` chain.
- `src/Cleansia.Core.AppServices/Common/Validators/FileValidator.cs` — private `MaxFileSizeInMB` /
  `MaxFileSizeInBytes` / `HaveValidSize` deleted, now calls the shared predicate. Behaviour unchanged.
- `src/Cleansia.Tests/Common/Validators/ImageFileValidatorTests.cs` — **new**, 7 tests.
- `src/Cleansia.Tests/Common/Validators/FileValidatorTests.cs` — **new**, 5 characterization tests
  written **before** the refactor.
- `src/Cleansia.Tests/Features/Users/UpdateCurrentUserValidatorTests.cs` — 2 tests added to the
  existing characterization class, whose stated purpose is pinning every error code the validator emits.

**No fixture broke.** The only real base64 payload in the test tree is the 1×1 PNG in
`UpdateCurrentUserProfilePhotoTests`; `sql-scripts/` carries no base64 at all.

**Archetype:** `patterns-backend.md` (validators own all checks; `Cascade.Stop`; error codes are
`BusinessErrorMessage` constants) + `security-rules.md` (a client-side check is never a control).

**Catalog harvest — please sanity-check this as part of the review.** A new entry was folded into
`agents/knowledge/patterns-backend.md`, *"A rule that REJECTS cheaply runs before any rule that
MATERIALIZES the payload"*, immediately above the ADR-0017 tenancy/region section. It records the
ordering law, the two shapes of ordering proof, the encoded-length derivation, the extracted-data
requirement (with the ~22-byte fixture constraint that makes such a test discriminating), the
one-limit-one-place rule, and "a client cap is never a control". Per ADR-0032 it declares
**`T1-CI`** with a **closed roster stated in the entry** — it gates the two `AbstractValidator<BlobFileDto>`
siblings and explicitly does **not** claim the other intake paths, which are the sweep's follow-ups.
This is a clarification of existing validator practice, not a redefinition of "the one way to do X", so
it was taken as a developer-level harvest; if the reviewer reads it as broader than that, it is an
Architect call and should be bounced back.

## Status log

- 2026-08-05 — created by backend, filed at `in_review` because the work is complete and mutation-proven.
  T-number picked immediately before writing the file: `T-0546` is taken (customer Jest tsconfig) and
  `T-0547` is reserved by ADR-0042 for the wire-enum refactor, so this is `T-0548`, verified free
  repo-wide.

- 2026-08-05 — **red:** the 14 tests were written against the unfixed validator — `Failed: 4, Passed: 21,
  Total: 25`. The allocation test's failure message recorded the cost precisely: *"Rejecting an oversized
  upload allocated 20 979 352 bytes — the payload was decoded before the size was checked."*
  **green** after the fix: `Failed: 0, Passed: 25, Total: 25`.

- 2026-08-05 — **full suites, all executed (not "up-to-date"):**
  `dotnet build Cleansia.Api.sln` → **Build succeeded, 0 Error(s), 88 Warning(s)**, exit **0**.
  `dotnet test Cleansia.Tests` → **Passed: 3037, Failed: 0, Skipped: 0**, exit **0**
  (baseline before this ticket was **3023**; +14 is exactly the tests added).
  `dotnet test Cleansia.IntegrationTests` (Testcontainers Postgres, Docker up) → **Passed: 140,
  Failed: 0**, exit **0**.
  `dotnet test Cleansia.HostTests` → **Passed: 120, Failed: 0**, exit **0**.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
