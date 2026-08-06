---
id: T-0459
title: Apply the image sanitizer to the order-photo and dispute-evidence upload pipelines
status: ready
size: M
owner: backend
created: 2026-07-30
updated: 2026-07-30
depends_on: [T-0458]
blocks: []
stories: []
adrs: []
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
- Read `agents/knowledge/security-rules.md` including the rule added by **T-0460**.

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-3; split from T-0458 so neither ticket is an `L`)
- 2026-07-30 — **not `ready`**: `depends_on: [T-0458]` unsatisfied — the policy and the seam do not exist yet.

## Review
<!-- reviewer + security verdicts here; AC2 must name all three mutation-proving tests -->
