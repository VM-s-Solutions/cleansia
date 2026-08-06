---
id: T-0460
title: SECURITY RULE — user-supplied artifacts served back by URL are sanitized at upload (magic-byte validation is not a sanitizer)
status: draft
size: S
owner: architect
created: 2026-07-30
updated: 2026-08-06
depends_on: []
blocks: []
stories: []
adrs: []
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
`agents/backlog/security/user-profile-avatar.md`.

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
> ruling are all in** `agents/backlog/adr/0043-user-artifact-metadata-is-scrubbed-at-intake-by-audience-without-a-decoder.md` **§D7
> / §D8 / §D9** (`proposed`, number not allocated, **panel owed**). This ticket remains the **sole
> writer** of `agents/knowledge/security-rules.md`; the draft ADR deliberately does not edit it.

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

The rule to add to `agents/knowledge/security-rules.md`, in substance (**the panel owns the final
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

- [ ] **AC1** — The rule is added to `agents/knowledge/security-rules.md` in the established S-series
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
- **Shared-file lane:** `agents/knowledge/security-rules.md` — this ticket is the **sole writer**. No
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
  `agents/backlog/adr/0043-user-artifact-metadata-is-scrubbed-at-intake-by-audience-without-a-decoder.md` §D7–§D9, with the living
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

## Review
<!-- architect + docs verdicts here -->
