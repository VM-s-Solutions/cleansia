---
id: T-0541
title: `docs/mobile-app/**` documents a single-module Android app that no longer exists, and has no iOS at all
status: ready
size: M
owner: docs
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0013]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 15
source: the docs sweep in `01b21746` corrected `CLAUDE.md`'s seven false claims and four `docs/` pages,
  and left this section flagged in place — *"Rewriting this section is tracked work, not a fact to infer
  from what is written here."* **It was not, in fact, tracked.** Filed by the PM in the sprint-15
  reconciliation.
---

## Context

`docs/mobile-app/` has three pages — `overview.md`, `features.md`, `api-integration.md` — and
`overview.md` opens with a `::: danger` banner it wrote about itself:

> It documents a **single-module** Android app at `src/cleansia_android/app/`. That module no longer
> exists. Mobile is now **four apps across two platforms**.

The banner is accurate and it is the right interim behaviour: a page that admits it is wrong is far
better than one that lies quietly. But it is an interim, and it names work that has no ticket. This is
that ticket.

**What is actually behind:**

- **The package/project tree is historical.** Android is multi-module (`:core`, `:partner-app`,
  `:customer-app`, packages `cz.cleansia.partner` / `cz.cleansia.customer`); the docs describe one app
  module.
- **iOS is absent as a platform.** `features.md` contains **zero** occurrences of "iOS" or "Swift"
  (counted at HEAD). There are two shipped iOS apps — a `CleansiaCore` SPM package plus `CleansiaPartner`
  and `CleansiaCustomer` XcodeGen targets — and the published mobile documentation does not know they
  exist.
- **The host count is wrong in the body.** The banner corrects it (two mobile hosts, not one), but the
  prose beneath it does not.
- **`features.md` is Android-partner-only** and titled as though it covers mobile.

**Why this is worth a real ticket rather than deleting the pages.** `01b21746` established the principle
the hard way: `CLAUDE.md` loads into every agent's context, so a wrong line there propagates. `docs/**`
is the published site — it propagates to humans instead, including whoever is onboarded next. And the
same commit found that **enumerating a fast-moving thing in prose guarantees drift** — which is why it
replaced `CLAUDE.md`'s tracker section with a pointer. That lesson applies directly here: this rewrite
should describe **structure and where to look**, not enumerate screens.

## Acceptance criteria

- [ ] **AC1 — every structural claim is verified against the tree, not against another document.** Given
      each page, When it is rewritten, Then module names, package names, paths, host names and ports are
      read from the repository. `01b21746`'s method — *"each verified against code rather than against an
      ADR's summary"* — is the standard, because four of the seven `CLAUDE.md` errors it found came from
      trusting a summary.
- [ ] **AC2 — iOS is a first-class platform in this section.** Given `docs/mobile-app/`, When a reader
      arrives, Then both platforms are covered symmetrically: the `CleansiaCore` package plus the two
      XcodeGen targets, and a pointer to `src/cleansia_ios/README.md`.
- [ ] **AC3 — the `::: danger` banner is DELETED, not edited.** Given the rewrite lands, Then the banner
      goes with it. A stale-warning banner that survives its own fix is the next reader's confusion.
- [ ] **AC4 — no count is stated in prose.** Given any list that will grow (screens, features, event
      types, locales beyond the fixed five), When it is written, Then it is not preceded by a number.
      `01b21746` deleted two *"all 13 displayable events"* comments for exactly this reason: **a number
      in prose goes stale silently, and that one already had.**
- [ ] **AC5 — the generated-client story is right.** Given `api-integration.md`, When it is read, Then it
      describes both mobile OpenAPI documents (`src/cleansia_android/openapi/customer-mobile-api.json`
      and `partner-mobile-api.json`), that they are **committed artifacts re-dumped by the owner**, and
      that iOS consumes generated models under `CleansiaPartnerApi` / `CleansiaCustomerApi`. **Do not
      instruct anyone to run a regen** — that is owner-only (`CLAUDE.md`).
- [ ] **AC6 — the VitePress site builds and the sidebar has no dead links.**
- [ ] **AC7 — nothing outside `docs/**` is touched.** In particular **not** `CLAUDE.md`, which
      `01b21746` already corrected.

## Out of scope

- `CLAUDE.md` — corrected in `01b21746`. If a claim there is still wrong, **file it**, do not fix it
  here.
- The other `docs/` pages `01b21746` fixed (wrong enum integers, an unpostable sample request body, a
  client-side price sum the code replaced with a quote endpoint).
- Writing the changelog — **T-0542**.
- Screenshots. Prose and structure first; images rot faster than either.

## Implementation notes

**Ground truth to read:** `src/cleansia_android/settings.gradle*` for the real module list, both apps'
`build.gradle*` for package ids and SDK levels, `src/cleansia_ios/README.md` and the two `project.yml`
targets **by name only** — ⚠️ **do not open `project.yml` or `Info.plist`**; they carry the owner's
Stripe key in the working tree. Take the target names from the directory layout instead.

**Archetype:** `agents/process/documentation.md` — the docs agent owns the published site; the living
architecture docs are a different lane.

**No-decision note:** this is documentation catching up to shipped structure. No behaviour, no
decision. No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. The `::: danger` banner in
  `docs/mobile-app/overview.md` explicitly calls the rewrite "tracked work"; it was not tracked until
  now. Passes DoR: AC observable, `M`, no dependencies, no owner-only steps.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
