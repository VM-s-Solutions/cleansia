---
id: T-0460
title: SECURITY RULE — user-supplied artifacts served back by URL are sanitized at upload (magic-byte validation is not a sanitizer)
status: in_review
size: S
owner: architect
created: 2026-07-30
updated: 2026-08-07
depends_on: []
blocks: []
stories: []
adrs: [0043]
layers: [architect, docs]
security_touching: false
manual_steps: []
sprint: 14
---

> ⚠️ **Reopened 2026-08-06.** I closed this at `6a901ed0` citing `f837e0ec`. That commit is
> an **unratified draft ADR**, not an implementation — every AC here is still unticked and the panel
> has not run. Blocked on the content-policy draft reaching `accepted`.

## Context

Filed from the **T-0446 security gate** (finding **SEC-5**). Full write-up:
`agents/archive/2026-08/backlog/security/user-profile-avatar.md`.

> ## 🛑 GATE 0 — 2026-08-05. **The gap is now HALF closed in code, and the proposed wording has two
> defects. Read the draft ADR before writing the rule.**
>
> Verified at HEAD by the architect (author mode), after T-0464 `b9753e85` / T-0548 `97bb7265` / T-0556:
>
> - **The served-type half of the proposed rule is already shipped and already enforced.**
>   `ServedContentType` (closed value type, private constructor, `text/html` and `image/svg+xml`
>   excluded by name, unknown → `application/octet-stream`) decides the served type on the **read**
>   path, so it governs blobs written before it existed; `DocumentContentType.ForDownload` does the same
>   for the two employee-document download handlers. Pinned by `ServedContentTypeTests` (26 cases),
>   `EmployeeDocumentDownloadContentTypeTests`, `EmployeeDocumentDownloadDispositionTests`,
>   `SasResponseHeaderOverrideTests` — all in `Cleansia.Tests`, a named step of `backend-ci.yml:70-71`,
>   i.e. **T1-CI today**. It is written down in `patterns-backend.md:1274-1315` as a *backend
>   convention*. **What is missing is that it is a LAW, and the content half.**
> - **Defect 1 — "served back by URL" is the wrong hinge.** Employee documents are **never** served by
>   URL (three API routes, `File(bytes, type, name)` → `Content-Disposition: attachment`) and they are
>   the surface carrying the **most** metadata — PDFs carry `/Author`/`/Producer`, DOCX carries revision
>   history and author names. A rule keyed on the delivery mechanism excludes its own worst case. The
>   draft ADR keys the rule on **audience**: *does a fetcher who is not the uploader receive these
>   bytes?*
> - **Defect 2 — the proposed wording mandates "re-encoded", and the draft ADR refuses re-encoding.**
>   No user image is decoded server-side anywhere today (zero `SixLabors`/`SkiaSharp`/`System.Drawing`/
>   `Magick` references in `src/**/*.csproj`); a decoder on a request path fed 10 MiB × 30 items on an
>   S1 / 1.75 GB shared plan **creates** a decompression-bomb primitive. A law that mandates it would be
>   violated by four of five surfaces on the day it is written.
> - **AC5 answer, pre-computed:** the rule **is** mechanically checkable — but only in parts, and one
>   part is live while four are `(gate pending: …)`. The full per-clause enforcer/tier table is in the
>   draft ADR §D7 and the living doc §5. **Do not label it `T1-CI` wholesale**; and note
>   `check-consistency.mjs` is **T2-ADVISORY** (zero `.github/` workflows) and the frontend lint step is
>   `continue-on-error: true`, so neither can carry this.
> - **AC4 is already one worse than stated:** `security-rules.md`'s own header reads **"S1–S10"** while
>   **S11** exists in the file. The header is part of the drift sweep.
>
> **Proposed S12 text, the S12-vs-S4 ruling, the "how wide is artifact" ruling and the retroactive-scope
> ruling are all in** `docs/decisions/adr-0043.md` **§D7
> / §D8 / §D9** (`proposed`, number not allocated, **panel owed**). This ticket remains the **sole
> writer** of `docs/architecture/security-rules.md`; the draft ADR deliberately does not edit it.

**This is a genuine gap in the rule set, not a violation of it.** Nothing in **S1–S11** addresses
**bytes embedded inside a stored artifact that is later served by URL**:

- **S4** governs **DTO fields** — what the handler puts in a response object.
- **S6** governs **logs**.
- **S8/S10** govern query scoping.

None of them reach *metadata inside a stored image*. That is why SEC-3 could sit undetected in
**three shipped upload pipelines** — order photos, dispute evidence and the avatar — without any gate
catching it. **The reviewers were not wrong against the rules; the rules were silent.** That is the
strongest possible argument for adding one, and it is the same argument that produced Gate 0.5
(T-0445) and the worktree rule (T-0456) this sprint.

The rule to add to `docs/architecture/security-rules.md`, in substance (**the panel owns the final
wording**):

> **User-supplied artifacts that will ever be served back by URL are sanitized at upload** — metadata
> stripped, dimensions/size bounded, re-encoded. **Magic-byte validation is an accept/reject check,
> not a sanitizer:** passing it establishes that the bytes are an image, not that the image is safe to
> hand back. The check that matters is not *"did we validate the upload?"* but *"what travels with
> these bytes when someone else fetches them?"*

## Deliberation required — NOT `ready`

**Architect panel** (author + 2–3 challengers + lead). A new law in the S-series is an architect call,
and the panel must settle more than the wording:

- **Is it S12, or an extension of S4?** S4 is "DTO leak prevention" — this is arguably the same
  *principle* (do not hand the client something you did not intend) at a different layer. A new
  number is more discoverable; an S4 extension is more honest about the shared root. Rule and say why.
- **How wide is "artifact"?** Images are the live case. PDFs carry author/producer metadata; Office
  documents carry revision history and author names. Employee documents are a **PDF** pipeline in this
  codebase. A rule scoped to images will need re-opening; a rule scoped to "any user-supplied file
  served back" may be unenforceable today. **Pick the honest scope and name what it excludes.**
- **What is the enforceable check?** `agents/knowledge/enforcement.md` distinguishes rules a reviewer
  reads from rules a checker asserts. A grep for `UploadAsync` reachable from a request-bound byte
  array without an intervening sanitizer is plausible; if it is not, say so — an unenforceable rule
  should be **written as guidance and labelled as such** rather than dressed as a law nobody can gate.
- **Retroactive scope.** Does the rule oblige an audit of already-stored artifacts, or only new
  uploads? T-0458/T-0459 deliberately excluded the backfill; the rule should not silently mandate it.

## Acceptance criteria

- [ ] **AC1** — The rule is added to `docs/architecture/security-rules.md` in the established S-series
      voice (statement of the law → the concrete failure it prevents → a reference to the code that
      complies once T-0458 lands). Evidence: the diff.
- [ ] **AC2** — The **audit checklist** at the foot of `security-rules.md` gains a corresponding
      numbered item, so the rule is reachable from the checklist a reviewer actually walks — not only
      from the prose above it.
- [ ] **AC3** — The rule cites the **real** finding that produced it (three shipped pipelines,
      `ImageFileValidator`'s 3–4 byte magic-prefix check, EXIF GPS on cross-user-visible order
      photos), because the S-series' authority comes from every rule naming the incident behind it.
- [ ] **AC4** — Any other agent doc that enumerates the security laws is updated in the same change so
      the count does not drift. **Find them first** — at minimum check `.claude/agents/security.md`,
      `agents/knowledge/enforcement.md`, `agents/knowledge/consistency.md` and the developer charters
      that say "walk S1–S10" / "S1–S11". Evidence: a grep for `S1-S1` / `S1–S1` across `agents/` and
      `.claude/` **in the PR body, with results**. Note that T-0446's own AC3 says "Walk S1-S10" and is
      already one rule stale — that drift is the failure mode this AC exists to stop.
- [ ] **AC5** — `enforcement.md` records whether the rule is **mechanically checkable** and, if not,
      says so explicitly rather than leaving it ambiguous.

## Out of scope

- Implementing any sanitization — **T-0458** / **T-0459**.
- Rewriting or renumbering the existing S1–S11. If the panel picks "extend S4" over "add S12", that is
  an edit to S4, not a renumbering of the series.
- The `CLAUDE.md` summary — **owner-gated**. If the panel thinks it needs a line there, **flag it to
  the owner**; do not edit it.

## Implementation notes

- **Precedent to mirror:** **T-0445** (Gate 0.5 → `process/quality-gates.md`) and **T-0456**
  (worktree/stash → `process/shared-file-lanes.md`) — both are approved process/knowledge changes
  routed as `architect` + `docs` tickets this sprint, because **the PM does not own
  `agents/knowledge/*.md` or `agents/process/*.md`**. Follow that shape.
- **Sequencing preference (not a hard dependency):** this should ideally land **before or alongside
  T-0458's panel**, so that ADR can cite the rule rather than invent the reasoning from scratch. It is
  deliberately left without a `depends_on` in either direction so neither can deadlock the other.
- **Shared-file lane:** `docs/architecture/security-rules.md` — this ticket is the **sole writer**. No
  overlap with T-0454 (`check-consistency.mjs`), T-0456 (`shared-file-lanes.md`) or T-0439
  (`quality-gates.md`).

## Status log
- 2026-07-30 — draft (created by pm from the T-0446 security gate, finding SEC-5)
- 2026-07-30 — **not `ready`**: awaiting the architect panel (a new S-series law is an architect call, per the T-0445 precedent).
- 2026-08-05 — **Gate 0 run by the architect (author mode).** The gap is half closed in code and the
  proposed wording carries two defects — see the GATE 0 block at the top. **The ticket survives and its
  premise is stronger, not weaker:** the reason SEC-3 sat undetected in three shipped pipelines was
  that no law reached inside a byte array, and that is still true; what changed is that the *served
  type* half now has shipped code and a live T1-CI enforcer to cite (AC1 asks for exactly such a
  citation, and it no longer has to wait for T-0458).
  Proposed S12 text + the four rulings this ticket's `## Deliberation` demands (S12 vs S4 · how wide
  "artifact" goes · what is enforceable · retroactive scope) are drafted in
  `docs/decisions/adr-0043.md` §D7–§D9, with the living
  doc at `agents/architecture/decisions/user-uploaded-artifacts.md`.
  **Still not `ready`:** that draft is `proposed` and its `## Challenge` is an author-run
  self-challenge; `process/deliberation.md` requires distinct author / challenger / lead instances.
  **The challenge the author flagged against his own S12 (C-4):** four clauses in one law may be four
  laws. If the lead rules they are separable, the natural split is *S12 = the disclosure law (audience +
  content)* with the served-type clause promoted into **S4** as a DTO-adjacent sentence — the author
  would accept that, and would not accept splitting out the no-decode prohibition, which is the reason
  the content clause takes the form it does.
  **Sequencing note, unchanged and now cheaper:** this ticket can land **ahead of** T-0458's
  implementation, because three of its five enforcer citations already exist at HEAD.
- 2026-08-07 — **WRITTEN (architect). `in_review`.** ADR-0043 reached `accepted` 2026-08-06, which
  discharged the "panel owed" block: this pass **transcribes** D7's text and §B.6's enforcement table
  and takes no new decision. S12 is in `security-rules.md`, the header reads **S1–S12**, checklist item
  12 exists, `enforcement.md` carries the mechanical-checkability answer, and the count sweep touched
  nine live docs. Two things changed against the ADR's own map, both because T-0459's scrub merged the
  same day: **the enforcement table grew from 7 rows to 11** (three shipped clauses that the ADR folded
  into one row now have their own, and the avatar exemption's *expiry* — which the ADR did not tier at
  all — is honestly `(guidance — no gate)`), and **five inherited `file:line` citations were corrected
  and three deleted** rather than carried. Full detail in `## Review`.
  **Two items leave here open, neither blocking:** the `CLAUDE.md:455` count is owner-gated (flagged as
  Q-ART-03 in `## Review`, deliberately **not** filed into `questions/open.md`, which another lane
  holds), and a ticket is owed for the avatar-expiry wire-surface assertion.

## Review

### Architect — 2026-08-07 — **written; ready for the docs/reviewer pass**

**ADR-0043 is `accepted` and assigns this rule to T-0460 (§E). This pass transcribes its D7 text and
its §B.6 enforcement table into `docs/architecture/security-rules.md` as **S12**. No decision was
re-taken.** Two transcription refinements are flagged below rather than made silently.

#### AC1 — the rule is added, in the S-series voice ✅

`docs/architecture/security-rules.md` §**S12 — What is inside a stored artifact is disclosed to everyone
who can fetch it.** Statement → the audience hinge (with the employee-document counter-example that
kills "served back by URL") → the three roster questions → the no-decoder prohibition → **the incident**
→ scope (new uploads only; not an S4 extension) → a per-clause enforcement table → a reviewer test.
The compliant code is cited throughout, not deferred: `ImageMetadata.Scrub(byte[])`
(`ImageMetadata.cs:35`) and its three call sites (`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`,
`UploadDisputeEvidence.cs:108`), `ServedContentType.cs:34-42,56`, `SniffedContentType.cs:88-104,127-128`,
`BlobContainerClient.cs:89-110`, `UploadIntakeRosterTests.cs:39-55,64,66-68,76-84`.

#### AC2 — audit checklist ✅

Item **12** added at the foot of the file, keyed on the three roster questions plus the two things that
are cheap to get wrong (the scrub taking a content type; a decoder becoming reachable).

#### AC3 — the finding that produced it ✅

Named in **The incident** paragraph: `ImageFileValidator` as a 3–4 byte magic-prefix check in front of
three shipped pipelines; `SaveOrderPhotos` reading its stored type off the client's `data:` prefix;
employee-document intakes storing the uploader's string; EXIF GPS and device serials on cross-user
order photos, defeating by *content* the two controls that withhold cleaner identity by *field*
(`GetOrderPhotos.cs:107-109`, ADR-0036). **Written in the past tense with a closing note that all three
intakes are hardened at HEAD** — `ImageFileValidator` now runs `SniffedContentType.FromContent(…,
UploadIntake.Avatar)` behind a size bound — because S8 already carries the rule that a security law must
not assert a live hole that has been closed.

#### AC4 — count drift swept ✅

`rg "S1[-–—]S1?[0-9]"` over `agents/`, `.claude/` (hidden dir — needs an explicit path; ripgrep skips it
by default, which is how it was missed before) and the repo root. **Live docs updated (9):**

| File | Was | Now |
|---|---|---|
| `docs/architecture/security-rules.md:1` | `S1–S10` (while S11 existed at `:319`) | `S1–S12` |
| `agents/README.md:86` | `S1–S10 non-negotiable security laws` | `S1–S12` |
| `agents/WAY-OF-WORKING.md:58,132` | `S1–S10 laws` | `S1–S12` |
| `agents/process/quality-gates.md:115` | Gate 3 walks `(S1–S10)` | `(S1–S12)` |
| `agents/knowledge/patterns-backend.md:583` | `(cache correctness, not S1–S10)` | `not S1–S12` |
| `.claude/agents/security.md:3,15,33` + its numbered walk | `S1–S10` ×3 **and a numbered walk that stopped at item 10** | `S1–S12`, **plus items 11 (S11) and 12 (S12)** — S11 was missing from the charter's walk too |
| `.claude/agents/backend.md:26` | `S1–S10. Non-negotiable.` | `S1–S12` |
| `.claude/commands/backend.md:16` | `(S1–S10 — non-negotiable)` | `(S1–S12 …)` |
| `.claude/commands/review.md:8` | `/review security # focus the S1–S10 security gate` | `S1–S12` |

**Deliberately NOT changed**, each for a stated reason:
- **`CLAUDE.md:455`** (`the S1–S10 security laws`) — **owner-gated** by this ticket's own §Out of scope.
  **Flagged to the owner**; one word, `S1–S10` → `S1–S12`.
- `WAKE-UP-SUMMARY.md:49,155` — describes the **archived** legacy YAML prompt system (`agents/_legacy/`),
  not the live catalog.
- `.claude/agents/db.md:16` — names `S8, S9, S10` selectively for the DB lane; it is not a count.
- Historical records — `agents/archive/2026-08/backlog/audits/*`, `agents/archive/2026-08/backlog/status/sprint-*`,
  `agents/archive/2026-08/backlog/security/*`, and closed tickets' `## Review` sections. Each says what was walked **on
  the day it was walked**; rewriting them would falsify a record, and one of them (T-0446 AC3) is the
  example this AC cites as the failure mode. The drift they represent is in the *live* docs above.

#### AC5 — mechanically checkable? recorded ✅ — **and the honest answer is "in parts"**

`agents/process/enforcement.md` gains **§"S12 — user-artifact content (upload intake) — mechanically
checkable in parts, and only in parts"**, beside the existing S11/E9 entry. It states the split, points
at S12's table as authoritative, and carries the two traps a future gate-builder walks into.

**The per-clause table in S12 — 11 rows, every enforcer opened at HEAD, not taken from the ADR:**

| Tier | Clauses |
|---|---|
| **`T1-CI`** (6) | served type is a closed set on the read path · the roster **enumerates** every intake · the scrub actually removes metadata · the scrub dispatches on bytes and reports honestly · orientation degrades without guessing · the avatar exemption is honoured |
| **`(gate pending: T-0458)`** (4) | accepted set ⊆ servable set · every intake **declares** audience + scrub · decoder **package** denylist · decoder **call-site** scan |
| **`(guidance — no gate)`** (1) | the avatar exemption's **expiry** |

Three enforcers were checked and found to enforce **less than their name suggests**, and S12 says so
rather than claiming the clause:

1. **`UploadIntakeRosterTests` does not enforce the roster's annotation.** `:66-68` is
   `Assert.Equal(ExpectedIntakes.Select(entry => entry.Split(" — ")[0]).ToList(), intakes)` — index `[1]`
   is read by nothing. It is a `T1-CI` enforcer of route **enumeration** and of nothing else, and S12's
   table says exactly that. (Independently flagged by the T-0459 lane and by the §7.1 lead; now written
   where a rule-reader sees it.)
2. **The decoder prohibition has no enforcer of any kind at HEAD** — not even the `.csproj` denylist.
   `rg "SixLabors|SkiaSharp|System\.Drawing|Magick|ImageDescriptor|FromBinaryData"` over `src/**` returns
   **zero files**, which also means no test contains those strings. Both prohibition rows read
   `(gate pending: T-0458)` with the ADR-0043 §B.6 fallback carried verbatim: if T-0458 cannot build the
   call-site scan, the clause is **re-declared `T2-ADVISORY` with a named reviewer check**, not left
   labelled as a gate.
3. **The avatar exemption's expiry is enforced by nothing, and the shipped test says so in its own
   docstring.** `UpdateCurrentUserAvatarScrubExemptionTests` is a real `T1-CI` enforcer of *"the avatar
   is stored exactly as it was sent"* (`Assert.Equal(photo, stored)` + the GPS sentinel still present),
   so it reddens if someone wires the scrub in — but nothing sees an avatar URL reaching a cross-user
   DTO, which is the event that ends the exemption. Labelled `(guidance — no gate)`; **a ticket is owed**
   and the closing shape is named (`PayoutDtoSurfaceTests`-style wire-surface assertion; in production
   only `GetCurrentUser.cs:59`, `UpdateCurrentUser.cs:160` and `GdprDeletionService.cs:134` touch
   `user-files`).

Conversely, four enforcers were opened and found **stronger** than a name-level read would grant, and
S12 records the specific assertion so a future reader does not have to re-derive it: the three
per-pipeline scrub suites read **the bytes handed to `IBlobContainerClient.UploadAsync`** via a
`Callback`, not "a helper was called"; `ImageMetadataDispatchTests` asserts `Assert.Same` (identity, not
equality) on the pass-through; `JpegMetadataScrubTests:43` carries its own anti-vacuity fact (the
fixture *does* contain GPS before the output is asserted not to); and `ServedContentTypeTests:80-87`
asserts there is no public constructor and no `op_Implicit`/`op_Explicit`, which is what makes the set
closed rather than merely currently-correct.

#### Two transcription refinements — flagged, not silent

Both make the law true at HEAD without touching a ruling. **If the reviewer disagrees with either, the
fix is a word in S12, not a re-run of the panel.**

1. **Q2's "the accepted set equals the servable set" is scoped to surfaces served by URL.** Unscoped it
   would put shipped code in violation: `AcceptedByIntake[EmployeeDocument]` carries
   `application/msword` and the OOXML type (`SniffedContentType.cs:96-103`) and `ServedContentType`
   knows neither (`:34-42`). That is not a defect — employee documents never mint a SAS; they resolve
   through `SniffedContentType.ForDownload` (`:127-128`), which never consults that table. The three
   SAS-served intakes (avatar, order photo, dispute evidence) are all inside the servable set. The
   living doc's §7.2 already records the same asymmetry from the other direction.
2. **D8's employee-document exclusion is written with all three limbs, which is what covers the image
   formats that intake accepts.** T-0459's catalog-routing note found the open edge: a general sentence
   *"an image whose audience is not its uploader is scrubbed"* reaches `SaveMyDocuments` /
   `UpdateEmployee`, which accept `image/jpeg`/`image/png` and do not scrub, and D8's **mechanism** limb
   is about PDF/OOXML only. ADR-0043 D4 rules those two intakes *"no image scrub"* explicitly, and D8
   supplies two further limbs — **audience** (an admin who already holds the cleaner's legal name, tax id
   and payout details) and **delivery** (`attachment`, never by URL) — which do cover images. S12 states
   all three, so the law does not put shipped code in violation. Transcription, not a new ruling.

#### The four `## Deliberation` questions — all answered by ADR-0043, transcribed here

| Question | Answer | Where |
|---|---|---|
| S12 or extend S4? | **S12.** Same principle, different **check** — S4's check is "read the DTO's field list", which never reaches inside a byte array | ADR-0043 D7 / A7, §Verdict C-4 |
| How wide is "artifact"? | The **law** covers every user-supplied artifact; the **scrub** covers images only, and the exclusion is written **per surface with its own reason** | D8 |
| What is enforceable? | 6 clauses today, 4 ticketed, 1 with no mechanism — table above | §B.6 + this pass's re-verification |
| Retroactive scope? | **New uploads only.** No backfill obligation; it is a real migration and its own ticket | D9 |

#### Files changed

- `docs/architecture/security-rules.md` — header, **S12**, checklist item 12 *(sole writer, per §Implementation notes)*
- `agents/process/enforcement.md` — the S12 mechanical-checkability section (AC5)
- `agents/architecture/decisions/user-uploaded-artifacts.md` — §header, §1 row 5, §2 scrub column ×3 + its ⚠️ note, §4 item 6, §5 preamble. **Surgical**; §7.x (the ADR-0044 lane) untouched
- `agents/README.md`, `agents/WAY-OF-WORKING.md`, `agents/process/quality-gates.md`,
  `agents/knowledge/patterns-backend.md`, `.claude/agents/security.md`, `.claude/agents/backend.md`,
  `.claude/commands/backend.md`, `.claude/commands/review.md` — AC4 count sweep

#### Method

No shell (`Read`/`Glob`/`Grep`/`Edit` only) — nothing compiled, executed or measured.

**~28 `file:line` citations stand in S12 and every one was opened at HEAD in this pass** — none was
copied from ADR-0043 on trust, because T-0459 merged into these same files and moved several. **Five
inherited citations were corrected:**

| Inherited (ADR-0043 / living doc) | At HEAD | Where it landed |
|---|---|---|
| `UploadOrderPhoto.cs:102` (the sniff) | **`:103`** | living doc §2 |
| `UploadDisputeEvidence.cs:95-99` (ownership refusal) | **`:96-99`** — the `if` opens at 96 | living doc §2 |
| `UploadDisputeEvidence.cs:104-105` (sniff + minted blob name) | **`:105-106`** | living doc §2 |
| `BlobContainerClient.cs:93-110` (the SAS mint) | **`:89-110`** — the overload opens at 89 | S12 + living doc §2 |
| `SaveOrderPhotos.cs:114-117` (the write-side assignment gate) | **`:115-118`** | S12 reviewer test |

**Three were deleted rather than re-pointed:**
- `patterns-backend.md:1351-1354` (the accepted-set sentence) — it is at `:1357-1364` today, having moved
  twice in two days. S12 states the rule in its own words instead; a line number into a file two other
  lanes are editing would be stale before review.
- `SaveOrderPhotos.cs:171-184` / `:183` (`DetermineContentType`, now `:174-187` / `:186`) — S12 makes the
  *general* point about dispatching on a client string and does not need the instance, which the
  ADR-0044 lane owns and is about to change.
- `GetCurrentUser.cs:44,47-61` — narrowed to the single line actually verified for the "audience: self"
  claim (`:59`, the only production `user-files` SAS mint), rather than carrying a range read by
  somebody else.

#### Owner question — flagged, NOT filed (`questions/open.md` is held by another lane)

> **Q-ART-03 — `CLAUDE.md` says "the S1–S10 security laws" (`:455`) and there are now twelve.** The file
> is owner-gated and T-0460 §Out of scope forbids the architect editing it. Change `S1–S10` → `S1–S12`,
> or say the summary should not carry a count at all (it is the same drift this ticket exists to stop,
> one file out of reach). No other change to `CLAUDE.md` is requested.
