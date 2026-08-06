# ADR-NNNN (DRAFT — number NOT allocated) — `DisputeEvidence` keeps the server-minted blob name as its content-type carrier; the column is refused and the round-trip is pinned instead

- **Status:** `proposed`
- **Date:** 2026-08-06 (drafted)
- **Mode:** **author**. A challenger and a lead are owed (`process/deliberation.md`).
- **Number:** not allocated. Highest on disk is **0042**. The PM allocates.
- **Related:** `NNNN-stored-content-type-is-byte-derived-on-every-intake.md` (the sibling ruling from the
  same routing; **this is a different decision and is deliberately not folded into it**), T-0556
  follow-up (which minted the extension from the bytes and left this open)
- **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md` §7.2
- **Tickets it creates:** the round-trip pin (`T-0562` proposed; PM confirms the id at filing)
- **Owner-run step it explicitly does NOT create:** no EF migration.

> ### ⚠️ Method declaration
> No panel has run; `## Challenge` is author-run. No shell in this invocation — nothing compiled,
> executed or measured; every fact is read from source at HEAD and cited at `file:line`.

---

## Context

`DisputeEvidence` (`src/Cleansia.Core.Domain/Disputes/DisputeEvidence.cs:6-43`) carries
`FileName`, `FilePath`, `UploadedBy`, `UploadedOn` — and **no content-type column.** Its byte-derived
answer therefore reaches readers through exactly one channel: the extension of the blob name the server
mints.

**Write** (`UploadDisputeEvidence.cs:104-105`):
```csharp
var contentType = SniffedContentType.FromContent(command.FileData, UploadIntake.DisputeEvidence)!;
var blobName = $"{command.DisputeId}/{Guid.NewGuid():N}{SniffedContentType.ExtensionFor(contentType)}";
```
**Read** (`DisputeMappers.cs:73-78`): `ServedContentType.ForFileName(evidence.FilePath)` — the stored
**path**, not the display `FileName`.

So the question routed here: is the extension a sufficient carrier, or is that a gap worth an owner-run
migration to add a real column?

### What actually holds this together, and what does not

The carrier works **only** while two tables in two different assemblies agree, in both directions, for
every type in this intake's accepted set:

| Accepted (`SniffedContentType.cs:92-95`) | `ExtensionFor` (`:66-78`) | `ServedContentType.ForFileName` (`:44-52`) | Round-trips? |
|---|---|---|---|
| `image/jpeg` | `.jpg` | `image/jpeg` | ✔ |
| `image/png` | `.png` | `image/png` | ✔ |
| `image/webp` | `.webp` | `image/webp` | ✔ |
| `application/pdf` | `.pdf` | `application/pdf` | ✔ |

**It round-trips today, for all four. Nothing asserts that.** `SniffedContentType` lives in
`Cleansia.Core.AppServices`; `ServedContentType` lives in `Cleansia.Core.Blobs.Abstractions`, which
cannot reference it — so the agreement is by hand, across an assembly boundary, with no compiler and no
test holding it. The one test that exercises the round trip
(`UploadDisputeEvidenceContentTypeTests.The_Read_Path_Resolves_The_Same_Type_From_The_Stored_Path`,
`:73-94`) covers **PNG only**, one of four.

**And the trap is one set-membership edit away.** `Signatures` already contains
`application/msword → .doc` and the OOXML type `→ .docx` (`SniffedContentType.cs:73-77`), and
`ServedContentType.ServableExtensions` knows **neither**. They are out of reach today only because
`AcceptedByIntake[DisputeEvidence]` happens not to include them. Adding `application/pdf`'s neighbours
to that set — a one-line, entirely reasonable-looking product change — would silently start serving
every such evidence file as `application/octet-stream`.

---

## Decision

### D1 — No content-type column. The blob name stays the carrier.

**Refused: adding `DisputeEvidence.ContentType` + an owner-run migration.** Not on cost — the schema is
pre-prod and folds back into the single `Initial` migration, so this is about as cheap as it will ever
be, and "cheap now, expensive after launch" is a genuine argument *for* doing it. It is refused on a
correctness argument that outlives the cost one:

**A column would give this surface two sources of truth for one fact, and the sibling that has one is
the sibling that had the bug.** `OrderPhoto` has a content-type column, and the follow-up ticket had to
fix `GetOrderPhotos` for emitting the raw column value beside a SAS header derived from it —
*"one fact with two sources, and the client believes the one that is wrong"*
(`patterns-backend.md:1344-1347`). A `DisputeEvidence.ContentType` column re-creates exactly that
opportunity: a row whose name says `.png` and whose column says `application/pdf` becomes
representable, and the next reader picks one.

The blob name is not a workaround for a missing column. It is **content-addressing**: the name is
minted from the bytes, in the same statement that reads them, and it cannot drift from them without the
blob itself being renamed. That is why `UploadOrderPhoto.cs:103` mints its extension from the sniff
**even though it has a column** — the name is the stronger carrier on both surfaces, and on this one it
is the only one, which means it is also the only one that can be wrong.

### D2 — The gap is not the column. It is that the round-trip is unpinned. Pin it.

One test, in `Cleansia.Tests`, over the composition rather than over a hand-listed pair:

> For **every** `UploadIntake`, and for **every** content type in that intake's accepted set:
> `ServedContentType.ForFileName(SniffedContentType.ExtensionFor(t)).Value == t`
> — **except** where the intake's read path does not use the name as its carrier, in which case the
> exemption is named in the test, per intake, with the carrier it uses instead.

Two properties make this the right shape rather than four `[InlineData]` rows:

- It is driven off `AcceptedByIntake`, so **widening an accepted set reddens it** — which is the failure
  mode above, caught at the edit that causes it rather than by a user report.
- It asserts a **non-vacuity floor** (`ADR-0032`: a guard that passes because its corpus is empty is not
  an enforcer): the enumerated pair count is asserted before any per-pair comparison, the same
  count-first discipline `UploadIntakeRosterTests.cs:62-64` already uses.

`EmployeeDocument`'s `.doc`/`.docx` are the intake that takes the named exemption: it carries a real
column and its read path is `SniffedContentType.ForDownload` over bytes the server holds, so its
extensions never pass through `ServedContentType`.

**Enforced by:** the new test class (`T1-CI`, `Cleansia.Tests`, the *"Unit tests (Cleansia.Tests)"* step
of `backend-ci.yml:69-74`). Baseline is **zero** — all four pairs round-trip today (table above) — so
`T1-CI` is the correct token on day one and no `(gate pending: …)` is needed.

### D3 — The failure mode if D1 is wrong, named so it is recognisable

**If the extension is not sufficient, this is how it shows:** an evidence file serves as
`application/octet-stream`, so the customer's photo or PDF **downloads instead of previewing** in the
dispute thread, for both the customer and the adjudicating staff member. It is a **silent capability
loss on a support-critical path**, not a security failure — the demotion direction is safe by
construction (`ServedContentType` falls back to `Opaque`, never up). Nobody would see a stack trace;
someone would eventually file "evidence previews are broken." D2's test is what converts that into a red
build at the commit that causes it, and it is the cheap half of what the column would have bought.

The uncoverable residue, stated: a change that rewrites `FilePath` after the fact loses the type
entirely, because the name is the only carrier. **That is not hypothetical** — `Anonymize()` does it
(`DisputeEvidence.cs:37-42`, `FilePath = AnonymizationMarker.Value`). See §Out of scope: that path has a
larger problem than its content type.

---

## Out of scope — but found while verifying, and it must not be lost

**GDPR erasure orphans every dispute-evidence blob and destroys the only pointer to it.**
`GdprDeletionService.AnonymizeUserDataAsync` deletes blobs for `user-files` (`:134-135`),
`employee-documents` (`:146-157`) and `order-photos` (`:164-180`), and then calls
`dispute.Anonymize()` (`:210-212`) — which walks into `evidence.Anonymize()`
(`Dispute.cs:160-163`) and overwrites `FilePath` with the anonymization marker
(`DisputeEvidence.cs:37-42`). **The `dispute-evidence` container is never touched.** So the customer's
uploaded evidence — which on this surface is photographs of their own home — survives an erasure
request in blob storage, and after the marker is written nothing in the database can name the blob to
delete it later.

This is a **data-protection defect, not a content-type one**, it is strictly larger than the question
routed here, and it is emphatically not mine to fix in an ADR about carriers. It is recorded here
because I found it while verifying D3's residue and because the ordering matters: **any future deletion
sweep must run before `Anonymize()`, or it has nothing to work from.** Owner/PM: file it against
`GdprDeletionService`, `security_touching: true`.

---

## Alternatives considered

**A1 — Add the column (the migration).** Rejected: D1. **What it gets right, conceded:** it makes
`DisputeEvidence` symmetric with `OrderPhoto` and `EmployeeDocument`, and symmetry across an aggregate
family has real value that D1 spends. It also survives a `FilePath` rewrite, which the name does not.
If the panel weighs the `Anonymize()` interaction above more heavily than I do, A1 becomes defensible —
though it does not fix the orphaned blob either.

**A2 — Store the type in blob metadata (`x-ms-meta-*`) instead.** Rejected outright: T-0464 established
that `UploadAsync`'s `Metadata` routes to `SetMetadataAsync` and the storage service **never serves from
it** (`patterns-backend.md:561-568`). Five constants already lied about this once; re-introducing the
same sink as a read source would be the identical defect with a new name.

**A3 — Re-derive from the bytes on read, as employee documents do.** Rejected on the same structural
fact as the sibling ADR: `DisputeMappers.MapToDto` mints a SAS and never holds the bytes, so
`SniffedContentType.ForDownload` has nothing to read. It would require the server to download every
evidence file on every dispute-detail render.

**A4 — Do nothing; the round trip works.** Rejected: it works **by coincidence of two hand-maintained
tables across an assembly boundary**, with one of four pairs tested. "It works today" is the state a
guard exists to keep true, and D2 costs one test.

---

## Consequences

- No schema change, no owner-run migration, no backfill. Existing rows are unaffected — including
  pre-follow-up rows whose extension came from the caller's file name, which resolve through the same
  closed set and demote to `Opaque` when unrecognised.
- `DisputeEvidence` remains the only upload surface with **exactly one** source of truth for its served
  type, and that source is server-minted. That is a property to defend, not a gap to fill.
- Widening any intake's accepted set becomes a change that must consider the read path, because the
  build says so.
- The GDPR finding above is now written down somewhere a person will find it.

## How a reviewer verifies compliance

1. `grep -n "ContentType" src/Cleansia.Core.Domain/Disputes/DisputeEvidence.cs` returns nothing, and
   `Migrations/` is untouched.
2. The new test enumerates **every** `(intake, accepted type)` pair, asserts the pair **count** before
   any comparison, and names each exemption with the carrier that intake uses instead.
3. Mutate `SniffedContentType.ExtensionFor`'s `image/webp` row to `.wbp` → the test goes red. Mutate
   `AcceptedByIntake[DisputeEvidence]` to include `application/msword` → the test goes red. Both
   mutations are recorded in the ticket's mutation table; a test that survives either is not the gate.
4. `DisputeMappers.MapToDto` still resolves `evidence.FilePath`, not `evidence.FileName`
   (`:75`) — restoring `FileName` must redden
   `The_Read_Path_Resolves_The_Same_Type_From_The_Stored_Path`.

## Challenge (author-run — an independent round is owed)

**C-1 — "You refused a column on a one-source-of-truth principle, then admitted the one source is
destroyed by `Anonymize()`."** Partly sustained, and it is the strongest attack. My answer is that a
column would not survive that path usefully either — the blob is orphaned regardless, and a column
naming its type without naming its location is not a recovery. But a challenger may reasonably rule
that a surface whose sole carrier a domain method deliberately overwrites has no business relying on
that carrier alone.

**C-2 — "Pre-prod is the cheapest this migration will ever be; you are spending a permanent option to
save one owner-run step."** Sustained as a cost point and rejected as a decision rule: "it is cheap now"
argues for doing everything now. The column has to be *right*, not just cheap, and D1 argues it is not.

**C-3 — "Your test is a property test over two tables. Nobody will understand why it fails."** Fair;
mitigated by making the failure message name the intake, the type, the minted extension and what the
read path resolved it back to. A guard whose red build is unreadable gets deleted.

**C-4 — Not self-challenged; start here.** Whether `ServedContentType.ServableExtensions` should simply
be **derived** from `SniffedContentType.Signatures` rather than duplicated — which would delete this
whole class of drift instead of testing for it. It cannot be done in the current assembly direction
(`Core.Blobs.Abstractions` cannot see `Core.AppServices`), so it is a question about where those two
types live, and that is a bigger ruling than this one.

## Verdict

**Not adjudicated.** No independent challenger has run and no lead has ruled.
