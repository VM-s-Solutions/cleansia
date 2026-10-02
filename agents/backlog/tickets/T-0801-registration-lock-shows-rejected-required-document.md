---
id: T-0801
title: The partner registration lock shows Documents as needing action when an admin rejects a required document
status: todo
size: M
owner: —
created: 2026-10-02
updated: 2026-10-02
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Remark P2 (2026-10-01): a cleaner left on the registration lock read *"waiting for an approval when
there is literally nothing to approve"*. The rejection half shipped in the mobile-polish wave (the
reason and *Contact support* on the lock, a push on approval and on rejection). The owner kept one
variant out of it (D13, *"not now"*): **an admin rejecting one of the cleaner's documents**, rather than
the cleaner. That variant still leaves the lock reading *Documents: Done* and *Awaiting review* for an
approval the admin cannot grant, because approval needs every required document type `Approved`.

Ground-truthed 2026-10-02 against `feature/mobile-polish-2026-10-01`:

- The lock's Documents row reads `RegistrationCompletionStatus.AreDocumentsUploaded`, which is
  `employee.Documents.Any(d => d.IsActive)` (`Cleansia.Core.AppServices/Mappers/EmployeeMappers.cs:11`):
  any active document, whatever its review status.
- `GET api/Employee/GetMyDocumentRequirements` on the partner mobile host already answers, per required
  document type of the cleaner's country, `IsRequired` and the status (`Pending` / `Approved` /
  `Rejected`) of the newest active document of that type
  (`Features/EmployeeDocuments/GetMyDocumentRequirements.cs`).
- Both apps already call it, on the documents screen only: iOS `PartnerProfileClient.getDocumentRequirements`,
  Android `ProfileRepository.getDocumentRequirements`. Neither lock reads it: iOS
  `RegistrationLock/RegistrationCompletion.swift` `buildSteps`, Android
  `features/orders/RegistrationLockViewModel.kt` `buildSteps`.
- Since P1 the Documents row opens the documents screen whatever its status, so a cleaner who knows can
  already replace the rejected file. The defect is that the lock does not tell them.

Client-only: no endpoint, DTO or spec change.

## Acceptance criteria

- [ ] **AC1** — Given a cleaner whose newest document of a **required** type is `Rejected`, When the lock
      loads on Android or iOS, Then the Documents row reads as needing action rather than *Done*, says
      which document type was rejected (its existing localized type name), and opens the documents screen.
- [ ] **AC2** — Given the same cleaner, Then the Approval row does not read *Awaiting review*.
- [ ] **AC3** — A required type whose newest document is `Pending` or `Approved`, and a rejected document
      of an **optional** type, leave the row as it is today.
- [ ] **AC4** — If the requirements call fails, the lock renders exactly as it does today from
      `AreDocumentsUploaded`: a failed read never adds a dead end.
- [ ] **AC5** — Both platforms carry the same rule, pinned by a `buildSteps` unit test each; every new
      string is in all five locales on both apps.
- [ ] **AC6** — The warning *"A document the admin rejected still reads Done"* comes off
      `/partner-app/onboarding#registration-lock-screen`, replaced by what the lock now shows.

## Out of scope

- **A required type with nothing uploaded at all** (an ID uploaded, the insurance certificate not). The
  same data shows it, and it is the same class of dead end, but the owner ruled only on the rejected
  variant. Decide it when this is picked up; do not absorb it.
- **Partner web.** `registration-lock.component.ts` reads the same `hasUploadedDocuments` and has the
  same gap. Named here, not decided.
- A push when an admin rejects a document. `RejectDocument` notifies nobody today; a new event key is
  its own decision.
- Changing `AreDocumentsUploaded` on the server. Other readers depend on its meaning.

## Implementation notes

- Load the requirements beside the registration status in the lock's view model on both apps, and fold
  them into the Documents step in `buildSteps`. No new type unless both platforms need one.
- The lock reloads on foreground and on a decision push already (P2), so a replaced document clears the
  row on the next load.

## Status log

- 2026-10-02 — filed as `todo` from the P2 finding the owner deferred (D13, 2026-10-01), on the
  mobile-polish branch.
