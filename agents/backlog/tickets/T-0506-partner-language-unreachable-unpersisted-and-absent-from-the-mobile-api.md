---
id: T-0506
title: Partner language — the onboarding route renders EmptyView(), no client persists it, and the endpoint has zero consumers
status: draft
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0504]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, frontend]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Source: the partner-onboarding investigation (2026-08-02).** *"Language is unreachable during mobile
onboarding (the screen renders `EmptyView()` for that route) and no client persists language at all —
the endpoint has zero consumers and is absent from the mobile partner API entirely, so pay-period
emails are frozen in the day-one language."*

**Status: RELAYED, NOT re-verified by the PM.** AC1 re-establishes it.

### Four independent failures on one field, and each is a different kind

| | Failure | Kind |
|---|---|---|
| 1 | The mobile onboarding route renders **`EmptyView()`** | A screen that exists in the navigation graph and shows nothing |
| 2 | **No client persists language at all** — not web, not mobile | A capability nobody consumes |
| 3 | The endpoint has **zero consumers** | Dead server code |
| 4 | The field is **absent from the mobile partner API entirely** | A contract gap — so even a willing mobile client *cannot* call it |

**Failure 1 is the one that should be alarming beyond this ticket.** A navigation route wired to
`EmptyView()` is a screen a user can reach that renders nothing — and nothing in the build, the tests
or the lint catches it. **AC6 asks whether there are others**, because if this happened once in the
onboarding flow it can have happened elsewhere, and it is invisible to every gate in the repo.

### The consequence is not cosmetic

**Pay-period emails are frozen in the day-one language.** A cleaner who onboarded in English and works
in Czech receives their **pay statements** — the document that tells them how much money they are
owed — in a language they may not read, permanently, with no way to change it. That is not a
preference toggle; it is the platform's most consequential recurring communication.

**And it compounds with T-0508:** those same emails carry an invoice that is not a valid CZ/SK
supplier document. The cleaner receives an unusable document, in the wrong language, and can change
neither.

## Acceptance criteria

- [ ] **AC1 — RE-ESTABLISH all four failures at file:line.** The `EmptyView()` route; the absence of
      any client persistence (**all four clients** — partner web, partner Android, partner iOS, admin);
      the endpoint with no callers; and the field's absence from the mobile partner API spec.
      Evidence: four answers, each with the file:line or the search command that establishes the
      negative.
- [ ] **AC2 — the mobile onboarding language step renders and works, or is removed from the graph.**
      A route to `EmptyView()` is not acceptable in either direction: it either becomes a screen or it
      stops being reachable. State which and why. Evidence: the screenshot or the route removal.
- [ ] **AC3 — the selection is PERSISTED and survives a cold restart**, proved by round trip on each
      client that offers the choice. Evidence: the round-trip recording per client.
- [ ] **AC4 — the persisted language actually drives an outgoing email.** **This is the AC that
      matters** — persisting a value nobody reads reproduces the original defect one layer up. Trace
      the pay-period email's language selection and prove it reads the stored preference. Evidence:
      the trace at file:line plus a test asserting the rendered email's language follows the stored
      value.
- [ ] **AC5 — the contract gap is closed and FLAGGED, not worked around.** If the field must be added
      to the mobile partner API, this carries **`manual_steps: nswag-regen` + `mobile-spec-redump`** —
      the **owner's** bundle. The PM **holds** the mobile legs until the owner confirms. Evidence:
      the flag before the client legs start.
- [ ] **AC6 — the `EmptyView()` SWEEP.** Grep both mobile apps' navigation graphs for routes resolving
      to an empty/placeholder view. **Do not fix them here** — list them with file:line in
      `## Review`. **A route to nothing is invisible to the compiler, to the test suites and to
      lint**, which is exactly why this needs a deliberate look rather than a hope. Evidence: the
      list, or "none".
- [ ] **AC7 — the default is defined for existing partners.** Every cleaner already onboarded has no
      stored preference. What do they get — the tenant default, the platform default, or an inferred
      one? **State it**, because this determines what thousands of future emails are written in.
      Evidence: the stated default plus where it is applied.
- [ ] **AC8 — the five supported languages are the five the platform supports.** `en`, `cs`, `sk`,
      `uk`, `ru`. Not a subset, not a superset. Evidence: the enumeration at file:line.
- [ ] **AC9 — a test that goes red against the pre-fix code (Gate 0.5 leg 1)** — AC4's email-language
      assertion is the natural candidate, since it fails today by construction. Evidence: the red
      run, then green.
- [ ] **AC10 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**; plus the client suites for whichever clients change.

## Out of scope

- **The customer apps' language handling.** Partner-scoped.
- **The pay-period email's CONTENT** — **T-0508**. This ticket decides what language it is written
  in; that one decides whether the document is legally usable. **Both are true at once and they are
  different tickets.**
- **Email** — T-0505. **Consent** — T-0507.
- **Fixing whatever AC6's sweep finds.** Listed, then filed with a real scope.
- **The app's UI language** (which locale the app renders in). This is the **notification/email**
  language stored server-side. If they are the same setting, say so; if not, do not conflate them.

## Implementation notes

**No panel of its own — T-0504 is the panel.**

**Contract before consumers** (`routing.md` rule 1) and **manual steps block** (rule 6): if AC5 fires,
the mobile legs **hold** until the owner's regen is confirmed. Sprint-14's record: the step
immediately after a regen has a demonstrated failure history.

**Fan-out after the contract locks:** one `backend` + up to three client instances in parallel, one
reviewer each.

**Do not open `src/cleansia_ios/**/Info.plist` or `**/project.yml`.** Before the iOS leg:
`generate-api-clients.sh` + `xcodegen generate` (**T-0474**).

## Status log
- 2026-08-02 — **draft (created by pm from the partner-onboarding investigation).** Findings marked
  RELAYED; AC1 re-establishes all four. **AC4 is the load-bearing criterion** — persisting a language
  that no email reads would reproduce the exact defect being fixed, one layer up. **AC6 was added by
  the PM and is not in the investigation:** a navigation route wired to `EmptyView()` is invisible to
  the compiler, the suites and lint, so if it happened once it can have happened elsewhere and nothing
  in this repo would say so.

## Review
