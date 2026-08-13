---
id: T-0459
title: Apply the image sanitizer to the order-photo and dispute-evidence upload pipelines
status: in_review
size: M
owner: backend
created: 2026-07-30
updated: 2026-08-06
depends_on: [T-0458]
blocks: []
stories: []
adrs: [0043]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Second half of **SEC-3** from the T-0446 security gate. **T-0458** decides the policy and builds the
shared sanitizer, piloting it on the avatar; this ticket applies it to the two pipelines where the
gap is **already live and already cross-user visible**.

Full write-up: `agents/backlog/security/user-profile-avatar.md`.

**These two are the cross-user-visible instances**, and the asymmetry with the avatar that started
this is worth being blunt about: a cleaner photographs the inside of a customer's home, and the
**customer, the cleaner and an admin can all fetch that blob**. The avatar — the thing T-0446 was
reviewed for — is the one instance nobody but its owner can reach.

> **⚠️ CORRECTION 2026-07-30 — do not overstate the live exposure.** An earlier draft said these
> photos carry GPS today, full stop. **Both mobile platforms already strip EXIF/GPS client-side**:
> Android via `cz.cleansia.core.media.ImageCompressor` (`2815c4f6`, PR #154) and **iOS via
> `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift`, which came first** — PR #154
> mirrored it. Both re-encode into a fresh bitmap/context, dropping metadata **by construction**.
>
> So the live residue on these two pipelines is: **(a) blobs uploaded before PR #154 merged**
> (2026-07-26) — a **backfill**, explicitly out of scope here; **(b) any web upload path**; **(c)**
> anything reaching the API outside the apps. **The reason to do this work anyway is that a
> client-side strip is unenforceable** — the server cannot distinguish a stripped upload from an
> unstripped one, so the only durable control is server-side. State it that way in the PR; a reviewer
> who checks the Android code will otherwise catch the ticket overstating its own case.

Pipelines in scope:

| Pipeline | Entry point | Blob name (already unique — do not change) |
|---|---|---|
| Order photos (batch save) | `Features/Orders/SaveOrderPhotos.cs` | `SaveOrderPhotos.cs:120-121` |
| Order photos (single upload) | `Features/Orders/UploadOrderPhoto.cs` | `UploadOrderPhoto.cs:95-97` |
| Dispute evidence | `Features/Disputes/UploadDisputeEvidence.cs` | unique per upload |

Note the two order-photo entry points take **different input shapes** — `SaveOrderPhotos` takes
base64 (`file.Base64Content`) and `UploadOrderPhoto` takes `command.FileData` as a byte array. The
sanitizer seam must serve both without a base64 round-trip in the byte-array case.

## Deliberation

**No panel of its own.** T-0458's ADR is the decision; this ticket is its application. If the
implementer finds a case T-0458's ADR does not cover — a format, a size, an orientation case, or a
pipeline whose shape the seam cannot serve — **stop and re-open T-0458's panel**. Do not extend the
policy from inside this ticket.

## Acceptance criteria

- [ ] **AC1** — Given a JPEG with GPS EXIF uploaded through **each** of the three entry points, When
      it is stored, Then the persisted bytes carry no EXIF/GPS. Evidence: one test per entry point
      that reads metadata back out of the bytes handed to the blob client — **not** an assertion that
      the sanitizer was invoked.
- [ ] **AC2 (Gate 0.5 leg 1)** — Each of AC1's three tests goes **RED** if that pipeline's sanitizer
      call is removed. The reviewer **names all three**. Three tests that pass because a shared helper
      is exercised once are one test wearing three hats.
- [ ] **AC3** — The agreed size/dimension caps are enforced on all three entry points, with the
      `BusinessErrorMessage` key from T-0458 and its `errors.*` translations present in **all five**
      languages on every client bundle that can trigger the path (partner web + partner mobile at
      minimum — confirm the reachable set before editing).
- [ ] **AC4 (regression)** — Existing order-photo and dispute-evidence tests stay green, **including
      the content-type behaviour**: these pipelines already record a `contentType` in blob metadata
      (`SaveOrderPhotos.cs:117`, `:124-125`). A re-encode may change the effective type — if it does,
      the recorded metadata must change with it, or the client will be told one thing and served
      another.
- [ ] **AC5** — Orientation is preserved end-to-end on a real phone photo (T-0458 AC4's concern, now
      on the pipeline where it matters most: a rotated before/after photo of a job is a visible
      product defect and a dispute-evidence integrity problem).
- [ ] **AC6 (Gate 5)** — `SaveOrderPhotos` is a **batch** path. Measure the added cost for a realistic
      batch (the largest photo count the partner app permits × a 4 MB photo) and record it. If it
      pushes the request past a sane budget, say so and stop — the fix is a background job, and that
      is a T-0458 re-open, not a silent timeout increase here.
- [ ] **AC7 (Gate 8)** — `dotnet build` + `Cleansia.Tests` + `Cleansia.IntegrationTests` green with
      real counts; anything not run locally named **DEFERRED-TO-CI / UNVERIFIED-LOCALLY**.

## Out of scope

- The library / policy / seam decision — **T-0458**, and its ADR is binding here.
- The avatar pipeline — piloted in T-0458 AC6.
- **Re-sanitizing already-stored images.** Order photos uploaded **before PR #154** (2026-07-26) carry
  GPS; ones uploaded from the apps since then do not. That backfill is a separate ticket with an
  owner-run step and its own risk profile, and per the correction above it is now **the larger share
  of the remaining real exposure**. **Do not smuggle it in** — but do say so in the PR so it is not
  mistaken for closed.
- Employee document photos — confirm early whether they are in the same class. If they are, **note it
  in the status log for the PM to file**; do not widen this ticket.
- Changing blob naming. All three pipelines already mint unique names — that is the correct behaviour
  (it is the avatar that was the outlier, fixed in T-0446 AC10).

## Implementation notes

- **Archetype:** T-0458's pilot wiring on `UpdateCurrentUser` is the reference. Mirror it; do not
  invent a second integration style per pipeline.
- **Shared-file lane:** `Features/Orders/SaveOrderPhotos.cs`, `Features/Orders/UploadOrderPhoto.cs`,
  `Features/Disputes/UploadDisputeEvidence.cs` — this ticket is the sole writer in its wave. No
  overlap with any T-0446…T-0457 lane.
- Read `docs/architecture/security-rules.md` including the rule added by **T-0460**.

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-3; split from T-0458 so neither ticket is an `L`)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0458]` unsatisfied — the policy and the seam do not exist yet.
- 2026-08-06 — **implemented** (backend). ADR-0043 is the binding spec and it overturns four things this
  ticket's body still says — read `## Review` §"Where the ADR overrode this ticket" before reviewing
  against the ACs above. `depends_on: [T-0458]` is **not** satisfied and was **not** a gate: ADR-0043
  §Verdict §F rules the only gate on this ticket is the ADR's own acceptance, which landed the same day.
  There is no sanitizer and no seam to mirror; the ADR refuses both (D1).

## Review

### Where the ADR overrode this ticket (ADR-0043 wins on every one of these)

| This ticket says | ADR-0043 says | What shipped |
|---|---|---|
| Mirror T-0458's `IImageSanitizer` seam and its avatar pilot | **D1** — no such abstraction; one helper per format | Three format walkers + one dispatcher, no interface, no DI registration |
| A sanitizer (re-encode implied) | **D2** — container rewrite only; nothing decodes | Segment/chunk walks; no package added |
| **AC3** — enforce size/dimension caps with a new error key and `errors.*` translations | Caps already shipped (`BlobFileSize`, the count caps); the namespace is `api.*`, not `errors.*` | **No new `BusinessErrorMessage` key, so no i18n work is owed by this ticket.** Nothing to translate in any locale on any app |
| **AC5** — orientation verified on a real phone photo | **D2.1** — the branch that matters is the malformed one, and production will barely exercise it; it carries a **synthetic-corpus** burden | 20 orientation cases, both TIFF byte orders, asserted on emitted bytes |
| **AC6** — measure the batch cost | Same, and ADR-0043 marks it ⚠ not measured, owed on the 30-item batch | Measured, below |

### AC1 / AC2 — the three mutation-proving tests, named

Each reads **the bytes handed to the blob client** (a `Callback` on `IBlobContainerClient.UploadAsync`
that copies the stream), never that a helper was called. Each goes red when **its own** call site stops
scrubbing, and no other pipeline's test moves:

| Pipeline | Test | Mutation that kills it | Other pipelines' tests when it fires |
|---|---|---|---|
| `UploadDisputeEvidence` | `UploadDisputeEvidenceMetadataScrubTests.A_Photograph_Submitted_As_Evidence_Reaches_Storage_Without_Its_Coordinates` | M15 — drop the scrub call in `UploadDisputeEvidence.cs` | green |
| `UploadOrderPhoto` | `UploadOrderPhotoMetadataScrubTests.A_Job_Site_Photograph_Reaches_Storage_Without_Its_Coordinates_Or_Its_Camera` | M16 — drop the scrub call in `UploadOrderPhoto.cs` | green |
| `SaveOrderPhotos` | `SaveOrderPhotosMetadataScrubTests.A_Job_Site_Photograph_Reaches_Storage_Without_Its_Coordinates_Or_Its_Camera` | M17 — drop the scrub call in `SaveOrderPhotos.cs` | green |

27 mutations were applied one at a time and restored byte-exact (`shasum` verified against a baseline
taken before the run; both harness logs end with a per-file restore check). **All 27 killed.** One
(**M5**, "repair a garbage segment length instead of refusing") **survived the first run** — the fixture
carried a payload behind the bad length, so a repaired walk simply refused one segment later and the test
could not tell repair from refusal. The fixture was replaced with a payload-less `APP1`, and M5 dies.
Recorded rather than quietly fixed: that test was passing for the wrong reason for one run.

### AC4 — regression, and what the recorded `contentType` does

No content type moves. The scrub is not a re-encode, so the recorded type still describes the stored
bytes: `UploadOrderPhoto` and `UploadDisputeEvidence` derive theirs from `SniffedContentType` over the
**pre-scrub** bytes and the scrub cannot change a container's format; `SaveOrderPhotos` keeps
`DetermineContentType` untouched (it is the sibling lane's, not this ticket's). `OrderPhoto.FileSizeBytes`
**does** move — it now records the stored length, pinned by
`UploadOrderPhotoMetadataScrubTests.The_Recorded_Size_Is_The_Size_Of_What_Was_Stored`.

### AC6 — the batch cost, measured

Release build, one thread, a temporary probe deleted after the reading was taken:

- **30 × 4 MiB** (this ticket's shape): **32.5 ms**, **120 MiB** allocated — one output array per photo,
  exactly 1× the input. No amplification, which is the property a decoder would not have.
- **30 × 700 KiB** (≈21 MiB, the request ceiling `request-intake-limits.md` actually allows — the
  120 MiB batch above is not reachable through Kestrel): **4.7 ms**.

Negligible against a request that already base64-decodes and uploads the same bytes. No background job
is needed and ADR-0043 does not need re-opening on cost.

### Two things a reviewer will ask about, answered here

- **`ScrubbedImage.Scrubbed` has no production reader.** It is the report ADR-0043 D2.2 requires (*"passed
  through untouched and **reported** as not scrubbed, never as scrubbed"*) and compliance check 6 asserts
  it. No call site branches on it because none of the three has a policy to apply to a *not scrubbed*
  outcome: refusing the upload would be a new `BusinessErrorMessage` key and a five-locale i18n change
  that the ADR does not authorize, and a PDF on the dispute path is a **legitimate** not-scrubbed result
  (D8). Reported by the type, asserted by
  `ImageMetadataDispatchTests.An_Unidentified_Payload_Passes_Through_Untouched_And_Says_So`.
- **`ImageMetadata` and `ScrubbedImage` are `public` where `SniffedContentType` and `BlobFileSize` are
  `internal`.** The repo declares no `InternalsVisibleTo` (two tests say so in writing), and the
  validators' internals are reachable from tests through the public validators that use them — a
  hand-rolled container walker has no such public front door. D2.1's corpus burden is 63 fuzz-style
  cases; routing them through a MediatR handler and five mocks apiece would obscure what is being
  tested. The three per-format walkers stay `internal`.
- **The directory is `Common/Media/`, not `Common/Artifacts/`.** `.gitignore:108` carries `artifacts/`,
  and `core.ignorecase=true` on macOS, so the first name made all fifteen new files invisible to
  `git status` — a build that compiles locally and not for anyone else. Worth knowing before the next
  person names a folder after this ADR's vocabulary.

### AC7 — the three suites, with every delta accounted for

| Suite | Baseline | Final | Delta |
|---|---|---|---|
| `Cleansia.Tests` | 3235 | **3320** ✅ | **+81 this ticket** (63 walker/dispatcher + 18 call-site) **+4 the sibling lane's `BusinessErrorSlotContractTests`**. The +81 is a TRX diff against a detached worktree at HEAD, not a hand count: 85 rows added, **0 removed** |
| `Cleansia.IntegrationTests` | 147 | **147** ✅ | 0 |
| `Cleansia.HostTests` | 135 | **138** ✅ | **+3, none of them this ticket's** — the sibling lane's untracked `Tests/ConsentErrorWireContractTests.cs`. Measured at 135 from this lane before that file appeared |

`dotnet build Cleansia.Api.sln` succeeds. Nothing was DEFERRED-TO-CI; all three suites ran locally.

### Catalog-edit routing (ADR-0033) — **no catalog edit made**, and why

- **Test 1 (does it put shipped code in violation?)** — it would. Sweep run: the 14 rows of
  `UploadIntakeRosterTests.cs:39-55`. A general sentence of the form *"an image whose audience is not its
  uploader is scrubbed at intake"* reaches `SaveMyDocuments` / `UpdateEmployee`, which accept
  `image/jpeg` and `image/png` (`SniffedContentType.cs:96-103`) for a **staff** audience and do not
  scrub. ADR-0043 D8 excludes employee documents on a **PDF/OOXML mechanism** argument that does not
  cover their image formats. That is a real open edge; it is **not** this ticket's to rule.
- **Test 2 (does it narrow open latitude?)** — searched `agents/knowledge/patterns-backend.md` for
  `metadata` and `scrub`. `:1284-1322` governs the *type* half of an intake and its callout at
  `:1306-1311` already states the bytes-in-hand rule for the scrub by reference. The *content* half is
  assigned by ADR-0043 D7 to **T-0460**, which is the sole writer of `security-rules.md`.
- **Routing:** test 1 fires → not mine to ratify. Nothing was added to `agents/knowledge/*`. The
  enforcer ADR-0043 D7 tiers `(gate pending: T-0459)` — *"per-pipeline tests reading metadata back out of
  the bytes handed to the blob client"* — now exists and is `T1-CI` (`Cleansia.Tests`,
  `backend-ci.yml:69-71`); **T-0460 writes the entry that claims it.**

### Not done, deliberately

- **The intake roster is untouched.** D6's `audience` / `scrub` columns are `(gate pending: T-0458)`, and
  this change alters no route's *guarding rule*, which is what the existing annotation states. ⚠️ Note
  for whoever does add them: `UploadIntakeRosterTests.cs:66-68` asserts `entry.Split(" — ")[0]` and
  **nothing reads index 1** — the annotation is enforced by nothing today.
- **Backfill** — out of scope per this ticket and ADR-0043 D9. Blobs written before this lands keep their
  metadata and **the read path cannot fix them** (a SAS hands the client the stored bytes). Still open.
- **`agents/architecture/decisions/user-uploaded-artifacts.md` §2 is now stale in two cells** — the
  "Metadata scrubbed" column reads `no` for both order-photo rows and for dispute evidence. Left for the
  architect rather than edited from this lane.

### ⚠️ Shared-tree contamination — for the PM, not fixed from this lane

A **sibling lane wrote into this working tree while this ticket ran**, and it cost two full test passes
before it was identified. Recorded so the next reader does not re-derive it:

- Files that appeared/changed under this lane, none of them touched here:
  `Features/Referrals/Admin/{ForceQualifyReferral,ReverseReferral}.cs`,
  `Features/Memberships/Admin/CreateMembershipPlan.cs`, their three test classes, the new
  `Cleansia.Tests/Common/BusinessErrorSlotContractTests.cs`, plus GDPR / promo-code / payroll files and
  two ADR documents.
- **Symptom:** two consecutive full unit runs failed 3–4 tests in `ForceQualifyReferralHandlerTests`,
  `ReverseReferralHandlerTests` and `CreateMembershipPlanHandlerTests` with `Assert.Equal … Strings
  differ at pos 0` — the shape of a half-applied `new Error(code, message)` slot swap, which is exactly
  what that lane's new `BusinessErrorSlotContractTests` polices. Six later full runs are green.
- **Nothing was reverted, stashed or checked out** (`agents/process/shared-file-lanes.md`). The seven
  files this ticket owns were `shasum`-verified unchanged by that lane afterwards.
- **It moves the reported unit count.** `BusinessErrorSlotContractTests` is four `[Fact]`s that did not
  exist at `7a350159`, so the suite total is `3235 + 81 (this ticket) + 4 (that lane) = 3320`. The +81 is
  established by a TRX diff against a detached worktree at HEAD, not by counting by hand.

### Owner/PM notes

- **`manual_steps: []` is correct** — no schema change (no EF migration) and no DTO or endpoint change
  (no NSwag regen). Command and Response records are byte-identical to before.
- **No `BusinessErrorMessage` key was added**, so no `api.*` translation is owed in any of the five
  locales on any app. The scrub never rejects: an unidentifiable payload is stored as sent and reported
  *not scrubbed*.
- **D10 (web clients re-encode on pick) is still owed and still ships independently.** ADR-0043 is
  explicit that it must not be sequenced behind this work: for order photos it is what removes the live
  volume, and this ticket is what makes it durable. It is not a control at all on dispute evidence.

<!-- reviewer + security verdicts below -->

