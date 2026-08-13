---
id: T-0506
title: Partner language — the onboarding route renders EmptyView(), no client persists it, and the endpoint has zero consumers
status: done
size: M
owner: backend
created: 2026-08-02
updated: 2026-08-05
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
- 2026-08-05 — **AC1 re-established by backend. Three of the four failures are fixed; the fourth is
  still live but has MOVED — it is now a client gap, not a server one.** There is a home
  (`User.PreferredLanguageCode`) and it is wired end to end, so **no `ef-migration` is needed**. AC4
  was true in code and pinned by nothing; that pin is now written and mutation-proved. The remaining
  work is one call per partner client and this ticket is no longer `owner: backend`. See `## Review`.

## Review

### AC1 — all four re-established. Three fixed in `2012b014` (#189); the fourth moved.

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | Mobile onboarding route renders `EmptyView()` | **FIXED** | iOS: the `.language` route now pushes `LanguagePickerView`, pinned by `CleansiaPartner/Tests/RegistrationLockLanguageAccessTests.swift` (which records that it was `EmptyView()` "for a full release and nothing went red"). Android: the chooser is in the intro header and the registration-lock chain |
| 2 | **No client persists language at all** | **STILL TRUE post-signup — on all three partner clients** | The shared web switcher writes `localStorage` + a cookie only (`cleansia-language-switcher.component.ts:70-78`; same in `translation-loader.service.ts:33-34`) and no web app calls `updateCurrentUser`. Partner iOS/Android likewise never call it — the only `updateCurrentUser` caller in either mobile tree is the **customer** iOS app (`CleansiaCustomer/.../UserProfileClient.swift:85-89`) |
| 3 | The endpoint has **zero consumers** | **FIXED server-side** | `UpdateCurrentUser` now carries `LanguageCode` (`UpdateCurrentUser.cs:122`), validates it against `Languages` (`:69-72`) and persists it (`:212-215`). Routed on all four partner-reachable hosts |
| 4 | Absent from the **mobile partner API** | **FIXED** | `Cleansia.Web.Mobile.Partner/Controllers/UserController.cs:27-36`, added by `2012b014`; proved end to end against real Postgres by `Cleansia.HostTests/Tests/PartnerMobileUpdateCurrentUserTests.cs` (7/7 green) |

**Consequence of #2:** the ticket's headline — *pay-period emails frozen in the day-one language* —
**still holds**, but for a different reason than filed. The server can store the change; nothing on a
partner client ever sends it. `RegisterEmployee` stamps the signup choice
(`RegisterEmployee.cs:76` → `User.CreateWithPassword(..., command.Language)`), and after that the
value never moves.

### There is a home — no schema work
`User.PreferredLanguageCode` (`User.cs:95`), FK onto `Languages.Code`, writer
`User.UpdateLanguagePreference` (`:390-394`). **No `ef-migration` needed; none requested.**

### AC4 — was true in code, enforced by nothing. Now pinned.
Both producers already read the stored value —
`PayPeriodBackgroundService.cs:230` and `PeriodReminderBackgroundService.cs:104`, both
`employee.User.PreferredLanguageCode ?? Constants.Language.English` — and both thread it into
`IEmailService`, which selects the `EmailTemplateTranslation` row by language. All five locales are
seeded for both types (`insert_seed_data.sql`: EmailType 4 and 5 carry 18 keys × `en/cs/sk/uk/ru`).

Nothing went red if that read were deleted, and the same file already shows the decay:
`PayPeriodBackgroundService.GenerateInvoicePdfAsync` takes a `languageCode` parameter (`:384`) and
**never reads it** — the PDF's language is chosen from the country (`:417-418`). *(That one is
deliberate per the invoice-jurisdiction rule and belongs to T-0522; recorded, not touched.)*

New: `Cleansia.Tests/Services/PayPeriodEmailLanguageTests.cs` — 3 facts. The preference is **not**
hand-assigned; it is written by running the real `UpdateCurrentUser.Handler`, so the assertion spans
submitted → stored → read back by the producer.

### AC6 — the `EmptyView()` sweep (listed, not fixed)
No further `EmptyView()` sits on a **navigation route**. The 18 remaining occurrences in the iOS trees
are all `@ViewBuilder` defaults or conditional-branch no-ops (`SectionScaffold.swift:17`,
`ProfileView.swift:148`, `OrdersListView.swift:189`, `OrderDetailView.swift:141`,
`StickyActionFooter.swift:72`, `BankSectionView.swift:187,194`, `DocumentsSectionView.swift:32`,
`NotesAndIssuesSection.swift:85`, `InvoiceDetailView.swift:55`, `RegistrationLockView.swift:161`,
`AppleIDButton.swift:47`, `BookingSuccessView.swift:103`, `ConfirmStepComponents.swift:157`,
`WhenWhereStep.swift:127,264`, `CustomerShellView.swift:332`). The only route-resolved one was the
`.language` case, now fixed. **Verdict: none.**

### AC7 / AC8
Default is the signup choice, `?? "en"` at three layers: `User.CreateWithPassword`/`WithGoogle`/
`WithApple` (`User.cs:138,154,166`) and again in both producers. No existing cleaner has a NULL
preference unless anonymized. The five languages are exactly `en/cs/sk/uk/ru`
(`insert_seed_data.sql:64-68`).

### Remaining work — reassign
One call per partner client on language change: `UpdateCurrentUser` with `languageCode`
(the customer iOS `LanguagePreferenceSync` is the working precedent). **`owner` should move off
`backend`**; AC2/AC3/AC5 belong to `frontend` + `android` + `ios`. **AC5 does not fire** — the field
is already on the contract on every host, so no `nswag-regen` / `mobile-spec-redump` is owed.
