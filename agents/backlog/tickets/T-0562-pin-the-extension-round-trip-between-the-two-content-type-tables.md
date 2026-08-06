---
id: T-0562
title: "Pin the extension round-trip between `SniffedContentType` and `ServedContentType` — `DisputeEvidence`'s served type depends on two hand-maintained tables in two assemblies, and one of four pairs is tested"
status: draft
size: XS
owner: backend
created: 2026-08-06
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: ["NNNN-dispute-evidence-type-carrier-is-the-blob-name (draft — panel owed)"]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
source: architect ruling 2026-08-06, on the "is `DisputeEvidence` missing a content-type column?"
  question routed by the T-0556 follow-up lane. The ruling is **no column**; this is the gap that
  actually exists instead.
---

> ⚠️ **Ticket id is PROPOSED.** Highest on disk at filing was T-0560 (T-0561 filed in the same pass).
> PM confirms or reassigns.

> **This ticket exists because a migration was REFUSED.** See the ADR: `DisputeEvidence` has no
> content-type column and is not getting one — the server-minted blob name is a content-addressed
> carrier and is the surface's single source of truth, which a column would double. What is missing is
> not a column; it is a guard on the thing the carrier depends on.

## Context

`DisputeEvidence` records no content type. Its served type is resolved from the stored **path**:

- **Write** — `UploadDisputeEvidence.cs:104-105`:
  `blobName = $"{disputeId}/{Guid:N}{SniffedContentType.ExtensionFor(sniffedType)}"`
- **Read** — `DisputeMappers.cs:73-78`: `ServedContentType.ForFileName(evidence.FilePath)`

That works only while the two tables agree in **both directions** for every type the intake accepts:

| Accepted (`SniffedContentType.cs:92-95`) | `ExtensionFor` (`:66-78`) | `ForFileName` (`ServedContentType.cs:44-52`) | Round-trips |
|---|---|---|---|
| `image/jpeg` | `.jpg` | `image/jpeg` | ✔ |
| `image/png` | `.png` | `image/png` | ✔ |
| `image/webp` | `.webp` | `image/webp` | ✔ |
| `application/pdf` | `.pdf` | `application/pdf` | ✔ |

**All four hold today. Nothing asserts it.** The two types live in different assemblies —
`SniffedContentType` in `Cleansia.Core.AppServices`, `ServedContentType` in
`Cleansia.Core.Blobs.Abstractions`, which cannot reference it — so the agreement is by hand with no
compiler behind it. The only test that exercises the round trip
(`UploadDisputeEvidenceContentTypeTests.cs:73-94`) covers **PNG**, one of four.

**And the trap is one line away.** `Signatures` already carries `application/msword → .doc` and the
OOXML type `→ .docx` (`SniffedContentType.cs:73-77`), and `ServedContentType.ServableExtensions` knows
**neither**. They are unreachable today only because `AcceptedByIntake[DisputeEvidence]` happens not to
list them. Adding a document type to that set — an entirely reasonable-looking product change — would
silently start serving every such evidence file as `application/octet-stream`.

**The failure mode, so a reviewer recognises it:** evidence **downloads instead of previewing** for the
customer and for the adjudicating staff member. Silent capability loss on a support-critical path, never
a security failure — the demotion direction is `Opaque`-ward by construction.

## Acceptance criteria

- [ ] **AC1 — the assertion is over the composition, driven off the accepted sets.** Given every
      `UploadIntake` and every content type in `AcceptedByIntake[intake]`, When the pair is checked,
      Then `ServedContentType.ForFileName(SniffedContentType.ExtensionFor(t)).Value == t`. Driven off
      the dictionary, **not** a hand-written `[InlineData]` list — the point is that widening an
      accepted set reddens the build.
- [ ] **AC2 — exemptions are named per intake, with the carrier used instead.** `EmployeeDocument` is
      the exemption: it has a real content-type column and its read path is
      `SniffedContentType.ForDownload` over bytes the server holds
      (`DownloadMyDocument.cs:88`, `DownloadEmployeeDocument.cs:52`), so its `.doc`/`.docx` never pass
      through `ServedContentType`. The exemption is a named constant in the test with that reason as a
      comment — an unexplained skip is how this drifts again.
- [ ] **AC3 — non-vacuity floor.** The enumerated pair **count** is asserted before any per-pair
      comparison (the `UploadIntakeRosterTests.cs:62-64` discipline). A guard that passes because its
      corpus is empty is not an enforcer (ADR-0032).
- [ ] **AC4 — the failure message is readable.** On failure it names the intake, the content type, the
      minted extension and what the read path resolved it back to. A guard whose red build is
      unreadable gets deleted by the next person.
- [ ] **AC5 — mutation table.** Two named mutations, each red, files restored byte-exact: (a)
      `ExtensionFor`'s `image/webp` row → `.wbp`; (b) `AcceptedByIntake[DisputeEvidence]` gains
      `application/msword`. **(b) is the one that matters** — it is the real-world edit this guard
      exists to catch, and a test that survives it is the wrong test.
- [ ] **AC6 — the catalog entry carries its tier.** The `patterns-backend.md` bullet
      *"Where the row records no type at all, the blob NAME is the only carrier — so mint it"* gains
      **`Enforced by:** <the new test class> — `T1-CI`* (`Cleansia.Tests`, the *"Unit tests
      (Cleansia.Tests)"* step of `backend-ci.yml:69-74`). Baseline is **zero** — all four pairs
      round-trip today — so `T1-CI` is correct on day one and no `(gate pending: …)` applies.
- [ ] **AC7 — no schema change.** `DisputeEvidence.cs` is untouched and `Migrations/` is untouched.
      A reviewer greps for `ContentType` in the entity and finds nothing. **This is an AC, not a note:**
      the obvious reading of "fix the dispute-evidence content type" is to add the column, and the ADR
      refuses it.

## Out of scope

- **Adding `DisputeEvidence.ContentType`** — refused by the ADR. If the panel overturns that, this
  ticket is superseded, not amended.
- **Deriving `ServedContentType.ServableExtensions` from `SniffedContentType.Signatures`** — would
  delete this class of drift rather than test for it, but it is blocked by the assembly direction and is
  a bigger ruling (ADR §Challenge C-4).
- **The GDPR finding on the same entity** (dispute-evidence blobs orphaned by `Anonymize()`) — its own
  ticket, `security_touching: true`, see `agents/architecture/decisions/user-uploaded-artifacts.md` §7.3.

## Implementation notes

**Files this ticket touches:**
- `src/Cleansia.Tests/Common/Validators/` — one new test class
- `agents/knowledge/patterns-backend.md` (AC6)
- **Read-only, must not change:** `SniffedContentType.cs`, `ServedContentType.cs`,
  `DisputeEvidence.cs`, `DisputeMappers.cs`, `Migrations/`

`SniffedContentType` and `UploadIntake` are `internal` to `Cleansia.Core.AppServices`;
`Cleansia.Tests` already reaches them (`UploadDisputeEvidenceContentTypeTests` calls
`SniffedContentType.FromContent`), so no visibility change is needed — verify rather than assume, and if
`AcceptedByIntake` is not reachable, expose an internal read-only accessor rather than duplicating the
sets into the test, which would defeat AC1 entirely.

### Staleness detectability (sprint-15 §D3)

Names product paths under `src/`. Manual check at dispatch:
`grep -n "ServableExtensions" src/Cleansia.Core.Blobs.Abstractions/ServedContentType.cs`.

**No-decision note:** the decision is in the ADR; this ticket is the gate it specifies. `draft` until
the panel rules on the ADR — the guard itself is uncontroversial, but AC7's "no column" is the ruling.

## Status log
- 2026-08-06 — created `draft` by the architect (author mode) alongside the ADR.

## Review
<!-- reviewer verdict here; PM reconciles before advancing state -->
