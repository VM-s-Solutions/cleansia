# User-uploaded artifacts — living decision doc

> **Status: §1–§3 are SHIPPED and verified at HEAD (2026-08-05, re-verified 2026-08-06). §4–§6 were
> DECIDED-BUT-UNPANELLED — ⚠️ THE PANEL HAS RUN (2026-08-06) and §8 is its verdict; read §8 BEFORE
> §4–§6, which it corrects in ten places. §7 is the content-type ruling — a separate lane whose
> **§7.1 has now been RULED — lead, 2026-08-06: verdict REVISE on a closed list, no further round, then
> it lands as `ADR-0044`** — while **§7.2 is still author-only**.** The immutable record for §4–§6 is
> **`../../backlog/adr/0043-user-artifact-metadata-is-scrubbed-at-intake-by-audience-without-a-decoder.md`**
> — **rev N+1 landed 2026-08-06** and the PM has since stamped it **`accepted`** (that ADR's `:3`, per
> its §Verdict §E), carrying the full `## Challenge` / `## Defense` / `## Verdict` trail. **The sole gate
> on T-0459 is therefore discharged** — and T-0459's scrub is on disk at both order-photo handlers; see
> the dated note under §2's table. The old draft path
> (`backlog/adr/drafts/NNNN-user-artifact-content-policy-no-decoder.md`) is a **tombstone** — it held
> rev N, whose map was stale in ~7 citations. Do not cite it.
> **Tickets:** T-0458 (policy + seam), T-0459 (application), T-0460 (the S-series law) — **all three are
> re-scoped by ADR-0043; do not read their `## Context` as current.**
> ✅ **T-0460 has LANDED (2026-08-07): the law is written as `security-rules.md` **S12**, with a
> per-clause enforcer/tier table, and it — not §5 below — is the enforceable home of that table.** §5 is
> kept as the design-time record and now points at it. T-0459's scrub is merged; the ⚠️ notes below that
> said otherwise are resolved in place.
> **Related:** T-0464 ✅ (`b9753e85`, the served-type clamp), T-0548 ✅ (`97bb7265`, the avatar size cap),
> T-0556 ✅ + follow-up (document intake, the roster), `request-intake-limits.md` (the host ceiling —
> the *transport* bound, a different guarantee).
>
> ⚠️ **§2's residue table (R1–R3) was written before the T-0556 follow-up and is STALE. Corrected
> in-place below; read §7 for the current content-type state.** Three names in §2/§5 no longer exist:
> `DocumentContentType` → `SniffedContentType` (one table, all four intakes),
> `Base64UploadIntakeRosterTests` → `UploadIntakeRosterTests` (**14 rows, not 10**), and
> `Constants.ImageSignatures` is **deleted** (`grep` over `src/` at 2026-08-06 returns nothing).

---

## 1. The five questions an upload surface has to answer

Conflating them is the recurring mistake in this area — three tickets have each fixed one and been read
as having fixed another.

| # | Question | Where it is answered | What it CANNOT do |
|---|---|---|---|
| 1 | **How many bytes?** | `Common/Validators/BlobFileSize.cs` — 10 MiB decoded, derived from the *encoded* length, **first** in every chain | bound the *collection*; bound the *request* |
| 2 | **How many items?** | each command's `Validator` — 10 documents, 30 photos | bound bytes at all; it runs after buffering, so it is answer-correctness, not resource protection |
| 3 | **Is it the kind of thing we take?** | the two `AbstractValidator<BlobFileDto>` siblings + their signature tables | say anything about what is *inside* the container |
| 4 | **What is it served as?** | `ServedContentType` on the **read** path (closed set) + `SniffedContentType.ForDownload` (`:127-128`) — *the header's rename note applies here too; `DocumentContentType` is gone* | change the bytes |
| 5 | **What travels inside it?** | `Common/Media/ImageMetadata.Scrub(byte[])` (`:35`) at intake, on the three cross-audience routes — `SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`, `UploadDisputeEvidence.cs:108`. **The law is `security-rules.md` S12** | be answered on the read path (a SAS hands the client the stored bytes directly); remove ICC, a JPEG `COM`, or anything inside the image data; touch a PDF or an OOXML file at all |

The transport ceiling is a **sixth** bound and lives in `request-intake-limits.md`. It is a host
property; 1–5 are per-surface properties.

## 2. State at HEAD — the fourteen upload routes and who fetches them

**Exposure is a property of the audience, not of the pipeline.** This is the table the tickets lack.

| Surface | Routes | Uploaded by | **Fetched by** | Delivery | Served as | Metadata scrubbed |
|---|---|---|---|---|---|---|
| Avatar | 3 (`UserController.UpdateCurrentUser` × Customer / Mobile.Customer / Mobile.Partner / Partner — 4 rows on the roster) | the user | **the same user only** | 1 h SAS → `<img>` | `application/octet-stream` (opaque overload) | no |
| Order photos (batch) | 2 (`OrderController.SavePhotos`, Partner + Mobile.Partner) | a cleaner | **customer + cleaner + admin**, 5 read hosts | 1 h SAS | closed-set typed | **yes** (`:137`) |
| Order photo (single) | 2 (`OrderController.UploadPhoto`) | a cleaner | same | 1 h SAS | closed-set typed at read; ~~raw client string stored~~ **byte-derived** (`:103`) | **yes** (`:107`) |
| Dispute evidence | 2 (`DisputeController.UploadEvidence`, `multipart/form-data`) | the customer — **and on this surface the uploader is an adversary with money on the outcome** (`:96-99`) | that customer + **staff adjudicating a refund** | 1 h SAS, **inline** — `GenerateSasUri` sets `rsct`/`rscc` and **no `rscd`** (`BlobContainerClient.cs:89-110`) | ~~typed from the client's file name~~ **byte-derived**; the blob-name extension is minted from the bytes (`:105-106`) | **yes** (`:108`) — images only; a PDF is stored byte-for-byte (D8) |
| Employee documents | 4 (`EmployeeController.SaveMyDocuments` / `.UpdateEmployee`, Partner + Mobile.Partner) | a cleaner | that cleaner + **admin** | **never by URL** — API host, `File(bytes, type, name)` → `attachment` | **byte-derived** | no (PDF/Office) |

> ⚠️ **The two order-photo rows' "Fetched by" is WRONG and was corrected by the panel (2026-08-06,
> CH-2c).** `GetOrderPhotos.cs:59` gates on **`CanBrowseOrderAsync`**, not `CanAccessOrderAsync`. After
> owner/admin/assigned fails, `OrderAccessService.cs:68-92` returns `true` for **any** caller with role
> `Employee` and a resolvable `employeeId` while `order.HasAvailableSpots && OrderVisibility.NotHeldFrom(…)`
> — the comment at `:84-87` says that branch is *"both browse surfaces at once — order detail and order
> photos."* So the fetch set is customer + admin + assigned cleaners **+ every cleaner in the tenant who
> can see the order while a seat remains open** (up to 12 seats on a 24 h order). **Writing** still
> requires assignment (`SaveOrderPhotos.cs:114-117`); **fetching does not.** This is why the scrub's
> justification is *"the audience is not enumerable at upload time"* and not *"three known parties"*.

> ✅ **The `Metadata scrubbed` column was stale for three rows and is now CORRECTED** *(flagged by the
> §7.1 lead 2026-08-06, left by the T-0459 lane "for the architect", fixed here 2026-08-07 with all
> three call sites re-opened at HEAD)*. All three now read **yes**; §5's `(gate pending: T-0459)` row is
> resolved with it. The **avatar** and the **employee-document** rows still read **no** and that is the
> ruling, not a gap (D4 / D8) — each carries its reason, and the avatar's expires the day an avatar URL
> reaches a cross-user DTO.

**Why the avatar row matters most for planning:** `GetCurrentUser.ResolveProfilePhotoUrl` is the *only*
SAS mint for `user-files`. `UserMappers.cs:23,66` and `EmployeeMappers.cs:37,63` map the photo **without**
a URL, so every list and employee DTO carries `BlobUrl = null`. **Cross-user avatar display is one line
away**, and the day it lands the avatar's "audience: self" exemption expires.

### What the four hardening tickets actually closed

- **Stored XSS from a served artifact: closed.** `ServedContentType` is a closed value type with a
  private constructor; `text/html` and `image/svg+xml` are excluded **by name**; unknown → `Opaque`
  (`application/octet-stream`, outside the MIME-sniffing standard's sniffable set). Applied via the SAS
  response-header override (`rsct`/`rscc`), so it governs blobs written **before** it existed.
  ⚠️ **One nuance the panel added (CH-7):** the closed set still admits a **scriptable container** —
  `application/pdf`, served **inline** with no `Content-Disposition`. That is *not* equivalent to stored
  XSS (the storage host carries no app session and browser PDF viewers are sandboxed) and the "closed"
  verdict survives — but the threat table must say so rather than reason only over `text/html` and
  `image/svg+xml`.
- **Type confusion: closed on 13 of 14 intakes.** ~~`DocumentContentType`~~ **`SniffedContentType`**
  `.FromContent` answers *may we accept* and *what is it* from the first 12 bytes, for **all four**
  intakes off **one** signature table; `ForDownload` re-derives from the same table on the read path, so
  legacy rows retype without a backfill. **The fourteenth is `SaveOrderPhotos`** — see §7.1.
- **Unbounded intake: closed.** One shared size predicate, ordered first; count caps on all three arrays.
- ~~**"How many intakes are there": partly closed.**~~ **CLOSED.** ~~`Base64UploadIntakeRosterTests`
  enumerates 10~~ → **`UploadIntakeRosterTests` enumerates all 14** (`:39-55`), with a second `[Theory]`
  (`:76-84`) naming the four `byte[]`/`IFormFile` intakes so narrowing the predicate cannot silently
  pass. **This is `T1-CI` today** — only the `audience` / `scrub` columns are outstanding.

### The residue, named so it is not rediscovered

*(R1–R3 as written 2026-08-05. **Re-checked at HEAD 2026-08-06 — all three are CLOSED.** Struck rather
than deleted, because the wrong half of a closed finding is what gets re-derived from an old checkout.)*

- ~~**R1**~~ — **CLOSED.** `UploadOrderPhoto.cs:102` and `UploadDisputeEvidence.cs:104` both sniff;
  the dispute blob name's extension is minted from the bytes (`:105`) and the read resolves the stored
  **path**, not `FileName` (`DisputeMappers.cs:75`).
- ~~**R2**~~ — **CLOSED.** `GetOrderPhotos.MapToDto` resolves `ServedContentType` once and uses it for
  both the DTO field and the SAS header (`:96,101,105`).
- ~~**R3**~~ — **CLOSED.** `Constants.ImageSignatures` is deleted; `SniffedContentType.Signatures`
  (`:66-78`) carries no BMP or TIFF and matches WebP as `RIFF` + `WEBP` at offset 8.
- **R4** — no metadata is removed from anything, anywhere. **Still open** (§4).
- **R5 — new, 2026-08-06.** `SaveOrderPhotos` is the fourteenth intake and reads no byte of its
  payload. **See §7 — it is the subject of a ruling, not an unowned residue.**

## 3. The property that decides everything — nothing here **calls** a decoder

> ⚠️ **Corrected by the panel, 2026-08-06 (§8 / CH-3iii). This section said "nothing decodes an image",
> which is wrong in one direction and overstated in another. Both corrections are below and both were
> re-verified by the lead.**

`SixLabors`, `SkiaSharp`, `System.Drawing`, `Magick` appear in **zero** `src/**/*.csproj`, and
`OrderPhoto.Width`/`Height` exist and are **never populated** — both writers omit the optional
arguments. **But a complete decoding stack is already deployed:** QuestPDF 2024.12.1
(`Cleansia.Infra.Services.csproj:14`, pinned `Directory.Packages.props:55`) ships its own native Skia as
runtime assets — `runtimes/{linux-x64,linux-arm64,linux-musl-x64}/native/libQuestPdfSkia.so`
(`Cleansia.Infra.Services/obj/project.assets.json:832-864`) with bundled `libjpeg-turbo` / `libpng` /
`libwebp` / `skia` licences (`:2362-2368`).

**What is absent is the call site**, and that is the real property: `.Image(` / `ImageDescriptor` /
`Image.FromBinaryData` return **zero** matches across `src/**/*.cs`. So the prohibition is a
**reachability** property, not a package-inventory one — which is why a `.csproj` name-denylist cannot
enforce it (§5) and why one `.Image(orderPhotoBytes)` inside an invoice or dispute-pack document would
create the primitive while the denylist stayed green.

**Why a decoder on a request path is still refused**, restated without the arithmetic error the panel
caught (CH-3i — Kestrel's 30,000,000 B ceiling means ≈21 MiB decoded per request, so "10 MiB × 30" was
never reachable and never needed):

- **One bounded upload already suffices.** A single-colour 30 000 × 30 000 PNG is a few hundred KB on
  the wire and ≈3.6 GB decoded. The array cap is irrelevant to the argument.
- **The plan is S1 / 1.75 GB** (`weu.prod.bicepparam:34`) and carries **the 5 APIs + SSR + Functions**
  (`appServicePlan.bicep:22`). DEV is **B2 with autoscale off** (`weu.dev.bicepparam:26`) — one fixed
  instance, and DEV is live.
- **Autoscale is CPU-driven only** (`appServicePlan.bicep:70,88` — both `CpuPercentage`). A decoder's
  failure mode is **memory**, so scale-out never fires and an OOM takes every site on the instance.
  *(This is a stronger argument than the one originally written here — panel, CH-3ii.)*

## 4. The decided shape — panelled 2026-08-06, transcribed into ADR-0043

1. **No shared sanitizer seam.** The shareable things already are shared (`BlobFileSize`,
   `ServedContentType`, the roster). A metadata transform is not shareable — JPEG segments, PNG chunks
   and PDF object graphs have nothing in common but the word. **The shareable part is the *obligation*,
   and its home is a roster column, not an interface.**
2. **No decoder, ever, on a request path.** Metadata is removed by **container rewrite**: drop JPEG
   `APP1`/`APP13` (re-emitting a minimal EXIF carrying only `Orientation`), PNG `eXIf`/`tEXt`/`iTXt`/
   `zTXt`/`tIME`, WebP `EXIF`/`XMP ` chunks + the `VP8X` flag bits. GIF passes through. **Call it a
   metadata scrub, not a sanitizer** — it does not remove ICC, JPEG `COM`, or anything inside the image
   data, and saying otherwise oversells it.
3. **Applied by audience:** order photos and dispute evidence **yes**; the avatar **no**, recorded with
   an expiry (the ticket that first emits an avatar URL on a cross-user DTO owes it); employee
   documents **no** (PDF/OOXML rewriting refused, exclusion written on the roster).
   ⚠️ **The RULING survives the panel; the REASON does not.** Order photos are scrubbed because their
   audience is **not enumerable at upload time** — `GetOrderPhotos.cs:59` gates on `CanBrowseOrderAsync`,
   and `OrderAccessService.cs:68-92` admits **any** tenant cleaner while the order has an open seat — not
   because it is "customer + cleaner + admin". See §8 / CH-2c and the corrected §2 note.
4. ~~**Narrow the accept set to the serve set**~~ — **SHIPPED.** `SniffedContentType.Signatures:66-78`
   carries no BMP or TIFF and matches WebP as `RIFF`@0 + `WEBP`@8. This is a **ratification**, not a
   decision the ADR gets to make (§8 / CH-1).
5. ~~**Widen the roster to 14 rows**~~ — **SHIPPED.** `UploadIntakeRosterTests.cs:39-55` is 14 rows,
   plus a `[Theory]` at `:76-84` naming the four non-`BlobFileDto` intakes. **Only the two extra columns
   (`audience`, `scrub`) remain outstanding**, and the widening is `T1-CI` **today** — the ADR mis-tiered
   this in both directions (§8 / CH-1).
6. **The law is a new S12**, keyed on **audience**, not on "served back by URL" — because the surface
   carrying the most metadata (employee documents) is not served by URL at all. Not an S4 extension:
   same principle, but S4's check is "read the DTO's field list," and no reading of a field list reaches
   inside a byte array. ✅ **WRITTEN, 2026-08-07 — `docs/architecture/security-rules.md` §S12**, with the
   audit-checklist item 12, the per-clause enforcer/tier table, and the incident named. The header now
   reads S1–S12 (it read "S1–S10" while S11 existed), and the count was swept across `agents/` and
   `.claude/`.
7. **The web clients re-encode on pick, and that ships FIRST.** Both mobile clients already do; no
   `canvas`/`createImageBitmap` exists anywhere in `src/Cleansia.App`. ~30 lines per picker, zero server
   cost, removes essentially all live volume.

### The threat-model inversion this rests on — ⚠️ **scoped to the avatar by the panel**

T-0458 argues the server work is required because *"a client-side strip is unenforceable."* That is
decisive for XSS, where **the uploader is the adversary**. The draft generalised the inverse — *"for
metadata the uploader is the victim"* — to all four surfaces. **The panel scoped it to one** (§8 /
CH-2). It is a claim about **whose metadata is in the file**, and:

- **It is unknowable in the ordinary case.** No intake establishes capture provenance; `SaveOrderPhotos.cs:114-117`
  proves *assignment*, which is an authorization fact, not a capture fact. A cleaner may upload a photo a
  colleague sent them (CH-2a).
- **On dispute evidence the uploader is an adversary with money on the outcome.** `UploadDisputeEvidence.cs:95-99`
  refuses unless the uploader **is the dispute's own customer**, and the outcome is a refund against a
  cleaner. "The client strips it" is not a control against a party whose interest is to not strip — so
  **the scrub there is enforceability, not durability, and it may NOT be deferred behind the web
  re-encode** (§8 / B.5). *Severity bound, recorded: no surface reads EXIF today, so this is latent, not
  live — it bounds urgency, not the availability of the deferral.*
- **It holds on the avatar, which is the surface the ADR exempts.** The only fetcher is the subject, so
  **T-0446 disclosed nobody's EXIF to anyone.**

The genuinely new disclosure on order photos, restated: device identity, capture timestamp and — if the
photo was taken away from the job — the cleaner's own location, handed **not** to a known triangle but to
**any tenant cleaner who can browse the order while a seat remains open**. GPS taken at the job is the
customer's own address, which all parties already hold; a **device serial is a stable cross-order
correlation key** that walks straight through the two controls that deliberately withhold cleaner
identity (`GetOrderPhotos.cs:107-109`, ADR-0036). That is the S12 argument in its purest form — a
DTO-level control defeated by content.

## 5. Enforcement (ADR-0032 tiers — one clause is live, four are pending)

*(Table replaced by the panel, 2026-08-06 — §8 / B.6. The previous version mis-tiered two rows in
opposite directions and named an enforcer that cannot see its clause's real failure mode.)*

> ✅ **This table is now the DESIGN-TIME record. The enforceable one is `security-rules.md` §S12
> "Enforcement"** (T-0460, 2026-08-07), which carries eleven rows rather than seven — because writing it
> against HEAD split three of these and added the enforcers T-0459 shipped. **Two rows below are
> superseded by it and are corrected in place; where the two ever disagree, S12 wins.**
> - *"The scrub actually removes metadata"* is **`T1-CI` today**, not `(gate pending: T-0459)`: three
>   per-pipeline suites read the bytes handed to the blob client and each dies to its own call site.
>   The dispatch-on-bytes, orientation-degradation and avatar-exemption clauses got their own rows and
>   are `T1-CI` too.
> - *"Every intake declares audience + scrub"* keeps `(gate pending: T-0458)` **and the reason is worse
>   than stated here**: `UploadIntakeRosterTests.cs:66-68` compares `entry.Split(" — ")[0]`, so the
>   existing annotation is asserted by **nothing**. Adding two columns to a string no test reads buys
>   nothing; the replacement gate owes a failure-identity assertion and a positive control per case.
> - **One row S12 adds that this table has none of:** the avatar exemption's *expiry* is
>   `(guidance — no gate)` — no shipped mechanism sees an avatar URL reaching a cross-user DTO. A
>   wire-surface assertion in the `PayoutDtoSurfaceTests` shape would close it; **ticket owed.**

| Clause | Enforcer | Tier |
|---|---|---|
| Served type is server-derived from a closed set | `ServedContentTypeTests`, `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`, `SasResponseHeaderOverrideTests` (`Cleansia.Tests`, a named step of `backend-ci.yml:70-71`) | **T1-CI** |
| Accept set ⊆ serve set | **true by construction today** — one `Signatures` table + `AcceptedByIntake` (`SniffedContentType.cs:66-104`) — but **unpinned**: `ServedContentTypeTests` carries no assertion that every `Signatures` MIME resolves to a non-`Opaque` `ServedContentType`, so a seventh row reintroduces the defect silently | `(gate pending: T-0458)` — for that reason, not the one previously written here |
| The roster **enumerates** all 14 intakes | `UploadIntakeRosterTests` (`:39-55` + `:76-84`) | **T1-CI — shipped** |
| Every intake **declares** audience + scrub | the same test, with two added columns | `(gate pending: T-0458)` |
| The scrub actually removes metadata | per-pipeline tests reading metadata out of the bytes handed to the blob client | `(gate pending: T-0459)` |
| No **direct package reference** to a decoder | `.csproj` denylist walk (`SixLabors.*`, `SkiaSharp*`, `System.Drawing.Common`, `Magick.NET*`) + non-vacuity floor | `(gate pending: T-0458)` |
| No **call site** reaching a decoder — incl. QuestPDF's transitively-shipped Skia (§3) | source scan of `src/**/*.cs` for `.Image(` / `ImageDescriptor` / `Image.FromBinaryData`, non-vacuity floor. **If T-0458 cannot build it, this clause is declared `T2-ADVISORY` with a named reviewer check — it is not left labelled as a gate** | `(gate pending: T-0458)` |

**The rule must not be labelled `T1-CI` wholesale.** `enforcement.md:177-179` provides
`(gate pending: <ticket>)`, and note that `check-consistency.mjs` is **T2-ADVISORY** (in zero
workflows) and the frontend lint step is `continue-on-error: true` — neither can carry a law here.

## 6. Open / owed

- ~~**Panel.**~~ **RUN, 2026-08-06.** Author + independent challenger + lead, all distinct instances.
  Verdict **REVISE** (rulings survive; map and several reasons rewritten) — **§8**.
  ~~rev N+1 owed~~ **REV N+1 LANDED, 2026-08-06, as `ADR-0043` (`proposed`).** It applied §Verdict §C's
  twelve items and nothing else, re-opened every surviving `file:line` at HEAD, and restated **D5/D6 as
  ratifications** of shipped code rather than decisions. **What remains is the PM's acceptance stamp**,
  checked against §Verdict §C only — T-0458 AC1 is satisfied at that point, and it is the sole gate on
  T-0459.
- ~~**The challenge I most want pressed:**~~ **ANSWERED — split per surface.** Deferring the scrub
  behind the web re-encode is available for **order photos** (the argument there is durability) and
  **not** for **dispute evidence** (adversarial uploader — §8 / B.5).
- ~~**The second** (hand-rolled parsers)~~ — **ANSWERED as a condition.** Sustained as a real cost;
  answered by construction (forward-only, length-prefixed, refuse-never-repair) **plus the new,
  stronger property: no attacker byte reaches the output** — the emitted `APP1` is server-synthesized
  end to end. Condition: the §8 / B.4 degradation rule and the synthetic-corpus burden are written into
  rev N+1.
- ~~**The avatar exemption**~~ — **CHALLENGED AND SUSTAINED.** The challenger independently verified it
  (`GetCurrentUser.cs:44,47-60` is the only `user-files` SAS mint; `UserMappers.cs:23,66` and
  `EmployeeMappers.cs:37,63` carry no URL; `GdprExportDto.cs:85-90` carries file **names**, not bytes or
  URLs) and **could not improve on the author's own mitigation.** The exemption holds, with its expiry.
- **Escalation Q-ART-01 (owner):** keep accepting DOC/DOCX on employee documents? They carry author
  names and revision history, no scrub is proposed, and an OOXML rewriter is not worth building.
  Dropping them changes a five-locale promise. Product call.
- **Backfill.** Blobs uploaded before PR #154 (2026-07-26) plus every web upload since carry metadata.
  **It cannot be fixed on the read path** — the SAS hands the client the stored bytes — so unlike the
  content-type residue this needs a real migration. Own ticket, after the panel.
- **Unexamined, and worth someone's time:** whether `UploadOrderPhoto` should be **deleted** rather than
  hardened (it duplicates `SaveOrderPhotos` for one photo and R1 is entirely its fault), and whether the
  four `byte[]`/`IFormFile` routes should become `BlobFileDto` so one roster predicate and one validator
  family covers all fourteen.
- **Adjacent, out of scope, still open:** `X-Content-Type-Options: nosniff` is set by **no** host
  (`patterns-backend.md:1306`) and real Azure sends none on a SAS fetch (owner-verified on DEV,
  T-0464 status log). The closed served-type set is the control; a header would be defence in depth and
  is a Bicep change. Storage-account CORS is a separate live gap (T-0447 C2).

---

## 7. The content-type question — ruled 2026-08-06 (two drafts, panel owed)

The T-0556 follow-up brought thirteen of the fourteen intakes onto a byte-derived stored type and routed
two calls here rather than deciding them. Both are now drafted. **Neither is `accepted`.** §7.1 has had
an **independent challenge round** (`backlog/adr/challenges/0044-stored-content-type-byte-derived.md`,
five blocking findings), the author's **rev 2** answered it, and a **lead ruled (2026-08-06): verdict
REVISE on a closed twelve-item list, zero blocking challenges surviving, no further round.** That list
is now **transcribed (rev 3)** and the decision has **landed as `ADR-0044`, status `proposed`** — the
author does not accept their own ADR; the PM checks rev 3 against §E only and stamps it. §7.2 is still
an author-mode draft awaiting both.

### 7.1 `SaveOrderPhotos` — the exception closes

`backlog/adr/0044-stored-content-type-is-byte-derived-on-every-intake.md`
*(was `backlog/adr/drafts/NNNN-…`; the draft path is a tombstone)*

**The trade-off space, so the next reader does not re-derive it.** ⚠️ **Rewritten 2026-08-06 after the
independent challenge round** (`backlog/adr/challenges/0044-stored-content-type-byte-derived.md`). Three
rows changed: the read path was one row and is three; the `Opaque` row's why-not was backwards; the
refuse-vs-`Opaque` branch is now an owner call.

| Option | Refusals it introduces | Why not (or why) |
|---|---|---|
| **Keep the exception, document it honestly** | none | Its justification bounds the served type to *inert*, not to *right*; its fallback invents a fact; it hides a live 500; and its cost is zero — a carve-out with no cost behind it buys nothing and forbids stating the rule |
| **Sniff at intake → refuse on failure** ← **chosen seam, default branch** | web-only, and only for a file whose browser-derived type disagrees with its bytes | Matches `UploadOrderPhoto` on the same container/table/accept set. **The refuse-vs-store branch is escalated — `Q-ART-02`**; the seam (byte-derived type, minted extension, decodability rule) is identical either way |
| **Sniff at intake → store `Opaque` on failure** | **none at all** | ~~evidence silently lost~~ **that reason was backwards and is withdrawn**: under `Opaque` the bytes are stored and download rather than preview (the same cost §7.2's D3 names), while a refusal the cleaner does not retry loses the photo outright. What survives is a **usability** claim, plus symmetry with the sibling endpoint — a tie-breaker, not a ruling. Hence `Q-ART-02` |
| **Narrow the READ clamp to the intake's accepted set** ← **also chosen (a complement, not a substitute)** | none | Closes the `application/pdf`/`image/gif` capability on **every row including those already written**, which the write-path rule cannot reach. Reads no bytes, changes no client, needs no migration. Already obliged by `patterns-backend.md:1371-1373` — *"the read path reads the intake's own signature table"* — which `GetOrderPhotos.cs:96` violates today. **Cost: a legacy GIF/PDF row downloads instead of rendering.** Row count owed as an owner query |
| Re-derive the served type from the bytes on read (the document technique) | none | **Structurally unavailable.** The `GetOrderPhotos` → SAS path never holds the bytes; adopting it means downloading every photo on every gallery render |
| Set `rscd` (`Content-Disposition: attachment`) on non-image served types | none | **Routed, not rejected.** `patterns-backend.md:1359-1364` rules it out as *the* control, and `GenerateSasUri` is one shared mint whose dispute-evidence user legitimately previews PDFs — a product change on a surface §7.1 does not own. Belongs with the dispute/content-policy lane (its CH-7) |
| Delete `SaveOrderPhotos`, route everyone to `UploadOrderPhoto` | none | Right direction, wrong ticket — wire change across 3 generated clients + 2 shipped apps, and it drops the 30-photo batch the web picker uses. **Cheaper after the ruling than before it** |

**Why BOTH rows are chosen, in a form a reviewer checks by reading (lead, §A).** The two govern disjoint
populations, and the check is three steps: (1) `AcceptedByIntake[OrderPhoto]` ⊆ `ServableTypes`' values
and `ForRecordedType` is idempotent on each of the three, so the narrowed clamp is the **identity** on
every row the sniff writes — it is not doing the write rule's work; (2) the clamp acts non-trivially only
when the recorded value falls outside the accepted set, which the sniff makes unwritable, so every row it
changes predates the sniff — disjoint **by construction**; (3) each reaches what the other cannot — the
sniff alone gets a true column, the 500, and the container-bytes invariant; the clamp alone gets the rows
already written, which no write rule reaches and which `ForDownload` cannot reach either because the SAS
path never holds the bytes. **The stronger statement, and the one that stops a future reader deleting
whichever limb they meet first: each is the other's backstop** — the clamp against a write-path
regression, the sniff against a read path that skips the clamp, which is the defect this very method
already shipped once (`patterns-backend.md:1374-1377`).

**The row NEITHER reaches, stated rather than left to be rediscovered:** a pre-change row recorded
`"image/jpeg"` by the tier-3 literal (`SaveOrderPhotos.cs:186`) over bytes that are not JPEG. The write
rule is too late; the clamp is the identity on it because `image/jpeg` is in the accepted set. It stays a
broken tile, **inert**, exactly as today. So *"every recorded content type is now a statement about bytes
the server read"* is true only of rows written **from here on** — the ADR's Consequences bullet is
corrected accordingly (§E-2).

**The fact that decided it, and the one to re-check if this is ever revisited:** *both mobile clients
re-encode every pick to JPEG and cannot emit anything else* — iOS `ImageCompressor.swift:77`
(`UTType.jpeg`), Android `ImageCompressor.kt:248` (`Bitmap.CompressFormat.JPEG`), both emitting
`photo.jpg` and bare base64. **The "it would break a live mobile path" objection is false**, and the set
of uploads that succeed today, render correctly, and would newly fail is **empty**.

**A second, independent lane reached the same verdict from the other direction.** The challenge on the
content-policy draft (`backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md` CH-4,
`c6370115`) attacks that draft for electing this surface as its metadata-scrub **pilot**: a per-format
scrub dispatching on the client's `data:` prefix runs the PNG chunk walker over JPEG bytes when the
uploader says so — **a no-op the attacker selects, under a green "scrub applied" test**
(premise pinned by `SaveOrderPhotosContentTypeTests.cs:49-59`). That is a stronger argument than "the
stored type is a lie", because it is about a future control being **unbuildable** rather than a fact
being wrong. It does **not** decide between the chosen option and the `Opaque` alternative — a
byte-derived `octet-stream` would make the scrub a *declared* no-op, which is fine.

> ⚠️ **Superseded 2026-08-06 by §8.2, and the correction runs against this lane.** The content-policy
> panel ruled that the scrub dispatches **from the bytes it is holding, at the moment it runs — never a
> client string, never a persisted `ContentType`, not even a correct one** (that challenge's repair
> **(a)**, not **(b)**). Two consequences, both recorded in the ADR's rev 2:
> **(i) ~~the closing ticket blocks T-0459~~ — WITHDRAWN.** A scrub that sniffs its own bytes is
> decision-complete on this surface today (§8.3). Sequencing the closing ticket first is preferable, not
> required; **the PM should remove `blocks: T-0459` from `T-0561`.** A false dependency in the backlog is
> worse than none.
> **(ii) The "unbuildable future control" argument is struck as a justification** for closing the
> intake. It killed the status quo for a control that no longer needs it. What carries the ruling is the
> capability (`application/pdf`/`image/gif` storable and servable here and nowhere else), the invented
> fact of the `"image/jpeg"` fallback, the live 500, and a cost of zero.

**Two audience facts re-verified here, because they change what the ruling is worth (CH-2c, CH-3iii):**

- `GetOrderPhotos.cs:59` gates on **`CanBrowseOrderAsync`**, not `CanAccessOrderAsync`
  (`OrderAccessService.cs:68-92`, comment at `:84-87`). Writing still requires assignment
  (`SaveOrderPhotos.cs:115-118`); **fetching does not.** Any tenant cleaner who can see the order while
  a seat remains open can mint a SAS for its photos — so the `application/pdf`-over-arbitrary-bytes
  capability is planted for an audience that is not enumerable at upload time.
- **A decoder is already deployed, and this ruling does not depend on it either way.** QuestPDF
  2024.12.1 ships native Skia + libjpeg-turbo/libpng/libwebp as runtime assets
  (`Cleansia.Infra.Services/obj/project.assets.json:832-864,2362-2364`, verified); the *call site* is
  what is absent. §3's "nothing decodes" is therefore a statement about **call sites**, not about the
  image on the box — correct §3 accordingly when that ADR is re-based. §7.1 leans on **neither** limb.

**Current shape (what the closing ticket implements) — ⚠️ two-sided after the challenge round:**

- **Write side.** A third `AbstractValidator<BlobFileDto>` family member, `PhotoFileValidator`
  (presence → size → sniff → decodability, `.WithErrorCode(nameof(BlobFileDto))` on every rule like both
  siblings), consumed by `SaveOrderPhotos.Validator` via `SetValidator` inside the existing `ChildRules`
  block — **not** a fourth inline copy of that chain. `DetermineContentType` deleted; blob-name extension
  minted via `SniffedContentType.ExtensionFor`. The decodability rule closes a **live 500**
  (`SaveOrderPhotos.cs:137` calls `Convert.FromBase64String` unguarded) and is unconditional on
  `Q-ART-02`.
- **Read side — new, and it is the repair for the finding the ruling leads with.** The clamp is
  **narrowed to the intake's accepted set** in one named function
  (`SniffedContentType.ServedFor(recorded, intake)`, same assembly, returning a `ServedContentType`), and
  `GetOrderPhotos.cs:96` calls it. `ServedContentType.cs` is **not** modified. After the write-side
  change the clamp is the **identity** on every row the new intake writes, so its whole effect is on rows
  written before it — precisely the population a write-path rule cannot reach.
  **`T-0561`'s "out of scope: the read-path clamp" and "read-only: `GetOrderPhotos.cs`,
  `SniffedContentType.cs`" are struck; `ServedContentType.cs` stays read-only.**
  ⚠️ **The order inside `ServedFor` is load-bearing (lead, §E-3).** Resolve `ForRecordedType(recorded)`
  **first**, then membership-test the **result's `.Value`** — never the raw recorded string.
  `ServableTypes` maps `image/jpg → image/jpeg` (`ServedContentType.cs:37`), and rows recorded
  `image/jpg` exist (`UploadOrderPhoto.cs:39` allows the alias and that endpoint stored the client string
  until the T-0556 follow-up), so the wrong order demotes real JPEGs to `Opaque`. One test case pins it.
- **The invariant the composition now rests on, found by the lead and pinned by nobody yet.** ADR-0043's
  scrub has **shipped on both order-photo handlers** (`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`),
  so the recorded type describes the **submitted** bytes while the blob holds the **scrubbed** ones. They
  agree only because `ImageMetadata.Rewrite` (`:42-57`) dispatches to a walker whose own `Identifies`
  matched and returns the input unchanged otherwise. Pin it:
  `SniffedContentType.FromContent(ImageMetadata.Scrub(p).Bytes, intake) == SniffedContentType.FromContent(p, intake)`
  over every `(intake, accepted type)` — `T1-CI`, zero baseline, and it spans `UploadDisputeEvidence` too,
  so the generalized pin is its own small ticket.
- **Messages.** No new `BusinessErrorMessage` key — but not the document family's either.
  `FileTypeNotAllowed`'s five-locale value is *"Accepted: PDF, JPEG, PNG, DOC, DOCX"*, the **document**
  promise (`SniffedContentType.cs:83-86`), and partner web is the only client that can reach the new
  refusal. The sniff and decodability rules use `FileNotMatchContentType`
  (`file.content_type_doesnt_match`) — the **image** family's key, what `ImageFileValidator` already
  uses, present in all five partner-web locales (`:1216`), all five Android partner locales and iOS.
- **Carrier ruling.** On `OrderPhoto` the **`ContentType` column** is authoritative (read at
  `GetOrderPhotos.cs:96`); the minted blob-name extension is defence in depth that no read path consults.
  That does **not** contradict §7.2's refusal of a `DisputeEvidence.ContentType` column: **two carriers
  minted from one derivation is redundancy; two carriers written from two derivations is ambiguity.**
  This is the **shared premise of both drafts** and the lead adopted it for both.
  ⚠️ **Corrected by the lead (§D-4): `OrderPhoto` is COVERED by §7.2's round-trip test, not exempt.** Its
  three pairs round-trip today (`.jpg`/`.png`/`.webp` → `image/jpeg`/`image/png`/`image/webp`,
  `SniffedContentType.cs:69,70,72` × `ServedContentType.cs:44-52`), so exempting it would delete the only
  assertion that could ever catch the minted extension drifting — leaving it written by one endpoint,
  read by no path and asserted by nothing, which is the state CH-9 objected to. `EmployeeDocument`
  remains the **only** named exemption.

**Escalated, not decided:** whether a photo failing the byte check is **refused** or **stored
un-previewable** — `Q-ART-02` (`backlog/questions/open.md:1480-1505`), filed, owner, `resolve-by:
pre-prod`. The architecture is complete either way; the default if unanswered is *refuse*, on
sibling-endpoint symmetry, which is a tie-breaker and is labelled as one.

**Owner measurement owed before merge:** `SELECT "ContentType", count(*) FROM "OrderPhotos" GROUP BY 1`.
It does not decide whether the read-side narrowing is right; it says how many existing photos change
from rendering inline to downloading on the day it ships.

**Blocked on:** ~~the lead's ruling, then the transcription of the lead's closed list, then~~ **the PM's
acceptance stamp, then the ticket.** Rev 3 landed 2026-08-06 as `ADR-0044` (`proposed`) carrying a
`## Transcription record` that maps each of §E's twelve items to where it went; §F (the `T-0561`
staleness) and §G (the `patterns-backend.md` callout's stale `SaveOrderPhotos.cs` citations) are routed
to the PM, not absorbed by that pass.
The catalog sentence is written in that ADR's D2 with tier `(gate pending: <closing ticket>)`. **Its
enforcer changed after the challenge round:** `UploadIntakeRosterTests` splits each roster row on `" — "`
and keeps `[0]` (`:66-68`), so **the annotation is enforced by nothing** — it is a `T1-CI` enforcer of
route *enumeration* only. D2 now names a per-intake refusal theory (count-first, driven off the roster
array) plus a vocabulary assertion, with an explicit `T2-ADVISORY` fallback if the theory cannot be built
in the ticket. Until then `patterns-backend.md` carries the exclusion **as a named, dated deviation at the
rule** — restated descriptively 2026-08-06, because two of its sentences read as normative and pre-decided
the "keep the exception" option the panel convened to weigh.

> ⚠️ **Lead ruling on that enforcer (2026-08-06, §B) — it is not yet one, and the fix is two clauses.**
> The refusal theory *can* fail a build on the shape it names, and `(gate pending: T-0561)` is the honest
> token (`conventions.md:234`, `:237-242` — the baseline is non-zero twice: `SaveOrderPhotos` and
> `GetOrderPhotos.MapToDto`). But **the count-first floor bounds the CASE SET, not the REASON.** The
> fourteen rows collapse to six validators with heterogeneous dependencies (`IOrderRepository`,
> `IDisputeRepository`, `IUserRepository`+`IUserSessionProvider`+`ILanguageRepository`,
> `ICountryRepository`+`IEmployeeRepository`+`IUserSessionProvider`+`ITaxIdValidator`), several running
> `MustAsync` existence checks — so `Assert.False(result.IsValid)` is green on any un-stubbed dependency,
> which is CH-4's own failure mode one level down. **Required:** (a) assert the failure's identity (the
> file property, that route's error code, that intake's content key), and (b) a **positive control** per
> case — the same command with an accepted payload validates clean.
> **And the vocabulary companion is over-claimed by one clause:** it is green on a new unguarded intake
> whose author writes a validator name in the annotation. It is a genuine `T1-CI` enforcer of the
> *narrower* clause (closed vocabulary, no row reads `only`) and must say so — `conventions.md:246-250`.

### 7.2 `DisputeEvidence` — the column is refused, the round-trip is pinned

`backlog/adr/drafts/NNNN-dispute-evidence-type-carrier-is-the-blob-name.md`

**Ruling: the server-minted blob-name extension is sufficient. No migration.** The name is
content-addressed — minted from the bytes in the same statement that reads them — which makes
`DisputeEvidence` the only upload surface with exactly **one** source of truth for its served type. A
column would give it two, and the sibling that has two (`OrderPhoto`) is the one that shipped the
"client believes the wrong one" defect the follow-up had to fix.

**The real gap is elsewhere, and it is cheap:** the carrier depends on `SniffedContentType.ExtensionFor`
and `ServedContentType.ForFileName` agreeing in both directions, across an assembly boundary
(`Core.AppServices` vs `Core.Blobs.Abstractions`, which cannot reference it), with **no test**. It holds
for all four accepted types today; one of four is exercised. `.doc`/`.docx` are already in the signature
table and unknown to `ServedContentType`, so a one-line widening of an accepted set silently demotes a
whole surface. Pin it with a property test over every `(intake, accepted type)` pair, count-asserted
first, exemptions named — `T1-CI`, zero baseline.

**Failure mode if this ruling is wrong:** evidence downloads instead of previewing for the customer and
the adjudicating staff member. Silent capability loss on a support path; never a security failure (the
demotion direction is `Opaque`-ward by construction).

### 7.3 Found while verifying 7.2 — not a content-type problem, and larger

**GDPR erasure orphans every dispute-evidence blob and destroys the only pointer to it.**
*(Unchanged by the panel — recorded here because it belongs to neither ADR.)*
`GdprDeletionService` deletes blobs for `user-files` (`:134-135`), `employee-documents` (`:146-157`) and
`order-photos` (`:164-180`), then calls `dispute.Anonymize()` (`:210-212`) → `evidence.Anonymize()`
(`Dispute.cs:160-163`) → `FilePath = AnonymizationMarker.Value` (`DisputeEvidence.cs:37-42`). The
`dispute-evidence` container is **never touched**, and after the marker is written nothing in the
database can name the blob to delete it later. **Ordering matters: any deletion sweep must run before
`Anonymize()`.** Needs its own ticket against `GdprDeletionService`, `security_touching: true`. Recorded
here so it is not lost between two ADRs that are not about it.

---

## 8. Panel verdict on the content policy — 2026-08-06 (the current shape)

Author (2026-08-05) · independent challenger
(`backlog/adr/challenges/NNNN-user-artifact-content-policy-threat-model.md`, 2026-08-06) · lead
(2026-08-06), all distinct instances. Full trail and the closed change list live in the ADR's `## Verdict`
— **`backlog/adr/0043-user-artifact-metadata-is-scrubbed-at-intake-by-audience-without-a-decoder.md`**.

**Outcome: REVISE — the rulings survive, the map and several reasons do not. No further challenge round.**
~~Rev N+1 is a transcription pass~~ — **rev N+1 landed 2026-08-06 as ADR-0043 (`proposed`)**, applying
§Verdict §C's twelve items and nothing else; **D5 and D6 are restated there as ratifications of shipped
code, not as pending work.** The PM checks it against §C and accepts.

### 8.1 What survived unchanged (do not re-litigate)

- **No decoder on a request path.** The central ruling survived every attack, and CH-3(ii) strengthened
  it (memory-blind autoscale, seven sites per instance).
- **Removal by container rewrite, not re-encode** (JPEG `APP1`/`APP13`, PNG `eXIf`/`tEXt`/`iTXt`/`zTXt`/
  `tIME`, WebP `EXIF`/`XMP ` + `VP8X` bits, GIF passthrough). It is a **metadata scrub**, not a sanitizer.
- **The seam location** — intake, in the handler, between the decode and `UploadAsync`. A FluentValidation
  validator cannot mutate; a decorator on `IBlobContainerClient.UploadAsync` would rewrite our own
  generated invoices/receipts/GDPR exports; the read path cannot touch bytes a SAS serves directly.
- **No `IImageSanitizer` seam** — the shareable thing is the *obligation*, and its home is a roster column.
- **The avatar exemption**, with its expiry (challenged, independently verified, sustained).
- **Audience, not delivery mechanism, is the hinge of the law** (S12), and S12 is not an S4 extension.
- **No backfill obligation** from the rule; it is a real migration and its own ticket.

### 8.2 What the panel changed

| Ruling | Change |
|---|---|
| **Generation loss** | **Not** a rejection ground for re-encode. The platform already re-encodes at q0.7 / 1920 on both mobile clients (`ImageCompressor.swift:31-32`, `.kt`). A1 is rejected on the **resource** and **PDF-generality** limbs alone — both falsifiable-proof, and the rejection is over-determined without the weak limbs |
| **The ImageSharp licence** | **Deleted as a rejection ground.** The repo already ships a revenue-threshold-licensed graphics package (QuestPDF). The decision does **not depend** on the licence, because no library is adopted; if an ADR ever overrules the no-decoder ruling, the licence is an **owner/legal** question filed then — never an architect's finding |
| **A4 (strip specific tags)** | Rejection **restated**, not re-scored. The parser-size argument is conceded (both designs need an IFD reader). A4 loses on **allowlist vs denylist** (a tag denylist ages with every new `MakerNote` variant) and because **A4 re-emits attacker bytes with rewritten offsets** while the chosen design emits a server-synthesized `APP1` containing one validated value |
| **EXIF `Orientation`** | **Completed.** Preserve **iff** the source `APP1` reads unambiguously and yields 2–8; on anything else — malformed IFD, unexpected byte order, out-of-range value, truncation — emit **no EXIF at all** and accept the rotation. **Never guess, never repair.** A rotated photo is a rare cosmetic defect on a largely adversarial branch; a corrupted photo or a surviving GPS tag is not. Because D10 will leave this branch with near-zero production exercise, it carries a **synthetic corpus** burden: truncated segments, garbage lengths, both byte orders, orientation 1 / 2–8 / 9 / absent |
| **What the scrub dispatches on** | **From the bytes it is holding, at the moment it runs.** Never a client string, never a persisted `ContentType` — not even a correct one. An unidentifiable format is **passed through untouched and reported as "not scrubbed"**, never as "scrubbed". This is stricter than, and consistent with, §7.1's ruling, and it means **T-0459 is not gated on §7.1's closing ticket** |
| **The deferral option** | **Per surface.** Deferrable for order photos (durability); **not** for dispute evidence (enforceability — the uploader is the adversary) |
| **Enforcement** | Replaced — see §5. The no-decode prohibition is a **reachability** property and needs a call-site enforcer; the roster widening is `T1-CI` **today**; accept ⊆ serve is true by construction but unpinned |
| **The document exclusion (D8)** | **Scoped per surface** — §4 item 3's "employee documents no" was one reason doing two jobs. For **employee documents** all three limbs hold (mechanism cost, audience-already-has-more, `attachment`-only delivery), with an **expiry**: no admin-side document upload exists today (`UploadIntakeRosterTests.cs:45-46,50-51`), and if one is added the audience limb inverts. For **dispute-evidence PDFs** (`application/pdf` is in that intake's accepted set, `SniffedContentType.cs:92-95`) only the **mechanism limb** survives: the audience limb is false (customer → cleaner → adjudicating staff — "already has more" is true of nobody) and the delivery limb is false (served **inline**, no `rscd`, `BlobContainerClient.cs:93-110`). So the exclusion there rests on cost alone, **the one-sentence evasion is named rather than hidden** (wrap the photo in a PDF), and narrowing the accept set is escalated as **Q-ART-01(b)** — product, not architecture |

### 8.3 Sequencing this produces

1. **D10 (web clients re-encode on pick) ships first and independently** — best exposure/effort ratio,
   and it is a **complement**, not a substitute. Unpriced in the draft: there are **four** distinct web
   file-read call sites, not one per app, and the documents picker must **not** re-encode. The shared
   `file-transformation.utils.ts:127-129` sets `contentType` and `fileName`, both of which a canvas
   re-encode changes and `SaveOrderPhotos.DetermineContentType` reads.
2. **Rev N+1 of the ADR accepted** — the only true gate on T-0459.
3. **T-0459** — `UploadDisputeEvidence` (not deferrable) and `UploadOrderPhoto` are decision-complete
   today; `SaveOrderPhotos` too, under the bytes-in-hand rule. §7.1's closing ticket should still land
   first if the PM can sequence it, but it is **not** a blocker.
4. **Backfill** — its own ticket, after the panel. Bounded by PR #154 (2026-07-26) plus web uploads since.

### 8.4 Open with the owner (not blocking)

**Q-ART-01, now two-part** — whether to keep accepting **DOC/DOCX on employee documents** and
**`application/pdf` on dispute evidence**, given that neither will be scrubbed and an OOXML/PDF
object-graph rewriter is refused. Both are **product** narrowings, not architecture. D8 is scoped per
surface either way and the roster records `scrub: none` with its reason regardless of the answer.
