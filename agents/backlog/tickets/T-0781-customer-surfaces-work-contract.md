---
id: T-0781
title: Customer surfaces — /work-contract, the wizard sentence, the acceptance line, the contract page/screen (web, Android, iOS) (ADR-0068 D1, D4, D6)
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0777, T-0779, T-0780]
blocks: []
stories: []
adrs: [ADR-0068, ADR-0063]
layers: [frontend, android, ios]
security_touching: false
manual_steps: []
sprint: —
---

## Context

ADR-0068 D1: the contract for work is a **customer-audience** text — the customer is bound at booking,
so it is published where the customer's texts are and named at the offer by the lawyer's P074
sentence (an information line, not a checkbox). D4: once a cleaner accepted it, the customer reads
what was accepted with the facts frozen at that instant. One lane, sequential (web → Android → iOS),
after T-0777's customer client and spec and T-0779/T-0780's shared HTML views.

## Doing

- **Web — the public page.** `legal-pages`: a `work-contract` component in the `terms` component's
  shape (`LegalDocumentType.WorkContract`, `work_contract_page.title`), route `/work-contract` (SSR
  like the other two), the footer link beside Terms and Privacy. Five locales.
- **Web — the wizard sentence.** Beneath the consent block on the confirm step, **unconditional**:
  *By confirming the order you conclude a contract for work with the cleaner on these terms* → `/work-contract`.
  Copy only; no tick, no facade change.
- **Web — the order detail.** Per `workContractAcceptances` entry the line *Contract for work accepted
  by {firstName} on {acceptedOn}, version {documentVersion}* (the name from the `assignedEmployees`
  entry whose `id` is the acceptance's `orderEmployeeId`) with **Read the contract** →
  `/orders/:orderId/contract/:acceptanceId`, a `work-contract-page` (facade + signals,
  `getWorkContract(acceptanceId, uiLanguage)`, the facts table, the acceptance facts incl. *accepted
  in {language}* when it differs, `[innerHTML]` through the sanitizer, error state with retry). Specs
  pin the line, its absence, the page's render and its error state.
- **Android customer.** The same line per entry on `OrderDetailDetailsCards.kt`; a `WorkContractScreen`
  rendering `getWorkContract` through `:core`'s `HtmlContentView`; the wizard's confirm step gains the
  sentence linking to the web `/work-contract` page; five locales; view-model tests.
- **iOS customer.** The same line on `OrderDetailDetailsCards.swift`; a `WorkContractView` using
  Core's `HtmlContentView`; the wizard sentence with the link; `L10n` entries; view-model tests.

## NOT

- No "contract pending" line before any acceptance (D4); no PDF; no customer signature or second
  checkbox; no change to the terms tick or the `termsAccepted` member.
- No guest contract read (`LookupOrder` has no crew; a finding for the PM).
- No admin or partner work; no regen (T-0777's).

## Done looks like

`/work-contract` renders the in-force template; the wizard's confirm step names the contract and links
to it on all three clients; a customer whose order has an acceptance sees one line per accepting crew
member on web, Android and iOS, and opens the accepted text with the frozen facts in-app; an order
without one shows nothing new; Jest, Gradle unit tests and XCTest green.

## Acceptance criteria

- [x] **AC1** — `/work-contract` renders the in-force template with title, effective date and version
      like `/terms` (`work-contract.component.ts`, `app.routes.ts`, `app.routes.server.ts`); the
      wizard's confirm step carries the sentence and its link, signed-in or guest
      (`order-wizard.component.html`, `order-wizard.component.spec.ts`).
- [x] **AC2** — one acceptance → exactly one line *accepted by {given name} on {date}, version
      {yyyy-MM-dd}*; two → two; none → no line and no placeholder
      (`order-detail-work-contract.component.spec.ts`, `order-work-contract.models.spec.ts`;
      `WorkContractViewModelTest.kt`, `WorkContractViewModelTests.swift`).
- [x] **AC3** — **Read the contract** shows the stored facts, the acceptance facts and the accepted
      document's text in the customer's UI language (`work-contract-page.*`, `WorkContractScreen.kt`,
      `WorkContractView.swift`).
- [x] **AC4** — another customer's acceptance id renders the error state (`order.not_found`) and
      nothing of the order (`work-contract-page.facade.spec.ts`).
- [x] **AC5** — every new string resolves in five locales on each client (`work-contract-copy.spec.ts`
      on the web; the Android and iOS catalogue guards).

## Status log

- 2026-09-20 — filed by the docs lane from the batch-9 panel; runs after T-0777 and the two partner
  mobile lanes, beside T-0784.
- 2026-09-20 — done: **web `6588cab0`** (*the public text at /work-contract, the wizard names it at
  the offer, the order detail states each crew member's acceptance and opens the accepted text with
  the frozen facts*) + **`a26546fd`** (the legal document's docstring stops counting its callers);
  **Android `a6046331`** and **iOS `4cbd2c09`** (*the wizard names it at the offer with the public
  page behind it, the order detail states each crew member's acceptance, and Read opens the accepted
  text with the frozen facts in-app*). The shared web fact rows live in `libs/shared/utils/src/work-contract.utils.ts`
  (the admin dialog reads the same helper).
