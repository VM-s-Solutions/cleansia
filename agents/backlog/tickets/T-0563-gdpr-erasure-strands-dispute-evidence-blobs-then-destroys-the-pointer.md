---
id: T-0563
title: "GDPR erasure leaves every dispute-evidence file in storage and then overwrites the only pointer to it — delete the blob before `Anonymize()`, and state the container roster it was missing from"
status: in_review
size: S
owner: backend
created: 2026-08-06
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: ["0007-soft-delete-policy (D4 — the anonymize-not-remove boundary this reads)"]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
source: owner-verified defect, reported while checking an unrelated ruling. Chain independently
  re-verified against the code by the backend lane before any edit.
---

> ⚠️ **Ticket id.** Highest on disk immediately before filing was **T-0562** (re-checked at write time;
> no `T-0563`+ token appears anywhere under `agents/`). The PM confirms or reassigns.

## Context

`GdprDeletionService.AnonymizeUserDataAsync` deletes blobs from three containers — `user-files`
(`:130-141`), `employee-documents` (`:143-158`), `order-photos` (`:160-180`). It then loads the
subject's disputes and calls `dispute.Anonymize()` (`:210-212` before this change).

`Dispute.Anonymize()` (`Dispute.cs:152-165`) walks `_evidence` and calls `DisputeEvidence.Anonymize()`
(`DisputeEvidence.cs:37-42`), which overwrites **both** `FileName` and `FilePath` with
`AnonymizationMarker.Value` (`"[DELETED]"`). The collection is populated: `GetDisputesByUserIdAsync`
(`DisputeRepository.cs:10-17`) `Include`s `Messages` and `Evidence`, and its own XML doc says it exists
"to cascade-delete a user's dispute history".

So the file in `dispute-evidence` is never deleted, and the row that names it is wiped in the same unit
of work.

### What was verified, including where it makes the report LESS severe

- **`FilePath` is the only stored pointer.** `Dispute.AddEvidence` (`Dispute.cs:118-122`) has exactly
  one caller — `UploadDisputeEvidence.Handler` (`:111`) — which mints the name server-side as
  `{DisputeId}/{Guid:N}{ext}` (`:105`). No other table, no audit snapshot and **not the GDPR export**
  records it: `GdprExportService` has no dispute leg at all (grep for `Dispute`/`Evidence` in it
  returns nothing). `DisputeEvidence.Anonymize()` has exactly one caller (inside `Dispute.Anonymize()`),
  which itself had exactly one production caller (`GdprDeletionService.cs:212`).
- **But the file is not unrecoverable, and the report should not say it is.** The blob name is
  *prefixed by the dispute id*, the `Dispute` row survives erasure (anonymized, `Id` intact), and
  `IBlobContainerClient.GetFilesAsync(path)` exists. A file stranded here is therefore recoverable by
  enumerating the container under `{disputeId}/` — out of band, by an operator, but recoverable. That
  is why this ticket does **not** also rework `Anonymize()` to retain the path on a failed delete: the
  failure path is not the irreversible thing it looks like. Severity: a real Art. 17 erasure gap
  (customer-uploaded photographs of their home survive an erasure), not an unrecoverable one.
- **Retention vs erasure — established before writing code, and the answer is "missed".** ADR-0007 D4
  keeps *rows* with financial/referential obligation (orders, receipts, invoices, disputes) and
  anonymizes them; it says nothing about their blobs. Three independent facts say evidence blobs were
  meant to go, not to be kept: (a) `DisputeEvidence.Anonymize()` already destroys the path, which is
  only coherent as "the file is gone"; (b) the closest sibling — `order-photos`, also customer-uploaded
  imagery — *is* deleted twenty lines earlier; (c) there is no retention sweep, no legal-hold flag, no
  ADR and no doc anywhere that mentions retaining dispute evidence
  (`DataRetentionBackgroundService` touches `employee-documents` only). Nothing was deliberately
  retained here.

### The reason nobody had noticed: three container lists, no two the same

| Container | Declared in `Constants.BlobContainers` | In `storage.bicep` | In `infrastructure.md` table | Erasure verdict |
|---|---|---|---|---|
| `user-files` | yes | yes | yes | **erased** — profile photo, `:130-141` |
| `employee-documents` | yes | yes | yes | **erased** — `:143-158` |
| `order-photos` | yes | yes | yes | **erased** — `:160-180` |
| `dispute-evidence` | yes | yes | **missing** | **was missed — this ticket** |
| `generated-invoices` | yes | yes | yes | **retained** — the rendered financial record ADR-0007 D4 keeps; `HasBlockingInvoiceAsync` (`:99-107`) even refuses erasure while one is Pending/Approved/Disputed |
| `generated-receipts` | yes | yes | yes | **retained** — same, fiscal (ADR-0004) |
| `beta-whitelist` | yes | **no** | **missing** | **unused** — the constant is its own only reference in the whole solution |

`docs/architecture/infrastructure.md:77-83` lists five of seven; `storage.bicep:43-50` provisions six.
Neither states which an erasure must reach. That absent statement is the actual defect class, and it is
now a test rather than a table (AC3).

**Open for the owner, deliberately not decided here:** the retained receipt/invoice PDFs contain the
subject's name and address. Whether an erasure must redact or re-render them is a legal question, not a
backend one. The roster records only that nothing deletes them today, and why.

## Acceptance criteria

- [x] **AC1 — the subject's evidence blob is deleted, by its real path.** Given an erasable user with a
      dispute carrying one evidence row, When `DeleteUserAccountAsync` runs, Then
      `IBlobContainerClient.DeleteAsync` is called on the **`dispute-evidence`** container with the row's
      stored `FilePath`, and afterwards the persisted row's `FilePath` and `FileName` are
      `AnonymizationMarker.Value`. The assertion is on the **path**, not merely on "a delete happened" —
      that is what makes it fail if the two steps are transposed.
- [x] **AC2 — no other subject's evidence is touched.** Given a second user with their own dispute and
      evidence, When the first user is erased, Then no delete is issued for the second user's blob and
      their row keeps its real `FileName`/`FilePath`.
- [x] **AC3 — the container roster is stated and mechanically enforced.** Every `const` on
      `Constants.BlobContainers` carries a written verdict (`ErasedOnRequest` /
      `RetainedFinancialRecord` / `Unused`); a container marked `ErasedOnRequest` must be named in
      `GdprDeletionService.cs` and one marked otherwise must not be. Adding a container without a
      verdict, or dropping a container's deletion, reddens the build.
- [x] **AC4 — ordering is documented at the site, not inferred.** The delete and `Anonymize()` sit in
      one loop body with a comment naming `FilePath` as the sole pointer and what reversing them costs.
- [x] **AC5 — a failed blob delete is logged at Error with the name.** The existing sibling blocks log
      at Warning; this one is Error because the row's path is cleared on the next statement, making the
      log line the last in-band handle. The blob name is `{disputeId}/{guid}.ext` — server-minted, no PII
      — so S6 is satisfied (same precedent as `:155`, which logs `doc.FilePath`).
- [x] **AC6 — mutation table (Gate 0.5).** Below. Files restored byte-exact by checksum.

## Mutation table (Gate 0.5)

| # | Mutation | Expected | Observed |
|---|---|---|---|
| M1 | Remove the evidence-delete loop; keep everything else | `Erasure_Deletes_The_Subjects_…` red, bystander green | red 1 / passed 5 — exactly T1 |
| M2 | `GetDisputesByUserIdAsync(user.Id)` → `GetQueryable().Include(Evidence)` (all disputes) | bystander red, T1 green | red 1 / passed 5 — exactly T2 |
| M3 | Move `dispute.Anonymize()` above the delete loop | T1 red, naming the real path as *not found* | red 1 / passed 5 — `Not found: Tuple ("dispute-evidence", "dispute-erased/2f9c…")` |
| M4 | Add a container const to `Constants.BlobContainers` | roster equality red | red 1 / passed 3 |
| M5 | Strip the `DisputeEvidence` container from `GdprDeletionService` entirely | roster `ErasedOnRequest` leg red **and** T1 red | red 2 / passed 4 |

M1 and M2 kill **strictly disjoint** tests — this is why AC1 asserts `Assert.Contains` rather than an
exact delete-list equality; pinning over-deletion in AC1 too would have let one mutation kill both
directions and would have hidden a widening from AC2.

## Out of scope

- **Reworking `Anonymize()` to retain `FilePath` when the delete fails.** Considered and rejected on
  evidence: recovery-by-prefix exists (see Context), and both sibling containers already behave the
  same way — a failed `order-photos`/`employee-documents` delete strands a blob too. Changing the
  domain's anonymization contract for a recoverable failure path is a larger blast radius than the bug.
- **Whether an open dispute should block erasure.** The shipped position is already "erasure wins" —
  `Dispute.Anonymize()` destroys `Description` and every message unconditionally, and only *orders* and
  *invoices* appear in the blocking checks. Making evidence follow the same position is consistent.
  Changing the blocking set is an owner decision (GDPR Art. 17(3)(e)).
- **Redacting the retained receipt/invoice PDFs.** Owner/legal, recorded above.
- **`docs/architecture/infrastructure.md:77-83`.** Its container table is stale (five of seven, no
  erasure column). Left for the docs lane deliberately — the enforceable statement is AC3's test, and a
  doc edit was outside the scope fence given for this ticket.

## Implementation notes

**Files touched:**
- `src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs` — `:210-234`
- `src/Cleansia.Tests/Features/Gdpr/DisputeEvidenceErasureTests.cs` — new
- `src/Cleansia.Tests/Features/Gdpr/BlobContainerErasureRosterTests.cs` — new

**Read-only, unchanged:** `Dispute.cs`, `DisputeEvidence.cs`, `DisputeRepository.cs`,
`Constants.cs`. No entity, no schema, no DTO, no endpoint changed — hence **no `ef-migration` and no
`nswag-regen`**.

The unit tests run the real `GdprDeletionService` over real repositories on in-memory SQLite, mirroring
`UserNotificationRetentionAndGdprTests`; only the storage edge is a double, because the calls recorded
against it *are* the assertion. A pure-mock test would pass over the shape of this bug, which is a
navigation being populated by the repository's `Include`.

### Staleness detectability (sprint-15 §D3)

Names product paths under `src/`; the candidate-3 path rule covers it. Manual check at dispatch:
`grep -n "BlobContainers.DisputeEvidence" src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs`

## Verification

| Suite | Baseline | After | Exit |
|---|---|---|---|
| `Cleansia.Tests` | 3229 passed | **3235** passed (+6) | 0 |
| `Cleansia.IntegrationTests` | **147** passed (the brief's 144 is stale) | 147 passed | 0 |
| `Cleansia.HostTests` | 135 passed | 135 passed | 0 |

`dotnet build Cleansia.Api.sln` executed and compiled both before (39.5 s, 226 warnings, 0 errors) and
after (37.3 s, 0 errors) — not a no-op "up-to-date" pass; tests then ran `--no-build` against those
binaries.

## Status log
- 2026-08-06 — filed by the backend lane with the fix and its mutation proof. Chain re-verified from
  source before any edit; two of the owner's framings were narrowed rather than repeated (the file is
  recoverable by dispute-id prefix; the integration baseline is 147, not 144).

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->

**Catalog-edit routing:** no catalog edit proposed. Test 1 (code sweep): the only production caller of
`Dispute.Anonymize()` is `GdprDeletionService.cs:233`, and the only three other blob-deleting sites are
the sibling blocks in the same method — one call site each, no pattern to generalize. Test 2 (floor):
searched `docs/architecture/security-rules.md` for `blob`, `erasure`, `container`, and `consistency.md`
for `blob` — nothing governs "which containers an erasure must reach" at any level of generality, so a
new sentence would be a first statement, not a narrowing. It is not written as a catalog rule anyway:
the statement it would make is enforced instead by
`BlobContainerErasureRosterTests` (`Cleansia.Tests`, runs in `backend-ci.yml`'s unit step) — **T1-CI** —
which is a stronger form than prose and needs no catalog entry to bind.
