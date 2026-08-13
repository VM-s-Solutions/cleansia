---
id: T-0458
title: Image upload sanitization — decide the policy and build the shared sanitizer seam (EXIF strip, size cap, resize)
status: draft
size: M
owner: architect
created: 2026-07-30
updated: 2026-08-06
depends_on: []
blocks: [T-0459]
stories: []
adrs: []
layers: [architect, backend]
security_touching: true
manual_steps: []
sprint: 14
---

> ⚠️ **Reopened 2026-08-06.** I closed this at `6a901ed0` citing `f837e0ec`. That commit is
> an **unratified draft ADR**, not an implementation — every AC here is still unticked and the panel
> has not run. Blocked on the content-policy draft reaching `accepted`.

## Context

Filed from the **T-0446 security gate** (finding **SEC-3**). Full write-up:
`agents/archive/2026-08/backlog/security/user-profile-avatar.md`.

> **⚠️ CORRECTION 2026-07-30 — read this before you size the problem.** An earlier draft of this
> ticket said no user-uploaded image is sanitized anywhere. **That was wrong.** Both mobile platforms
> already strip EXIF/GPS **client-side**: Android via `cz.cleansia.core.media.ImageCompressor`
> (`2815c4f6`, PR #154) and **iOS via `CleansiaCore/Sources/CleansiaCore/Media/ImageCompressor.swift`,
> which came first** — PR #154 mirrored it. Both decode and re-encode into a fresh bitmap/context, so
> metadata is dropped **by construction, not by erasure**.
>
> **What is actually missing:** **the web apps do not strip**, and — the load-bearing point — **the
> server cannot enforce that anyone did.** A client-side strip is unverifiable from the server: a
> modified app, a direct API call with a valid token, or the web upload path all land raw EXIF in blob
> storage, and nothing downstream can tell the difference. This ticket is **defence in depth over two
> already-covered clients and a real gap on the third**, not a wide-open hole. Size and argue it that
> way. Also note that **blobs uploaded before PR #154 still carry EXIF** — see the backfill note under
> Out of scope.

> ## 🛑 GATE 0 — 2026-08-05. **Everything below this block is stale. Read the draft ADR first.**
>
> Four intake-hardening tickets have landed since this was filed (T-0464 `b9753e85`, T-0548 `97bb7265`,
> T-0556 + follow-up). **Verified at HEAD by the architect (author mode):**
>
> | This ticket says | At HEAD |
> |---|---|
> | *"there is no size limit anywhere"* | **SHIPPED** — 10 MiB decoded, one shared predicate, derived from the *encoded* length, **first** in every chain (`Common/Validators/BlobFileSize.cs:8-9,17-28`) |
> | *(not asked for)* | **Per-request count caps SHIPPED** — 10 / 10 / **30** (`SaveOrderPhotos.cs:46`) |
> | *"`ImageFileValidator` checks a 3–4 byte magic prefix and nothing else"* | still true for **images**; **superseded for documents** — `DocumentContentType.FromContent` derives the stored type from the bytes and discards the client's claim |
> | *"a magic-byte check says the bytes are an image, not that the image is safe to hand back"* | the *hand-back* half is **closed** — `ServedContentType` is a closed value type on the READ path (so it governs already-stored blobs); `text/html` and `image/svg+xml` are excluded by name |
> | *"EXIF/GPS is not stripped"* | **STILL TRUE. This is the entire remaining residue.** |
>
> **So this ticket is ~70 % satisfied, and its remaining 30 % is a different decision than the one it
> frames.** Three specific things in it are now **wrong**, not merely stale:
>
> 1. **The library question (ImageSharp vs SkiaSharp) is moot unless the panel overrules the draft ADR's
>    D2.** `SixLabors`/`SkiaSharp`/`System.Drawing`/`Magick` appear in **zero** `src/**/*.csproj`, and
>    **no user image is decoded server-side anywhere** (`OrderPhoto.Width`/`Height` are never
>    populated). Adding a decoder on a request path fed 10 MiB × 30 items, on an S1 / 1.75 GB plan
>    shared by 5 APIs + SSR + Functions, **creates** a decompression-bomb primitive that does not exist
>    today. The draft ADR refuses re-encoding and rules for a **container rewrite** instead.
> 2. **AC6's pilot choice is backwards.** The avatar is not "lowest blast radius" — it is the surface
>    with **no exposure at all**: `GetCurrentUser.ResolveProfilePhotoUrl` is the only SAS mint for
>    `user-files`, and every list/employee DTO maps the photo with `BlobUrl = null`. Piloting there
>    reduces zero exposure and measures the one-item case instead of the 30-item batch. The draft ADR
>    moves the pilot to `SaveOrderPhotos`.
> 3. **The "shared sanitizer seam" premise is refused** by the draft ADR: the shareable seams already
>    exist (`BlobFileSize`, `ServedContentType`, `Base64UploadIntakeRosterTests`); a metadata transform
>    is not one of them, and the shareable part is the *obligation* — a roster column, not an interface.
>
> **AC status:** AC1 stands (rewritten scope). AC2/AC3 stand. **AC4 (orientation) stands and is the
> hardest part of the adopted design.** **AC5 is satisfied** by T-0548/T-0556 — no new error key is
> needed (the draft ADR reuses `file.content_type_doesnt_match`). **AC6 is overturned.** AC7 stands but
> is owed on the **batch**, not the avatar. AC8 stands.
>
> **Draft ADR:** `docs/decisions/adr-0043.md`
> (`proposed`, number not allocated, **panel owed**).
> **Living doc:** `agents/architecture/decisions/user-uploaded-artifacts.md`.

**No user-uploaded image is sanitized SERVER-SIDE, and the server cannot verify that any client did.**
`ImageFileValidator`
(`src/Cleansia.Core.AppServices/Common/Validators/ImageFileValidator.cs`) checks a **3–4 byte magic
prefix** against `Constants.ImageSignatures` and nothing else; the upload paths then store the decoded
bytes **verbatim**. `UpdateCurrentUser.UploadPhotoAsync` (`:160-164`) is the clearest example:

```csharp
await using var stream = new MemoryStream(Convert.FromBase64String(base64Content.ExtractBase64Data()));
await client.UploadAsync(fileName, stream, Metadata.CacheMetadata, cancellationToken);
```

JPEG and TIFF both carry EXIF, and EXIF carries GPS. A magic-byte check is an **accept/reject** test —
it says the bytes *are* an image, not that the image is *safe to hand back*.

**Scope is the upload pipeline, not the avatar.** The avatar is the **least** exposed instance:

- **Order photos** and **dispute evidence** are the **cross-user visible** surfaces — a cleaner's
  photos of a customer's home, fetchable by customer, cleaner **and** admin. Per the correction above,
  the mobile apps now strip before upload, so the live residue there is **(a) blobs uploaded before
  PR #154**, **(b) any web upload path**, and **(c) anything that bypasses the apps**. Still the
  higher-risk surface of the two, but state it precisely — do not repeat the overstatement.
- The **avatar** is reachable only by the photo's own owner (`GetCurrent` is self-only; no list shape
  emits a URL — verified in the findings doc §0). **T-0446 discloses nobody's EXIF to anyone new.**

**This is post-demo — but it is a HARD PRECONDITION for any cross-user avatar display**, which is the
obvious next feature (a cleaner's face on an assigned order; an avatar column in the admin user list).
The moment a second person can fetch that blob, this becomes a live geolocation disclosure. **Record
that gate on whatever ticket proposes cross-user avatars.**

**Also in scope: there is no size limit anywhere.** No `MaximumLength` on `Base64Content`, no
dimension bound, no re-encode — the only ceiling is Kestrel's 30 MB request default. That is a DoS and
a storage-cost surface as much as a privacy one, and it is cheapest to fix in the same seam.

**Split from T-0459 deliberately.** Decision + seam + one pilot pipeline is an `M`; decision + seam +
**three** pipelines with their own tests is an `L`, and `ticket-lifecycle.md` forbids running an `L`.
T-0459 applies the seam to the remaining pipelines.

## Deliberation required — NOT `ready`

**Architect panel** (author + 2–3 challengers + lead) per `agents/process/deliberation.md`. This
introduces a **new third-party dependency** into the server-side hot path and a **new cross-cutting
policy**, so it wants an accepted **ADR**, not a code review. The space to defend:

- **Which library.** `SixLabors.ImageSharp` (note the **licence** — it is no longer unconditionally
  free for commercial use above a revenue threshold; the panel must rule on this explicitly, in
  writing, because getting it wrong is a legal problem rather than a technical one), `SkiaSharp`
  (native binaries — check the Azure Functions and container base images), or `System.Drawing`
  (**not** an option: unsupported on non-Windows since .NET 6).
- **Strip vs. re-encode.** Surgically removing EXIF preserves the original bytes but requires
  per-format handling and leaves other metadata containers (XMP, IPTC, ICC) behind. A full decode +
  re-encode drops everything by construction, costs CPU, and is lossy for JPEG. **Recommend one and
  say what it loses.**
- **Where the seam sits.** A FluentValidation validator cannot mutate — validators reject, they do
  not transform. So this is *not* an extension of `ImageFileValidator`. Candidates: a new
  `IImageSanitizer` in `Cleansia.Infra.Services` called by each handler; a MediatR pipeline behavior
  keyed on a marker interface; or a decorator on `IBlobContainerClient.UploadAsync`. The last is the
  most tamper-proof (nothing reaches a blob unsanitized) and the most surprising. **Rule on it.**
- **Orientation.** Stripping EXIF removes the `Orientation` tag, so a photo that rendered upright via
  EXIF will render **rotated** afterwards. The sanitizer must **apply** orientation before discarding
  it. This is the single most likely way to ship a visible regression here — pin it with a test.
- **The caps.** Max bytes, max dimensions, target dimensions per pipeline (an avatar and an
  order-photo want different answers), and the error key + i18n for a rejected upload (`errors.*`
  ×5 languages).
- **Animated / multi-frame inputs and SVG.** Confirm what `Constants.ImageSignatures` actually admits
  before deciding — the answer bounds the whole problem.

## Acceptance criteria

_(PM floor; the panel finalizes)_

- [ ] **AC1** — An **ADR is accepted** in `docs/decisions/` recording: the library + its licence
      position, strip-vs-re-encode, the seam's location, the caps, and orientation handling. Evidence:
      the ADR file with the deliberation trail.
- [ ] **AC2** — Given a JPEG carrying GPS EXIF, When it goes through the sanitizer, Then the output
      bytes carry **no** GPS tag and no EXIF block. Evidence: a test that reads the metadata back out
      of the **output** — not one that asserts the sanitizer was *called*.
- [ ] **AC3 (Gate 0.5 leg 1)** — AC2's test goes **RED** if the sanitizer is stubbed to pass bytes
      through. The reviewer **names that test**.
- [ ] **AC4** — Given an image whose EXIF `Orientation` is 6 (rotate 90° CW), When sanitized, Then the
      output renders in the same visual orientation as the input did. Evidence: a pixel-level
      assertion, not a visual claim.
- [ ] **AC5** — Given an upload exceeding the agreed byte or dimension cap, When submitted, Then it is
      rejected with a defined `BusinessErrorMessage` key that has a matching `errors.*` translation in
      **all five** languages on every client bundle that can trigger it.
- [ ] **AC6** — The sanitizer is wired into **exactly one** pipeline as a pilot — **the avatar**
      (`UpdateCurrentUser`), because it is the lowest-blast-radius of the three and the one whose
      write path is already being edited this sprint. The other two are T-0459.
- [ ] **AC7 (Gate 5)** — The CPU and allocation cost of sanitizing a representative 4 MB phone photo
      is **measured and recorded**, because this now sits on a synchronous request path. If it is
      material, the panel rules on whether the work moves to a background job — do not discover this
      in production.
- [ ] **AC8 (Gate 8)** — `dotnet build` + `Cleansia.Tests` green with real counts; anything not run
      locally named **DEFERRED-TO-CI / UNVERIFIED-LOCALLY**.

## Out of scope

- Applying the sanitizer to order photos and dispute evidence — **T-0459**.
- Content moderation / NSFW classification. Different problem, different decision.
- Re-sanitizing images **already stored**. A backfill is a real question — and the correction at the
  top makes it **the larger share of the remaining exposure**, since blobs uploaded **before PR #154**
  carry EXIF while new mobile uploads do not. It is still a data-migration ticket with an owner-run
  step and must not be smuggled in here. **File it separately once the panel has ruled** — note it in
  the status log, and note that its scope is bounded by PR #154's merge date (2026-07-26).
- Thumbnailing / responsive variants. Adjacent, and tempting once a decoder is in the build. Not this.

## Implementation notes

- **Archetype:** none exists — this is a new cross-cutting service. Closest structural precedents for
  *placement* are the existing `Cleansia.Infra.Services` services (PDF/QuestPDF, email, blob) and
  their DI registration in `Cleansia.Config`.
- Read `docs/architecture/security-rules.md` — and note that **the rule this ticket enforces does not
  exist yet**; it is being written by **T-0460** (SEC-5). Ideally T-0460's panel runs first or
  alongside, so the ADR can cite the rule rather than invent it.
- The order-photo pipelines already compute a `contentType` (`SaveOrderPhotos.cs:117`,
  `DetermineContentType`) — reuse that, do not add a second content-type inference.

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-3; split from T-0459 to keep both off `L`)
- 2026-07-30 — **not `ready`**: awaiting the architect panel (DoR item 7 — no archetype exists, and the library/seam decision is unmade).
- 2026-08-05 — **Gate 0 run by the architect (author mode). Premises verified at HEAD; ~70 % of this
  ticket is already satisfied and the remainder is reframed.** See the GATE 0 block at the top. A draft
  ADR is on disk — `docs/decisions/adr-0043.md` — with
  the living doc `agents/architecture/decisions/user-uploaded-artifacts.md`. **Still not `ready`:** the
  draft is `proposed`, its `## Challenge` section is an author-run self-challenge, and
  `process/deliberation.md` requires distinct author / challenger / lead instances. AC1 is unsatisfied
  until that panel runs.
  **What the panel must settle** (the author's positions are in the draft, and the two he most wants
  attacked are marked): **(a)** whether the web clients re-encoding on pick — matching both mobile
  clients, ~30 lines per picker, zero server cost — makes the server-side scrub unnecessary rather
  than merely less urgent; the ticket's *"a client-side strip is unenforceable"* argument imports the
  XSS threat model, where the uploader is the **adversary**, into the metadata case, where the uploader
  is the **victim**. **(b)** whether hand-rolled JPEG/PNG/WebP container walks are acceptable
  attacker-facing code versus a third-party decoder. **(c)** whether the avatar's "audience: self"
  exemption is safe, given a cross-user avatar URL is a one-line change in `UserMappers`.
  **Renaming owed:** if the draft survives, this ticket's and T-0459's titles are wrong — there is no
  sanitizer in the adopted design, there is a **metadata scrub** on two surfaces.
  **Also filed by this pass, for the PM:** an owner escalation candidate **Q-ART-01** (keep accepting
  DOC/DOCX on employee documents, whose author/revision metadata is not scrubbed?) — draft ADR
  §Escalations; to be written to `questions/open.md` by the panel lead, not by this ticket.

## Review
<!-- reviewer + security verdicts here; AC3 must name the mutation-proving test -->
