

---

All primitives confirmed: `Policy.CanUploadDisputeEvidence` exists in the frontend (CustomerOnly), and `cleansia-file` is a real shared component. Everything is grounded. Here is the user story.

---

```yaml
---
id: US-customer-0007
title: Attach and view evidence (and see the agreed refund) on a web dispute
persona: customer
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As a **customer using the web app**, I want to **attach photo/PDF evidence to a dispute and see the evidence already on file plus the refund amount the team agreed**, so that **I can prove a damage or quality claim and confirm what I am owed without having to switch to the Android app**.

## Context (grounded in code)

The backend already fully supports this on the customer surface, and the Android customer app already consumes it — only the **web** customer UI lags, a true web↔mobile parity hole:

- **Backend endpoint exists:** `POST api/Dispute/UploadEvidence` (multipart) gated by `Policy.CanUploadDisputeEvidence` — `src/Cleansia.Web.Customer/Controllers/DisputeController.cs:64-93`.
- **Backend validator (the whitelist to mirror):** max **10 MB**; content types `image/jpeg, image/jpg, image/png, image/webp, application/pdf` — `src/Cleansia.Core.AppServices/Features/Disputes/UploadDisputeEvidence.cs:14-23,55-67`. Handler also enforces ownership (`DisputeNotOwnedByUser`).
- **Backend read DTO already carries both fields:** `DisputeDetails.RefundAmount` (line 15) and `DisputeDetails.Evidence` (line 18) — `src/Cleansia.Core.AppServices/Features/Disputes/DTOs/DisputeDetails.cs`; each `DisputeEvidenceDto` has a SAS `BlobUrl` (`DTOs/DisputeEvidenceDto.cs:7`).
- **Android already does all of this:** client-side size/MIME guard + sequential upload + refresh in `DisputeDetailViewModel.uploadEvidence` (`.../features/disputes/DisputeDetailViewModel.kt:112-134`, whitelist at 136-145, mirroring the backend); multipart call in `core/disputes/DisputeApi.kt:58-69`; mapper reads `refundAmount` and `evidence` (same file, 117,122).
- **Web is missing it:** the detail dialog renders reason/status/description/resolution/messages only — **no evidence, no refund, no upload control** — `libs/cleansia-customer-features/disputes/src/lib/disputes/disputes.component.html:188-264`; the facade has **no `uploadEvidence` method** and never reads `evidence`/`refundAmount` — `.../disputes.facade.ts` (only `create`, `sendMessage`, `loadDisputeDetail`).
- **Frontend primitives already exist:** `Policy.CanUploadDisputeEvidence` (CustomerOnly) at `libs/core/services/src/lib/auth/policy.ts:76,236`; the shared `cleansia-file` upload component (`libs/shared/components/.../cleansia-file/cleansia-file.component.ts`); dispute i18n namespace `pages.disputes.*` in all locales (`apps/cleansia.app/src/assets/i18n/en.json:942`).

## Acceptance criteria

- **AC1 (view evidence)** — Given an open dispute that has evidence on file, When the customer opens the detail dialog (`disputes.component.html` detail dialog), Then an Evidence section lists each item by file name with a working link/preview that opens the SAS `blobUrl` from `DisputeDetails.evidence`; and Given a dispute with no evidence, Then the section is hidden (mirrors the existing `@if` pattern used for messages/resolution).

- **AC2 (view refund)** — Given a resolved dispute whose `refundAmount` is non-null, When the customer opens the detail dialog, Then the agreed refund amount is shown formatted with the order currency; and Given `refundAmount` is null, Then no refund line is shown (no "0"/blank placeholder).

- **AC3 (upload happy path)** — Given the customer holds `Policy.CanUploadDisputeEvidence` and the dispute is theirs, When they pick a valid file (≤10 MB and one of jpeg/jpg/png/webp/pdf) via the `cleansia-file` control and confirm, Then the facade calls `customerClient.disputeClient.uploadEvidence(...)` (via `takeUntil(this.destroyed$)` + `catchError` + `finalize`), a success snackbar shows, the detail reloads, and the new item appears in the Evidence section.

- **AC4 (client-side whitelist mirrors backend)** — Given the customer picks a file over 10 MB or of an unlisted type, When they attempt to attach it, Then the request is blocked **client-side** with a translated error (no network call), matching the backend whitelist in `UploadDisputeEvidence.cs:14-23` exactly as Android does in `DisputeDetailViewModel.kt:136-145`.

- **AC5 (permission + ownership gating)** — Given a customer who lacks `Policy.CanUploadDisputeEvidence`, When they view the detail dialog, Then the upload control is hidden via `*cleansiaPermission="Policy.CanUploadDisputeEvidence"`; and Given a backend rejection (e.g. `dispute.not_owned_by_user`, type/size), When upload fails, Then the error surfaces through `SnackbarService.showApiError` with the matching `api.*` key present in all 5 locales.

- **AC6 (in-flight + states)** — Given an upload is in progress, When the customer waits, Then the upload control shows a loading state and is disabled to prevent duplicate submits, and the dialog continues to render the standard loaded/error/loading states already used by the detail view.

## Out of scope

- Any **backend** change — endpoint, `UploadDisputeEvidence` command/validator, `DisputeDetails` DTO, and SAS generation already exist and are unchanged. (No `manual_step: nswag-regen` expected unless a missing client method on the generated `disputeClient` is discovered; if so, flag it — do not hand-edit generated clients.)
- **Android / partner / admin** dispute surfaces — Android is the reference implementation; this story brings web to parity only.
- **Deleting / replacing** existing evidence, drag-and-drop, multi-file batch upload UX, image thumbnails/lightbox, or virus scanning — single-file pick mirroring Android's per-file flow is sufficient.
- **Editing** the refund (set/approve refund is a staff/admin action) — web customer only *displays* `refundAmount`.
- Changing the dispute **create** flow to accept evidence at creation time (upload is post-creation, as on Android).
- New evidence-related notifications, emails, or status transitions.

## Layers touched

- **Frontend (Angular, customer app) — primary, the entire change:**
  - `libs/cleansia-customer-features/disputes/.../disputes.facade.ts` — add `uploadEvidence(...)` (client-side size/MIME guard + generated-client call + reload) and surface `evidence`/`refundAmount` from the loaded detail.
  - `libs/cleansia-customer-features/disputes/.../disputes.component.html` (detail dialog, ~188-264) and `.component.ts` — render Evidence list + refund line, add the `cleansia-file` upload control gated by `*cleansiaPermission`.
  - `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — new `pages.disputes.*` keys (evidence, refund, upload, validation errors) in **all 5 locales**, plus any new `api.*` mapping for upload errors.
- **NgRx customer-stores** (only if the detail load needs no change but you prefer to route upload through it — otherwise the facade calls the client directly, matching the existing `create`/`sendMessage` idiom): `libs/data-access/customer-stores/src/lib/dispute/*`.
- **Backend / mobile / DB:** none.

## Open questions

- **Q (refund currency source):** `DisputeDetails` exposes `refundAmount` as a bare `decimal?` with no currency code on the DTO. Default assumption: format using the related order's currency (the detail already keys off `orderId`/`displayOrderNumber`); confirm whether a currency code should be added to `DisputeDetails` or resolved client-side. If a DTO field is needed, that becomes a backend + `manual_step: nswag-regen` follow-up and is out of this story's current scope.

---

Files cited (all absolute):
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\DisputeController.cs` (64-93)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\UploadDisputeEvidence.cs` (14-23, 55-67, 88-91)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\DTOs\DisputeDetails.cs` (15, 18) and `DTOs\DisputeEvidenceDto.cs` (7)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\disputes\src\lib\disputes\disputes.component.html` (188-264)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\disputes\src\lib\disputes\disputes.facade.ts` (no upload method)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\cleansia_android\customer-app\src\main\java\cz\cleansia\customer\features\disputes\DisputeDetailViewModel.kt` (112-134, 136-145)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\cleansia_android\customer-app\src\main\java\cz\cleansia\customer\core\disputes\DisputeApi.kt` (58-69, 117, 122)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\core\services\src\lib\auth\policy.ts` (76, 236)


---

I have a complete, grounded picture. The order repository currently has no lookup-by-payment-intent method, which is a real gap the implementation must close (charge → payment_intent → order). I have everything needed to write the story.

---

# US-admin-0042 — Reconcile Stripe card chargebacks into platform disputes

## Context (grounded in code)

The `Dispute` aggregate already exposes `LinkStripeDispute(stripeDisputeId, updatedBy)` (`src/Cleansia.Core.Domain/Disputes/Dispute.cs:104-108`) and persists a `StripeDisputeId` column (`Dispute.cs:38`; `DisputeEntityConfiguration.cs:42`, `varchar(100)`, nullable). **Nothing calls either.** The Stripe webhook handler `HandlePaymentNotification` (`src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs`) only recognizes checkout-session, payment-intent, and subscription events — `Constants.StripeEventType` (`src/Cleansia.Core.AppServices/Common/Constants.cs:21-54`) has no `charge.dispute.*` member, so a real bank chargeback (`charge.dispute.created`) hits the endpoint, matches no branch, and is silently ignored (`HandlePaymentNotification.cs:173`).

Result: when a customer files a card chargeback with their bank, Finance has no link between the real money movement and any internal `Dispute`/`Order`. The data model is half-built; the wiring is the missing half.

The `Order` aggregate stores `StripePaymentIntentId` (`src/Cleansia.Core.Domain/Orders/Order.cs:103`) and `StripeSessionId` (`Order.cs:101`) but **no charge id**, and `IOrderRepository` has no lookup-by-payment-intent method — so correlating a dispute (which carries `charge` + `payment_intent`) back to an order requires a new repository query.

## Story

**As an** admin (Finance/Operations),
**I want** the platform to automatically ingest Stripe `charge.dispute.created` / `.updated` / `.closed` webhooks and correlate each bank chargeback to its order and internal dispute,
**so that** every real-money chargeback is linked to a platform `Dispute` with its live Stripe status, and Finance can reconcile without double-handling the same case manually.

## Acceptance Criteria

1. **Given** Stripe sends a `charge.dispute.created` event whose underlying charge resolves to an `Order` that has **no** open dispute, **When** the webhook is processed, **Then** a new `Dispute` is created for that order and `LinkStripeDispute` records the Stripe dispute id (`dp_…`), with status reflecting Stripe's dispute status.

2. **Given** a `charge.dispute.created` event whose order **already** has an open platform dispute (`GetOpenDisputeForOrderAsync` returns non-null, per `IDisputeRepository.cs:19`), **When** processed, **Then** the existing dispute is linked via `LinkStripeDispute` (no second/duplicate dispute is stacked — mirroring the `DisputeAlreadyExists` guard in `CreateDispute.cs:57-60`).

3. **Given** the same Stripe event id is delivered more than once (Stripe retries on 5xx/socket reset), **When** the webhook is processed again, **Then** no duplicate dispute is created and no field is re-mutated — the existing `ProcessedStripeEvents` idempotency gate (`HandlePaymentNotification.cs:144-159`) short-circuits, satisfying **S7** (`agents/knowledge/security-rules.md:83-91`).

4. **Given** a subsequent `charge.dispute.updated` or `charge.dispute.closed` event for an already-linked Stripe dispute id, **When** processed, **Then** the matching `Dispute` is found by its `StripeDisputeId` and its status is updated to reflect Stripe's current status (e.g. won → Resolved-class, lost → Closed/Escalated-class), without creating a new dispute.

5. **Given** a `charge.dispute.*` event whose charge cannot be correlated to any local `Order` (no matching `StripePaymentIntentId`), **When** processed, **Then** the handler returns success and logs a warning at most — with no PII / no raw Stripe payload above Debug level (**S6**, `security-rules.md:78-81`) — so Stripe does not retry indefinitely.

6. **Given** the webhook signature is invalid, **When** the event is received, **Then** the request is rejected exactly as the existing path does (`HandlePaymentNotification.cs:128-134`) and no dispute is created.

## Out of Scope

- Issuing or automating Stripe refunds, accepting/contesting the dispute, or submitting evidence to Stripe via the API (this story only *records and reflects* status; outbound dispute responses are a separate story).
- The missing **admin UI** to view/manage Stripe-linked disputes (surfacing `StripeDisputeId` in the admin disputes screen is follow-up frontend work — flag for `frontend`).
- Adding a dedicated `StripeChargeId` column to `Order` — correlation is via `payment_intent → Order.StripePaymentIntentId`; introducing a charge-id column is a separate schema decision.
- Customer/partner notifications about chargeback outcomes.
- Backfilling historical chargebacks that predate this handler.
- Changing the order lifecycle or payment status as a consequence of a chargeback (no auto-cancel/auto-refund of the order).

## Layers Touched

- **Backend — Domain:** none new (reuses `Dispute.LinkStripeDispute` / `UpdateStatus`); possibly a status-mapping helper from Stripe dispute status → `DisputeStatus`.
- **Backend — AppServices:** extend `Constants.StripeEventType` with `charge.dispute.created/updated/closed` + an `IsChargeDisputeEvent` predicate; add a `charge.dispute.*` branch (or sibling handler) in `HandlePaymentNotification`; reuse the `ProcessedStripeEvents` idempotency gate.
- **Backend — Repositories:** new `IOrderRepository` lookup by `StripePaymentIntentId` (does not exist today); new `IDisputeRepository.GetByStripeDisputeIdAsync` for update/close correlation.
- **Backend — Web (controllers):** none — the existing `PaymentController` `webhook` endpoints already forward the raw payload (`Cleansia.Web.Customer`, `Cleansia.Web.Mobile.Customer`, `Cleansia.Web.Partner`); the new event types flow through unchanged.
- **Database:** no schema change required (column already exists). If a unique index on `StripeDisputeId` is added for defense-in-depth dedup, that is a `MANUAL_STEP: ef-migration` (owner-only, **S9**).
- **NSwag / DTO contract:** none — webhook ingestion has no client-facing DTO. No `nswag-regen`.
- **Frontend / Mobile:** none in this story (admin display is out-of-scope follow-up).

## Notes for downstream agents

- **S7 (idempotency)** is the headline risk and is *already* satisfied at the event level by the `ProcessedStripeEvents` UNIQUE-index gate; the create-or-link logic must additionally be idempotent at the *dispute* level (criteria 2 & 4) so a replay that slips the gate still cannot double-create.
- A Stripe `Dispute` object carries `charge` and `payment_intent`; the cleanest correlation is `event.Data.Object (Stripe.Dispute).PaymentIntentId → Order.StripePaymentIntentId`. The handler currently extracts order id from event *metadata* (`ExtractOrderId`, `HandlePaymentNotification.cs:90-106`) — chargeback events won't have that metadata, hence the new repository lookup.
- `CreateDispute` derives `userId` from `IUserSessionProvider` (`CreateDispute.cs:52`) — webhook-created disputes have **no** authenticated user, so the system-created dispute must source `UserId`/`createdBy` from the order (`Order.UserId`) or a system principal, not a session (**S1** is satisfied: nothing trusts client input here).


---

The frontend confirms `WorkCountry` only appears in the approve-dialog flow — there's no post-approval edit surface for it. I now have everything I need: the gap is grounded on both the write path (`AdminUpdateEmployee.Command` lacks `WorkCountryId`), the read path (`AdminEmployeeDetail` DTO omits it), the domain (`AssignWorkCountry` is independently callable), the downstream impact (currency/language/VAT/pay resolution), and the frontend (only approve-dialog surfaces it).

Here is the user story.

---

```yaml
---
id: US-admin-0012
title: Reassign a cleaner's approved work country after approval
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As an **admin operator**, I want **to view and change a cleaner's approved work country (`WorkCountryId`) after their contract has already been approved**, so that **I can correct a wrong jurisdiction or relocate a cleaner who moves to another serviced country, without rejecting and re-approving them and without their currency/language/VAT/pay defaults silently staying wrong.**

### Why this gap matters (grounded in code)

- `WorkCountryId` is set **only** at approval: `ApproveEmployee.Handler` calls `employee.AssignWorkCountry(command.WorkCountryId)` (`src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs:114`). There is no other write path.
- The post-approval admin edit command does **not** carry the field: `AdminUpdateEmployee.Command` (`src/Cleansia.Core.AppServices/Features/Employees/AdminUpdateEmployee.cs:76-95`) has no `WorkCountryId`, and the handler (`:143-156`) never touches it. The controller route `PUT api/AdminEmployee/{employeeId}/update` (`AdminEmployeeController.cs:80-91`) is `Policy.CanAdminUpdateEmployee`-gated and correctly overrides body id with route id at `:88` (S1 clean — no security defect; this story is a functional gap, not a vuln).
- The admin **read** model is also blind to it: `AdminEmployeeDetail` (`Features/Employees/DTOs/EmployeeListItem.cs:32-68`) exposes `NationalityId`, `CountryId` (residency), `State`, etc., but **omits `WorkCountryId`/`WorkCountryName`** entirely — so an operator cannot even see the current value post-approval.
- The domain already supports an independent update: `Employee.AssignWorkCountry(string)` (`Core.Domain/Users/Employee.cs:213-221`) is decoupled from `Approve(...)` and validates non-empty.
- Downstream blast radius is real, not cosmetic: `CurrencyResolutionService.ResolveCurrencyCodeForEmployeeAsync` keys currency off `WorkCountryId` (`Core.AppServices/Services/CurrencyResolutionService.cs:16-23`), and the entity comment (`Employee.cs:68-80`) states it "Drives currency / language / VAT / pay-rule defaults via CountryConfiguration." A wrong-but-frozen work country pays/bills the cleaner in the wrong currency.
- Frontend confirms the same blind spot: `WorkCountry` only appears in the approve flow (`approve-dialog.component.*`); the employee-detail edit facade has no work-country field.

## Acceptance criteria

- **AC1 — Read surface exposes current value.**
  Given an approved cleaner with `WorkCountryId = "CZ"`,
  When an admin opens employee detail (`GET api/AdminEmployee/details/{employeeId}`),
  Then the response includes the cleaner's current work country id and its display name (e.g. `WorkCountryId`/`WorkCountryName`), populated from `Employee.WorkCountry`.

- **AC2 — Post-approval reassignment persists.**
  Given an approved cleaner,
  When an admin submits the existing admin-update path (`PUT api/AdminEmployee/{employeeId}/update`, `Policy.CanAdminUpdateEmployee`) with a new, serviced work country id,
  Then `Employee.WorkCountryId` is updated to the new value and persists across reload, while `ContractStatus` remains `Approved` and `ApprovedAt`/`ApprovedByUserId` are unchanged.

- **AC3 — Same validation as approval.**
  Given an admin attempts to set a work country that does not exist or is not serviced,
  When the update is submitted,
  Then it is rejected with the same business errors used at approval (`CountryNotFound` / `CountryNotServiced` from `BusinessErrorMessage`), and the cleaner's existing `WorkCountryId` is left unchanged.

- **AC4 — Field is optional in the partial edit.**
  Given the admin edits other fields (name, address, IBAN) and does not supply a work country,
  When the update is submitted,
  Then `WorkCountryId` is preserved unchanged (consistent with the existing "partial edit preserves unsupplied fields" behavior at `AdminUpdateEmployee.cs:142-156`).

- **AC5 — Currency resolution follows the new country.**
  Given a cleaner whose work country is changed from one serviced country to another with a different `CountryConfiguration.DefaultCurrencyCode`,
  When currency is next resolved for that cleaner,
  Then `CurrencyResolutionService` returns the new country's default currency (no app restart / re-approval required).

- **AC6 — Admin UI edit control.**
  Given an admin on the employee-detail edit screen for an approved cleaner,
  When the screen loads,
  Then a work-country selector (serviced countries only, label translated in all 5 locales, using `<cleansia-*>`/PrimeNG controls) shows the current value and lets the admin change and save it, with errors surfaced via `SnackbarService` (`showApiError`).

## Out of scope

- **Not a security fix.** No S1–S4 defect exists on `AdminUpdateEmployee`; the route-id-overrides-body-id behavior (`AdminEmployeeController.cs:88`) is correct and stays as-is.
- **Auditing/history of work-country changes** (who changed it, when, from→to). Not introducing a new audit log beyond existing `Auditable` timestamps.
- **Retroactive recalculation** of already-generated pay (`OrderEmployeePay`) or already-issued invoices (`EmployeeInvoice`) for the cleaner. This story only changes the *default* used going forward; back-dating settled payroll is explicitly excluded.
- **Cleaner self-service** changes to their own work country (cleaner/partner persona). Admin-only.
- **Changing the approval workflow itself** (`approve`/`reject` endpoints) or the `IsProfileComplete()` gate.
- **Multi-country approval** (a cleaner approved for more than one work country at once). `WorkCountryId` remains a single value.
- NSwag client regeneration and EF migration generation are owner-only `MANUAL_STEP`s, not part of the delivered code change.

## Layers touched

- **Backend — AppServices:** add `WorkCountryId` to `AdminUpdateEmployee.Command`, validate it (reuse `ICountryRepository.ExistsAsync` + `IsServicedAsync`, mirroring `ApproveEmployee.Validator:54-60`), and call `employee.AssignWorkCountry(...)` in the handler when supplied.
- **Backend — DTO/read model:** add `WorkCountryId` + `WorkCountryName` to `AdminEmployeeDetail` and its mapper (include `Employee.WorkCountry` in the detail query).
- **Backend — Domain:** none required; `AssignWorkCountry` already exists (`Employee.cs:213-221`).
- **API contract:** `AdminEmployeeController` `update` and `details` endpoints change shape → `manual_step: nswag-regen` (admin client).
- **Frontend — Admin app:** `cleansia-admin-features/employee-management` employee-detail facade + edit form (add serviced-country selector, bind to the new field) and i18n keys (`en/cs/sk/uk/ru`).
- **Database:** none — `WorkCountryId` column already exists (migration `20260526110831_AddEmployeeWorkCountry`); no new migration needed.

## Open questions

- **Q (product intent):** Should reassigning the work country be allowed in **any** post-approval contract state, or blocked while the cleaner has in-flight orders (`Confirmed`/`InProgress`) to avoid mid-job currency drift? Default assumption taken: allow in any state, with retroactive recalculation explicitly out of scope (above). Confirm before locking to `ready`.

---

Story file path (not created — owner/PM creates under the locked-stories dir): `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\agents\backlog\stories\US-admin-0012.md`

Grounding sources: `src/Cleansia.Web.Admin/Controllers/AdminEmployeeController.cs:80-91`, `src/Cleansia.Core.AppServices/Features/Employees/AdminUpdateEmployee.cs:76-156`, `src/Cleansia.Core.AppServices/Features/Employees/ApproveEmployee.cs:54-60,114`, `src/Cleansia.Core.Domain/Users/Employee.cs:68-80,213-221`, `src/Cleansia.Core.AppServices/Features/Employees/DTOs/EmployeeListItem.cs:32-68`, `src/Cleansia.Core.AppServices/Services/CurrencyResolutionService.cs:16-23`.


---

I have everything needed. The grounding confirms: there is a per-country single-active invariant enforced only on Create/Update (not at DB level, "uniqueness enforced at application level"), but the **country-less** `GetActiveCompanyInfoAsync()` fallback (`FirstOrDefaultAsync(c => c.IsActive)` with no ordering) is genuinely non-deterministic across countries, and no command lets an admin pick which record wins. This is the global fallback path used by `OrderFactory`, `ReceiptService`, `RegenerateInvoicePdf`, and `PayPeriodBackgroundService`.

Here is the user story.

---

# US-admin-0142 — Deterministic "active company" selection for fiscal documents

## Story

**As an** admin operating Cleansia across multiple legal entities or through a rebrand (where more than one `CompanyInfo` record can exist),
**I want** an explicit, deterministic way to designate which company record is the platform-wide "active/default" company stamped on fiscal documents,
**so that** receipts, invoices, VAT breakdowns, and fiscal-authority registrations always carry the correct legal entity instead of whichever record the database happens to return first.

## Context / grounding (read code)

- `AdminCompanyController.cs:91-101` exposes a legacy `GET get-current` endpoint that delegates to `GetCompanyInfo.Query`, the only way to read "the current company". The full CRUD (`get-paged`, `details/{id}`, `create`, `update/{id}`, `delete/{id}`) treats `CompanyInfo` as a multi-record collection, but **no endpoint lets an admin choose which record is the active/default one**.
- `GetCompanyInfo.cs:21` resolves the company via `companyInfoRepository.GetActiveCompanyInfoAsync(...)`.
- `CompanyInfoRepository.cs:9-12` implements that as `GetDbSet().FirstOrDefaultAsync(c => c.IsActive, ...)` — **no `OrderBy`, no tie-breaker**. If two or more records have `IsActive == true`, the returned record is non-deterministic (EF/Postgres ordering is not guaranteed).
- There is **no command to set `IsActive`**: it is the generic `BaseEntity.IsActive` flag (`BaseEntity.cs:7`, defaults `true`; flipped to `false` only by `Auditable.Deactivated()` at `Auditable.cs:35-42`, which doubles as soft-delete). Neither `CreateCompanyInfo.Command` nor `UpdateCompanyInfo.Command` exposes `IsActive`. So "active" really means "not soft-deleted", and every non-deleted record is "active".
- The per-country single-active invariant is enforced **only at application level on write** (`CreateCompanyInfo.cs:93-95` via `ExistsActiveForCountryAsync`; `UpdateCompanyInfo.cs` via `ExistsActiveForCountryExcludingAsync`), and the entity config comment confirms "uniqueness enforced at application level" (`CompanyInfoEntityConfiguration.cs:41`). There is **no global single-active invariant** — multiple countries each have their own active record, so the country-less fallback is inherently ambiguous.
- The ambiguous global fallback `GetActiveCompanyInfoAsync()` feeds **fiscal-document-critical** paths: `OrderFactory.cs:145-146` (VAT breakdown stamped on the order), `ReceiptService.cs:41` and `:206` (company legal name/registration/VAT number on the receipt PDF **and** the data sent to the fiscal authority via `RegisterReceiptAsync`), `RegenerateInvoicePdf.cs:71`, and `PayPeriodBackgroundService.cs:399` (employee invoice PDFs).

## Acceptance criteria

1. **Given** more than one `CompanyInfo` record exists, **when** an admin opens the company list, **then** exactly one record is shown as the designated active/default company, and the list visibly marks which one it is.

2. **Given** the admin views the company list with several records, **when** the admin selects a different record and confirms "set as active", **then** that record becomes the single designated active/default company and the previously active one is no longer marked active — with the change persisted atomically so there is never zero or two simultaneously-active defaults.

3. **Given** the admin attempts to set a record active that violates a defined invariant (e.g. would create a second active record for the same country, or the record is soft-deleted), **when** the command is submitted, **then** it fails validation with a specific `BusinessErrorMessage` (reusing/extending `company.*` keys such as `company.exists_for_country`) and the active selection is unchanged.

4. **Given** an admin has explicitly designated an active company, **when** an order is created or a receipt/invoice is generated and no country-specific `CompanyInfo` matches the customer's country, **then** `GetActiveCompanyInfoAsync` returns the **same** explicitly-designated record on every call (deterministic), so the legal entity stamped on the document is stable and reproducible.

5. **Given** the designated active company is changed, **when** a new fiscal document is generated afterward, **then** it reflects the newly designated company, while previously issued receipts/invoices retain the entity they were already stamped with (no retroactive mutation of issued documents).

6. **Given** the legacy `GET api/AdminCompany/get-current` endpoint, **when** the explicit selector is introduced, **then** `get-current` either returns the explicitly-designated active company (now deterministic) or is retired in favour of the new endpoint — and the decision is recorded so NSwag clients and any consumers are updated consistently.

## Out of scope

- Per-country active-company management beyond the existing Create/Update uniqueness rules (this story is about the **global/default** fallback determinism, not redesigning country scoping).
- Changing VAT calculation logic, fiscal enforcement modes, or the fiscal-authority registration contract (`FiscalReceiptRequest`).
- Multi-tenant `TenantId` scoping redesign — `CompanyInfo` is `ITenantEntity`, but introducing tenant-aware active selection is a separate concern.
- Retroactively re-stamping or re-issuing already-generated receipts/invoices.
- Migrating the `IsActive` soft-delete semantics across other entities; any new "is designated active" concept must not break the existing `BaseEntity.IsActive` / `Auditable.Deactivated()` soft-delete usage.
- Frontend work in the partner and customer apps (this is admin-only).
- Adding a DB-level unique constraint/migration is a likely consequence but the migration itself is **owner-only** (`MANUAL_STEP`), not implemented in this story's code.

## Layers touched

- **Backend — Domain** (`Cleansia.Core.Domain`): introduce an explicit "designated active/default company" concept on `CompanyInfo` distinct from the `BaseEntity.IsActive` soft-delete flag (e.g. a dedicated property + domain method), so activeness is set deliberately, not inferred.
- **Backend — AppServices** (`Cleansia.Core.AppServices/Features/Company`): a new `SetActiveCompanyInfo` command + handler + validator; make `GetActiveCompanyInfoAsync` deterministic (explicit designation + tie-breaker ordering); surface the active flag in `CompanyInfoListItem` (already has `IsActive` — clarify its meaning).
- **Backend — Repository/Infra** (`Cleansia.Infra.Database`): add a deterministic `OrderBy`/explicit-flag query for the global active lookup; entity configuration and a **MANUAL_STEP** migration for any new column/constraint enforcing single global active.
- **Backend — Web.Admin** (`AdminCompanyController`): new `POST set-active/{companyInfoId}` endpoint behind a permission (e.g. `Policy.CanUpdateCompanyInfo`); decide the fate of the legacy `get-current`.
- **Errors / i18n**: extend `BusinessErrorMessage` `company.*` keys if a new rule is needed, with matching `errors.company.*` translations in all 5 locales (en, cs, sk, uk, ru).
- **Frontend — Admin app** (`cleansia-admin-features`): "set as active" action in the company list/detail, an active badge column, facade + NgRx wiring, all strings via `TranslatePipe`.
- **MANUAL_STEP (owner-only)**: EF Core migration for the new active concept/constraint, and NSwag admin-client regeneration (`generate-admin-client`) after the contract change.

## Owner confirmation needed

The proposed fix has two viable directions and the owner should confirm intent before build: **(A)** add an explicit "set active company" command/flag surfaced in the admin list (richer, supports deliberate rebrand/multi-entity switching), or **(B)** enforce a single global-active invariant at the DB level and retire the legacy `get-current` endpoint (simpler, assumes only one company is ever active platform-wide). This story is written for **(A)**; if the platform is contractually single-entity-per-deployment, (B) is the lighter path.


---

I have complete grounding. The deletes are admin-only endpoints (`Policy.CanDeleteService`/`CanDeletePackage`), reachable via `AdminServiceController` and `AdminPackageController`. The gap is a clear B6 consistency violation (history-bearing entity hard-deleted without a guard) and a data-integrity defect (orphaning OrderService/EmployeePayConfig that feed receipts and pay). Both the backend error constants and all 5 admin locale strings already exist — only the backend guard wiring is missing.

Here is the user story.

---

# US-admin-0007 — Block deletion of Services and Packages that are in use

## Context (grounded in code)

`DeleteService` and `DeletePackage` perform an unconditional hard delete with no in-use guard:

- `Features/Services/DeleteService.cs:33-37` — handler loads the service and calls `serviceRepository.Remove(service!)`. The validator (`:15-26`) only checks `NotEmpty` + `ExistsAsync`.
- `Features/Packages/DeletePackage.cs:33-37` — identical pattern: `packageRepository.Remove(package!)` with a validator that only checks existence.

This is inconsistent with the deliberate guard pattern used for the other catalog entities in the same domain:

- `Features/Currencies/DeleteCurrency.cs:32-34` and `:55-59` — `IsInUseAsync` guard in **both** validator and handler, returning `BusinessErrorMessage.CurrencyInUse`.
- `Features/Languages/DeleteLanguage.cs:26-28` / `:45-49` and `Features/Countries/DeleteCountry.cs:26-28` / `:45-49` — same `IsInUseAsync` guard.

Services and Packages are history-bearing: `Service.cs:35-36` exposes `IncludedInOrders` (`OrderService`) and `:32-33` exposes `Packages` (`PackageService`); `Package.cs:23-24` exposes `IncludedServices`. `EmployeePayConfig.cs:15-19` references `ServiceId`/`PackageId`, and pay is computed from those configs (`EmployeePayConfig.CalculatePay`, base pay = services × serviceRate). Hard-deleting a referenced row orphans historical `OrderService` links, breaks receipt/order reconstruction, and detaches the pay config that payroll relies on — corrupting fiscal/payroll records.

Both deletes are exposed as admin-only endpoints: `AdminServiceController.cs:96` (`Policy.CanDeleteService`) and `AdminPackageController.cs:80` (`Policy.CanDeletePackage`).

**Already in place (no work needed):** the backend error constants `BusinessErrorMessage.ServiceInUse = "service.in_use"` (`BusinessErrorMessage.cs:223`) and `PackageInUse = "package.in_use"` (`:230`) exist but are **never referenced**. The matching admin frontend keys `errors.service.in_use` and `errors.package.in_use` already exist in **all 5 locales** (verified in `en/cs/sk/uk/ru.json`). Only the backend wiring (`IsInUseAsync` + the guard) is missing.

This is a direct violation of consistency rule **B6** ("`repo.Remove` only for true join/scratch rows that carry no history and are never referenced") and the conventions "production-ready / root-cause" bar.

## Story

**As an** admin managing the service/package catalog,
**I want** the system to refuse to hard-delete a service or package that is still referenced by any order, package composition, or pay configuration,
**so that** I cannot silently orphan historical orders, receipts, or payroll records, and I instead get a clear "in use" error matching how Currency, Language, and Country already behave.

## Acceptance Criteria

1. **Given** a service that is referenced by at least one `OrderService`, `PackageService`, or `EmployeePayConfig`, **when** an admin sends `DeleteService` for it, **then** the command fails with a `BusinessResult.Failure` carrying `BusinessErrorMessage.ServiceInUse` on the `ServiceId` field, and the service row and all its references remain intact in the database.

2. **Given** a package that is referenced by at least one `EmployeePayConfig` (or any other referencing relation), **when** an admin sends `DeletePackage` for it, **then** the command fails with `BusinessErrorMessage.PackageInUse` on the `PackageId` field, and the package row remains intact.

3. **Given** a service or package that has **never** been referenced (no `OrderService`, no `PackageService`, no `EmployeePayConfig`), **when** an admin deletes it, **then** the delete succeeds and returns the existing success `Response` shape (`new Response(service!.Id)` / `(package!.Id)`) unchanged.

4. **Given** the in-use guard, **when** the delete is attempted, **then** the check is enforced in **both** the validator (`MustAsync(... !IsInUseAsync ...)` with `Cascade.Stop`) **and** the handler (fetch-and-guard returning the failure), mirroring `DeleteCurrency`/`DeleteLanguage`/`DeleteCountry` exactly (rules B3/B4/B6).

5. **Given** an admin hits the in-use error in the admin app, **when** the error surfaces, **then** the already-present localized strings `errors.service.in_use` / `errors.package.in_use` are displayed in the active language (en/cs/sk/uk/ru) with no new untranslated key.

6. **Given** the existing automated test suite, **when** the build runs, **then** new xUnit tests cover: (a) delete blocked when a referencing `OrderService` exists, (b) delete blocked when a referencing `EmployeePayConfig` exists, and (c) delete succeeds for an unreferenced catalog row — for both Service and Package.

## Out of scope

- **Replacing hard-delete with soft-deactivate** (the long-term B6 / CC-03 direction of `IsActive=false` + filtered catalog reads). This story only adds the in-use **guard** to match Currency/Language/Country; converting the catalog to soft-delete is a separate, larger architectural ticket (ADR-owned).
- Retroactively cleaning up or detecting any orphans already created by past unguarded deletes.
- Changing `DeleteCurrency` / `DeleteLanguage` / `DeleteCountry` (already correct) or any other delete command (e.g. `DeleteServiceCity`).
- Cascade-delete or "force delete with reassignment" UX — there is no override flow; in-use simply blocks.
- Any new frontend strings or UI beyond surfacing the already-existing error keys; no new admin screens.
- Mobile apps (services/packages are admin-managed; no Android delete path).
- EF Core migration — none required (no schema change; the guard reads existing FK relations). If implementation reveals a needed index, flag `manual_step: ef-migration`.

## Layers touched

- **Domain / Repositories:** add `Task<bool> IsInUseAsync(string id, CancellationToken ct)` to `IServiceRepository` and `IPackageRepository` (`Cleansia.Core.Domain/Repositories/`), mirroring `ICurrencyRepository.cs:10`.
- **Infra.Database:** implement `IsInUseAsync` in the concrete `ServiceRepository` / `PackageRepository` — `ServiceRepository` checks `OrderService` ∪ `PackageService` ∪ `EmployeePayConfig` by `ServiceId`; `PackageRepository` checks `PackageService`/`EmployeePayConfig` by `PackageId`.
- **AppServices (CQRS):** add the guard to `DeleteService.cs` and `DeletePackage.cs` (validator rule + handler fetch-and-guard) using the **existing** `BusinessErrorMessage.ServiceInUse` / `PackageInUse` constants.
- **Tests:** `Cleansia.Tests` (and/or `Cleansia.IntegrationTests`) for the three AC-6 scenarios per entity.
- **Frontend:** none (admin locale keys already present); no NSwag regen needed (no DTO/endpoint signature change — the `Response` shapes are unchanged).

**Manual steps:** none expected (`manual_step: nswag-regen` **not** required — no contract change; `manual_step: ef-migration` only if an index is added during implementation).


---

I now have everything I need. The GAP is confirmed and grounded. Two important observations from the actual code:

1. The receipt Function at `GenerateReceiptFunction.cs:66-70` already has an `order.Receipt is not null` no-op guard — so duplicate **receipts** are partly defended at the Function side (the PDF/email won't double-fire if the first message already produced a receipt). But this does **not** save the design: the push notifications (`OrderConfirmed`, `OrderCancelled`) have **no** such idempotency guard, and the receipt guard relies on a race-prone read of `order.Receipt` with no unique constraint. This sharpens the story scope (the push side has no defense at all).

2. The code comment at lines 136-159 is verifiably misleading per `security-rules.md` S7, which explicitly names "webhook re-delivery (Stripe retries on 5xx/socket reset), pipeline retries" as the threat model the project must defend against.

Here is the user story.

---

# US-admin-0042 — Make Stripe webhook side-effects fire exactly once (transactional outbox / post-commit dispatch)

> Persona note: the observable victim is the **customer** (duplicate receipts + duplicate push), but the durable system actor who owns financial-document integrity and the idempotency invariant is the **admin/platform**. Filed as `admin` because the fix is a platform reliability guarantee, not a customer-facing feature.

## Context (grounded in code)

`HandlePaymentNotification.Handler` stamps `ProcessedStripeEvent` (`HandlePaymentNotification.cs:156-159`) and mutates the order, then — **still inside the handler** — enqueues `GenerateReceipt` and the `OrderConfirmed` push (`HandlePaymentNotification.cs:241-257`). The EF transaction does not commit until **after** the handler returns, in `UnitOfWorkPipelineBehavior.Handle` (`UnitOfWorkPipelineBehavior.cs:19-20`). The queue dispatch in `AzureStorageQueueClient.SendAsync` (`AzureStorageQueueClient.cs:14-27`) is an immediate, non-transactional network send with no enlistment in the DB transaction.

Therefore, if `CommitAsync` throws — the parallel-retry `DbUpdateException` the comment at `HandlePaymentNotification.cs:136-143` explicitly relies on, a transient PostgreSQL error, or a `CancellationToken` trip — the receipt and push messages are **already on the wire** while the `ProcessedStripeEvent` stamp and order state roll back. Stripe retries (it retries on 5xx/socket reset), the handler re-runs with the stamp absent, and enqueues a **second** receipt-generation and a **second** push per retry.

This directly violates **security-rules.md S7** ("Idempotency on side-effecting commands… must be idempotent… protects against webhook re-delivery"). The in-code comment at `HandlePaymentNotification.cs:152-159` asserting side effects "fire at most once" and the stamp commits "atomically with the rest of the handler's work" is **factually wrong** — the queue send is not in the transaction.

The `GenerateReceipt` Function has a partial guard (`GenerateReceiptFunction.cs:66-70`: no-op if `order.Receipt is not null`), so duplicate receipt **emails** are partly mitigated downstream — but it is a race-prone read with no unique constraint, and the **push notifications have no idempotency guard at all** (`HandlePaymentNotification.cs:244-258` and the `OrderCancelled` push at `:276-290`), so duplicate `OrderConfirmed`/`OrderCancelled` notifications reach the customer on every retry.

## Actor narrative

**As** the Cleansia platform (admin/operations owner of webhook reliability),
**I want** every Stripe webhook side-effect (receipt generation, order-confirmed/cancelled push) to be dispatched only after the `ProcessedStripeEvent` idempotency stamp and order state are durably committed, and to be idempotent if dispatched again,
**so that** a Stripe retry after a failed commit cannot produce a second customer receipt (a financial document) or a duplicate confirmation/cancellation notification, and the `ProcessedStripeEvent` table actually protects the side-effects that matter.

## Acceptance criteria

**AC1 — No dispatch before commit (the core invariant)**
**Given** a `checkout.session.completed` / `payment_intent.succeeded` webhook is being handled and the order moves to Paid/Confirmed,
**When** the handler runs,
**Then** no `GenerateReceipt` message and no `OrderConfirmed` push are placed on any queue until the EF transaction containing the `ProcessedStripeEvent` stamp and the order state change has committed successfully (verifiable: the queue send no longer happens inside `HandleCompletedSession` before `UnitOfWorkPipelineBehavior` commits).

**AC2 — Failed commit produces zero side-effects**
**Given** the same webhook, **When** `CommitAsync` throws (simulated `DbUpdateException`, transient PG error, or cancellation), **Then** the `ProcessedStripeEvent` stamp is rolled back **and** zero receipt messages and zero push messages have been enqueued — i.e. the post-commit dispatch never ran.

**AC3 — Stripe retry after a failed commit yields exactly one of each side-effect**
**Given** a first delivery whose commit failed (AC2) and Stripe re-delivers the same `event.id`, **When** the second delivery commits successfully, **Then** exactly one `GenerateReceipt` message and exactly one `OrderConfirmed` push are produced across both deliveries (not two).

**AC4 — Receipt generation is idempotent at the source**
**Given** two `GenerateReceipt` messages for the same `OrderId` are somehow drained (e.g. an at-least-once outbox redelivery), **When** the `GenerateReceiptFunction` processes the second, **Then** no second `OrderReceipt` row, PDF, or receipt email is produced — the existing `order.Receipt is not null` no-op (`GenerateReceiptFunction.cs:66-70`) is preserved/strengthened so the guarantee does not depend on a race-prone read alone.

**AC5 — Confirmation/cancellation push is idempotent**
**Given** two `OrderConfirmed` (or two `OrderCancelled`) dispatches for the same order+event, **When** the notification dispatcher processes the second, **Then** the customer receives at most one push for that order transition (de-duplicated by order id + event key, or by the same outbox-once guarantee).

**AC6 — Misleading comment corrected**
**Given** the fix is in place, **When** a developer reads `HandlePaymentNotification.cs:136-159`, **Then** the comment accurately describes the new ordering (stamp committed first, side-effects dispatched only post-commit and idempotently) and no longer claims the non-transactional queue send is atomic with the DB write.

## Out of scope

- The **subscription** webhook path (`subscriptionWebhookHandler.HandleAsync`, `HandlePaymentNotification.cs:164-168`) and Cleansia Plus membership side-effects — separate flow, separate story.
- The `payment_intent.payment_failed` no-op (`HandlePaymentIntentFailed`, `:222-228`) — it intentionally has no side-effect.
- Refund / `EmployeeInvoice` / payout / loyalty / referral side-effects and their idempotency — those already follow S7 patterns (`LoyaltyService`, `ReferralService`) and are not part of this webhook path.
- Stripe signature verification, the `OrderExists` validator, and tenant-override resolution (`:193-196`) — unchanged.
- Building a general-purpose, polled outbox **framework** for all queues. Minimum viable fix (post-commit dispatch + idempotent consumers) is acceptable; a full transactional-outbox table is the preferred design but only as far as this webhook's two side-effects require.
- Fiscal/`BlockingOnline` receipt-hold logic (`GenerateReceiptFunction.cs:75-90`) — unchanged.
- Frontend, mobile, and i18n — no user-visible string or contract changes (no `nswag-regen`).
- Changing the queue transport / `MessageEncoding = Base64` config (`AzureStorageQueueClient.cs:19-26`).

## Layers touched

- **Backend / AppServices** — `Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs` (move dispatch out of the handler into post-commit, or write an outbox row in-transaction; fix the comment at 136-159).
- **Backend / pipeline** — `Cleansia.Core.AppServices/Behaviors/UnitOfWorkPipelineBehavior.cs` (the post-commit dispatch hook lives here or in an adjacent post-commit behavior, since commit ordering is owned by this behavior at line 19-20).
- **Backend / queue infra** — `Cleansia.Infra.Azure.Storage.Queues/AzureStorageQueueClient.cs` and `Cleansia.Core.Queue.Abstractions/IQueueClient.cs` (only if an outbox-backed `IQueueClient` or a deferred-send buffer is introduced).
- **Functions / consumers** — `Cleansia.Functions/Functions/GenerateReceiptFunction.cs` (harden receipt idempotency, AC4) and the notification-dispatch function behind `QueueNames.NotificationsDispatch` (push de-dup, AC5).
- **Database** *(MANUAL_STEP: ef-migration, owner-only)* — only if the chosen design adds an outbox table or a unique constraint backing the receipt/notification idempotency (e.g. unique `(OrderId)` on `OrderReceipt`, or a `(OrderId, EventKey)` notification-dedup row). Flag as `manual_step: ef-migration`; do not run migrations.
- **Tests** — `Cleansia.Tests` (handler: no pre-commit dispatch, AC1–AC3) and `Cleansia.IntegrationTests` (Stripe-retry-after-failed-commit → exactly-once, AC3).

**No** frontend / Angular, mobile, or NSwag-client changes — contracts unchanged, so **no** `manual_step: nswag-regen`.


---

This confirms the consumer immediately re-reads the order via `CalculateOrderPay.Command` and acks validator rejections without retry — so if the message arrives before commit and the order isn't yet `Completed`, the consumer reads the pre-completion state and either mis-calculates or permanently acks a rejection (no retry recovers it). The GAP is fully grounded. I have everything I need.

# US-customer-0042 — Async side effects must fire only after the order write commits

## Context (grounded in code)

`CompleteOrder.Handler.Handle` enqueues three durable messages **inside the handler body** — `generate-receipt` (`CompleteOrder.cs:219`), `notifications-dispatch` for the "All done!" push (`CompleteOrder.cs:227`), and one `calculate-order-pay` per assigned cleaner (`CompleteOrder.cs:266`). The actual database commit happens **afterwards**, outside the handler, in `UnitOfWorkPipelineBehavior.Handle` (`Behaviors/UnitOfWorkPipelineBehavior.cs:19-20`: `next(...)` runs the handler, *then* `unitOfWork.CommitAsync(...)`).

So the messages are sent against an *uncommitted* `Completed` transition. If the commit fails — or the process dies between enqueue and commit — the consumers run against an order whose completion was never persisted:

- `CalculateOrderPayFunction` (`Cleansia.Functions/Functions/CalculateOrderPayFunction.cs:43-65`) immediately re-sends `CalculateOrderPay.Command`, reads the order, and **acks validator rejections without retrying** ("retrying won't change the validator's verdict") — so a too-early message that hits a pre-completion order is silently swallowed and the pay row is never created.
- `GenerateReceiptFunction` reads the order in its pre-completion state.
- The customer receives an "All done!" push (`NotificationEventCatalog.OrderCompleted`) for an order that is not, in the database, done.

The identical enqueue-before-commit shape exists in the siblings: `CreateOrder.cs:376`, `ConfirmRecurringOrder.cs:112`, `HandlePaymentNotification.cs:241`. There is no transactional outbox reconciling the enqueue with the write.

**Rules this violates (verified in the knowledge base):**
- `agents/knowledge/runtime-readiness.md` checklist item **4** — "Side effects are enqueued (durable + retried), not inline-fire-and-forget" and its dependency matrix row: *"If the enqueue is part of the transaction, a failure should fail the command before committing user-visible state, OR use the outbox pattern… Never 'fire and hope'."*
- `agents/knowledge/consistency.md` **B8** + `agents/knowledge/security-rules.md` **S7** — side-effecting commands must be idempotent and reconciled; here the enqueue ordering breaks the reconciliation guarantee (a side effect can be observed for a write that never landed).

---

## Story

**As a** customer who has just had a cleaning order completed,
**I want** the receipt, the "All done!" push notification, and the cleaner's pay calculation to be triggered **only after my order's completion is durably saved**,
**so that** I never receive a "your order is done" notification (or a receipt) for an order the system later has no record of completing, and my cleaner's pay is reliably calculated for the work that was actually recorded as finished.

*(Persona = customer: the observable, user-facing failure is the false "All done!" push and a receipt for a non-persisted completion. The same fix transitively protects the partner — the pay-calc fan-out — and admin reconciliation.)*

---

## Acceptance Criteria (Given / When / Then)

1. **Side effects do not precede the commit**
   **Given** an order in `InProgress` and a valid `CompleteOrder` command,
   **When** the command is handled,
   **Then** no message is delivered to `generate-receipt`, `notifications-dispatch`, or `calculate-order-pay` until after the database transaction that persists the `Completed` status has committed successfully.

2. **A failed commit produces no orphaned messages**
   **Given** a `CompleteOrder` command whose database commit fails (e.g. transient DB outage at `UnitOfWorkPipelineBehavior.cs:20`),
   **When** the handler had already prepared its three side effects,
   **Then** the operation returns a failure, the order remains in `InProgress`, and **none** of the three consumers ever receive a message for that order (no false "All done!" push, no receipt, no pay row).

3. **A crash between handler and commit is recoverable, not silently dropped**
   **Given** the process dies after the handler runs but before/while committing,
   **When** the system recovers,
   **Then** either (a) the completion was not committed and no side effects were emitted, or (b) the completion was committed and the side effects are still delivered exactly-once-effectively on recovery — there is no state where the order is `Completed` in the DB but its receipt / push / pay-calc were permanently lost, and no state where a side effect fired for an order that is not `Completed`.

4. **Customer push reflects true persisted state**
   **Given** a completed order,
   **When** the `OrderCompleted` push is delivered to the customer,
   **Then** the order is verifiably in `Completed` status in the database at the time the message was emitted.

5. **Pay calculation is not lost to early delivery**
   **Given** the `calculate-order-pay` fan-out for an assigned cleaner,
   **When** `CalculateOrderPayFunction` consumes the message,
   **Then** the order it reads is already `Completed`, so the consumer's "ack-on-validator-rejection, no retry" path (`CalculateOrderPayFunction.cs:55-65`) is never reached merely because the message arrived before the write landed.

6. **The ordering rule is documented and consistently applied**
   **Given** the four enqueue-before-commit sites (`CompleteOrder.cs:219/227/266`, `CreateOrder.cs:376`, `ConfirmRecurringOrder.cs:112`, `HandlePaymentNotification.cs:241`),
   **When** the fix lands,
   **Then** all four follow the same post-commit / outbox ordering rule, and that rule is written into `agents/knowledge/consistency.md` as an extension of **B8** (and cross-referenced from `runtime-readiness.md` item 4).

---

## Out of Scope

- **Idempotency of the consumers themselves** (S7) — `GenerateReceipt`, the notification dispatcher, and `CalculateOrderPay` already have / are assumed to have their own duplicate guards; this story is about *when* messages are emitted, not deduplicating them. Hardening any consumer that lacks an idempotency check is a separate ticket.
- **The unrelated payroll-settlement / admin-intervention / CancelOrder-hardcoding gaps** from the prior audit — tracked separately.
- **Switching the queue technology** (Azure Queue Storage stays); no change to message schemas (`GenerateReceiptMessage`, `SendPushNotificationMessage`, `CalculateOrderPayMessage`).
- **The inline `emailService.SendOrderStatusUpdateEmailAsync` call** (`CompleteOrder.cs:244`) and the `loyaltyService` / `referralService` calls (`:252`, `:254`) — they are not queue enqueues; whether they should also move post-commit is noted as a follow-up, not delivered here.
- **Correlation-id propagation** into the messages (runtime-readiness observability) — desirable but a distinct story.
- **Retry/back-off tuning and dead-letter dashboards** for these queues.

---

## Layers Touched

- **Backend — AppServices (CQRS handlers):** `Features/Orders/CompleteOrder.cs`, `Features/Orders/CreateOrder.cs`, `Features/Orders/ConfirmRecurringOrder.cs`, `Features/Payments/HandlePaymentNotification.cs` — move enqueues out of the handler body.
- **Backend — pipeline / infrastructure:** `Behaviors/UnitOfWorkPipelineBehavior.cs` and the `IUnitOfWork` / DbContext layer — to introduce a post-commit dispatch hook and/or an outbox table written in the same transaction, drained after commit.
- **Backend — Queue abstraction:** `Cleansia.Core.Queue.Abstractions` (`IQueueClient`, message types) — possibly a buffering/outbox-aware send path.
- **Azure Functions (consumers):** `Cleansia.Functions/Functions/CalculateOrderPayFunction.cs` (and `GenerateReceiptFunction.cs`, notification dispatcher) — no behavioral change required, but they are the observable victims and must be regression-tested against the new ordering.
- **Database (if outbox chosen):** new outbox table + EF config/migration — **owner-only manual step** (`MANUAL_STEP: ef-migration`).
- **Knowledge base / docs:** `agents/knowledge/consistency.md` (extend **B8**) and a cross-reference in `agents/knowledge/runtime-readiness.md`.
- **Tests:** `Cleansia.Tests` / `Cleansia.IntegrationTests` — a failed-commit-emits-no-messages test and a happy-path-emits-after-commit test.

*No frontend, mobile, or NSwag-client impact (message schemas unchanged).*


---

I now have all the grounding I need. The GAP is fully confirmed at file:line, and S3/S8 explicitly state the rule it violates. The story is a customer-persona problem (the public marketing/subscribe page shows empty/wrong plans). The "platform-config vs resolve-tenant" fork is real, with `Country` as the not-tenant-scoped precedent and the entire anon catalog (`Service`/`Package`/`Extra`) sharing the footgun.

One correction to the GAP framing I'll fold in: the **loyalty tier reads** (`LoyaltyController.GetTiers`) are NOT on an anonymous route (they require `CanViewMyLoyalty`), so they are not silently-empty for anonymous callers — the pure anonymous bug is the membership-plans route. I'll scope the story to that confirmed defect and treat the broader anon-catalog footgun as related context, not invented scope.

---

# US-customer-0042 — Anonymous membership-plans page returns empty/wrong plans in multi-tenant mode

## Actor narrative

**As a** prospective Cleansia customer browsing the public pricing/subscribe page before signing in,
**I want** to see the correct set of membership plans (e.g. "Cleansia Plus" monthly/yearly with prices, discounts, and the "Save XX%" badge) for the tenant whose site I'm visiting,
**so that** I can compare plans and decide to subscribe — instead of seeing an empty or wrong plan list that makes the product look broken or unavailable.

## Problem (grounded in code)

`GET /api/Membership/GetPlans` is `[AllowAnonymous]` on both the web and mobile customer APIs:
- `src/Cleansia.Web.Customer/Controllers/MembershipController.cs:58-65`
- `src/Cleansia.Web.Mobile.Customer/Controllers/MembershipController.cs:58-65`

It runs `GetMembershipPlans.Query` → `IMembershipPlanRepository.GetActivePlansAsync` (`src/Cleansia.Core.AppServices/Features/Memberships/GetMembershipPlans.cs:42`), reading `MembershipPlan`, which is `ITenantEntity` (`src/Cleansia.Core.Domain/Memberships/MembershipPlan.cs:24`).

The global query filter (`src/Cleansia.Infra.Database/CleansiaDbContext.cs:111-177`) resolves the tenant via `TenantProvider.GetCurrentTenantId()`, which on an anonymous request has no `tenant_id` claim and no override, so it returns `null` (`src/Cleansia.Infra.Database/TenantProvider.cs:12-20`). With `currentTenantId == null`, the filter body collapses to `e.TenantId == null` (the `singleTenantMatch` clause, `CleansiaDbContext.cs:154-156`). Net effect in any **multi-tenant** deployment: the anonymous plans route returns **only null-tenant rows** — wrong/empty for every real tenant — while the authenticated subscribe flow (`Subscribe`, JWT present) sees the correct tenant's plans. The inverse footgun: a plan seeded with `TenantId == null` as a "shared" plan would leak to **every** tenant's anonymous page.

This directly violates the project's own rules:
- **S3** (`agents/knowledge/security-rules.md:54-56`): "For `[AllowAnonymous]` endpoints there is no tenant claim, so the global filter is bypassed — anonymous routes must not return tenant-scoped data unless gated by a different shared secret."
- **S8** (`agents/knowledge/security-rules.md:93-98`): entities are either `ITenantEntity` or documented "true platform config" — this decision was not made explicitly for `MembershipPlan`.

Precedent for the fix fork already exists in the codebase: `Country` is deliberately **not** `ITenantEntity` (`src/Cleansia.Core.Domain/Internationalization/Country.cs:7`) i.e. true platform config served anonymously, whereas `Service`/`Package`/`Extra` are all `ITenantEntity` and also served on `[AllowAnonymous]` routes — so whichever model is chosen for plans should be applied consistently.

## Acceptance criteria

1. **Given** a multi-tenant deployment with active plans seeded for tenant *T*, **when** an anonymous client calls `GET /api/Membership/GetPlans` against tenant *T*'s public site (no JWT), **then** the response contains tenant *T*'s active plans with correct `Price`, `MonthlyEquivalentPrice`, `DiscountPercentage`, `BillingInterval`, and `SavingsPercentVsMonthly`.

2. **Given** two tenants *T1* and *T2* each with distinct active plans, **when** an anonymous client resolves to *T1*, **then** it sees only *T1*'s plans and never *T2*'s plans (no cross-tenant leakage and no empty list).

3. **Given** the same anonymous response and the subsequent authenticated `Subscribe`/`GetMine` flow for a logged-in customer of the same tenant, **when** both are observed, **then** the plan set, codes, and prices shown anonymously match those available to subscribe — i.e. plan visibility no longer depends on whether a JWT is present.

4. **Given** the chosen design, **when** the resolution model is decided, **then** it is one of (a) tenant resolved from the request host/subdomain and applied via `ITenantProvider.SetTenantOverride(...)` **before** the query for the anonymous route, or (b) `MembershipPlan` is reclassified as true platform config (not `ITenantEntity`) with the reason documented per S8 — and the same decision is applied to both web and mobile `GetPlans`.

5. **Given** a request that resolves to an unknown or unconfigured tenant host, **when** `GetPlans` is called, **then** it returns an empty list (or a defined fallback) deterministically rather than silently returning null-tenant rows, and the behavior is documented.

6. **Given** the response DTO `GetMembershipPlans.Response`, **when** plans are returned, **then** `TenantId` is never exposed in the payload (S4), and the existing fields/shape remain unchanged so the NSwag-generated customer client does not break.

## Out of scope

- Any change to the authenticated membership flows: `Subscribe`, `Cancel`, `GetMine`, `CreateCheckoutSession`, `SwapPlan` — they already resolve tenant from the JWT and are not part of this defect.
- The **loyalty tier** reads: `LoyaltyController.GetTiers` / `GetLoyaltyTiers` are **not** anonymous (they require `Permission(CanViewLoyaltyTier/CanViewMyLoyalty)` — `src/Cleansia.Web.Customer/Controllers/LoyaltyController.cs:41-51`), so they are not silently-empty for anonymous callers; no change to them here. (The GAP's "loyalty tier reads on an anonymous route" framing does not hold for the current Loyalty controllers.)
- Fixing the identical anonymous-tenant-collapse footgun on the other `ITenantEntity` catalog routes (`Service`, `Package`, `Extra` on their `[AllowAnonymous]` endpoints) — same root cause, but separate tickets; flag as a follow-up, do not bundle.
- Building the admin back-office UI to manage membership plans (the repository doc notes that surface "doesn't exist yet").
- Stripe Product/Price registration, plan seeding content, and pricing copy.
- Host/subdomain → tenant mapping infrastructure if it does not already exist (if option (a) is chosen and no host→tenant resolver exists, that is its own prerequisite ticket — call it out as a dependency).

## Layers touched

- **Backend — API host (anonymous route):** `Cleansia.Web.Customer` and `Cleansia.Web.Mobile.Customer` `MembershipController.GetPlans` (and/or middleware to set tenant override pre-query for anonymous tenant-scoped reads).
- **Backend — Domain/entity decision:** `Cleansia.Core.Domain` `MembershipPlan` (`ITenantEntity` keep-and-resolve vs. reclassify as platform config) and, if reclassified, `Cleansia.Infra.Database` entity configuration + global filter applicability.
- **Backend — query/handler:** `Cleansia.Core.AppServices` `GetMembershipPlans` (only if tenant resolution is handled here rather than at the host).
- **Data / MANUAL_STEP:** if `MembershipPlan` is reclassified (drop `TenantId`/change unique index from `(TenantId, Code)` to `(Code)`) an EF migration is required — owner-applied (`MANUAL_STEP`), per S9 add-don't-rename rules.
- **Contracts / MANUAL_STEP:** keep `GetMembershipPlans.Response` shape stable; if anything in the DTO changes, flag `manual_step: nswag-regen` for the customer client.
- **No frontend/Android code change expected** beyond re-consuming a regenerated client only if the contract changes (it should not).

Relevant files (absolute):
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\MembershipController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Mobile.Customer\Controllers\MembershipController.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Memberships\GetMembershipPlans.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Memberships\MembershipPlan.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Database\CleansiaDbContext.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Infra.Database\TenantProvider.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Internationalization\Country.cs` (platform-config precedent)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\agents\knowledge\security-rules.md` (S3:54-56, S8:93-101)


---

I have enough grounding from the actual code. The knowledge files referenced in the env are at the repo root `agents/` directory; the S1/S3 laws are already cited in the GAP. The code confirms every claim:

- `AddDisputeMessage.cs:37` — `IsStaffMessage` is a wire field on the command record.
- `AddDisputeMessage.cs:50` — ownership check `dispute.UserId != userId` is short-circuited by `!request.IsStaffMessage`.
- `AddDisputeMessage.cs:59` — flag is passed straight into `dispute.AddMessage(... isStaff: request.IsStaffMessage)`.
- `AddDisputeMessage.cs:65` — `IsStaffMessage` true fires the `DisputeReply` push to the dispute owner.
- `DisputeController.cs:53` — gated only by `Policy.CanRespondToDispute`, which `policy.ts:233` maps to `PhysicalPolicy.Authenticated` (not CustomerOnly).
- `disputes.facade.ts:109` — legit client hardcodes `isStaffMessage: false`, proving it's purely a wire artifact.
- `disputes.component.html:228` — `msg.isStaffMessage` toggles the `--staff` styling that renders the message as an official support reply.

Here is the user story.

---

# US-customer-0007 — Server-side staff/customer authorship and ownership enforcement for dispute messages

## Persona
`customer` (the vulnerability is reachable by any authenticated customer; the fix also formalizes the legitimate admin/support path)

## Severity / Type
Security — Privilege escalation + Broken access control (S1 server-truth, S3 ownership-in-handler). Confirmed functional gap, not theoretical.

## Actor narrative

> **As a** customer using the disputes feature,
> **I want** the platform to decide message authorship (customer vs. official support) and to enforce dispute ownership entirely on the server,
> **so that** no one can post into a dispute they don't own or impersonate Cleansia support, and I can trust that any message styled as an official staff reply genuinely came from staff.

### Grounding (why this story exists)

The `AddMessage` endpoint trusts a client-supplied boolean to decide both authority and access:

- `AddDisputeMessage.Command` carries `bool IsStaffMessage` straight off the wire (`AddDisputeMessage.cs:37`). The legitimate customer client always sends `false` (`disputes.facade.ts:109`), confirming the field has no business reason to be client-controlled.
- The handler skips the ownership check whenever the flag is `true`: `if (!request.IsStaffMessage && dispute.UserId != userId)` (`AddDisputeMessage.cs:50`). So `isStaffMessage=true` lets a caller write into **any** dispute by id, owned by anyone.
- The same flag is passed into the domain as `isStaff: request.IsStaffMessage` (`AddDisputeMessage.cs:59`) and, when `true`, fires a `DisputeReply` push to the dispute's real owner (`AddDisputeMessage.cs:65`).
- The customer UI renders `msg.isStaffMessage` with the official-support styling (`disputes.component.html:228`), so the forged message appears to the victim as a genuine staff reply.
- The endpoint is gated only by `Policy.CanRespondToDispute` (`DisputeController.cs:53`), which maps to `PhysicalPolicy.Authenticated` — **not** customer- or admin-scoped (`policy.ts:233`). Any logged-in principal passes.

Net effect: an authenticated customer can POST `{ disputeId: <someone else's>, message: "...", isStaffMessage: true }` to `/api/Dispute/AddMessage` and (a) write into a stranger's dispute and (b) have it delivered + rendered to that stranger as an impersonated Cleansia support reply.

## Acceptance Criteria

**AC1 — Authorship is server-derived, never client-supplied**
> **Given** any caller invokes `POST /api/Dispute/AddMessage` from the Customer API
> **When** the request body includes an `isStaffMessage` (or equivalent) flag set to `true`
> **Then** the flag is ignored and the message is persisted with `isStaff = false`, because authorship is derived from the authenticated caller's role claim on the server, not from the request payload.

**AC2 — Ownership is enforced unconditionally for customers**
> **Given** an authenticated customer whose `userId` does not match `dispute.UserId`
> **When** they call `AddMessage` for that dispute (with any value of any client flag)
> **Then** the request is rejected with the existing `DisputeNotOwnedByUser` business error and no message is written, no notification is sent — the ownership check can no longer be short-circuited.

**AC3 — Customers cannot trigger the staff notification path**
> **Given** an authenticated customer posts a valid message into their own dispute
> **When** the message is stored
> **Then** no `DisputeReply` push notification is dispatched to the dispute owner (the staff→customer push fires only for genuine staff-authored replies).

**AC4 — Legitimate customer flow is unchanged**
> **Given** a customer viewing a dispute they own
> **When** they send a message via the existing UI
> **Then** the message is saved as a customer message, appears in the thread without the official-support styling, and the call still satisfies `CanRespondToDispute` — no UI or contract change is visible to the honest user.

**AC5 — The staff reply path is admin-scoped and server-authenticated**
> **Given** a genuine support/admin operator authenticated against the Admin host
> **When** they reply to a dispute through the admin staff-reply path
> **Then** the message is persisted with `isStaff = true`, the owner receives the `DisputeReply` push, and this staff authority is granted by the operator's admin role/policy — never by a customer-host caller and never by a request flag.

**AC6 — Contract no longer exposes the escalation field**
> **Given** the regenerated customer API client and OpenAPI contract
> **When** the `AddMessage` command shape is inspected
> **Then** it contains only `disputeId` and `message` (no `isStaffMessage`), so the privilege-escalation field is removed from the wire surface entirely.

## Out of scope

- Reworking dispute creation, status transitions, resolution, or evidence upload (`CreateDispute`, `CanResolveDispute`, `CanUpdateDisputeStatus`, `UploadEvidence`).
- Building or redesigning the admin support reply UI beyond wiring/exposing the server-authenticated staff path required by AC5 (if an admin reply endpoint already exists, reuse it; do not build a new dispute console).
- The broader, separately-tracked GAPs noted in the brief: admin order intervention, payroll adjustment/settlement lifecycle, and the customer-hardcoded `CancelOrder`. Each gets its own story.
- Auditing other endpoints for the same client-trusted-flag pattern (recommend a follow-up sweep, but this story fixes disputes only).
- Notification template / copy changes for `DisputeReply` (BUG-22 territory).
- Migrating or backfilling historical dispute messages whose `isStaff` value may have been forged.

## Layers touched

- **Backend — AppServices (CQRS):** `Features/Disputes/AddDisputeMessage.cs` — drop `IsStaffMessage` from `Command` (`:37`); derive `isStaff` from role claim via the session/role provider; make the ownership check unconditional (`:50`); gate the push on the server-derived staff value (`:65`).
- **Backend — Web.Customer:** `Controllers/DisputeController.cs:52-62` `AddMessage` action and its `Policy.CanRespondToDispute` binding — confirm/tighten so the customer host only ever produces customer-authored messages.
- **Backend — Authorization / Policy:** `PolicyBuilder.cs:76` and the `CanRespondToDispute → Authenticated` mapping (`policy.ts:233`) — re-scope the customer respond path and ensure the staff path lives behind an admin-only policy/host.
- **Backend — Web.Admin (if staff replies route here):** the admin dispute-reply endpoint must be the only producer of `isStaff = true`.
- **Frontend — Customer app:** `libs/cleansia-customer-features/disputes/.../disputes.facade.ts:109` (remove the now-defunct `isStaffMessage: false`) and the generated `customer-client.ts` `AddDisputeMessageCommand` shape; `disputes.component.html:228` rendering remains correct once authorship is trustworthy.
- **Contract / NSwag:** customer client regeneration after the command shape changes — **owner-only `manual_step: nswag-regen`** (do not run `npm run generate-customer-client`).
- **Tests:** xUnit handler/integration tests for AC1–AC3 and AC5 (cross-user write rejected, client flag ignored, no push for customer messages, admin path still produces staff message + push).

---

**Note for the PM/reviewer:** No database migration is implied — `isStaff`/`IsStaffMessage` already exist on the dispute message store; only the source of truth for that value changes. The single load-bearing line to fix is `AddDisputeMessage.cs:50`, where `!request.IsStaffMessage` makes ownership optional.


---

This is a critical grounding detail. The order-taking gate (`TakeOrder.cs:117`) checks `ContractStatus != Pending` — meaning a **Rejected or Terminated** cleaner would currently pass this gate (since their status is not Pending), and a never-set `Terminated` status means there's no way to actually stop a cleaner from working. This confirms the GAP's "Update the order-taking gate" note and strengthens the impact.

I have everything I need. Here is the user story, fully grounded in the cited code.

---

# US-admin-0042 — Employee contract status lifecycle (off-boarding, termination, and reversal)

## Context (grounded in code)

`ContractStatus` (`src/Cleansia.Core.Domain/Enums/ContractStatus.cs`) defines five values: `Pending=1, Active=2, Terminated=3, Approved=4, Rejected=5`. Only three are ever **written**:

- `Pending` is the seeded/registration state.
- `Approved` is set by `Employee.Approve()` (`Employee.cs:229`), invoked only from `ApproveEmployee.cs:115`.
- `Rejected` is set by `Employee.Reject()` (`Employee.cs:243`), invoked only from `RejectEmployee.cs:72`.

`Active` and `Terminated` are **never assigned by any command**. The generic setter `Employee.UpdateContractStatus()` (`Employee.cs:223`) has **no caller anywhere in the backend** (confirmed by repo-wide grep). Yet both orphaned values are actively **read** in production paths, so they are not merely cosmetic dead enum members — the system already behaves as if they can occur:

- `EmployeeRepository.GetAllActiveWithUserAsync()` (`EmployeeRepository.cs:31`) filters out `Terminated` — a filter that can never match anything today.
- `RequireCompleteProfileAttribute.cs:35` and `registration-completion.service.ts:76` grant resource access when status is `Approved` **or** `Active`.
- `NewJobsDigestService.cs:68-69` and `PeriodReminderBackgroundService.cs:83` branch on `Active`/`Terminated`.
- Admin UI offers `Active` and `Terminated` as **filter options** (`employee-management.helpers.ts:30-33`, `employee-management.facade.ts:60,78`) for statuses no employee can ever hold.

On the write side, the admin UI gate `canApproveOrReject()` (`employee-detail.facade.ts:321-327`) only enables actions when `contractStatus === Pending`. So once a cleaner is `Approved` or `Rejected`, **no further status change is possible** — no termination, no reversal of a mistaken rejection, no re-instatement.

Compounding this, the order-taking gate `TakeOrder.cs:117` (`HasUploadedDocumentsAsync`) only blocks `Pending`. It returns `true` for **every other status**, so a `Rejected` cleaner is not blocked from taking orders, and there is no `Terminated` state to block a fired cleaner with. "Can work" is currently defined as "not Pending," which is the wrong gate once a real terminal state exists.

This story makes the contract lifecycle a complete, enforced state machine. The canonical transitions and the precise semantics of `Active` (whether it is a distinct "onboarded & working" state or should be collapsed into `Approved`) are an **Architect ADR decision** — this story depends on that ADR and does not pre-decide it.

---

## User story

**ID:** US-admin-0042

**As an** admin operating the partner workforce,
**I want** to move an employee through a complete, enforced contract-status lifecycle after the initial approve/reject decision — including terminating an active cleaner (with a reason), reversing a mistaken rejection, and re-instating a terminated cleaner —
**so that** I can off-board misbehaving or departed cleaners, correct human errors, and keep the workforce list accurate, while the platform reliably stops terminated/rejected cleaners from being able to take new work.

---

## Acceptance criteria (Given/When/Then)

> Exact transitions and whether `Active` is distinct from `Approved` are fixed by the ADR (see Dependencies). The criteria below are written against the lifecycle the ADR confirms; "a non-working terminal state" = `Terminated`, "a working state" = `Approved`/`Active` per the ADR.

1. **Termination is reachable and recorded**
   Given an employee in a working state (`Approved`/`Active`),
   When the admin terminates them with a reason,
   Then the employee's `ContractStatus` becomes `Terminated`, the terminating admin, timestamp, and reason are persisted, and the action succeeds via a `BusinessResult` (no other field is mutated). The termination reason is required and validated (FluentValidation, `Cascade.Stop`, max-length error keyed in `BusinessErrorMessage`).

2. **Terminated/Rejected cleaners cannot take work**
   Given an employee whose status is `Terminated` (or `Rejected`),
   When they attempt to take an order,
   Then `TakeOrder` rejects the attempt with the appropriate `BusinessErrorMessage`, i.e. the order-taking gate is changed from "not `Pending`" to "is in a working state" so the terminal states actually block work (closing the `TakeOrder.cs:117` hole, coordinated with EMP-GAP-01).

3. **A mistaken rejection can be reversed**
   Given an employee whose status is `Rejected`,
   When the admin reverses the rejection (an allowed transition per the ADR),
   Then the status returns to the ADR-defined target (e.g. `Pending` or a working state), the prior rejection metadata (`RejectionReason`, `RejectedByUserId`, `RejectedAt`) is cleared consistent with the existing `Approve()`/`Reject()` mutual-exclusion pattern at `Employee.cs:236-238,250-252`, and the change is persisted.

4. **Re-instatement of a terminated cleaner**
   Given an employee whose status is `Terminated`,
   When the admin re-instates them (an allowed transition per the ADR),
   Then the status moves to the ADR-defined working state and the employee reappears in `GetAllActiveWithUserAsync()` results (which already excludes `Terminated` at `EmployeeRepository.cs:31`) and in downstream notification/reminder eligibility.

5. **Illegal transitions are refused, not silently applied**
   Given any employee,
   When the admin requests a status change that is not an allowed transition in the ADR state machine (e.g. terminate a `Pending` applicant, or approve a `Terminated` one),
   Then the command fails with a clear `BusinessErrorMessage` (e.g. `employee.invalid_status_transition`) and the employee's status is unchanged. Transitions are enforced in the domain/handler — the generic `Employee.UpdateContractStatus()` setter is not exposed as an unguarded write path.

6. **Admin UI exposes the full lifecycle correctly**
   Given the employee detail screen,
   When an employee is in a state with allowed transitions,
   Then the UI offers exactly those actions (replacing the `Pending`-only `canApproveOrReject()` gate at `employee-detail.facade.ts:321-327` with per-status allowed-action logic), each action confirms and shows success/error via `SnackbarService`, and all new labels/messages exist in **all 5 locales** (en, cs, sk, uk, ru). If the ADR removes `Active`/`Terminated` as a selectable state, the corresponding filter options (`employee-management.helpers.ts:30-33`, `employee-management.facade.ts:60,78`) are updated to match the real domain — no UI offering a status the backend can never produce.

---

## Out of scope

- **The lifecycle decision itself** — defining the canonical state machine and resolving whether `Active` is a real distinct state or should be merged into `Approved`/deleted. That is the prerequisite **Architect ADR**, not this story's deliverable.
- **EF Core migration execution** — any schema delta (e.g. new `TerminatedAt`/`TerminatedByUserId`/`TerminationReason` columns) is flagged `manual_step: ef-migration` for the owner; this story does not run migrations.
- **NSwag client regeneration** — flagged `manual_step: nswag-regen` after the new command DTOs/endpoints land; dependent frontend work waits for owner confirmation.
- **Deleting unused enum values** — if the ADR collapses the model, the actual enum-value removal / DB-column handling is a separate follow-up ticket (per the "never delete DB columns in code" convention), not this story.
- **Self-service / partner-app off-boarding** — a cleaner resigning or pausing themselves from the partner app or Android app. Admin-initiated only.
- **GDPR anonymization / hard delete** — `Employee.Anonymize()` (`Employee.cs:257`) and data erasure are a separate flow.
- **Pay/payroll consequences of termination** (final settlement, blocking future pay periods) — separate payroll-lifecycle tickets.
- **Email/push notifications to the cleaner** on termination/reversal — not required for this story unless the ADR mandates it.
- **Bulk status changes** — single-employee operations only.

---

## Layers touched

- **Domain** — `src/Cleansia.Core.Domain/Users/Employee.cs`: add guarded transition methods (e.g. `Terminate(byUserId, reason)`, `Reactivate(...)`, `ReverseRejection(...)`) mirroring the existing `Approve()`/`Reject()` mutual-exclusion pattern; encode allowed transitions; retire/guard the unguarded `UpdateContractStatus()` setter. Possibly new persisted fields (termination metadata).
- **App Services (CQRS/MediatR)** — `src/Cleansia.Core.AppServices/Features/Employees/`: new commands + handlers + FluentValidation validators (transition-legality + reason rules), new `BusinessErrorMessage` keys (e.g. `employee.invalid_status_transition`). Update `TakeOrder.cs` "can work" gate (coordinate with EMP-GAP-01).
- **Infra / Database** — `EmployeeEntityConfiguration.cs` mapping for any new fields; `manual_step: ef-migration`. `EmployeeRepository` "active" query semantics confirmed against the final state machine.
- **Web (Admin API)** — new admin endpoints on the employee controller in `Cleansia.Web.Admin`, with the existing `*ApiController` + `HandleResult` + `Policy.CanXxx` RBAC pattern.
- **Frontend (Admin app)** — `libs/cleansia-admin-features/employee-management/`: `employee-detail.facade.ts` (replace `canApproveOrReject()` with per-status action availability) + component/template actions; reconcile `employee-management.helpers.ts` / `employee-management.facade.ts` filter options; i18n keys in all 5 locales; `manual_step: nswag-regen` for the generated admin client.
- **Tests (xUnit, test-first)** — transition state-machine unit tests (legal + illegal transitions), validator tests (required reason), and the updated `TakeOrder` gate behavior for `Terminated`/`Rejected`.

## Dependencies / cross-references

- **Blocked by ADR** — "Employee contract status state machine" (Architect): canonical transitions and the fate of `Active`.
- **EMP-GAP-01** — the order-taking eligibility gate change in AC-2 must be coordinated with that ticket's definition of "can work."
- Owner manual steps: `manual_step: ef-migration`, `manual_step: nswag-regen`.


---

I have everything I need. Critical design facts the story must respect, all grounded in code:

1. **No DB uniqueness** — `UserEntityConfiguration.cs` defines `Email`/`PhoneNumber` as `citext` columns with `HasMaxLength` but **zero `HasIndex(...).IsUnique()`**. Confirmed.
2. **TOCTOU + scan** — `Register.Validator.UserWithEmailNotExistsAsync` (line 45-49) and `ExistsWithEmailAsync` do an `AnyAsync`/`FirstOrDefaultAsync` scan; two concurrent registrations both pass.
3. **Re-registration nuance** — `Register` deliberately allows a duplicate email row *path* only when the existing user is unconfirmed; the Handler then **reuses** that row (`GetByEmailAsync` → `UpdateConfirmationCode`) rather than inserting. So a naive `UNIQUE(Email)` won't break the re-registration flow because no second row is inserted — the constraint must be plain unique on `Email`.
4. **Anonymize uses `deleted_{Id}@anonymized.local`** (line 202) — distinct per user, so GDPR anonymization won't collide under a unique email index. Good — the story must call this out as a thing not to break.
5. **PhoneNumber is nullable** — unique index must permit multiple NULLs (Postgres default), and `Anonymize` sets it to `null`. A partial/standard unique index is fine.
6. **GoogleId** — also unique-worthy but out of scope per the GAP (Email/PhoneNumber only).
7. **Owner-only**: the migration itself is `manual_step: ef-migration` per conventions.md line 99.

The literal "PERF-IDA-01" ID isn't in the backlog (the perf/index work appears to be referenced externally), so I'll reference it as the named dependency the GAP cites without fabricating a backlog row.

---

# US-customer-0042 — Database-enforced uniqueness for user email & phone

**Type:** Correctness (data integrity) · **Priority:** High — lands with the index/perf work (PERF-IDA-01)
**Persona:** customer (the registration & profile-update flows are customer-facing; partner/admin users share the same `User` table, so the constraint protects all three)

## Context (grounded in code)

Uniqueness of `User.Email` and `User.PhoneNumber` is enforced **only by application-code pre-checks**, with **no backing database constraint**:

- `Register.cs:32-49` — `UserWithEmailNotExistsAsync` reads the user via `GetByEmailAsync` and decides in app memory whether the email is free. Two concurrent registrations can both pass this check before either commits (TOCTOU race), producing two rows for the same email.
- `UpdateCurrentUser.cs:56-78` — `UserWithPhoneNumberNotExistsAsync` does the same read-then-decide for phone numbers; concurrent profile updates can both claim the same number.
- `UserEntityConfiguration.cs:28-35` — `Email` and `PhoneNumber` are `citext` columns with a max length but **no `HasIndex(...).IsUnique()`**. The check methods (`ExistsWithEmailAsync`, `GetByPhoneNumberAsync` in `UserRepository.cs:35-37,57-60`) run as table scans.

The fix is to add a unique index (shipping with PERF-IDA-01's index migration) so the database is the source of truth for correctness, and demote the existing app-code checks to a fast-path UX message.

Two existing behaviors the constraint must **not** break (both verified in code):
- **Re-registration of an unconfirmed account:** `Register` deliberately *reuses* the existing row when the user is unconfirmed (`Register.cs:74-85` — `GetByEmailAsync` then `UpdateConfirmationCode`); it never inserts a second row, so a unique email index is compatible with this flow.
- **GDPR anonymization:** `User.Anonymize()` (`User.cs:198-215`) rewrites email to a per-user value `deleted_{Id}@anonymized.local` and sets phone to `null`, so anonymized rows do not collide under the new constraints.

## Story

**As a** customer (and, transitively, every partner and admin who shares the `User` table)
**I want** my email address and phone number to be guaranteed unique at the database level
**so that** two people can never end up with duplicate accounts on the same email or phone — even under simultaneous sign-ups or profile edits — and account lookup, login, and notifications stay unambiguous.

## Acceptance Criteria

**AC1 — Email uniqueness is enforced by the database**
**Given** the system has a confirmed user with email `a@x.com`
**When** a process attempts to INSERT a second distinct `User` row with email `a@x.com` (case-insensitively, since the column is `citext`)
**Then** the database rejects the write with a unique-constraint violation, independently of any application pre-check.

**AC2 — Phone uniqueness is enforced by the database**
**Given** a user already owns phone number `+420123456789`
**When** another user's profile update attempts to persist the same phone number
**Then** the database rejects the write with a unique-constraint violation; **and** multiple users with a `NULL` phone number remain allowed (NULL is not constrained).

**AC3 — Concurrent registration race is closed**
**Given** two registration requests for the same brand-new email arrive concurrently and both pass the in-memory `UserWithEmailNotExistsAsync` pre-check
**When** both attempt to commit
**Then** exactly one succeeds and the other fails on the database constraint — never two rows for the same email.

**AC4 — App-code check becomes a friendly fast-path, not the only guard**
**Given** a user submits an email/phone that already exists
**When** the request is validated
**Then** they still receive the existing localized business error (`ExistingUserWithEmail` / `ExistingPhoneNumber`, present in all 5 locales) for a clean UX; **and** if that check is bypassed by a race, the database constraint violation is surfaced as the same user-facing "already in use" error rather than an unhandled 500.

**AC5 — Existing legitimate flows still pass**
**Given** the unique indexes are in place
**When** (a) an unconfirmed user re-registers on the same email, or (b) a GDPR delete anonymizes a user, or (c) two users each have a `NULL` phone
**Then** all three operations succeed — re-registration reuses the existing row, anonymization writes a per-user `deleted_{Id}@anonymized.local` email, and NULL phones do not collide.

**AC6 — Migration is delivered as an owner-run manual step**
**Given** adding unique indexes is a schema change
**When** the work is handed off
**Then** the EF Core migration is flagged `manual_step: ef-migration` with the exact schema delta described (unique index on `Email`; unique index on `PhoneNumber` permitting multiple NULLs), folded into the PERF-IDA-01 index migration; Claude does not run `dotnet ef`.

## Out of Scope

- Uniqueness on `User.GoogleId` (separate concern; the GAP covers only Email/PhoneNumber).
- Phone-number **format/normalization** or canonicalization (e.g. E.164) — uniqueness is enforced on the value as stored; normalization is a separate story.
- Email-confirmation / verification flow changes — the unconfirmed-user re-registration path stays as-is.
- Merging or de-duplicating any **pre-existing** duplicate rows already in the database (a data-cleanup task that must precede the migration in any non-empty environment) — flagged as a migration prerequisite, but the remediation script is its own ticket.
- The broader PERF-IDA-01 performance index work beyond the two uniqueness indexes (this story only adds the *uniqueness* aspect on top of that migration).
- Frontend form-level async uniqueness validators / new UI — existing validation messages are reused unchanged.
- Multi-tenancy scoping of uniqueness (whether email is unique per-tenant vs. global) — current `User` uniqueness is global; if per-tenant scoping is desired, that is an Architect/ADR decision, not assumed here.

## Layers Touched

| Layer | What changes | File(s) |
|---|---|---|
| **DB / EF config** | Add `HasIndex(u => u.Email).IsUnique()` and `HasIndex(u => u.PhoneNumber).IsUnique()` (NULLs permitted) | `src/Cleansia.Infra.Database/EntityConfigurations/UserEntityConfiguration.cs` |
| **DB migration (owner-only)** | Generate + apply the unique-index migration; fold into PERF-IDA-01 | `manual_step: ef-migration` |
| **Backend / AppServices** | Keep app-side pre-checks as fast-path UX; ensure a constraint violation maps to the existing `ExistingUserWithEmail` / `ExistingPhoneNumber` business error instead of an unhandled exception | `src/Cleansia.Core.AppServices/Features/Auth/Register.cs`, `src/Cleansia.Core.AppServices/Features/Users/UpdateCurrentUser.cs` (+ the command pipeline's exception-to-`BusinessResult` handling) |
| **Tests** | TDD: failing test proving two concurrent inserts on the same email yield exactly one row (AC3), NULL-phone multiplicity (AC2), re-registration + anonymization still pass (AC5) | `src/Cleansia.IntegrationTests` (DB-level), `src/Cleansia.Tests` (validator fast-path) |

**Note:** No NSwag regeneration required — DTOs and endpoint contracts are unchanged, so no `manual_step: nswag-regen`.


---

I now have full grounding. Here is the user story.

---

# US-admin-0007 — Admin dispute management surface (Admin API + admin web feature)

## Context (grounded in code)

The dispute admin lifecycle exists in the domain and app-service layers but has **no admin product surface**:

- The admin-only actions are mounted on the **Partner API host**: `src/Cleansia.Web.Partner/Controllers/DisputeController.cs:66-88` exposes `Resolve` (`Policy.CanResolveDispute`) and `UpdateStatus` (`Policy.CanUpdateDisputeStatus`). Both policies map to `PhysicalPolicy.AdminOnly` (`src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:77-78`). `AddMessage` (line 54-64) is `CanRespondToDispute = Authenticated` and can carry a staff message (`AddDisputeMessage.Command.IsStaffMessage`, `AddDisputeMessage.cs:34-38`).
- The read paths on the same Partner controller are `CustomerOnly`: `GetPaged`/`GetById`/`GetDisputeList` (`PolicyBuilder.cs:74-75`). Yet the query handler `GetPagedDisputes.Handler` (`GetPagedDisputes.cs:30-41`) is already **role-aware** — `Administrator` sees all disputes, everyone else is scoped to their own `UserId`. That admin branch is unreachable today because no admin-scoped host/policy exposes the list.
- There is **no `AdminDisputeController`** in `src/Cleansia.Web.Admin/Controllers/` (which already holds `AdminOrderController`, `AdminInvoiceController`, `AdminPayConfigController`, `AdminPayPeriodController`, etc.).
- There is **no `dispute*` feature** under `src/Cleansia.App/libs/cleansia-admin-features/` and **no dispute route/nav** anywhere in `src/Cleansia.App/apps/cleansia-admin.app/src/app`. The only "dispute" strings in admin-features are incidental references inside order/invoice facades.
- The domain already supports the full lifecycle: `Dispute.Resolve/UpdateStatus/Close/Escalate/AddMessage` (`Dispute.cs:64-102`) over `DisputeStatus { Pending, UnderReview, WaitingForResponse, Resolved, Closed, Escalated }` (`DisputeStatus.cs:6-14`).

**Consequence:** an administrator using the admin app cannot list, open, message, escalate, status-change, or resolve disputes. The only way to reach the workflow is to authenticate as an admin and call admin-only endpoints physically hosted on the Partner API — a cross-host inconsistency. This mirrors the prior audit's "admin order intervention is missing" pattern.

**Cited rules this violates:** consistency rule **A8** (the codebase's stated convention — admin-sees-all role scoping is already implemented in `GetPagedDisputes`, but only `order-management`/`invoice-management` get a real Admin host + admin-features surface to exercise it); the canonical admin archetype is **Admin API controller + `*-management` list + `*-detail`** feature (per `order-management`, `invoice-management`). Security rule **S2/S3** note that authorization must hold "regardless of which API host exposes it" — relying on the Partner host for an admin-only action is exactly the fragility the rule warns about.

---

## Story

**Id:** `US-admin-0007`
**Persona:** admin
**Title:** Manage customer disputes from the admin app

> **As an** administrator,
> **I want** to find, open, communicate on, change the status of, and resolve customer disputes from the admin application (backed by the Admin API),
> **so that** I can run the full dispute-resolution workflow on the product's admin surface instead of calling admin-only endpoints that are physically hosted on the Partner API.

---

## Acceptance Criteria (Given / When / Then)

**AC1 — Admin can list and filter disputes**
- **Given** I am authenticated as an `Administrator` in the admin app,
- **When** I open the Disputes section,
- **Then** I see a paged, sortable list of **all** disputes across customers (not scoped to one user), with the existing `DisputeFilter` controls (status, customer email/name, etc.), served by a new **Admin API** dispute list endpoint that reuses `GetPagedDisputes` and returns its `Administrator`-branch (all rows).

**AC2 — Admin can open a dispute detail**
- **Given** I am on the disputes list,
- **When** I open a dispute,
- **Then** I see its detail (order ref, customer, reason, description, current `DisputeStatus`, messages, evidence, resolution notes, refund amount) via the Admin API `GetById`, presented in the canonical `*-detail` admin layout (mirroring `order-detail`/`invoice-detail`).

**AC3 — Admin can post a staff message**
- **Given** I am viewing a dispute detail,
- **When** I submit a message,
- **Then** it is recorded as a **staff** message (`IsStaffMessage = true`) via the Admin API, the customer-notification side effect in `AddDisputeMessage` fires for the customer, and the new message appears in the thread without leaving the page.

**AC4 — Admin can change dispute status**
- **Given** I am viewing a non-terminal dispute,
- **When** I set its status (e.g. `UnderReview`, `WaitingForResponse`, `Escalated`),
- **Then** the Admin API `UpdateStatus` is invoked, the dispute's `Status` and audit (`UpdatedBy`/`UpdatedOn`) reflect the change, and the list/detail show the new status.

**AC5 — Admin can resolve a dispute with optional refund**
- **Given** I am viewing an unresolved dispute,
- **When** I resolve it with required resolution notes (≤2000 chars) and an optional non-negative refund amount,
- **Then** the Admin API `Resolve` is invoked, `Dispute.Resolve` sets `Status = Resolved`, `ResolvedBy`, `ResolvedOn`, `RefundAmount`, `ResolutionNotes`, and validation rejects an empty/over-length note or a negative refund with the existing `BusinessErrorMessage` keys (translated in all 5 locales).

**AC6 — Admin actions are authorized on the Admin host and removed from the Partner host**
- **Given** the dispute admin actions are exposed via the Admin API with their admin-only policies,
- **When** a request hits the **Partner** API's `Resolve`/`UpdateStatus` routes,
- **Then** those admin-only actions are no longer served from the Partner controller (they are removed there), so a single canonical host owns admin dispute management; an authenticated non-admin calling the Admin dispute endpoints receives 403.

**AC7 — Navigation & i18n**
- **Given** I am an admin,
- **When** I look at the admin navigation,
- **Then** a Disputes entry routes to the new feature, and every user-visible string (status labels, actions, errors) has keys present in `en/cs/sk/uk/ru`.

---

## Out of Scope (explicit)

- **Host-placement ADR.** The decision to make **Admin API the canonical home** (vs. keeping it on Partner) is an Architect/ADR call and is a **prerequisite**, not part of this story's implementation. This story assumes that decision lands on Admin API.
- **Customer-facing dispute flow.** Creating disputes, uploading evidence, and the customer's own dispute list/detail (`CanCreateDispute`/`CanViewDispute`/`CanUploadDisputeEvidence`, all `CustomerOnly`) stay on their current Customer/Partner surface — unchanged.
- **Stripe dispute integration.** Linking/syncing `StripeDisputeId` (`Dispute.LinkStripeDispute`, `StripeDisputeId`) and any chargeback webhook handling.
- **Actually issuing the refund.** `RefundAmount` is recorded on resolution; executing the Stripe refund/payout is a separate concern.
- **New lifecycle transitions or guards.** This story exposes the existing `Dispute` domain methods/statuses as-is; it does **not** add transition validation (e.g. blocking `Resolved → Pending`) or new `DisputeStatus` values.
- **NSwag client regeneration & EF migrations.** Owner-only manual steps (`manual_step: nswag-regen` after the new Admin DTOs/endpoints; no schema change is expected since `Dispute` already exists).
- **Notifications redesign.** Reuses the existing `AddDisputeMessage` push behavior; no new notification channels or admin-side dashboard alerts.
- **Mobile.** No Android partner/customer dispute changes.

---

## Layers Touched

- **Backend — API host:** `src/Cleansia.Web.Admin/Controllers/AdminDisputeController.cs` (new) exposing `GetPaged` / `GetById` / `AddMessage(staff)` / `UpdateStatus` / `Resolve`; **removal** of `Resolve`/`UpdateStatus` (and any admin-only routing) from `src/Cleansia.Web.Partner/Controllers/DisputeController.cs:66-88`.
- **Backend — authorization:** confirm/route admin-only policies (`CanResolveDispute`, `CanUpdateDisputeStatus`, and a list/detail policy for the admin read) in `PolicyBuilder.cs`; ensure the admin read reaches the `Administrator` branch of `GetPagedDisputes`.
- **Backend — app services (reuse, no new business logic expected):** `Disputes/{GetPagedDisputes, GetDisputeDetails, AddDisputeMessage, UpdateDisputeStatus, ResolveDispute}.cs` and their DTOs. (Note for the Backend agent: per consistency **B1**, `UpdateDisputeStatus`/`CreateDispute` currently return bare `ICommand`/`ICommand<string>` — a tracked violation; align if the canonical-home work touches them, otherwise out of scope.)
- **Frontend — admin-features (new):** `libs/cleansia-admin-features/disputes-management/` with a `disputes-management` list + `dispute-detail` (facade + models + component), mirroring `order-management`/`invoice-management`.
- **Frontend — admin app shell:** route + nav entry in `apps/cleansia-admin.app/src/app`.
- **Frontend — generated client:** new Admin dispute client methods (owner regenerates via NSwag — `manual_step`).
- **i18n:** `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` (status labels, actions, dispute error keys).

---

**Relevant files (absolute):**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Partner\Controllers\DisputeController.cs` (lines 66-88)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\PolicyBuilder.cs` (lines 73-79)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\{GetPagedDisputes,ResolveDispute,UpdateDisputeStatus,AddDisputeMessage}.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Disputes\Dispute.cs` and `...\Enums\DisputeStatus.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Admin\Controllers\` (no `*Dispute*` present; reference: `AdminOrderController.cs`, `AdminInvoiceController.cs`)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-admin-features\` (no `dispute*`; reference: `order-management\`, `invoice-management\`)
- Cited rules: `agents\knowledge\consistency.md` (A8), `agents\knowledge\security-rules.md` (S2, S3 — "regardless of which API host exposes it")


---

I now have everything grounded in real code and real rules. I'll write the user story. The persona is **admin** (payroll operations is an admin/owner concern — period closure and employee invoicing). The story resolves the architectural decision (option b: finish the extraction) because that aligns with the project's own `runtime-readiness.md` rule that side-effecting work "goes through a queue + Function, not inline." The story's first AC also covers the "decide and don't ship the stub" requirement.

---

```markdown
---
id: US-admin-0007
title: Move employee invoice generation onto a durable retried queue so one slow PDF can't stall period payroll
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---

## Narrative
As an **admin/payroll operator**, I want each employee's pay-period invoice (PDF generation + email
delivery) to be generated on its own durable, retried background message instead of inline inside the
nightly period-close timer, so that one employee's slow or failing PDF/email cannot stall, serialize,
or silently skip invoicing for everyone else in the period — and the long-dead `generate-invoice`
queue stops being a maintenance trap.

### Why this exists (grounded in code)
- `src/Cleansia.Functions/Functions/GenerateInvoiceFunction.cs:11-27` — the `GenerateInvoice` queue
  consumer deserializes the message, logs *"not yet implemented"*, and returns `Task.CompletedTask`.
  It does no work.
- `src/Cleansia.Core.Queue.Abstractions/QueueNames.cs:6` + `Messages/GenerateInvoiceMessage.cs:3` —
  the queue name and message contract exist, but a repo-wide search finds **zero**
  `SendAsync(QueueNames.GenerateInvoice, …)` producers (contrast: `QueueNames.GenerateReceipt` has 4
  real producers — `CompleteOrder.cs:219`, `CreateOrder.cs:376`, `ConfirmRecurringOrder.cs:112`,
  `HandlePaymentNotification.cs:241`). The queue is fully dead.
- `src/Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs:216-293` — invoices are instead
  built **inline**: `SendPeriodClosedEmailsAsync` loops every active employee on the single timer thread
  (`PayPeriodTimerFunction` `CloseExpiredPayPeriods`, cron `0 0 2 * * *`), calling
  `GenerateInvoiceForEmployeeAsync` (line 234) which generates the PDF, uploads to blob, and emails —
  serially, with no per-employee retry or dead-letter. A swallowed PDF failure (line 370-380) just
  logs and emails without the invoice; an email throw is caught per-employee but the whole loop shares
  one timer execution.

This violates the project's own `agents/knowledge/runtime-readiness.md`:
*"Side-effecting work that can fail transiently goes through a queue + Function, not inline in the
request"* and the Queue/Blob matrix row *"Never fire and hope"*; and `conventions.md:60` *"No dead
code — delete unreferenced methods/classes."* The existing stub satisfies neither — it is dead code
**and** the durable path is not wired.

## Acceptance criteria
- **AC1 (decision is made, no stub ships)** — Given the `generate-invoice` queue, message, and
  `GenerateInvoiceFunction`, When this story is delivered, Then the system is in exactly one coherent
  state: the queue path is **fully wired end-to-end** (producer + working consumer) — the no-op
  `logger.LogInformation(... "not yet implemented")` body and its `// TODO` at
  `GenerateInvoiceFunction.cs:20-24` no longer exist, and no code path reaches a silent
  `Task.CompletedTask` no-op.

- **AC2 (producer enqueues per employee)** — Given a pay period that the nightly job closes with N
  active employees who have unassigned `OrderEmployeePay` rows, When the close-and-rollover job runs,
  Then it enqueues exactly one `generate-invoice` message per eligible employee (carrying
  `EmployeeId`, `PayPeriodId`, `LanguageCode`, and the correlation id per runtime-readiness), and the
  timer thread itself does **not** generate any invoice PDF or send any invoice email inline.

- **AC3 (consumer does the real work)** — Given a `generate-invoice` message for an employee/period
  with unassigned pay rows and no existing invoice, When `GenerateInvoiceFunction` processes it, Then
  it creates the `EmployeeInvoice`, assigns the `OrderEmployeePay` rows to it, generates and uploads
  the PDF to the `GeneratedInvoices` blob container, and the employee receives their period-closed
  email with the invoice attached — i.e. the observable end state matches today's inline output.

- **AC4 (idempotency — S7)** — Given a `generate-invoice` message is re-delivered (queue retry,
  duplicate enqueue, or admin re-trigger) for an employee/period that already has an invoice, When the
  consumer runs again, Then it detects the existing invoice (mirroring the receipt consumer's
  `if (order.Receipt is not null) … skip` guard at `GenerateReceiptFunction.cs:66-70`, and the
  existing `(EmployeeId, PayPeriodId)` uniqueness check at
  `PayPeriodBackgroundService.cs:309-320`) and does **not** create a second invoice, double-assign pay
  rows, or send a duplicate email.

- **AC5 (isolation + dead-end on failure)** — Given one employee's PDF generation or email throws,
  When that employee's message fails, Then only that message retries via the queue's visibility timeout
  and lands in `generate-invoice-poison` after max attempts (the poison queue already exists per
  `scripts/setup-azure-functions.sh:253-254`), the failure is logged with structured context +
  correlation id, and every **other** employee in the period is invoiced and emailed unaffected
  (no shared-thread serialization or all-or-nothing).

- **AC6 (tenant correctness — S8)** — Given the period close runs across multiple tenants, When a
  `generate-invoice` message is consumed (a queue trigger has no JWT/tenant context), Then the
  consumer resolves and applies the correct tenant override before any write (mirroring
  `GenerateReceiptFunction.cs:54-57` `SetTenantOverride`), so the `EmployeeInvoice` and
  `OrderEmployeePay` assignments are stamped with the originating tenant's `TenantId`.

## Out of scope
- The **manual/admin** invoice path — `EmployeePayrollController.GenerateInvoice` →
  `Features/EmployeePayroll/GenerateInvoice.cs` and `RegenerateInvoicePdf` — keeps its existing
  synchronous request/response behavior; this story only moves the **automatic nightly** generation
  onto the queue. (Whether the admin path *also* enqueues is a separate decision.)
- No change to invoice **content, numbering, VAT/country-config logic, PDF layout** (QuestPDF), or the
  period-closed email template — output must be byte-for-behavior equivalent to today.
- No change to pay **calculation** (`PayCalculator`, `EmployeePayConfig` overrides) or to the
  period close/rollover scheduling cadence itself.
- No new admin UI for viewing stuck/poison invoice messages (alerting/dashboard is a follow-up; AC5
  only requires the poison queue dead-end to exist and be logged).
- The unrelated dead/stub items noted in the prior audit (admin order intervention, payroll
  settlement lifecycle, hardcoded `CancelOrder`) are tracked separately.

## Layers touched
- **Backend — Functions**: `Cleansia.Functions/Functions/GenerateInvoiceFunction.cs` (implement the
  real consumer; remove the no-op).
- **Backend — AppServices**: `Cleansia.Core.AppServices/Services/PayPeriodBackgroundService.cs`
  (replace inline `GenerateInvoiceForEmployeeAsync`/`GenerateInvoicePdfAsync` calls in
  `SendPeriodClosedEmailsAsync` with `SendAsync(QueueNames.GenerateInvoice, …)`; the extracted
  generation logic moves into / is shared with the consumer).
- **Backend — Queue abstractions**: `Cleansia.Core.Queue.Abstractions` (`GenerateInvoiceMessage`
  already exists; confirm it carries `LanguageCode` + correlation id).
- **Infrastructure**: depends on `Cleansia.Infra.Services` PDF (QuestPDF), email, and blob services,
  plus `generate-invoice` / `generate-invoice-poison` Azure Storage queues (already provisioned in
  `scripts/setup-azure-functions.sh`).
- **Not touched**: Frontend (Angular), Android, public API contracts, DB schema — no migration and no
  NSwag regeneration expected.

## Open questions
- **Q (default assumed)** — Should the **period-closed email** still be sent by the timer immediately
  (now *without* the invoice attachment, since generation is async), or should the email itself be
  emitted by the `generate-invoice` consumer once the PDF is ready so the invoice stays attached?
  **Default taken:** the consumer owns the invoice email (employee still gets one email with the PDF
  attached, preserving today's UX) — raise as Q-NNNN in `agents/backlog/questions/open.md` for owner
  confirmation, as it changes which component sends the email.
- **Q (default assumed)** — Resolution of the orphaned admin-facing `GenerateInvoice` HTTP path vs the
  queue path is left as-is (synchronous) per out-of-scope; flag if the owner wants them unified.
```


---

No existing `US-persona-NNNN` ids, so I'll use `US-partner-0001` (the partner population is the most materially affected by the role-demotion case, since PartnerLogin already gates Employee/Administrator but the refresh path does not re-pin it). This is a partner-and-customer-spanning security story. Here is the user story.

---

# US-partner-0001 — Re-pin user role on every refresh-token rotation (Customer/Partner/Mobile hosts)

**Type:** Security fix (functional GAP) · **Persona:** partner (spans customer + partner populations) · **Security-touching:** yes (S1, S2, S10) · **Related:** AUD-01 is a separate admin-intervention story; this is auth-rotation only.

## Context / grounded GAP

`RefreshToken.Handler` only enforces a profile match when the calling controller passes `RequiredProfile`:

- `src/Cleansia.Core.AppServices/Features/Auth/RefreshToken.cs:75-79` — `if (command.RequiredProfile.HasValue && user.Profile != command.RequiredProfile.Value)` → the profile check is **conditional on the host opting in**.
- The user re-load + `IsActive` guard at `RefreshToken.cs:68-73` already re-validates the user on every rotation — this is the correct template the profile check should mirror.

Only the **Admin** host opts in:

- `src/Cleansia.Web.Admin/Controllers/AdminAuthController.cs:48` — `command with { ..., RequiredProfile = UserProfile.Administrator, RequiredAudience = JwtAudiences.Admin }`.

The other four hosts pass **audience only**, so role is never re-pinned on rotation:

- `src/Cleansia.Web.Customer/Controllers/AuthController.cs:90` — `RequiredAudience = JwtAudiences.Customer` (no profile).
- `src/Cleansia.Web.Partner/Controllers/AuthController.cs:101` — `RequiredAudience = JwtAudiences.Partner` (no profile).
- `src/Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:106` — `RequiredAudience = JwtAudiences.Mobile` (no profile).
- `src/Cleansia.Web.Mobile.Customer/Controllers/AuthController.cs:101` — `RequiredAudience = JwtAudiences.Customer` (no profile).

Why this is exploitable, grounded in the login handlers:

1. **Customer host issues tokens to any role.** `Login.Handler` (`Login.cs:78-88`) does **no** profile check — only `IsActive`. An Administrator or Employee who authenticates through the Customer Web or Customer Mobile host receives a *customer-audience* token, and every subsequent refresh re-mints it forever because audience matches and profile is never checked. The host's intended audience population (Customers) is not enforced at refresh.
2. **Partner host does not re-pin on demotion.** `PartnerLogin.Handler` (`PartnerLogin.cs:90-94`) correctly rejects non-Employee/Administrator at login. But if a user is later **demoted to Customer**, their outstanding partner-audience refresh token keeps rotating — `RefreshToken.cs:61-66` only compares `Audience`, never re-checks that `user.Profile` is still in the partner-allowed set.

This is both a security hole (S1/S2: the host's audience population must be re-validated server-side, not assumed sticky from a prior login) and a consistency defect (`consistency.md` — "the same operation written N ways": the Admin host is the canonical form, four hosts deviate).

## Story

**As a** platform operator responsible for partner and customer account integrity,
**I want** every refresh-token rotation to re-validate that the user's current `Profile` still matches the population the calling host is allowed to serve,
**so that** a role change (promotion, demotion, or a cross-population login) cannot leave a stale long-lived session that keeps minting access tokens for a role the user no longer holds on that host.

## Acceptance Criteria

1. **Customer host rejects non-Customer on refresh**
   **Given** a user whose current `Profile` is `Administrator` or `Employee` holds a valid, non-reused customer-audience refresh token (e.g. obtained by logging in through the Customer Web or Customer Mobile host),
   **When** they call `RefreshToken` on `Cleansia.Web.Customer` or `Cleansia.Web.Mobile.Customer`,
   **Then** the response is the same generic refresh failure used today for a bad token (`BusinessErrorMessage.InvalidRefreshToken`, HTTP 400) and **no** new access/refresh token is issued.

2. **Partner host rejects a demoted user on refresh**
   **Given** an Employee logged in through a Partner host (Web or Mobile) and holds a valid partner-audience refresh token, **and** their `Profile` is subsequently changed to `Customer`,
   **When** they next call `RefreshToken` on `Cleansia.Web.Partner` or `Cleansia.Web.Mobile.Partner`,
   **Then** rotation fails with `InvalidRefreshToken` and no new token is minted, **even though** the audience still matches.

3. **Legitimate same-role refresh still succeeds (no regression)**
   **Given** an active Customer with a customer-audience token (or an active Employee/Administrator with a partner-audience token whose role is still in the host's allowed set),
   **When** they call `RefreshToken` on the matching host,
   **Then** a new access token and a rotated refresh token are returned exactly as today, with `Role` reflecting the current `user.Profile`.

4. **Admin host behaviour is unchanged**
   **Given** the Admin host already pins `RequiredProfile = Administrator` (`AdminAuthController.cs:48`),
   **When** this story is implemented,
   **Then** the Admin refresh path produces identical observable results to today (regression-only; it is the reference template).

5. **Partner host accepts both partner roles**
   **Given** an active `Administrator` who legitimately logged in via a Partner host (mirroring `PartnerLogin.Handler` allowing Employee **or** Administrator, `PartnerLogin.cs:90`),
   **When** they refresh on a Partner host,
   **Then** rotation succeeds — the host's allowed set is `{Employee, Administrator}`, not a single profile, so the re-pin must not lock out Administrators who are valid on the partner population.

6. **Deactivated/unknown user still rejected, ordering preserved**
   **Given** a refresh token for a user who is missing or `IsActive == false`,
   **When** any host's `RefreshToken` is called,
   **Then** rotation fails with `InvalidRefreshToken` (preserving the existing `RefreshToken.cs:68-73` guard); the new profile re-check must run **after** a successful rotation and **not** weaken or reorder the existing reuse-detection / `IsActive` checks.

## Out of scope

- **Login-time profile gating on the Customer host.** This story does **not** add a profile check to `Login.Handler` (`Login.cs`). Whether the Customer host should reject Administrator/Employee at *login* is a separate decision; here we only re-pin at *rotation*. (If the team wants login-time gating too, raise it as its own story.)
- The reuse/theft-signal detection and rotation mechanics inside `IRefreshTokenService.RotateAsync` (`RefreshToken.cs:44-59`) — unchanged.
- Audience-pinning logic (`RefreshToken.cs:61-66`) and `JwtAudiences` values — unchanged; this story is additive to audience, not a replacement.
- The HttpOnly-cookie / CSRF augmentation flow (`AuthCookieWriter`, `HandleTokenIssuingResult`) — unchanged.
- Access-token claim contents and `User.SetClaims` (`RefreshToken.cs:101-115`) — unchanged.
- Forcing existing/outstanding sessions to re-validate proactively (bulk revoke on demotion). This story closes the hole at the next rotation; an eager "revoke all tokens on profile change" is a separate hardening story.
- Admin order intervention, payroll settlement lifecycle, and `CancelOrder` hardcoding (separate known GAPs / AUD-01).
- New error keys or i18n: reuse the existing `InvalidRefreshToken` key so no `errors.*` translation work is required across the 5 locales.

## Layers touched

- **Backend — API hosts (primary):** `Cleansia.Web.Customer`, `Cleansia.Web.Partner`, `Cleansia.Web.Mobile.Partner`, `Cleansia.Web.Mobile.Customer` AuthControllers — enrich the `RefreshToken.Command` with the host's expected profile/allowed-set before `Mediator.Send`, mirroring `AdminAuthController.cs:48` (S1 enrich-in-controller pattern).
- **Backend — AppServices:** `Cleansia.Core.AppServices/Features/Auth/RefreshToken.cs` — if a single `RequiredProfile` cannot express the partner host's `{Employee, Administrator}` set, the handler's profile guard (`:75-79`) is extended to accept an allowed-set (e.g. `RequiredProfiles`) while keeping the `RequiredProfile == null` ⇒ no-op default for back-compat. Re-check runs after the `IsActive` guard.
- **Backend — Tests:** `Cleansia.Tests` (and/or `Cleansia.IntegrationTests`) — handler/host coverage for AC1–AC6.
- **Not touched:** Domain entities, EF/database (no schema change → no `ef-migration` manual step), frontend (3 Angular apps), mobile (Kotlin) — observable contract is identical for legitimate same-role refresh, so **no `nswag-regen` manual step** is required.

**Manual steps:** none (no DTO/endpoint signature change on the wire; `RequiredProfile`/`RequiredAudience` are server-enriched and already absent from the client-sent body).


---

I now have everything grounded. The customer-reachable dispute flows are:
- **CreateDispute** → `order.not_found`, `dispute.max_length_exceeded`, `dispute.already_exists`
- **AddDisputeMessage** → `dispute.not_found`, `dispute.max_length_exceeded`, `dispute.not_owned_by_user`
- **UploadDisputeEvidence** → `dispute.not_found`, `dispute.not_owned_by_user`, plus `file.*`

The customer locale files have an `api` namespace at line 1302 (not `errors`) — matching the actual working mechanism. The facade never calls `showApiError` for create/send (only `showError` with hardcoded keys). I'll write the story to be faithful to both the convention text (`errors.*`) and the real plumbing (`DEFAULT_SNACKBAR_ERROR_MAPPINGS` + `api.*` keys), and flag the convention/code-mechanism mismatch as a deliberate decision point rather than papering over it.

---

# US-customer-0042 — Show the real backend reason when a customer's dispute action fails

## Story

**As a** Cleansia customer raising or messaging on an order dispute,
**I want** a failed dispute action (create, send message, upload evidence) to tell me the *specific* reason it failed in my own language,
**so that** I understand what to do next (e.g. "you already have an open dispute on this order", "that file is too large") instead of a generic "something went wrong" that leaves me stuck or retrying blindly.

## Context (grounded in code)

- The customer disputes facade `disputes.facade.ts` never surfaces the API reason. `createDispute` (lines 94–98) and `sendMessage` (lines 119–123) both call `this.snackbar.showError(this.translate.instant('pages.disputes.create_error' | 'send_error'))` — a hardcoded generic string — instead of `showApiError(err, …)`. This is the DA-6 fallback.
- The backend *does* emit specific, customer-reachable codes (`BusinessErrorMessage.cs`): `CreateDispute.cs` emits `order.not_found`, `dispute.max_length_exceeded`, `dispute.already_exists`; `AddDisputeMessage.cs` emits `dispute.not_found`, `dispute.max_length_exceeded`, `dispute.not_owned_by_user`; `UploadDisputeEvidence.cs` emits `dispute.not_found`, `dispute.not_owned_by_user` plus `file.*`.
- These codes can never reach the user because the translation/plumbing is absent. The customer locale files (`apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`) have **no `errors` object at all** and no `dispute.*`/`file.*` keys under any namespace. The working resolution path is `snackbar.service.ts → extractApiErrorMessage → convertToTranslationKey → DEFAULT_SNACKBAR_ERROR_MAPPINGS` (lines 37–57), which maps a normalized (lowercase, alpha-only) backend code to an `api.*` translation key. That table has **zero** dispute/file entries (only order + 3 service-area address codes).
- This directly violates `conventions.md` line 52: *"Every backend error key has a matching frontend `errors.*` key in all 5 locales (en, cs, sk, uk, ru)."*

> Decision point for the architect (NOT for the BA to settle): the convention text says `errors.*`, but the codebase's actual working mechanism is `DEFAULT_SNACKBAR_ERROR_MAPPINGS` → `api.*` keys. The implementer must follow whichever is canonical (the existing `api.*` + mappings pattern is the de-facto standard the order errors already use) and the two should be reconciled, not both invented.

## Acceptance Criteria

1. **Given** a customer tries to create a dispute on an order that already has an open dispute, **When** the backend returns `dispute.already_exists`, **Then** the snackbar shows the locale-specific message for that code (e.g. EN: "You already have an open dispute for this order") and not the generic `pages.disputes.create_error` text.

2. **Given** a customer sends a dispute message that exceeds the max length, **When** the backend returns `dispute.max_length_exceeded`, **Then** the snackbar shows the locale-specific max-length message, in the customer's active language, sourced from the i18n file (no hardcoded literal).

3. **Given** a customer uploads dispute evidence that the backend rejects (`file.size_exceeded`, `file.invalid_file_type`, `file.count_exceeded`, or `dispute.not_owned_by_user`), **When** the upload fails, **Then** the snackbar shows the corresponding locale-specific reason for that exact code.

4. **Given** any of the customer-reachable dispute/file/order codes is returned (`dispute.not_found`, `dispute.already_exists`, `dispute.invalid_refund_amount`, `dispute.not_owned_by_user`, `dispute.max_length_exceeded`, `order.not_found`, and the `file.*` codes the upload path emits), **When** that code is the error detail, **Then** a matching translation key exists and resolves in **all five** customer locales (en, cs, sk, uk, ru) — no key falls back to the raw code string or to a different language.

5. **Given** the dispute create / send-message / upload-evidence handlers in `disputes.facade.ts`, **When** an API call errors, **Then** the facade routes the error through `showApiError(err, <feature-fallback-key>)` (keeping a feature-level fallback for unmapped/unexpected errors) rather than calling `showError` with a hardcoded key directly.

6. **Given** a backend code that has *no* specific translation mapping (an unexpected/unmapped error), **When** it surfaces in a dispute action, **Then** the user still sees a graceful generic fallback (the existing `pages.disputes.*_error` style key) — never a blank snackbar or a raw enum/PascalCase string.

## Out of Scope

- Any **non-customer** locale files — partner app, admin app, and Android/iOS string resources are untouched (this story is customer-facing only).
- The **admin/partner** dispute resolution flows (`ResolveDispute`, `UpdateDisputeStatus`) and their codes (e.g. `dispute.invalid_refund_amount` is admin-reachable via resolve — include its *key* for parity per AC-4, but do not wire admin facade behavior here).
- Adding, renaming, or changing any **backend** `BusinessErrorMessage` code, validator, or handler — backend error contract is treated as fixed.
- A **global sweep** of every other empty/missing `errors.*` area (membership, addresses CRUD, referral, etc.). This story is scoped to the dispute + the file/order codes the dispute flows actually emit.
- Building a **generic auto-mapping / lint rule** that guarantees backend↔frontend key parity for the whole app (worth a separate tooling ticket; here we only fix the dispute surface).
- **NSwag client regeneration** — not required; no DTO/endpoint contract changes (flag `manual_step: nswag-regen` only if the implementer discovers a contract change is unavoidable, which it should not be).
- Visual/UX redesign of the snackbar component itself.

## Layers Touched

- **Frontend — i18n (primary):** `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — add the dispute/file/order error translations in all 5 locales.
- **Frontend — facade:** `libs/cleansia-customer-features/disputes/src/lib/disputes/disputes.facade.ts` — switch create/send/upload error branches to `showApiError`.
- **Frontend — shared service (likely):** `libs/core/services/src/lib/services/snackbar.service.ts` — add the dispute/file code → translation-key entries to `DEFAULT_SNACKBAR_ERROR_MAPPINGS` (the existing mechanism order errors use), unless the architect rules the `errors.*` direct-lookup path is canonical.
- **Not touched:** backend, NgRx stores/effects, NSwag clients, partner/admin/mobile.

### Reference files (absolute paths)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\disputes\src\lib\disputes\disputes.facade.ts`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\core\services\src\lib\services\snackbar.service.ts`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\apps\cleansia.app\src\assets\i18n\{en,cs,sk,uk,ru}.json`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Common\BusinessErrorMessage.cs` (lines 196–201 dispute, 42–49 address/order, 161–166 file)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Features\Disputes\{CreateDispute,AddDisputeMessage,UploadDisputeEvidence}.cs`
- Rule: `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\agents\knowledge\conventions.md` line 52


---

Fully grounded. `CancelOrder.cs:86-103` returns `OrderAlreadyCancelled` / `OrderAlreadyCompleted` / `OrderInProgressCannotCancel` on real customer cancellation paths — and these have no `api.order.*` translation in the customer app, so the customer literally sees `api.order.already_cancelled`.

The convention rule is real and explicit (`conventions.md:50-53`): "Every backend error key has a matching frontend `errors.*` key in **all 5 locales** (en, cs, sk, uk, ru)." Note the catalog says `errors.*` but the actual interceptor uses `api.*` — a documentation/code drift worth noting but the binding rule (every key translated in all 5 locales) holds.

I have everything needed. Writing the user story.

---

# US-customer-0042 — Localize the 54 orphaned backend error keys (and guard against regression)

## Persona / Actor narrative

**As a** Cleansia customer (and, on shared paths, a partner or admin user)
**I want** every error the backend can return on a real failure to appear as a human-readable, fully translated message in my chosen language
**so that** when an action fails — for example trying to cancel an already-cancelled order, or exporting my GDPR data — I understand what went wrong instead of seeing a raw machine key like `api.order.already_cancelled`.

## Context (grounded in code)

- The frontend HTTP error interceptor (`libs/core/services/src/lib/interceptors/http-error.interceptor.ts:26-41`) takes the first **value** from the backend `errors` map (the dot-notation key, e.g. `order.already_cancelled`), prefixes it with `api.`, and calls `translate.instant('api.order.already_cancelled')`. When ngx-translate misses, `instant()` returns the key string verbatim, so that literal string is shown in the snackbar.
- 54 keys defined in `Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` have **no** matching entry under `api.*` in the app(s) that can surface them. The customer `api.*` block (`apps/cleansia.app/src/assets/i18n/en.json:1302-1316`) has no `order` sub-object at all, so every order-failure key is orphaned. The admin `api.order` block (`apps/cleansia-admin.app/src/assets/i18n/en.json:1979`) is likewise missing `already_cancelled`, `already_completed`, `in_progress_cannot_cancel`, `cancellation_window_closed`, and others.
- These are reachable on real flows, not dead code. `CancelOrder.cs:86-103` returns `OrderAlreadyCancelled` / `OrderAlreadyCompleted` / `OrderInProgressCannotCancel` directly from the customer cancel path; the GDPR and consent handlers return the `gdpr.*` keys.
- Project rule being violated — `agents/knowledge/conventions.md:50-53`: "Every backend error key has a matching frontend key in **all 5 locales** (en, cs, sk, uk, ru)." Localization rule — `conventions.md:107-111`: a key must be added to all five `{en,cs,sk,uk,ru}.json` files; wording with business impact (legal/compliance tone) goes to the owner, not invented silently.

The 54 orphans span: customer order/money/cancellation path, `address.*`/`dispute.*` ownership errors, all GDPR/consent keys, all 14 `promo.*`, 4 `referral.*`, 4 `loyalty.*`, admin config keys (`country.*`, `service_city.*`, `city.not_serviced`, `service.category_not_found`, `feature_flag.*`, `tenant_config.*`, `country_config.*`), `auth.invalid_refresh_token`, and `order.note.content_required` / `order.issue.description_required` / `employee_already_has_order_in_progress`.

## Acceptance Criteria

1. **Given** a customer with an order already in `Cancelled` status, **when** they trigger cancel again and the backend returns `order.already_cancelled` (`CancelOrder.cs:90`), **then** the snackbar shows a human-readable localized message in the active locale and the raw string `api.order.already_cancelled` is never displayed.

2. **Given** any of the 54 identified backend error keys is returned to the app that can reach it (customer / partner / admin), **when** the error interceptor resolves `api.<key>`, **then** a non-key, human-readable translation exists and resolves in **all 5 locales** (en, cs, sk, uk, ru) for that app.

3. **Given** the GDPR/consent failure keys (`gdpr.export_failed`, `gdpr.deletion_failed`, `gdpr.consent_not_found`, `gdpr.consent_already_granted`), **when** any is returned on the customer GDPR/consent flow, **then** the user sees a localized, compliance-appropriate message; any wording requiring a legal/tone decision is raised to the owner via `questions/open.md` with a placeholder rather than invented.

4. **Given** the test suite runs in CI, **when** a guard test parses `BusinessErrorMessage.cs` and cross-references each app's `en.json`, **then** it asserts that every backend key reachable from a given API has a corresponding `api.*` translation in that app's `en.json`, and **fails** if any key is missing.

5. **Given** the same CI guard, **when** a locale file is checked, **then** it asserts the non-`en` locale files (cs, sk, uk, ru) contain the same key set as `en.json` for each app (no missing or extra keys), so a regression in any single language fails the build.

6. **Given** a developer later adds a new `BusinessErrorMessage` constant without a translation, **when** CI runs, **then** the guard test fails and names the offending key(s) and locale(s), preventing the regression from merging.

## Out of scope

- Changing the interceptor's `api.` prefixing scheme or the `getObjectValues(...)[0]` "first error wins" behavior in `http-error.interceptor.ts`.
- Reconciling the `errors.*` vs `api.*` naming drift between `conventions.md` (says `errors.*`) and the actual interceptor (uses `api.*`) — note it for docs, but do not rename the live namespace here.
- Translating keys that are **not** reachable from any API surface, or backend-internal/log-only messages never returned to a client.
- Backend changes: no new error keys, no handler/validator changes, no change to the `BusinessResult`/`Error` shape.
- Android/iOS string resources (`strings.xml`) — this story is the 3 web apps only.
- Re-localizing keys that already resolve correctly; this is purely closing the 54-key gap plus the guard.
- Final legal sign-off on GDPR message copy (owner decision; story only ensures a placeholder + flag exists).
- NSwag client regeneration (not triggered — no contract change).

## Layers touched

- **Frontend i18n (primary):** `apps/{cleansia.app, cleansia-partner.app, cleansia-admin.app}/src/assets/i18n/{en,cs,sk,uk,ru}.json` — add the orphaned keys under the `api.*` namespace, each in 5 locales, only in the app(s) that can surface them.
- **Frontend tests / CI:** a Jest unit/guard test that reads `BusinessErrorMessage.cs` and the per-app `en.json` (+ locale-parity check across cs/sk/uk/ru).
- **No backend code change** — `BusinessErrorMessage.cs` is read-only input to the guard.
- **Owner / process:** `agents/backlog/questions/open.md` for any GDPR/compliance wording decision; optional `MANUAL_STEP` note if the owner prefers to author legal copy.

**Reference (do not edit):** backend keys in `src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs`; resolution path in `libs/core/services/src/lib/interceptors/http-error.interceptor.ts`; binding rule in `agents/knowledge/conventions.md:50-53` and `:107-111`.


---

I have everything I need. The gap is fully grounded in the cited code. Here is the user story.

---

# US-admin-0007 — Admin user create/edit honors birth date & preferred language (and stops silently wiping birth date)

## Context / grounding (read code, file:line)

- `AdminUserDetailDto` exposes `BirthDate` and `PreferredLanguageCode` on the read path — `src/Cleansia.Core.AppServices/Features/AdminUsers/DTOs/AdminUserDetailDto.cs:15-16`.
- The read query materializes them: `GetAdminUserById` → `MapToAdminDetailDto`, which sets `BirthDate: user.BirthDate` and `PreferredLanguageCode: user.PreferredLanguageCode` — `src/Cleansia.Core.AppServices/Mappers/AdminUserMappers.cs:38-39`.
- The write path cannot set either field: `CreateAdminUser.Command` accepts only `Email, Password, FirstName, LastName, PhoneNumber` — `CreateAdminUser.cs:15-20`; `UpdateAdminUser.Command` accepts only `UserId, FirstName, LastName, PhoneNumber` — `UpdateAdminUser.cs:13-17`.
- Data-loss footgun: `User.Update(string firstName, string lastName, string phoneNumber, DateOnly? birthDate = null)` unconditionally assigns `BirthDate = birthDate` (`User.cs:132-140`). `UpdateAdminUser` calls `user.Update(firstName, lastName, phoneNumber)` with **no** `birthDate` argument (`UpdateAdminUser.cs:66-69`), so the default `null` is applied and **`BirthDate` is wiped on every admin edit**. `PreferredLanguageCode` is left untouched by `Update` (only `UpdateLanguagePreference` at `User.cs:181-185` sets it), so it can never be changed via the admin user flows at all.

This is a read/write contract drift (Consistency §A/§B: the DTO and the command archetypes disagree) and a silent data-integrity loss on update — adjacent to S5 "audit every Response/DTO for fields that must not [drift]" in `agents/knowledge/security-rules.md:61`.

## Decision required before build (for PM/Architect)

The proposed fix offers two directions. This story is written for the **"make the write contract match the read contract"** option (admins can set `BirthDate` and `PreferredLanguageCode`), because the fields already exist on the entity, are already displayed, and `PreferredLanguageCode` is a real per-user setting elsewhere. If the product decision is instead to **drop** the fields, ACs 1-4 below are replaced by a single "fields no longer appear in the detail DTO/UI" criterion — but **AC5 (no silent nulling) is mandatory in both directions.**

## Story

**As an** admin managing platform administrator accounts
**I want** the admin-user create and edit forms to read and write `BirthDate` and `PreferredLanguageCode` consistently
**so that** the values I see on the detail screen are the values I can actually set, and a routine edit (e.g. fixing a phone number) never silently erases an admin's stored birth date.

## Acceptance Criteria (Given / When / Then — observable outcomes)

**AC1 — Create accepts the fields**
Given I am an authenticated admin on the "create admin user" form
When I submit a new admin user with a birth date and a preferred language code set
Then the created user persists exactly those values, and an immediate `GET admin user by id` returns the same `BirthDate` and `PreferredLanguageCode` I entered.

**AC2 — Update accepts the fields**
Given an existing admin user
When I change the birth date and/or preferred language on the edit form and save
Then the persisted user reflects the new values and the detail screen shows them after reload.

**AC3 — Round-trip fidelity (no read/write drift)**
Given an admin user whose detail DTO shows a non-null `BirthDate` and a `PreferredLanguageCode`
When I open the edit form
Then both fields are pre-populated from the current values (the form is seeded from the same data the detail screen shows), and saving with no changes leaves both values unchanged.

**AC4 — Validation on the new write fields**
Given the edit/create form
When I submit a `PreferredLanguageCode` longer than the entity limit (5 chars, per `User.cs:56-57`) or a `BirthDate` outside the allowed range (per the `[DateRangeControl(yearsRange:100)]` attribute on `User.BirthDate`, `User.cs:41-42`)
Then the command is rejected with a field-level `BusinessErrorMessage` (e.g. `MaxLength` / an invalid-date key), no partial write occurs, and the message resolves in all 5 locales under `errors.*`.

**AC5 — No silent data loss on unrelated edits (mandatory in both fix directions)**
Given an admin user with a stored non-null `BirthDate`
When I edit only an unrelated field (e.g. last name or phone number) and save
Then the stored `BirthDate` is preserved unchanged (i.e. `UpdateAdminUser` no longer passes a defaulting `null` into `User.Update`), confirming the footgun at `UpdateAdminUser.cs:66-69` / `User.cs:132-140` is closed.

**AC6 — Clearing is explicit, not accidental**
Given the edit form with a populated birth date
When I deliberately clear the birth date field and save
Then `BirthDate` becomes null **only because I explicitly cleared it** — and the same explicit-clear semantics apply to `PreferredLanguageCode` — distinguishing an intentional clear (AC6) from the accidental wipe forbidden by AC5.

## Out of scope

- Any change to **customer**, **partner/employee**, or **mobile** user create/update flows — this story touches only the `AdminUsers` feature. (Note for the audit trail: `User.Update`'s defaulting `birthDate = null` is shared domain code; the fix to its call site must not change behavior for other callers — verify no other caller relies on the wipe.)
- Adding new persisted fields to `User` (none needed; `BirthDate`/`PreferredLanguageCode` already exist).
- Reworking how `PreferredLanguageCode` drives runtime UI/email localization, or validating the code against the `Language` reference table beyond the existing length constraint.
- Populating `LastLoginAt` (currently hardcoded `null` in the mapper, `AdminUserMappers.cs:22,41`) — separate gap, not part of this story.
- EF Core migration authoring/applying and NSwag client regeneration — **owner-only manual steps**; flag `manual_step: nswag-regen` because `CreateAdminUser.Command`/`UpdateAdminUser.Command` shapes change and the admin TypeScript client must be regenerated before the frontend form work.
- Audit-logging of who changed an admin's PII (desirable but a separate security story).

## Layers touched

- **Backend — AppServices (CQRS):** `CreateAdminUser.Command` + `Validator`, `UpdateAdminUser.Command` + `Validator` + `Handler` (`src/Cleansia.Core.AppServices/Features/AdminUsers/`).
- **Backend — Domain:** call-site fix so `UpdateAdminUser` passes `birthDate` through `User.Update`; review the `birthDate = null` default on `User.Update` (`src/Cleansia.Core.Domain/Users/User.cs:132-140`) and the language setter (`UpdateLanguagePreference`, `User.cs:181-185`).
- **Contract / NSwag:** regenerated admin API client (`libs/core/admin-services/.../admin-client.ts`) — owner-run.
- **Frontend — Admin app:** `admin-user-form` component/facade and its NgRx user store/actions (`libs/cleansia-admin-features/admin-user-management/...`, `libs/data-access/admin-stores/src/lib/user/...`) to add the two inputs, seed them from detail, and bind validation.
- **i18n:** any new error keys added across `en, cs, sk, uk, ru`.
- **Tests:** xUnit handler/validator tests covering AC1-AC2 (persist), AC4 (validation), and especially **AC5 (regression: birth date survives an unrelated edit)**.


---

I have everything I need. Important grounding facts:

- The existing F8/T-0008 consistency finding covers idempotency on `CreateMembershipSubscription` / `CreateOrder` (synchronous create commands) — NOT the queue fan-out. **FUP-3** explicitly calls for an "Azure Functions trigger-graph pass — ... idempotency/poison/dead-letter," confirming this fan-out idempotency gap is in-scope but not yet ticketed as its own story. So this story is distinct from F8/T-0008 and is a concrete instance under FUP-3.
- The story must be an `admin` persona (the actor who triggers the campaign and suffers the duplicate-push reputational/user-trust consequence). Per the template, persona is the user whose capability is described.

I have grounded the GAP in: `SendSitewidePromoFanoutFunction.cs:104-163` (the bug), `SendSitewidePromoMessage.cs` (no `CampaignId`), `SendSitewidePromo.cs` (enqueue side), the two reference patterns (`ProcessedStripeEvent`, `SendMembershipLifecycleNotifications` sent-stamps), and rules **S7** + **B8** + FUP-3. Here is the story.

---

```markdown
---
id: US-admin-0001
title: Sitewide promo campaign sends each opted-in user exactly one push, even when the fan-out retries
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: [FUP-3, T-0008]
---

## Narrative
As an **admin** running a marketing campaign, I want a "send sitewide promo" dispatch to deliver **at most one** promo push per opted-in user **even when the fan-out job fails partway and the queue retries it**, so that a single flaky run never spams my early-paged users with up to 5 duplicate marketing notifications and erodes trust in the brand (and push opt-in rates).

## Grounding (why this story exists)
`SendSitewidePromoFanoutFunction.Run` (`src/Cleansia.Functions/Functions/SendSitewidePromoFanoutFunction.cs:104-163`) pages through opted-in users from `offset = 0` (line 105) and enqueues one `SendPushNotificationMessage` per recipient. The **inner** per-user `try/catch` (lines 121-146) correctly swallows a single user's enqueue failure and continues. But any failure **outside** that inner try — e.g. the `query.Skip(offset).Take(PageSize).ToListAsync(ct)` page read at lines 108-111 throwing on page 40 of 50 — falls through to the **outer** `catch (Exception ex)` at lines 157-163, which re-throws (line 162, comment: *"poison-message pipeline retries the whole campaign"*). The retry restarts the same campaign message from `offset = 0`, re-enqueuing pushes to every user already processed on the prior attempt. With the queue's default retry budget this is up to ~5x duplicate promo pushes to early-paged users.

The fan-out is **not idempotent**: `SendSitewidePromoMessage` (`src/Cleansia.Core.Queue.Abstractions/Messages/SendSitewidePromoMessage.cs:26-36`) carries no `CampaignId` / dedup key, and nothing persists "this user was already sent this campaign." This is a direct **S7** violation (idempotency on side-effecting dispatch; explicitly names "pipeline retries" and "admin re-triggers") and the consistency-rule **B8** ("side-effecting commands are idempotent ... never a broad `catch (Exception)` for control flow"). It is the concrete Functions-layer instance that **FUP-3** ("idempotency / poison / dead-letter") was raised to cover — and is **distinct from** F8 / T-0008, which only covers the synchronous `CreateOrder` / `CreateMembershipSubscription` commands, not this queue fan-out.

The fix follows patterns already in the codebase, so it is not a new abstraction: the near-identical fan-out `SendMembershipLifecycleNotifications` (`src/Cleansia.Core.AppServices/Features/Memberships/SendMembershipLifecycleNotifications.cs:75-145`) persists a per-recipient "sent" stamp (`RenewalReminderSentAt` / `CancellationReminderSentAt`) and filters on `== null` so a retry skips already-notified recipients; and `ProcessedStripeEvent` (`src/Cleansia.Core.Domain/Payments/ProcessedStripeEvent.cs`) is the established dedup-marker-with-unique-index pattern.

## Acceptance criteria
- **AC1 (no duplicates on mid-run failure)** — Given a campaign of N opted-in users where the page read throws after some users have already been enqueued, When the queue redelivers the same campaign message, Then no user who was already enqueued on a prior attempt receives a second `SendPushNotificationMessage` (verified by asserting at most one `notifications-dispatch` enqueue exists per `(CampaignId, UserId)`).
- **AC2 (resume, not restart)** — Given a fan-out that previously processed M of N recipients before failing, When it retries, Then it resumes from recipient M+1 (via a persisted progress cursor or per-recipient sent marker) rather than restarting at `offset = 0`, and on success the total distinct users enqueued equals N.
- **AC3 (idempotency key exists end-to-end)** — Given the admin triggers "send sitewide promo", When `SendSitewidePromo.Handler` enqueues the campaign, Then the `SendSitewidePromoMessage` carries a stable `CampaignId` that the fan-out uses as the idempotency scope for every recipient. (`manual_step: nswag-regen` if the admin-facing command/DTO contract changes.)
- **AC4 (admin double-trigger is safe)** — Given the admin submits the same campaign twice (double-click or accidental re-send producing two messages with the same `CampaignId`), When both fan-out runs execute, Then each opted-in user still receives at most one push for that campaign.
- **AC5 (per-user failure still isolated)** — Given a single user's enqueue throws, When the fan-out continues, Then that one user is skipped/logged (preserving today's behavior at lines 137-146) without re-enqueuing the rest of the campaign and without poisoning the whole message.
- **AC6 (true poison still dead-letters)** — Given a campaign message that can never succeed (e.g. un-deserializable, or missing the `en` title/body fallback at lines 79-85), When it is processed, Then it is discarded/dead-lettered exactly as today and does not loop, so retry-on-transient-failure does not become retry-forever.

## Out of scope
- Any change to **per-user enqueue failure handling** beyond preserving the existing inner try/catch behavior (lines 137-146) — that already works correctly.
- Idempotency for the **synchronous** create commands `CreateOrder` / `CreateMembershipSubscription` — that is F8 / T-0008, a separate ticket.
- The **downstream** `SendPushNotificationFunction` re-throw/no-dedup behavior on `notifications-dispatch` (a related but separate idempotency gap); this story only guarantees one *enqueue* per `(CampaignId, UserId)`, not de-duplicating an already-correctly-enqueued message.
- Admin **UI** changes (campaign history, "already sent" indicators, send-progress bar) — this is a backend/Functions reliability fix; surfacing campaign status to the admin is a follow-up.
- Tenant-targeting / cross-tenant send semantics (campaigns remain single-tenant via the existing `TenantId` override; unchanged).
- The page-size, throttling, and locale-resolution logic — unchanged.
- Retiring or replacing Azure Storage Queues / dead-letter infrastructure (a broader FUP-3 concern).

## Layers touched
- **backend** — `SendSitewidePromoFanoutFunction` (resume cursor / per-recipient sent marker; replace the outer broad-catch-then-rethrow control flow); `SendSitewidePromo.Handler` + `SendSitewidePromoMessage` (add `CampaignId`).
- **db** — a persisted idempotency/progress record (e.g. a `SentSitewidePromo` / `ProcessedPromoRecipient` marker analogous to `ProcessedStripeEvent`, or a campaign-cursor row); `manual_step: ef-migration` (owner-only) for the new table/columns + the `(CampaignId, UserId)` unique index.
- **qa** — Functions-level tests asserting AC1–AC6 (mid-page failure → retry → no duplicate enqueues; resume-from-cursor; double-trigger safety; poison still dead-letters).

## Open questions
- **Q (default assumption taken):** Idempotency granularity — **per-recipient `(CampaignId, UserId)` marker** (membership-reminder style) vs. a **single per-campaign resume cursor (last `UserId` processed)**. Default assumption: a **per-recipient marker with a `(CampaignId, UserId)` unique index** (most robust against AC4 admin double-trigger and out-of-order retries, and matches the existing `ProcessedStripeEvent` precedent); the cursor approach is acceptable only if storage cost for large sends is shown to be prohibitive. Raise as Q-NNNN in `agents/backlog/questions/open.md` if the owner wants the cheaper cursor instead.
- **Q:** `CampaignId` lifetime/source — generated in `SendSitewidePromo.Handler` at enqueue time (default assumption) so the same admin submit maps to one id even across queue redelivery.
```


---

Confirmed the `manual_step: nswag-regen` convention (conventions.md:100-101) and the controller/endpoint exposure pattern. I have everything grounded. Here is the user story.

---

```yaml
---
id: US-partner-0007
title: Save my profile one section at a time on the web partner app
persona: partner
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As a **partner (cleaner) using the web partner app**, I want to **save each section of my profile independently — personal info, identification, address, bank details, emergency contact, and availability — without re-completing the entire onboarding form**, so that **I can correct a single detail (e.g. update only my IBAN or my weekly availability) at any time, even if some unrelated section is incomplete or the one-time onboarding Consent is no longer present.**

## Context (grounded in code)

The six granular self-update commands already exist in the shared app-services layer and are fully wired on mobile, but the web partner controller exposes none of them:

- Commands: `UpdatePersonalInfo`, `UpdateIdentificationInfo`, `UpdateAddressInfo`, `UpdateBankDetails`, `UpdateEmergencyContact`, `UpdateAvailability` — `src/Cleansia.Core.AppServices/Features/Employees/Update*.cs`. Each validates only its own section and reuses the same `AllowedToUpdateEmployee` ownership check.
- Mobile exposes all six: `src/Cleansia.Web.Mobile.Partner/Controllers/EmployeeController.cs:50-108`.
- Web exposes only the monolithic update: `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs:40-49` (`UpdateEmployee` only).
- The monolithic `UpdateEmployee.Command` requires the full form **including `Consent == true`** — `src/Cleansia.Core.AppServices/Features/Employees/UpdateEmployee.cs:136-138`.
- The web profile is already built section-by-section (`profile-personal-info`, `profile-bank-details`, `profile-emergency-contact`, `profile-availability` components) but the facade's only save path, `onSubmit()`, hard-rejects on `!this.formGroup.valid` and pushes everything through a single `updateEmployee()` call — `src/Cleansia.App/libs/cleansia-partner-features/profile/src/lib/profile/profile.facade.ts:151-158, 196-198`. The per-section UI is therefore misleading: it looks editable section-by-section but cannot be saved that way.

## Acceptance criteria

- **AC1 — Bank-only save succeeds without the rest of the form** — Given an authenticated web partner whose profile is otherwise unchanged (and whose Consent is not re-confirmed in this session), When they edit only their IBAN and save the Bank Details section, Then the IBAN is persisted via the granular bank endpoint and no other section's validation is triggered (no "fill required fields" error).
- **AC2 — Availability-only save succeeds independently** — Given an authenticated web partner, When they change only their weekly availability and save the Availability section, Then the availability is persisted via the granular availability endpoint, and unrelated fields (e.g. a blank optional emergency contact) do not block the save.
- **AC3 — Per-section validation is scoped to that section** — Given a web partner editing one section, When that section contains an invalid value (e.g. a malformed IBAN), Then only that section's save is blocked with that section's validation message, and the other sections remain independently saveable.
- **AC4 — Each web granular save reaches the matching backend command** — Given the web partner controller, When a section is saved, Then the request is routed to the corresponding granular command (`UpdatePersonalInfo` / `UpdateIdentificationInfo` / `UpdateAddressInfo` / `UpdateBankDetails` / `UpdateEmergencyContact` / `UpdateAvailability`), mirroring the mobile partner controller's six endpoints.
- **AC5 — Ownership is enforced per section** — Given an authenticated partner, When they call any granular web save for an `EmployeeId` that is not their own, Then the request is rejected by the existing `AllowedToUpdateEmployee` ownership check (returns the `NotAllowedToUpdateEmployee` business error), identical to the mobile behavior.
- **AC6 — Saved values survive a reload** — Given a section was saved successfully, When the partner reloads the profile page, Then the persisted value for that section is shown, confirming the granular write reached the database.

## Out of scope

- **Admin-initiated edits** of an employee's profile (the `AdminUpdateEmployee` / `AdminUpdateEmployeeAvailability` path) — this story is the partner editing their own profile only.
- **Mobile partner app** behavior — mobile already has the six endpoints; this story does not alter them.
- **Document upload/delete** flows (`SaveMyDocuments` / `DeleteMyDocument` / `DownloadMyDocument`) — those remain on their existing endpoints and are not section-split here.
- **The one-time onboarding flow itself** — the monolithic `UpdateEmployee` + `Consent` gate stays for first-time profile completion; this story adds granular *edit* of an already-onboarded profile, it does not remove or redesign onboarding.
- **New validation rules or schema changes** — reuse the existing per-section validators and the existing `Employee` aggregate update methods; no `Consent` requirement is introduced on granular saves.
- **NSwag client regeneration is owner-only** — `manual_step: nswag-regen` per `agents/knowledge/conventions.md:100-101`; the new web endpoints require the owner to regenerate the partner client before the frontend wiring can compile against them. No EF migration is needed (no schema change).

## Layers touched

`backend` (add six granular actions to `src/Cleansia.Web.Partner/Controllers/EmployeeController.cs`, reusing existing commands — no new handlers/validators), `frontend` (replace the single `onSubmit()`/`updateEmployee` save in the partner profile facade with one save per section, each calling its granular endpoint). **No `db` layer** (no schema change). Gated on `manual_step: nswag-regen` for the partner NSwag client.

## Open questions

- **Q (default assumption taken):** Should the web profile keep a single "Save all" affordance in addition to per-section saves, or move entirely to per-section saves? Default assumption until the owner answers: **per-section saves only**, matching the mobile model and the six granular commands; the monolithic `UpdateEmployee` endpoint is retained solely for the initial onboarding submission, not for post-onboarding edits.


---

I have everything needed. The i18n keys live under `pages.employee_detail.*` across all 5 locales, the dialog pattern (`RejectDialogComponent` via PrimeNG `DialogService`) is the established way to render modal content, and per-version download is already covered by the existing `download(documentId)` endpoint. Here is the user story.

---

# US-admin-0007 — View employee document version history

**Type:** User Story (functional gap — orphaned backend capability)
**Persona:** admin (operator / HR reviewer)
**Status:** Ready for refinement
**Source gap:** EMP-GAP — Document version-history endpoint has no UI consumer

## Context (grounded in code)

Auto-versioning for employee documents is implemented end-to-end on the backend, but it is **invisible to operators** because no frontend code calls the version endpoint.

- **Entity:** `EmployeeDocument.CreateNewVersion(...)` (`src/Cleansia.Core.Domain/Documents/EmployeeDocument.cs:72`) creates a new row with `Version = previousVersion.Version + 1` and `PreviousVersionId = previousVersion.Id`, status reset to `Pending`. Each version is a distinct persisted row with its own `Id`.
- **Repository:** `IEmployeeDocumentRepository.GetVersionHistoryAsync(documentId)` (`IEmployeeDocumentRepository.cs:9`) returns the full chain.
- **Query:** `GetDocumentVersionHistory` (`src/Cleansia.Core.AppServices/Features/EmployeeDocuments/GetDocumentVersionHistory.cs`) returns `Response { List<EmployeeDocumentItem> Versions }`, including each version's `Version`, `PreviousVersionId`, `Status`, `ReviewNotes`, `ReviewedAt`, `FileName`, `FileSizeBytes`, `CreatedOn`, `CreatedBy`.
- **Endpoint:** `AdminEmployeeDocumentController.GetVersionHistory` (`AdminEmployeeDocumentController.cs:62`), `GET /api/AdminEmployeeDocument/{documentId}/versions`, guarded by `Permission(Policy.CanViewEmployeeDocuments)`.
- **Generated client:** `versions(documentId)` already exists (`libs/core/admin-services/src/lib/client/admin-client.ts:3266`/`3526`) returning `GetDocumentVersionHistoryResponse { versions: EmployeeDocumentItem[] }`. **No backend or NSwag work is required.**

**The gap:** `EmployeeDocumentsFacade` (`employee-documents.facade.ts`) loads documents with `filter.latestVersionOnly = true` (line 39) and **never calls `.versions(...)`**. The card template (`employee-documents-section.component.html:77`) displays `v{{ doc.version }}` as a static label but provides no action to view or download prior versions. An operator who sees "v3" cannot see what the cleaner replaced in v1/v2, nor audit a re-upload.

## User Story

> **As an** admin reviewing an employee's documents,
> **I want** to open the version history of any document and download any prior version,
> **so that** I can see what a cleaner replaced on a re-upload and audit document changes before approving the latest version.

## Acceptance Criteria

1. **Given** an admin viewing the documents section of an employee detail page, **when** a document card is rendered for a document whose `version` is greater than 1, **then** a "Version history" action is visible on that card.

2. **Given** a document card whose `version` equals 1 (no prior versions), **when** it is rendered, **then** the "Version history" action is either hidden or disabled, so the admin is never offered an empty history.

3. **Given** an admin clicks "Version history" on a document, **when** the facade calls `adminEmployeeDocumentClient.versions(documentId)`, **then** a dialog/panel opens listing every version returned, each row showing at minimum: version number, file name, status (Pending/Approved/Rejected) with the same status styling used on cards, file size, and upload date (`createdOn`).

4. **Given** the version history is displayed, **when** the admin views a prior (non-latest) version row, **then** a per-version "Download" control is available that calls the existing `download(version.id)` endpoint and downloads that specific version's file (each version has its own `Id`).

5. **Given** the `versions(documentId)` request is in flight, **when** the dialog is open, **then** a loading indicator is shown; **and given** the request fails, **then** a localized error message is shown via the snackbar and no stale/partial list is rendered (consistent with the existing three-data-state rule and the facade's `catchError(() => of(null))` pattern).

6. **Given** the admin lacks the `CanViewEmployeeDocuments` permission, **when** the documents section loads, **then** the "Version history" action is not actionable (the section itself is already permission-gated), so no unauthorized call is made.

## Out of Scope

- **Any backend change.** The query, controller endpoint, repository method, DTO, and NSwag client all already exist; this story is frontend-only. (If, during build, a backend/DTO gap is discovered, raise it as a separate ticket — do **not** regenerate clients here.)
- Running `npm run generate-admin-client` or hand-editing the generated `admin-client.ts` — owner-only `manual_step`, and not needed since `versions(...)` is already generated.
- **Restoring / rolling back** to a prior version, deleting individual versions, or any write operation on historical versions (view + download only).
- **Diffing** file contents between versions (visual/PDF diff) or inline preview of historical versions in a new tab — only per-version download is in scope; preview-in-tab is a possible follow-up.
- Surfacing version history in the **partner** app, the **mobile** apps, or the cleaner's own self-service document view — admin web only.
- Changing the default documents list, which intentionally stays `latestVersionOnly = true` (the latest-only list stays as-is; history is additive).
- Pagination of the version list (chains are expected to be short; the query returns the full list).

## Layers Touched

| Layer | Change | File(s) |
|---|---|---|
| Backend | **None** | — (endpoint `AdminEmployeeDocumentController.cs:62` + `GetDocumentVersionHistory.cs` already exist) |
| NSwag client | **None** (already generated) | `libs/core/admin-services/.../admin-client.ts:3266` |
| Frontend — facade | Add `loadVersionHistory(documentId)` calling `adminEmployeeDocumentClient.versions(...)`; expose a `versions` signal + `versionsLoading` signal; add a per-version download (reuse existing `downloadDocumentBlob`/`triggerDownload`); add an `openVersionHistoryDialog(doc)` opener | `employee-documents.facade.ts` |
| Frontend — components | New version-history dialog/panel component (follow the existing `RejectDialogComponent` + PrimeNG `DialogService` pattern); register it in the feature `components/index.ts`; use only `<cleansia-*>`/PrimeNG controls | `.../employee-detail/`, `.../components/index.ts` |
| Frontend — template | Add the "Version history" action button to the document card (gated on `version > 1`) | `employee-documents-section.component.html` |
| i18n (5 locales) | New keys under `pages.employee_detail.*` (e.g. `actions.view_version_history`, `version_history_dialog.title`, column labels) and any new `errors.*` key, added to **en, cs, sk, uk, ru** | `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` |

**Conventions cited:** logic in facade not component; signals + `OnPush`; `<cleansia-*>`/PrimeNG only (no raw controls); every user-visible string via `TranslatePipe` with keys in all 5 locales; three explicit data states (loading / error-via-snackbar / data); no `any`; reuse the existing dialog pattern. Permission reuse: `CanViewEmployeeDocuments` already governs both the list and the versions endpoint, so no new policy is introduced.


---

No `US-*` story files exist yet in the backlog, so I'm starting the partner series. I'll use `US-partner-0001`. I have all the grounding I need to write the story.

---

```markdown
---
id: US-partner-0001
title: Available-jobs preview card on the web partner dashboard
persona: partner
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---

## Narrative
As a **partner (cleaner)**, I want the web dashboard to show a short preview list of the highest-value
unclaimed jobs I could take right now — not just a count — **so that** I can see "work waiting for me"
and the potential earnings at a glance and jump straight to claiming a job, the same way the mobile app
already lets me.

## Context (grounded in code)
- The capability already exists in the backend: `GetAvailableJobsPreview.Query(Limit = 5)` →
  `AvailableJobsPreviewResponse(Jobs, TotalPotentialEarnings, TotalAvailableCount)` in
  `src/Cleansia.Core.AppServices/Features/Dashboard/GetAvailableJobsPreview.cs:23-79`, with the slim
  row `AvailableJobPreviewDto(Id, DisplayOrderNumber, CustomerAddress, CleaningDateTime, TotalPrice)`
  in `…/Dashboard/DTOs/AvailableJobPreviewDto.cs:9-19`.
- It is wired **only** on mobile: `Cleansia.Web.Mobile.Partner/Controllers/DashboardController.cs:30-40`
  exposes `GET GetAvailableJobsPreview` (policy `CanGetCurrentEmployee`).
- The **web** partner controller `src/Cleansia.Web.Partner/Controllers/DashboardController.cs` omits it
  — it goes straight from `GetStats` (line 20) to `GetUpcomingOrders` (line 32); no preview endpoint.
- The web facade `libs/cleansia-partner-features/dashboard/src/lib/dashboard/dashboard.facade.ts:128-133`
  surfaces only `stats.availableOrdersCount` (an `int` from `DashboardStatsDto.AvailableOrdersCount`,
  `…/Dashboard/DTOs/DashboardStatsDto.cs:5`) as a stat card — a number, with no list and no
  "earn up to" figure.

This is a **feature-parity gap**, not a defect: mobile cleaners get a "jobs waiting for you" preview;
web cleaners only get a count of the same set.

## Acceptance criteria
- **AC1 — Web endpoint reaches the existing capability.**
  Given an authenticated partner with a resolvable employee identity,
  When the web partner dashboard requests the available-jobs preview,
  Then `Cleansia.Web.Partner/Controllers/DashboardController` returns the same
  `AvailableJobsPreviewResponse` (jobs, total potential earnings, total available count) the mobile
  controller already returns, gated by the same `CanGetCurrentEmployee` permission, with **no change**
  to the `GetAvailableJobsPreview` handler, query, or DTOs.

- **AC2 — Preview card renders the list.**
  Given the web dashboard has loaded and the preview returns one or more jobs,
  When the partner views the dashboard,
  Then a preview card lists up to the server-returned limit (5) of those jobs, each row showing display
  order number, customer address, cleaning date/time, and price, ordered highest-price-first (as the
  server already sorts by `TotalPrice DESC`).

- **AC3 — Headline earnings + total are shown.**
  Given the preview response,
  When the card renders,
  Then it displays `TotalPotentialEarnings` (formatted in the partner's currency/locale, consistent with
  how the existing stat cards format money) and surfaces `TotalAvailableCount` so the partner can tell
  there are more jobs than the previewed rows when the count exceeds the list length.

- **AC4 — Empty state.**
  Given the partner has no claimable jobs (`Jobs` is empty and `TotalAvailableCount` is 0),
  When the card renders,
  Then it shows a translated empty-state message (keys present in all five locales: en, cs, sk, uk, ru)
  instead of an empty/broken card — and no error is surfaced.

- **AC5 — Navigate to act on a job.**
  Given a previewed job row,
  When the partner activates the card or a row,
  Then they are taken to the orders view where they can claim/take that job (reusing the existing
  `/orders` route already wired on the dashboard's available-orders stat card).

- **AC6 — Three explicit data states, house pattern.**
  Given the preview is loading, has loaded, or failed,
  When the facade fetches it,
  Then the facade uses the canonical client pipe
  (`takeUntil(this.destroyed$) → catchError(() => of(null)) → finalize(...)`, consistency rule C3),
  exposes a `loading` signal, surfaces failures via `SnackbarService` (C4), and the component stays
  `standalone` + `OnPush` (C7) — i.e. loading, empty, and error are all observably handled.

## Out of scope
- **Any change to the backend capability itself** — `GetAvailableJobsPreview.cs`, its `Query`,
  `AvailableJobPreviewDto`, `AvailableJobsPreviewResponse`, and `DashboardSpecifications` are reused
  verbatim. No new query, no new filter/spec, no change to sort or limit semantics.
- **Removing or changing the existing `availableOrdersCount` stat card** — the count card may stay; this
  story adds the preview list, it does not redesign the stats row.
- **Mobile app changes** — the mobile dashboard already has this card; no Android work.
- **The actual claim/take action and its confirmation flow** — AC5 only requires navigation to the
  existing orders view; the claim command/UX is owned elsewhere.
- **Pagination / infinite scroll / filtering of the preview** — it is a fixed top-N preview, not a list
  feature (no `offset`/`limit`/sort controls, no `cleansia-table` paging).
- **Real-time / push updates** — the card refreshes on the dashboard's existing load/refresh cycle only.
- **Admin and customer dashboards** — partner web only.
- **NSwag client regeneration and EF migrations** — owner-only manual steps; this story does not perform
  them. Adding the web endpoint changes the partner OpenAPI surface, so it carries
  `manual_step: nswag-regen` (regenerate `@cleansia/partner-services` so `PartnerClient` exposes the new
  endpoint before the facade can call it). No DB migration is required.

## Layers touched
- **Backend (web wiring only):** add the `GetAvailableJobsPreview` action to
  `src/Cleansia.Web.Partner/Controllers/DashboardController.cs`, mirroring the mobile controller's
  attributes/permission. No AppServices change.
- **Contract:** partner OpenAPI spec changes → NSwag-generated `PartnerClient` must be regenerated
  (`manual_step: nswag-regen`, owner-only).
- **Frontend (partner):** `dashboard.facade.ts` (new signal + canonical fetch via the regenerated
  client), the dashboard component/template (new preview card + empty/loading/error states), `*.models.ts`
  if a row/card definition is added, and `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`
  for the card title, "earn up to", "X more available", and empty-state strings.
- **Mobile:** none.

## Open questions
- **Q (default assumption taken):** Should the web preview reuse the mobile default `Limit = 5`, or show
  more rows on the larger web viewport? **Default assumed: keep `Limit = 5`** to reuse the capability
  verbatim and match mobile; raise as a Q-NNNN if the owner wants a web-specific limit.
- **Q (default assumption taken):** Should the count card and the new preview card coexist, or does the
  preview replace the count? **Default assumed: coexist** (count stays; preview is additive) per the
  out-of-scope note; confirm with the owner.
```


---

I now have everything needed and grounded in code. Key confirmed facts:

- **No Admin dispute controller** exists (Admin Controllers folder has 30 controllers, none for disputes; `DisputeController` lives only in `Cleansia.Web.Partner`).
- **No `dispute-management` admin-features library** (21 admin libraries listed, none for disputes).
- The mutating endpoints `Resolve` (line 66-76) and `UpdateStatus` (line 78-88) are gated by `CanResolveDispute`/`CanUpdateDisputeStatus` → both mapped to `PhysicalPolicy.AdminOnly` (PolicyBuilder.cs:77-78). So an admin must hit the **Partner API** to resolve a dispute — wrong API host for the refund decision-maker.
- `AddMessage` is gated by `CanRespondToDispute` → `PhysicalPolicy.Authenticated` (PolicyBuilder.cs:76) — **any authenticated user**, including a cleaner, can post a staff message via `IsStaffMessage=true` (AddDisputeMessage.cs:50, the ownership check is skipped when `IsStaffMessage` is true). This violates security-rules **S2** (over-broad authorization).
- The query handler `GetPagedDisputes` already branches on `UserProfile.Administrator` (line 32) — backend already anticipates an admin caller, but no admin host invokes it.
- Domain entity exposes `Resolve`, `Close`, `Escalate`, `UpdateStatus`, `LinkStripeDispute`, `AddMessage(isStaff)`, but `Close()` and `Escalate()` have **no command/handler** at all.

Here is the user story.

---

# US-admin-0042 — Admin Dispute Management Surface

## Actor narrative

**As a** support administrator working in the Cleansia Admin app,
**I want** a dispute management surface (list, detail, and a resolution panel) served by the Admin API, where I can review every customer-filed dispute, exchange staff messages, move it through its lifecycle status, escalate it, resolve it with an optional refund, and close it,
**so that** disputes filed by customers are actually worked and resolved by the correct actor (support/admin) instead of accumulating in a write-only inbox that is only reachable by a partner/cleaner calling the Partner API directly.

### Grounding / why this is a real gap (code-cited)
- The only `DisputeController` lives in the Partner API: `src/Cleansia.Web.Partner/Controllers/DisputeController.cs`. `Resolve` (`:66`) and `UpdateStatus` (`:78`) are admin-gated (`Policy.CanResolveDispute`, `Policy.CanUpdateDisputeStatus`) but exposed only on the **Partner** host — so an admin's refund decision is made against the wrong API.
- `src/Cleansia.Web.Admin/Controllers/` contains 30 controllers but **no dispute controller**. `libs/cleansia-admin-features/` contains 21 feature libraries but **no `dispute-management`**.
- `PolicyBuilder.cs:76` maps `CanRespondToDispute → PhysicalPolicy.Authenticated`, and `AddDisputeMessage.cs:50` skips the ownership check whenever `IsStaffMessage == true` — so **any authenticated user (including a cleaner) can post a staff-authored message**. This is an S2 (authorization-on-every-endpoint) over-broad-policy defect, not just a missing screen.
- The backend already anticipates the admin actor: `GetPagedDisputes.cs:32` branches on `UserProfile.Administrator` to return all disputes and honor `CustomerEmail`/`CustomerName` filters; non-admins are force-scoped to their own `userId`. The query layer is ready; the host + UI are missing.
- The domain entity (`Dispute.cs`) already exposes `Resolve` (`:82`), `Close` (`:92`), `Escalate` (`:98`), and `UpdateStatus` (`:64`); statuses are `Pending → UnderReview → WaitingForResponse → Resolved/Closed/Escalated` (`DisputeStatus.cs`). But **`Close()` and `Escalate()` have no command/handler** — only `Resolve`, `UpdateStatus`, and `AddMessage` do.

## Acceptance criteria (Given / When / Then)

1. **Admin can list disputes**
   Given an authenticated administrator in the Admin app,
   When they open the Dispute Management page,
   Then they see a paged, sortable, filterable list of **all** disputes across customers (order number, customer name/email, reason, status, created date), served by an Admin-API endpoint — and a non-admin token calling the same endpoint receives 403.

2. **Admin can open a dispute detail with full thread**
   Given a dispute exists,
   When the administrator opens its detail,
   Then they see the order reference, customer identity, reason, description, current status, full message thread (staff vs. customer), uploaded evidence, and any existing resolution notes / refund amount, sourced via the Admin API (no call to the Partner host).

3. **Admin can post a staff message**
   Given the administrator is viewing a dispute,
   When they post a reply,
   Then the message is persisted as a staff message (`IsStaff = true`) attributed to the admin, the customer receives the existing `DisputeReply` push notification, and the action is authorized by an **admin-only** policy (a cleaner/partner token can no longer post a staff message).

4. **Admin can advance lifecycle status and escalate**
   Given a dispute in a non-terminal status,
   When the administrator changes its status (e.g. `Pending → UnderReview → WaitingForResponse`) or escalates it,
   Then the dispute's `Status` is updated, the change is audited with the admin as `updatedBy`, and an `Escalate` action sets status to `Escalated` (a command/handler for `Escalate` must exist, since today only `Resolve`/`UpdateStatus` are reachable).

5. **Admin can resolve with an optional refund**
   Given an open dispute,
   When the administrator resolves it with resolution notes (required, ≤ 2000 chars) and an optional refund amount (≥ 0),
   Then the dispute status becomes `Resolved`, `ResolvedBy`/`ResolvedOn`/`RefundAmount`/`ResolutionNotes` are recorded, and invalid input (empty notes, negative refund) is rejected with the existing `BusinessErrorMessage` keys.

6. **Admin can close a dispute**
   Given a dispute that is resolved or otherwise actionable,
   When the administrator closes it,
   Then status becomes `Closed`, audited with the admin as actor, via an Admin-API endpoint backed by a `Close` command/handler (which does not exist today).

7. **Partner can no longer resolve / update status / post staff messages**
   Given the resolve, update-status, and add-(staff)-message actions are admin/support responsibilities,
   When a partner/cleaner token calls those operations,
   Then the request is rejected (403), because the policies are admin-only and the Partner `DisputeController` no longer exposes these mutating routes (unless a deliberately-scoped "partner may comment" capability is defined as a separate, explicitly-named permission — see out-of-scope).

## Out of scope

- **Triggering an actual Stripe refund.** This story records `RefundAmount` and resolution intent only; wiring `RefundAmount` to a Stripe refund call (and the `LinkStripeDispute` / `StripeDisputeId` chargeback flow on `Dispute.cs`) is a separate ticket.
- **A "partner can comment on a dispute" capability.** If product later wants cleaners to add context, that needs its own explicitly-named permission and a separate (non-staff) message channel; it is not assumed here.
- **Inbound Stripe chargeback ingestion** (creating/linking disputes from Stripe webhook events).
- **Customer-side dispute UX changes** (filing, evidence upload, viewing own thread) — those already exist on the customer/partner path and are untouched.
- **New analytics/SLA dashboards, bulk actions, dispute assignment/ownership routing, and email (non-push) notifications.**
- **EF Core migration authoring and NSwag client regeneration** — owner-only manual steps; flag as `manual_step: ef-migration` (if any new column is needed) and `manual_step: nswag-regen` (new Admin DTOs/endpoints).

## Layers touched

- **Backend — Admin API:** new `AdminDisputeController` in `src/Cleansia.Web.Admin/Controllers/` exposing list / detail / add-staff-message / update-status / escalate / resolve / close, using `[Permission(...)]` (S2). Mirrors the `AdminOrderController` archetype.
- **Backend — AppServices (`Features/Disputes/`):** reuse `GetPagedDisputes`, `GetDisputeDetails`, `AddDisputeMessage`, `UpdateDisputeStatus`, `ResolveDispute`; **add** `EscalateDispute` and `CloseDispute` command/handlers (entity methods `Escalate()`/`Close()` already exist).
- **Backend — Authentication policies:** tighten `CanRespondToDispute` from `Authenticated` to admin-only in `PolicyBuilder.cs:76`; add `CanEscalateDispute` / `CanCloseDispute` constants in `Policy.cs` mapped to `AdminOnly` in `PolicyBuilder.cs`.
- **Backend — Partner API:** remove `Resolve` / `UpdateStatus` (and the staff-message path) from `src/Cleansia.Web.Partner/Controllers/DisputeController.cs`.
- **Frontend — Admin app:** new `libs/cleansia-admin-features/dispute-management` library (list component + facade + models, detail component + facade, resolution panel) following the C-section list archetype used by `order-management`; route registration; admin nav entry.
- **Frontend — API client:** regenerated admin NSwag client for the new endpoints (owner-only `manual_step: nswag-regen`).
- **i18n:** new translation keys for the dispute screens and any new `errors.*` keys, across all 5 locales (en, cs, sk, uk, ru).
- **Not touched:** Android apps; customer dispute-filing flow; domain entity shape (no new fields required for the core story).


---

Confirmed: the admin frontend already collects and sends `reason` in the command payload — it is accepted by the backend `Command.Reason`, validated, then dropped in the handler. The Description column and `LoyaltyTransaction.Create(description:)` overload already exist, so no schema migration is required. All citations are grounded. Here is the user story.

---

# US-admin-0007 — Persist and surface the justification for manual loyalty point adjustments

## Context (grounded in code)

When an admin manually grants or revokes loyalty points, the form already collects a mandatory **Reason** and sends it to the API:

- `user-loyalty-detail.facade.ts:99,136` sends `reason: input.reason` on both grant and revoke.
- `GrantPointsManually.Command` (`GrantPointsManually.cs:13-16`) and `RevokePointsManually.Command` (`RevokePointsManually.cs:13-16`) both require it: `NotEmpty` → `LoyaltyReasonRequired`, plus `MaximumLength(500)` (`GrantPointsManually.cs:38-43`, `RevokePointsManually.cs:38-43`).

But the value is **silently dropped**. Neither handler passes the reason onward:

- `GrantPointsManually.cs:54-60` calls `loyaltyService.GrantPointsManuallyAsync(...)` with no description argument; `RevokePointsManually.cs:54-60` mirrors it.
- `ILoyaltyService.GrantPointsManuallyAsync` / `RevokePointsManuallyAsync` (`ILoyaltyService.cs:48-71`) have **no description parameter**, so the value cannot even be threaded through.
- `LoyaltyAccount.GrantPoints` / `RevokePoints` (`LoyaltyAccount.cs:54-61, 78-85`) call `LoyaltyTransaction.Create(...)` without the optional `description` arg, so the ledger row is written with `Description = null`.

The persistence target **already exists** — `LoyaltyTransaction.Description` is a nullable `[MaxLength(500)]` column (`LoyaltyTransaction.cs:33-34`) and `LoyaltyTransaction.Create` already accepts `string? description = null` (`LoyaltyTransaction.cs:42-48`). The 500-cap on the validators (`GrantPointsManually.cs:42`) matches the column width exactly. So this is a **pure wiring gap, not a schema gap — no EF migration is required.**

Finally, even once stored, the reason is invisible: the admin ledger DTO `GetUserLoyaltyActivity.ActivityItem` (`GetUserLoyaltyActivity.cs:19-26`) and the customer DTO `GetLoyaltyActivity.ActivityItem` (`GetLoyaltyActivity.cs:14-20`) do **not** project `Description`.

This is a compliance/trust gap on a money-adjacent feature: admins are forced to type a justification for every manual adjustment, yet "why did this user get 5,000 points?" cannot be answered from the append-only ledger.

## User story

**As an** admin (and platform operator answerable for the loyalty ledger),
**I want** the mandatory justification I enter when manually granting or revoking a user's loyalty points to be permanently recorded against the resulting ledger entry and shown when I review that user's loyalty activity,
**so that** every manual point adjustment carries an accountable, auditable reason and I can later explain why any balance changed.

## Acceptance criteria

1. **Grant persists the reason**
   Given an admin submits a manual grant for an existing user with a non-empty Reason,
   When the grant is processed,
   Then the resulting `Earn` `LoyaltyTransaction` is persisted with `Description` equal to the submitted Reason (verbatim, up to 500 chars).

2. **Revoke persists the reason**
   Given an admin submits a manual revoke for a user who has a loyalty account, with a non-empty Reason,
   When the revoke is processed,
   Then the resulting `Revoke` `LoyaltyTransaction` is persisted with `Description` equal to the submitted Reason.

3. **Reason is mandatory end-to-end (unchanged guard still holds)**
   Given an admin submits a manual grant or revoke with an empty/whitespace Reason,
   When validation runs,
   Then the command is rejected with `LoyaltyReasonRequired` and no ledger entry is written — and given a Reason longer than 500 characters, it is rejected with `MaxLength`.

4. **Admin can see the reason in the ledger view**
   Given a user has manual-adjustment ledger entries that were created with a Reason,
   When the admin views that user's loyalty activity (`GetUserLoyaltyActivity`),
   Then each returned activity item exposes the stored Description, and the admin UI displays it on the manual grant/revoke rows.

5. **System-generated entries are unaffected**
   Given an order-completion grant or order-cancellation revoke (`LoyaltyEarnSource.OrderCompleted` / `OrderCancelled`, system actor),
   When that ledger entry is created,
   Then its Description remains `null` (no reason is required or invented), and existing idempotency behaviour on `(OrderId, Source)` is unchanged.

6. **Existing ledger rows degrade gracefully**
   Given historical manual adjustments written before this change (Description `null`),
   When they are listed in either the admin or customer activity view,
   Then they render without error and simply show no reason text (no placeholder fabricated).

## Out of scope

- No new `Reason`/`Description` column or schema change — the `LoyaltyTransaction.Description` column and the `Create(description:)` overload already exist; **no EF migration** is part of this story (if implementation finds one needed, flag `manual_step: ef-migration`).
- No backfill of reasons onto historical manual-adjustment rows that were saved as `null`.
- No change to point math, tier recomputation, idempotency keys, or the `(OrderId, Source)` dedup logic.
- No exposure of the actor (admin user id) in the activity DTOs — that is a separate accountability gap, not this story.
- No new "audit log" entity or cross-feature audit subsystem — this story uses the existing loyalty ledger only.
- No surfacing of the reason in the **customer-facing** Rewards UI (`rewards-activity.component.ts`); whether to show admin justifications to end users is a product decision deferred. (Threading Description into the customer `GetLoyaltyActivity` DTO is optional/at-discretion; the required surfacing is the admin view.)
- No change to the existing 500-character limit or the `LoyaltyReasonRequired` error key / translations.

## Layers touched

- **Backend — Domain** (`LoyaltyAccount.cs`): pass a `string? description` through `GrantPoints` / `RevokePoints` into `LoyaltyTransaction.Create(description:)`.
- **Backend — Service** (`ILoyaltyService.cs` + `LoyaltyService.cs`): add a `description` parameter to `GrantPointsManuallyAsync` / `RevokePointsManuallyAsync` and forward it.
- **Backend — AppServices/Features** (`GrantPointsManually.cs`, `RevokePointsManually.cs`): pass `command.Reason` into the service call (handler stays happy-path; validator unchanged).
- **Backend — Query DTOs** (`GetUserLoyaltyActivity.cs`; optionally `GetLoyaltyActivity.cs`): add `Description` to `ActivityItem` and project `t.Description`.
- **Contract** — adding `Description` to `ActivityItem` changes the admin API response shape → flag `manual_step: nswag-regen` (owner regenerates the admin client; hold dependent frontend work until confirmed).
- **Frontend — Admin** (`loyalty-user-detail` feature): render the returned Description on manual grant/revoke ledger rows; add i18n keys (en, cs, sk, uk, ru) for any new label.
- **Tests** (xUnit): cover AC 1, 2, 5, 6 — assert persisted `Description` on manual grant/revoke and `null` on system-sourced entries.

## Rule citations

- **S7 — Idempotency** (`agents/knowledge/security-rules.md:83-91`): the fix must not disturb the existing `(OrderId, Source)` dedup (AC 5) — the cited reference pattern is `LoyaltyService.GrantForCompletedOrderAsync`.
- **S4 — DTO leak prevention** (`security-rules.md:58-68`): the new `Description` field added to the activity DTOs is the admin-entered justification only (no PII / no actor id leak), consistent with the audit-every-DTO rule.
- **Naming trap — command suffix** (`agents/knowledge/conventions.md:93-95`): the `UnitOfWorkPipelineBehavior` only commits requests ending in `Command`; `GrantPointsManually.Command` / `RevokePointsManually.Command` already comply, so the persisted Description will be committed.
- **Owner-only steps** (`conventions.md:97-101`): `nswag-regen` flag is required for the admin DTO change; `ef-migration` is *not* expected (column already present).


---

I now have everything needed to ground the story. Key confirmed facts:

- `MapboxGeocodingService.cs:54-74` — `GetFromJsonAsync` throws `HttpRequestException` on 429/5xx, caught by the broad `when` filter at :68 alongside genuine misses; both log identical `LogWarning` and `return null`.
- `ServiceCollectionExtensions.cs:20-23` — 5s timeout, no resilience handler / no retry policy on the "Mapbox" client.
- `CreateOrder.cs:287-289` — geocoding is inline on the order-create hot path; null coordinates persist silently.
- `GetPagedOrders.cs:199-242` — there IS an inline fire-and-forget backfill (`Task.Run`), but it violates runtime-readiness.md:73 (inline fire-and-forget, not a durable queued Function), re-hits the same un-classified service, and only triggers when an admin lists orders.
- `AddressGeocoder.cs:23-26` — null result silently skips coordinate update.
- `PayCalculatorExtensions.cs:15,42` — `expensesPay = TravelDistance × DistanceRatePerKm`; but `Order.SetTravelDistance` (Order.cs:382) has zero callers — distance-from-coordinates is not yet wired, so pay impact is latent/future, not active today.
- `runtime-readiness.md:55,63,73` — the error-classification, alerting, and durable-queue rules this gap violates.

This grounds the scope precisely. Here is the story.

---

# US-admin-0042 — Make geocoding failures observable and recoverable instead of silently dropping coordinates

## Context (grounded in code)

`MapboxGeocodingService.GeocodeAsync` (`src/Cleansia.Infra.Services/Geocoding/MapboxGeocodingService.cs:51-74`) cannot tell a **transient provider failure** (HTTP 429 rate-limit, 5xx, or the 5s timeout configured at `ServiceCollectionExtensions.cs:20-23`) apart from a **genuine "address not found"**. On 429/5xx, `GetFromJsonAsync` (`:54`) throws `HttpRequestException`, which the broad `catch ... when (ex is HttpRequestException or TaskCanceledException ...)` at `:68` swallows into the *same* `LogWarning` and `return null` used for a legitimate empty result set (`:59-62`). The "Mapbox" `HttpClient` has **no resilience handler / retry policy** (`ServiceCollectionExtensions.cs:20-23`).

Downstream, `AddressGeocoder.PopulateCoordinatesAsync` (`src/Cleansia.Core.AppServices/Services/AddressGeocoder.cs:23-26`) treats `null` as "nothing to do" and silently skips the coordinate update. This call sits **inline on the order-create hot path** (`CreateOrder.cs:287-289`) and on employee address updates (`UpdateEmployee.cs:232`, `UpdateAddressInfo.cs:123`, `AdminUpdateEmployee.cs:140`). A pre-existing inline **fire-and-forget** backfill exists (`GetPagedOrders.cs:199-242`, `Task.Run(...)`), but it (a) only runs when an admin happens to page the orders list, (b) re-hits the same un-classified service, and (c) is exactly the inline-fire-and-forget pattern `agents/knowledge/runtime-readiness.md:73` says side effects must NOT use (they must be a durable, retried queue+Function).

Net effect: during a Mapbox rate-limit spike or outage, **every** address geocoded in the window lands with `Latitude/Longitude = null`, indistinguishable in logs from a real miss, with no retry, no alert, and no reliable backfill. This is the runtime-readiness violation set: error classification (`runtime-readiness.md:55`), differentiated logging/alerting (`:63`), and durable retried jobs (`:73`).

*Scope-shaping note for the team:* `expensesPay = TravelDistance × DistanceRatePerKm` (`PayCalculatorExtensions.cs:15,42`) does depend on geo, **but** `Order.SetTravelDistance` (`Order.cs:382`) currently has **zero callers** — distance-from-coordinates is not yet wired. So the pay impact is *latent/preventive* (protecting a feature that will exist), not an active payroll bug today. This story is scoped to making the failure **observable and recoverable**, not to building distance computation.

## User Story

**As an** Admin (platform operator)
**I want** geocoding to distinguish a transient Mapbox failure (rate-limit / outage / timeout) from a genuine "address not found", log them differently, retry transient failures durably, and give me a visible list of addresses still missing coordinates
**so that** a Mapbox rate-limit spike is immediately visible instead of silently polluting orders and employee records with missing geo, and those records get backfilled automatically once Mapbox recovers — keeping map, routing, and (future) distance-based pay reliable.

## Acceptance Criteria

1. **Transient failures are classified, not swallowed as "not found"**
   **Given** Mapbox returns HTTP 429 or 5xx (or the request hits the 5s timeout)
   **When** `GeocodeAsync` runs
   **Then** the outcome is classified as **Transient** (a distinct result/status, not the same `null` used for an empty feature set), and the boundary is logged at **Warning/Error with the provider, HTTP status, and a correlation id** — clearly distinguishable in logs from a genuine miss (`runtime-readiness.md:55,63,71`).

2. **Genuine "address not found" stays a low-noise outcome**
   **Given** Mapbox returns HTTP 200 with an empty `features` array
   **When** `GeocodeAsync` runs
   **Then** the result is classified as **NotFound** (a real, terminal miss) and logged at an informational/Debug level — and the record is **not** re-queued for retry.

3. **Order creation is never blocked by geocoding, and a transient failure is recoverable**
   **Given** Mapbox is rate-limited or down
   **When** a customer creates an order (`CreateOrder.cs:287-289`)
   **Then** the order is still created successfully with `Latitude/Longitude` null, **and** the address is enqueued for **durable, backoff-retried** backfill (queue + Function per `runtime-readiness.md:55,73`) rather than relying solely on the inline `Task.Run` fire-and-forget in `GetPagedOrders.cs:201`.

4. **Backfill is idempotent and self-healing once Mapbox recovers**
   **Given** an address persisted with null coordinates due to a transient failure
   **When** the backfill job runs after Mapbox recovers
   **Then** the address is geocoded and updated exactly once (re-running the job on an already-geocoded address is a no-op, matching the existing `address.Latitude != null && address.Longitude != null` guard at `GetPagedOrders.cs:229`), and the job has a **max-attempt dead-end** so it never retries forever (`runtime-readiness.md:58,75`).

5. **A rate-limit spike is observable to the operator**
   **Given** a burst of bookings drives Mapbox into sustained 429s
   **When** the transient-failure count crosses a threshold within a window
   **Then** the spike is visible to the owner via the structured logs / alerting surface described in `runtime-readiness.md:63` (e.g. a count of transient geocode failures), distinct from the normal not-found rate.

6. **Stuck records have a visible dead-end**
   **Given** addresses that have exhausted backfill retries
   **When** the owner inspects the failures surface
   **Then** there is a **human-visible place** (failures list/admin view or equivalent) showing addresses still missing coordinates and why, so nothing is stuck silently (`runtime-readiness.md:59,75`).

## Out of Scope

- Computing `TravelDistance` from coordinates / wiring `Order.SetTravelDistance` (`Order.cs:382`, currently uncalled) — distance and the `expensesPay` calculation (`PayCalculatorExtensions.cs:15,42`) are a separate, future piece of work.
- Replacing or adding a second geocoding provider (no Mapbox→Google fallback in this story).
- Any change to pay/payroll math, the order lifecycle, or `EmployeePayConfig`.
- Front-end map / routing UI changes; this story is server-side observability + recovery only.
- Customer/partner-facing UX or notifications about a missing-coordinate address (operator-facing surface only).
- Caching geocode results or address de-duplication beyond the existing idempotent "skip if already geocoded" guard.
- Migrating away from the inline `Task.Run` backfill in `GetPagedOrders` may be done, but reworking the broader paged-orders query is out of scope.

## Layers Touched

- **Backend — Infra.Services** (primary): `MapboxGeocodingService.cs` (error classification, differentiated logging), `ServiceCollectionExtensions.cs` (resilience handler / retry policy on the "Mapbox" `HttpClient`), and the `IGeocodingService`/`GeoCoordinates` contract (`IGeocodingService.cs`) to surface a classified result instead of bare `null`.
- **Backend — Core.AppServices**: `AddressGeocoder.cs` (propagate the classification; enqueue on transient), and the enqueue point at `CreateOrder.cs` and the employee update handlers (`UpdateEmployee`, `UpdateAddressInfo`, `AdminUpdateEmployee`); rework/replace the fire-and-forget backfill in `GetPagedOrders.cs`.
- **Backend — Cleansia.Functions**: a new idempotent, backoff-retried **geocode-backfill** Function (durable queue consumer, per `runtime-readiness.md:53-59`).
- **Infrastructure — queue + alerting**: the backfill queue and the transient-failure spike alert/metric surface.
- **Possible MANUAL_STEP** (owner-only): an EF Core migration **only if** a failures-tracking table/columns are introduced for AC 6; and a Function/queue binding configuration. No NSwag regeneration is expected (no public DTO/endpoint contract change is required by this story).

**Persona rationale:** the actor is **admin** — the observable beneficiaries (differentiated logs, spike alerting, the stuck-records dead-end, and reliable backfill) are operator-facing; the customer's only experience is that order creation keeps working unblocked (AC 3).


---

The questions/open.md doesn't exist yet (the backlog scaffolding is untracked/in-progress per git status). I'm in audit-only mode and must not create files. I have enough grounding now. I'll note the policy question is to be appended to `agents/backlog/questions/open.md` by the PM.

I have all the grounding I need. Here is the user story.

---

# US-admin-0007 — Currency catalog drives display formatting (symbol + code) across the web apps

**Status:** Draft (needs owner policy decision — see Open Question)
**Type:** Functional gap / multi-currency readiness
**Source GAP:** Currency catalog is cosmetic in the web display layer — prices are hardcoded to `'CZK'` in ~10 surfaces, so managing currencies (codes, symbols, exchange rates, and the CC-04 default) changes nothing the customer/partner/admin sees on screen.

## Grounding (what the code actually does today)

The "everything is inert" framing from the prior partial audit is **half true** and the story is scoped to the true half:

- **The catalog is real and partly wired on the backend.** `Currency` (`src/Cleansia.Core.Domain/Internationalization/Currency.cs:6-43`) carries `Code`, `Symbol`, `ExchangeRate (default 1.0)`, and `IsDefault`. The exchange rate **is** applied at quote time — `OrderPricingCalculator.cs:50,66` multiplies the subtotal by `currency.ExchangeRate`. The symbol **is** used in receipts, emails and PDFs — `ReceiptService.cs:311`, `EmailService.cs:94,270`, `FileExtensions.cs:51` (all `order.Currency?.Symbol ?? "Kč"`). A `CurrencyResolutionService` (`src/Cleansia.Core.AppServices/Services/CurrencyResolutionService.cs:11-28`) resolves a currency *code* per employee work-country, falling back to the default currency. So orders/invoices are stamped with a real currency.
- **The web display layer ignores it.** Of ~19 frontend files, roughly half already read the stamped code and only fall back (`order.currency?.code || 'CZK'` — `order-detail.component.ts:194`, `track-order.component.ts:191`, `guest-order-detail.component.ts:158`, partner `orders.models.ts:131,242`, `invoices.models.ts:51`, admin `invoice-management.models.ts:49`, `invoice-detail.facade.ts:274`). The other half **hardcode `'CZK'` unconditionally**, ignoring the catalog entirely:
  - `service-management.facade.ts:87-90` (`currency: 'CZK'`)
  - `package-management.facade.ts:88-90`
  - `pay-config-management.facade.ts:79-81`
  - `reports.facade.ts:124-130` (also a hardcoded `'0 Kč'` empty value)
  - customer `services-catalog.component.ts:149-151`, `home/.../services.component.ts:53-57`, `order-wizard.models.ts:244-252` (a module-level `CZK_FORMATTER`)
- **The admin "Currencies" area is a misleading config surface.** `currency-management.models.ts` renders editable `symbol` and `exchangeRate` columns and an `isDefault` flag, and there's a full create/edit form — but none of that `symbol`/`isDefault` ever reaches the hardcoded-CZK display surfaces. An operator who edits the CZK symbol, sets a new default, or maintains a EUR rate sees zero change on those screens.

This is a real expansion blocker per the GAP, and it makes the admin Currencies area (and CC-04's default) silently no-op on the affected screens.

## Actor narrative

**As an** admin operator of a multi-tenant, multi-country Cleansia tenant,
**I want** every monetary value shown in the customer, partner and admin web apps to be formatted from the platform's currency catalog (the order/invoice's stamped currency, or the catalog default for catalog-level screens — using that currency's symbol/code and locale),
**so that** maintaining the Currencies admin area (symbols, codes, the default) actually controls what users see, and the platform can operate a tenant in a currency other than CZK without code changes.

## Acceptance criteria (Given/When/Then — observable)

1. **Catalog default drives catalog-level prices.**
   **Given** the admin default currency (`IsDefault`) is set to EUR with symbol `€`,
   **When** an admin opens Service Management, Package Management, Pay-Config Management, or Reports, and a customer opens the Services catalog, Home services list, or the Order Wizard,
   **Then** every price renders with the default currency's symbol/code (e.g. `€`), and **no** hardcoded `CZK`/`Kč`/`0 Kč` string remains in those surfaces.

2. **Stamped order/invoice currency wins over the default on entity screens.**
   **Given** an order or employee invoice was stamped with a currency whose code differs from the current default,
   **When** that order/invoice is viewed in any of the customer order screens, partner orders/invoices, or admin invoice screens,
   **Then** the amount renders in the **entity's own stamped currency** (its persisted `currency.code` / `currencyCode`), not the current catalog default and not a hardcoded `'CZK'`.

3. **Editing the catalog is observable end-to-end.**
   **Given** an admin edits a currency's symbol in the Currencies admin area and saves,
   **When** they revisit a screen that formats amounts in that currency,
   **Then** the new symbol appears, with no deploy/code change (proving the catalog is no longer cosmetic on that screen).

4. **Single, shared formatting utility.**
   **Given** the codebase currently has ~10 independent `new Intl.NumberFormat(..., { currency: 'CZK' })` call sites plus a module-level `CZK_FORMATTER`,
   **When** the change is complete,
   **Then** price formatting goes through **one shared currency-format utility** (consistency rule "one way to do each thing", `consistency.md`) that takes a currency code/symbol + the active i18n locale, and the per-component formatters are removed.

5. **Safe fallback preserved.**
   **Given** an entity has no resolvable currency (missing `currency`/`currencyCode`),
   **When** its amount is formatted,
   **Then** the utility falls back to the catalog default currency (and only to `CZK` as a last resort), so no screen renders a blank or `NaN` price — matching the existing `|| 'CZK'` defensive pattern already used by half the surfaces.

6. **Locale stays correct per i18n.**
   **Given** the user has selected one of the 5 supported UI languages,
   **When** a price is formatted,
   **Then** grouping/decimal formatting follows the active locale (not a hardcoded `'cs-CZ'`/`'en-GB'`), while the currency code/symbol comes from the catalog.

## Out of scope (explicit)

- **Live FX / exchange-rate sourcing or refresh.** No external FX feed; `ExchangeRate` stays an admin-maintained field. This story does **not** change how/when `OrderPricingCalculator` applies the rate at quote time — that conversion already works (`OrderPricingCalculator.cs:66`).
- **Re-pricing or re-stamping historical orders/invoices.** Already-persisted amounts and their stamped currency are immutable; this is a **display/formatting** change only.
- **Charging/settlement in non-default currencies, Stripe multi-currency, or payout-currency logic.** No change to the charge boundary or payment provider integration.
- **Backend receipt/email/PDF currency rendering.** Those already use `Currency.Symbol` (`ReceiptService.cs:311`, `EmailService.cs:94,270`, `FileExtensions.cs:51`); not touched here.
- **Android apps.** Mobile currency formatting is a separate ticket if needed; this story is the 3 web apps only.
- **Per-tenant default-currency resolution rules** beyond the existing `CurrencyResolutionService` (work-country → default) — its behavior is consumed, not redesigned.
- **Decimal-places policy normalization** (today the surfaces mix `minimumFractionDigits: 0` and `2`) — the shared util may expose this, but standardizing the values per currency is a follow-up, not a gate.

## Layers touched

- **Frontend (primary):** all 3 web apps. New shared currency-format util (in `libs/shared/...`); refactor the ~10 hardcoded-`CZK` call sites and the half-wired `|| 'CZK'` ones to consume it; ensure the catalog **default currency** is available to catalog-level screens (likely a small NgRx selector / shared catalog state, consistent with rule C8 "NgRx for cross-feature catalogs").
- **Backend (verify only, likely no change):** confirm order/invoice/quote DTOs already expose `currency.code` + `symbol` and that a catalog **default currency** (symbol + code) is fetchable by the web apps for catalog-level screens; if the default's *symbol* isn't currently surfaced to the frontend where needed, that is a small read-side DTO addition → flag `manual_step: nswag-regen`.
- **i18n:** the existing `'0 Kč'` literal empty-state in `reports.facade.ts:124` must move to a translated/util-driven empty value (no hardcoded user-visible currency strings, per the frontend translation rule).
- **Admin UX (no schema change):** the Currencies area becomes truthful once consumed; alternatively, if the owner picks option (b) below, the area is explicitly relabeled "display-only".

## Open question for the owner (append to `agents/backlog/questions/open.md`)

Per the GAP's proposed fix, the v1 currency policy is an owner decision and this story's scope flexes on it:

> **Q (currency-v1-policy):** Do we (a) make the currency catalog actually drive web display formatting now — shared format util fed by the order/invoice's stamped currency, falling back to the catalog **default** for catalog-level screens (this story as written) — or (b) explicitly scope v1 to single-currency CZK and mark the admin **Currencies** area as **display-only / not-yet-wired** (banner + disabled non-default editing) so operators aren't misled? Either is acceptable; what is **not** acceptable is leaving a config surface (symbol/default/exchange rate) that silently does nothing on ~10 screens.

If the owner chooses (b), this story collapses to: add the "display-only" banner + gating to the Currencies admin area and remove the `IsDefault`/symbol-editing affordances' implied promise — ACs 1–3 and 6 are dropped, ACs 4–5 (single util, safe fallback) remain as cleanup.

**Files cited (absolute):**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.Domain\Internationalization\Currency.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\CurrencyResolutionService.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Services\OrderPricingCalculator.cs`
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-admin-features\service-management\src\lib\service-management\service-management.facade.ts` (and the 9 other hardcoded-CZK frontend files listed above)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-admin-features\currency-management\src\lib\currency-management\currency-management.models.ts`


---

No existing stories, so `US-admin-0001` is free. I have grounded every claim in the cited code. Here is the user story.

---

```yaml
---
id: US-admin-0001
title: Protect administrator accounts from self-deletion, GDPR anonymization, and last-admin lockout
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As an **admin (tenant administrator)**, I want the GDPR data-subject tools and the admin-user deactivation flow to **refuse to anonymize, delete, or deactivate administrator accounts in unsafe ways** (my own account, any administrator via the customer-facing GDPR tool, or the last remaining active administrator), so that **a single admin action can never irreversibly destroy an admin identity or lock the tenant out of its own admin console with no recovery path.**

## Grounding (what the code does today)

- `AdminDeleteUserAccount.Validator` (`src/Cleansia.Core.AppServices/Features/Gdpr/AdminDeleteUserAccount.cs:15-24`) only checks `NotEmpty` + `userRepository.ExistsAsync`. Any in-tenant `UserId` is accepted, and the handler calls `gdprDeletionService.DeleteUserAccountAsync(...)`, which runs `user.Anonymize()` + `user.Deactivated(...)` (`GdprDeletionService.cs:234-235`). `User.Anonymize()` (`User.cs:198-215`) is irreversible — it overwrites name/email/phone with markers. There is **no** `Profile == Administrator` guard and **no** self-target guard.
- `AdminExportUserData.Validator` (`src/Cleansia.Core.AppServices/Features/Gdpr/AdminExportUserData.cs:15-24`) has the same existence-only check — an admin's full personal data can be exported via the customer-subject tool with no admin-targeting guard.
- `DeactivateAdminUser.Validator` (`src/Cleansia.Core.AppServices/Features/AdminUsers/DeactivateAdminUser.cs:17-36`) **already** blocks self-deactivation (`CannotDeactivateSelf`, line 33-34) but has **no last-admin guard** — the final active administrator can still be deactivated, locking out the admin console.
- A `CannotDeleteSelf = "admin_user.cannot_delete_self"` constant already exists (`BusinessErrorMessage.cs:208`) but is **referenced nowhere** — the intended self-delete guard was never wired up.

This is a real authorization/availability gap, not a style nit — it maps directly to the project's security laws: **S3** (resource-by-id operations must guard who/what may be targeted) and the priority ordering **security > correctness** in `agents/knowledge/security-rules.md`.

## Acceptance criteria

- **AC1 — GDPR delete cannot target an administrator.** Given an authenticated admin calls `POST /api/v{version}/AdminGdpr/delete-account/{userId}` where the target user's `Profile == Administrator`, When the command is validated, Then it is rejected with a business error (e.g. `admin_user.cannot_gdpr_target_admin`) and no call to `GdprDeletionService.DeleteUserAccountAsync` / `User.Anonymize()` occurs (the target remains fully intact).

- **AC2 — GDPR delete cannot target the caller's own account.** Given an authenticated admin calls the GDPR delete endpoint with a `userId` equal to their own session user id (`IUserSessionProvider.GetUserId()`), When the command is validated, Then it is rejected with `admin_user.cannot_delete_self` (the already-defined, currently-unused constant) and nothing is anonymized.

- **AC3 — GDPR export cannot target an administrator.** Given an authenticated admin calls `GET /api/v{version}/AdminGdpr/export/{userId}` where the target's `Profile == Administrator`, When the query is validated, Then it is rejected with a business error and no `GdprExportDto` (admin PII) is returned.

- **AC4 — Admin deactivation cannot remove the last active administrator.** Given a tenant has exactly one active administrator (`Profile == Administrator` AND `IsActive == true`), When an admin calls `POST /api/AdminUser/{userId}/deactivate` targeting that administrator, Then it is rejected with a last-admin business error (e.g. `admin_user.cannot_deactivate_last_admin`) and the administrator stays active.

- **AC5 — GDPR delete cannot remove the last active administrator (defense in depth).** Given the target user is an administrator and would be the last active administrator in the tenant, When the GDPR delete is attempted, Then it is rejected by the last-admin guard even if AC1's admin-target rule were ever relaxed (the guards are independent and both fail closed).

- **AC6 — Legitimate flows still pass.** Given a tenant with two or more active administrators, When an admin deactivates a *different* administrator (not self, not the last one) via the dedicated `AdminUsers` feature, Then it succeeds; and when a customer/employee (non-administrator) user is GDPR-deleted/exported, Then it succeeds unchanged (the new guards add no false negatives for the intended data-subject population).

- **AC7 — Errors are localized and observable.** Given any new guard rejects an action, Then the response carries the `BusinessErrorMessage` key in `category.specific_error` form, every new key has a matching `errors.*` entry in all five locale files (`en, cs, sk, uk, ru`), and no partial anonymization/side effect has been committed.

## Out of scope

- Any change to **how** anonymization works (`User.Anonymize()`, `GdprDeletionService` blob/order/dispute handling) — only the *gating* of which targets are allowed.
- Building a separate "delete/anonymize an administrator" capability or an admin-offboarding workflow. Administrators continue to be managed solely through the `AdminUsers` feature (create / update / deactivate / activate). Admin data-subject GDPR requests, if ever needed, are a future story.
- A recovery / "promote an emergency admin" flow, break-glass tooling, or super-admin/owner role hierarchy. This story prevents lockout; it does not add recovery.
- Cross-tenant or platform-operator deletion (existing endpoints are already tenant-scoped via the global filter; no change to that scoping).
- Frontend UX beyond surfacing the new error messages (no new confirmation modals, no disabling of buttons based on last-admin count — server-side enforcement is the contract here).
- Audit-log schema changes (the existing `GdprRequest` audit-row pattern is unchanged; rejected attempts need not create a completed audit row).
- Rate limiting, idempotency, or other S5/S7 concerns — not implicated by this gap.

## Layers touched

- **Backend / AppServices (primary):** `AdminDeleteUserAccount.Validator`, `AdminExportUserData.Validator`, `DeactivateAdminUser.Validator` — add admin-target, self-target, and last-admin rules. Needs a repository query to count active administrators in the tenant (`Profile == Administrator && IsActive`) and to read a target's `Profile`; likely a small `IUserRepository` helper.
- **Backend / Common:** new `BusinessErrorMessage` constants (wire up existing `CannotDeleteSelf`; add `cannot_gdpr_target_admin`, `cannot_deactivate_last_admin`, and an export variant). Follows the `category.specific_error` convention.
- **Frontend (i18n only):** add the matching `errors.*` keys to `en/cs/sk/uk/ru.json` in the **admin** app so the rejections render localized. No component logic required.
- **Not touched:** Domain entities, EF schema/migrations (no new columns — `Profile` and `IsActive` already exist), NSwag clients (no DTO/endpoint signature change → no `nswag-regen` manual step), mobile.

## Open questions

- **Q (default assumed):** "Last active administrator" is scoped **per tenant** (count of `Profile == Administrator && IsActive` within the caller's tenant via the global query filter). Assumed yes until the owner confirms there is no platform-wide super-admin that should be exempt.
- **Q (default assumed):** GDPR export of an administrator is **blocked** (AC3) rather than allowed-with-audit. Assumed blocked because the admin GDPR tool is documented as serving customer/employee data-subject requests; revisit if admins must be able to satisfy their own Article 15 request through this tool.


---

All facts confirmed. `IRepository` already exposes `Deactivate(entity)` and `BaseEntity.IsActive` exists (defaults to true). The proposed fix is fully grounded: the soft-delete mechanism, the in-use pattern, the error keys, and the catalog `IsActive` filter all already exist in the codebase — only the guard wiring for Service/Package is missing.

I now have everything needed to write the story.

---

# US-admin-0007 — Guard Service & Package deletion against in-use catalog items (soft-delete history-bearing entries)

## Actor narrative

**As an** admin managing the cleaning-services catalog,
**I want** the system to refuse to hard-delete a Service or Package that is still referenced by past orders or pay configs — and instead deactivate it (hide it from the booking catalog while keeping its order history) —
**so that** I can retire obsolete catalog items without triggering opaque 500 errors or silently destroying order-line history, receipts, and pay calculations for completed work.

## Context (grounded in code)

- `DeleteService.Handler` (`src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs:31-38`) and `DeletePackage.Handler` (`src/Cleansia.Core.AppServices/Features/Packages/DeletePackage.cs:31-38`) call `repo.Remove(entity!)` after a validator that only checks existence (`ExistsAsync`). There is no in-use guard.
- These delete endpoints are admin-only: `AdminServiceController.cs:96` (`[Permission(Policy.CanDeleteService)]`) and `AdminPackageController.cs:80` (`[Permission(Policy.CanDeletePackage)]`); both map to `PhysicalPolicy.AdminOnly` (`PolicyBuilder.cs:92,98`). This is an **admin** persona story.
- Real FK references to these catalog rows exist on historical data:
  - `OrderService.ServiceId` (`src/Cleansia.Core.Domain/Orders/OrderService.cs:11`) / `Service.IncludedInOrders` back-reference (`Service.cs:35-36`)
  - `OrderPackage.PackageId` (`src/Cleansia.Core.Domain/Orders/OrderPackage.cs:11`)
  - `EmployeePayConfig.ServiceId` / `EmployeePayConfig.PackageId` (`src/Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs:15,18`) — drive per-employee pay calculation.
- The sibling deletes already implement the intended pattern: `DeleteCurrency.cs:32-34,55-59` and `DeleteCountry.cs:26-28,45-49` guard with `IsInUseAsync(...)` in both validator and handler and fail with `CurrencyInUse`/`CountryInUse`. `ICurrencyRepository` declares `IsInUseAsync` (`ICurrencyRepository.cs:10`); `IServiceRepository`/`IPackageRepository` are bare (`IServiceRepository.cs:5`, `IPackageRepository.cs:5`).
- The fix's building blocks already exist:
  - Error keys `ServiceInUse = "service.in_use"` and `PackageInUse = "package.in_use"` are already defined but **unused** (`BusinessErrorMessage.cs:223,230`) — the guard was anticipated but never wired.
  - `IRepository.Deactivate(entity)` already exists (`IRepository.cs:45`) and `BaseEntity.IsActive` defaults to `true` (`BaseEntity.cs:7`).
  - The customer catalog query already hides deactivated rows: `GetServiceOverview.cs:21` does `.Where(s => s.IsActive)` (per S10, with an explanatory comment at lines 17-19).
- Cited rules this satisfies: **consistency.md B6** (soft-delete via `repo.Deactivate` for business-/user-facing entities that carry history; existing hard-deletes are tracked violations — `consistency.md:67-72`) and **security-rules.md S10** (`IsActive` is the soft-delete flag with no global filter; catalog lists must self-filter — `security-rules.md:114-121`).

## Acceptance criteria (Given / When / Then)

1. **Block hard-delete of an in-use Service**
   **Given** a Service that is referenced by at least one `OrderService`, `PackageService`, or `EmployeePayConfig` row,
   **When** an admin calls `DELETE` on that Service,
   **Then** the request fails with `BusinessResult.Failure<Response>(new Error(nameof(command.ServiceId), BusinessErrorMessage.ServiceInUse))`, the row is **not** removed, and no FK violation / 500 is produced.

2. **Block hard-delete of an in-use Package**
   **Given** a Package referenced by at least one `OrderPackage` or `EmployeePayConfig` row,
   **When** an admin calls `DELETE` on that Package,
   **Then** the request fails with an `Error(nameof(command.PackageId), BusinessErrorMessage.PackageInUse)`, the row is **not** removed, and no FK violation / 500 is produced.

3. **In-use item is deactivated instead of removed**
   **Given** an admin chooses to retire (soft-delete) an in-use Service or Package,
   **When** the deactivate action is invoked,
   **Then** `repo.Deactivate(entity)` runs (sets `IsActive = false`), the row and all its order-line history survive, and the command returns success.

4. **Deactivated catalog items disappear from the customer booking catalog**
   **Given** a Service/Package has `IsActive = false`,
   **When** the customer booking catalog is fetched (`GetServiceOverview` and the equivalent package overview),
   **Then** the deactivated item is excluded (the existing `Where(x => x.IsActive)` filter continues to apply) while it remains visible/queryable in admin views.

5. **History and downstream calculations remain intact after deactivation**
   **Given** a past order or pay config references a now-deactivated Service/Package,
   **When** that order's receipt is regenerated or its pay is recalculated,
   **Then** the order line, receipt totals, and `EmployeePayConfig`-driven pay calculation resolve unchanged (no orphaned FK, no null catalog reference).

6. **Unreferenced catalog items still delete cleanly**
   **Given** a Service/Package with zero `OrderService`/`OrderPackage`/`PackageService`/`EmployeePayConfig` references,
   **When** an admin deletes it,
   **Then** the existing hard-delete path (`repo.Remove`) succeeds and returns `Response` as today — no behavioral regression for never-used items.

7. **Validator mirrors the handler guard (parity with siblings)**
   **Given** the in-use guard,
   **When** the `Delete{Service|Package}.Validator` runs,
   **Then** it includes a `MustAsync(!IsInUseAsync(...))` rule producing `Service/PackageInUse` (matching `DeleteCurrency`/`DeleteCountry`), and the handler re-checks defensively before mutating.

## Out of scope

- Backfilling / retroactively soft-deleting catalog items that may already have been hard-deleted in existing data.
- Changing the global query-filter design — **no** EF global filter for `IsActive` is to be added (S10 is intentional; admins must still see deactivated rows).
- A new "reactivate" flow / admin toggle UI beyond what is required to surface the new failure/deactivate outcome (a dedicated reactivation story can follow).
- Extending the same guard to other unguarded hard-deletes (e.g. `DeleteServiceCity`, category deletes) — separate findings/stories.
- The broader CAT-02 exception-swallowing fix that currently masks the FK 500 — referenced as the reason the symptom is opaque, but its remediation is a distinct ticket.
- Changing `EmployeePayConfig` pay-calculation logic or the order receipt/PDF generation itself.
- Frontend redesign of the service/package management tables beyond rendering the new error and (if in scope of the chosen UX) a deactivate affordance.

## Layers touched

- **Domain / Repositories** — add `IsInUseAsync(...)` to `IServiceRepository` and `IPackageRepository` (checking Orders + `EmployeePayConfig`, and `PackageService` for Service); their EF implementations in `Cleansia.Infra.Database`.
- **AppServices (CQRS)** — `DeleteService` and `DeletePackage` validator + handler (guard + `Deactivate` branch); reuse existing `ServiceInUse`/`PackageInUse` keys in `BusinessErrorMessage`.
- **Web.Admin API** — `AdminServiceController` / `AdminPackageController` delete endpoints surface the new `BusinessResult.Failure` as a 400 (and expose the deactivate outcome). **MANUAL_STEP: nswag-regen** for the admin client after any DTO/response shape change.
- **Frontend (admin app)** — `service-management` and `package-management` facades/components render the new in-use error and the deactivate path.
- **i18n** — add `errors.service.in_use` and `errors.package.in_use` keys across all 5 locales (en, cs, sk, uk, ru).
- **Tests** — xUnit handler/validator tests for in-use → failure, unreferenced → remove, and in-use → deactivate-preserves-history.
- **MANUAL_STEP: ef-migration** — only if the FK delete behavior (cascade vs restrict) on `OrderService`/`OrderPackage`/`EmployeePayConfig` is adjusted to enforce restrict at the DB level; owner-applied.


---

I have full grounding across all layers. The `SetDefaultSavedAddress` pattern uses `ClearDefaultForUserAsync` (repo) + `SetDefault(true)` within one handler/UnitOfWork transaction, exposed via `[HttpPost("SetDefault")]`. Currency's `SetAsDefault` is never called. There's an existing `set_default` i18n key for the customer app's saved addresses ("Set as default"), so the label string is established convention. Now I'll write the user story.

---

# US-admin-0007 — Promote a currency to platform default

## Context (grounded in code)

`Currency.SetAsDefault(bool)` exists at `src/Cleansia.Core.Domain/Internationalization/Currency.cs:39-42` but is **never called** anywhere in the solution (verified: zero references outside its own declaration). The admin currency surface (`src/Cleansia.Web.Admin/Controllers/AdminCurrencyController.cs`) exposes only `get-overview`, `details`, `create`, `update`, and `delete` — there is no set-default endpoint. The admin frontend (`currency-management.facade.ts`, `currency-management.component.ts`) offers Create / Edit / Delete actions only.

This produces a frozen-state dead-end:
- A new currency is always created as non-default (`Currency.Create` never sets `IsDefault`; `CreateCurrency.cs:60`).
- The seeded default can never be demoted, because `DeleteCurrency` blocks deleting the default (`DeleteCurrency.cs:29-31, 50-53` → `CannotDeleteDefaultCurrency`).
- `CurrencyRepository.GetDefaultAsync` (`CurrencyRepository.cs:10-14`) and the overview ordering (`GetCurrencyOverview.cs:18` `OrderByDescending(c => c.IsDefault)`) all assume a single default that, in practice, can only ever be the seed value.

This is inconsistent with the established same-pattern feature `SetDefaultSavedAddress` (`src/Cleansia.Core.AppServices/Features/SavedAddresses/SetDefaultSavedAddress.cs`), which clears the prior default then sets the new one in a single handler/UnitOfWork transaction (`ClearDefaultForUserAsync` + `SetDefault(true)`, lines 54-57), is exposed via `[HttpPost("SetDefault")]` (`Cleansia.Web.Customer/Controllers/SavedAddressController.cs:36`), and has a customer-app "Set as default" UI action (i18n key `set_default`, `apps/cleansia.app/src/assets/i18n/en.json:605`).

## User story

**As an** admin (platform operator),
**I want** to promote any existing currency to be the platform default from the currency-management screen,
**so that** I can change the platform's billing/display default currency after launch instead of being permanently locked to the seeded value.

## Acceptance criteria

1. **Given** I am an admin viewing the currency list with View permission, **when** a currency is not the current default, **then** I see a "Set as default" action for it; **and** the currency that is already the default does not offer that action (it is already default).

2. **Given** I select "Set as default" on a non-default currency and confirm, **when** the request succeeds, **then** that currency becomes `IsDefault = true`, the previously-default currency becomes `IsDefault = false`, and exactly one currency in the system has `IsDefault = true` (the change is applied atomically in a single transaction — no window with zero or two defaults).

3. **Given** the set-default request succeeded, **when** the list reloads, **then** the newly-promoted currency is shown as default and appears first (consistent with the existing `OrderByDescending(c => c.IsDefault)` ordering in `GetCurrencyOverview`), and a success message is shown.

4. **Given** I attempt to set-default a currency id that does not exist, **when** the command is validated, **then** it is rejected with the existing `CurrencyNotFound` business error and no currency's default flag changes.

5. **Given** I try to set-default the currency that is *already* the default, **when** the command runs, **then** the system remains in a valid single-default state (the operation is a no-op success or a guarded rejection — pick one and apply it consistently) and never ends with zero defaults.

6. **Given** I am an authenticated admin **without** the currency-management update permission (or an unauthenticated caller), **when** I call the set-default endpoint, **then** I receive 403 (or 401) and no data changes — consistent with the `[Permission(...)]` + `AdminOnly` policy gating on every other write in `AdminCurrencyController` (`Policy.cs:144-147`, `PolicyBuilder.cs:117-120`).

7. **Given** a currency was promoted to default, **when** an admin later tries to delete it, **then** deletion is still blocked by the existing `CannotDeleteDefaultCurrency` rule (the new feature must not regress delete-protection of the default).

## Out of scope

- Creating, editing, or deleting currencies (already implemented in `AdminCurrencyController`).
- Changing how the default currency is *consumed* (exchange-rate conversion, `CurrencyResolutionService`, order/invoice pricing) — only the promotion of the default flag is in scope; downstream resolution already reads `GetDefaultAsync`.
- Per-tenant default currency. `Currency` is not an `ITenantEntity`; default remains platform-wide. (No multi-tenant default-currency behavior is introduced.)
- Exposing set-default on the Partner API (`Cleansia.Web.Partner/Controllers/CurrencyController.cs` stays read-only) or any mobile/customer surface.
- New permission policy: reuse `Policy.CanUpdateCurrency` (do **not** invent a new policy).
- Backfilling/repairing data where zero or multiple defaults already exist (assumes the seed invariant of exactly one default holds).
- Currency-code/symbol/exchange-rate validation changes.

## Layers touched

- **Domain** — none new; reuse `Currency.SetAsDefault(bool)` (`Currency.cs:39-42`).
- **AppServices (CQRS)** — new `SetDefaultCurrency` command + handler + validator under `Features/Currencies/`, mirroring `SetDefaultSavedAddress`. Handler clears the prior default and sets the new one in one transaction (no `CommitAsync` in handler — UnitOfWork pipeline commits, per `patterns-backend.md:90,108`). Validator reuses `CurrencyNotFound`.
- **Infra.Database** — repository support to clear the existing default (e.g. a `ClearDefaultAsync` on `ICurrencyRepository`/`CurrencyRepository`, mirroring `ClearDefaultForUserAsync`). No schema change required (the `IsDefault` column already exists); confirm whether a one-default DB invariant/filtered index is desired (DB agent call).
- **Web.Admin** — new `[HttpPost("set-default/{currencyId}")]` (or `set-default` body) action on `AdminCurrencyController` gated by `[Permission(Policy.CanUpdateCurrency)]`.
- **Frontend (admin)** — "Set as default" row action in `currency-management.models.ts` + handler in `currency-management.component.ts`/`.facade.ts` calling the new client method; hide it on the current default; success snackbar. New i18n keys under `pages.currency_management.*` in all 5 locales (en, cs, sk, uk, ru) — a "Set as default" label already exists for saved addresses (`set_default`) as a wording reference.
- **MANUAL_STEP (owner-only):** `manual_step: nswag-regen` — regenerate the admin NSwag client after the new endpoint lands, before the frontend action can call it. No EF migration is strictly required unless the team chooses to add a one-default filtered index (flag as a separate `MANUAL_STEP: ef-migration` if so).


---

The stories folder is empty (no prior id to collide with). I have grounded every claim in the cited code. Here is the user story.

---

# US-customer-0001 — Disputes list: status filter + unread-reply indicator

## Context (grounded in code)
- **No status filter is wired.** `disputes.component.ts:117-119` calls `facade.loadDisputes(this.first, this.rows)` with only paging; `dispute.actions.ts:8-11` (`loadCustomerDisputes`) accepts only `{ offset, limit }`; `dispute.effects.ts:17-22` calls `disputeClient.getPaged(...)` passing `undefined` for all 13 filter/sort slots before `offset`/`limit`. The backend already supports it: `GetPagedDisputes.cs:18-21` exposes `DisputeFilter? Filter`, `DisputeFilter.cs:9` has `int[]? Statuses`, and the handler force-scopes non-admins to their own `UserId` (`GetPagedDisputes.cs:32-38`), so a customer status filter is purely additive client wiring — no new endpoint.
- **No unread/last-read concept exists.** `AddDisputeMessage.cs:65-77` fires a `DisputeReply` push to the customer for staff messages, but nothing records read state. `DisputeMessage.cs` has only `CreatedOn` (no per-recipient read marker); `Dispute.cs` has no "customer last viewed" timestamp; and `DisputeListItem.cs:5-16` carries no last-message time or unread flag. So the list cannot reflect the notification's "new reply" state. **This requires a data-model change and must be escalated** (see Out of scope + Dependencies).

## Actor narrative
**As a** customer with multiple disputes,
**I want** to filter my disputes by status and see at a glance which ones have a new support reply I haven't read,
**so that** I can separate open issues from resolved ones and jump straight to the dispute that needs my attention, instead of opening each one to check.

## Acceptance criteria

1. **Status filter is offered and applied**
   Given I am on the customer Disputes list with disputes in several statuses
   When I select one or more statuses (e.g. Pending, UnderReview, Resolved) in the list's status filter
   Then only my disputes in the selected status(es) are shown, the result count/paginator reflects the filtered set, and clearing the filter restores the full list.

2. **Filter is honoured server-side via the existing contract**
   Given I have applied a status filter
   When the list reloads
   Then the request passes the chosen status codes into the existing `DisputeFilter.Statuses` slot of `getPaged` (no new endpoint), and the results remain scoped to only my own disputes.

3. **Filter survives paging**
   Given I have a status filter applied and more results than one page
   When I move to another page
   Then the same status filter stays applied on the new page (filter and paging combine, they do not reset each other).

4. **Unread-reply indicator on the list**
   Given a support/staff reply was added to one of my disputes after the last time I opened that dispute
   When I view the Disputes list
   Then that dispute row shows a clear "new reply" indicator, and disputes with no unread staff reply show no indicator.

5. **Opening a dispute clears its indicator**
   Given a dispute row shows the "new reply" indicator
   When I open that dispute's detail and view the messages
   Then its last-viewed marker advances so the indicator is cleared for that dispute on the next list load, while other disputes' indicators are unaffected.

6. **Empty / loading / error states are explicit**
   Given the filtered list is loading, returns zero matches, or the request fails
   When I am on the Disputes list
   Then I see the loading state, a localized "no disputes match" empty state, or a localized error — never a blank screen or a raw error (the three explicit data states per `conventions.md`).

## Out of scope
- Admin and partner dispute lists (this story is the **customer** app only; the admin path in `GetPagedDisputes.cs:30-38` and partner/admin dispute controllers are untouched).
- Filtering by reason, date range, refund amount, customer name/email — only **status** filtering here, even though `DisputeFilter` exposes the others.
- Free-text search and custom sort UI on the disputes list.
- Push-notification delivery/wording and the `DisputeReply` event itself (`AddDisputeMessage.cs:65-77`) — unchanged.
- Per-message read receipts or an unread **count** per dispute; this story is a binary "has a new staff reply since I last viewed" indicator, not a counter.
- Real-time live update of the indicator without a reload (a refresh/navigation re-load is sufficient).
- Adding new dispute statuses or changing the `DisputeStatus` enum.

## Dependencies / escalation (blocks AC 4 & 5)
The unread indicator needs a persisted "customer last viewed this dispute" timestamp (or equivalent read marker), plus surfacing the dispute's last staff-message time and an unread flag on `DisputeListItem`. There is no such field today (`Dispute.cs`, `DisputeMessage.cs:23`, `DisputeListItem.cs`). Per the proposed fix and `conventions.md` ("a genuinely new abstraction is an Architect/ADR decision; raise it via the ticket"), this **data-model decision must be escalated to `agents/backlog/questions/open.md`** before AC 4/5 are built — options: a `CustomerLastViewedOn` column on `Dispute` vs. a separate read-state table. AC 1-3 (status filter) are unblocked and can ship independently of this decision.

Notes tied to project rules:
- New `DisputeListItem` field(s) and any `Dispute` column are **owner-only manual steps**: flag `manual_step: ef-migration` (nullable column, S9-safe) and `manual_step: nswag-regen` (added/nullable DTO field, non-breaking per S9). Do not hand-edit the generated client or run regen (root + frontend CLAUDE.md).
- Self-scoping is already enforced server-side (`GetPagedDisputes.cs:32-38`) and ownership on detail/message goes through `dispute.UserId` checks (`AddDisputeMessage.cs:50-54`), so this stays within S1/S3; advancing the last-viewed marker must also derive the user from the JWT/`IUserSessionProvider`, never from client input (S1).
- All new UI strings (status labels, indicator tooltip/badge, empty/error states) need keys in all 5 locales `{en,cs,sk,uk,ru}` via `TranslatePipe`; use `cleansia-*`/PrimeNG controls only, no raw HTML controls (`conventions.md`, frontend rules).

## Layers touched
- **Frontend (customer app)** — primary: `disputes.component.ts` + template (status filter UI, indicator column, empty/error states), `disputes.facade.ts` (pass status filter, advance last-viewed on open), `dispute.actions.ts` (add `statuses` to `loadCustomerDisputes`), `dispute.effects.ts` (map statuses into `getPaged`'s `DisputeFilter.Statuses`). i18n files for 5 locales.
- **Backend (AppServices)** — only if the indicator is taken: extend `DisputeListItem` (and its mapper in `DisputeMappers.cs`) with last-staff-reply time / unread flag, add a "mark dispute viewed" command, and add a last-viewed field to `Dispute`. The `Statuses` filter itself needs **no** backend change.
- **Database (Infra.Database)** — only if the indicator is taken: nullable column on `Dispute` (or a new read-state table) + entity config — owner-applied migration.
- **NSwag client** — regenerate customer client if `DisputeListItem`/commands change — owner-only.

Files cited: `dispute.actions.ts:8-11`, `dispute.effects.ts:12-36`, `disputes.component.ts:117-119`, `disputes.facade.ts:60-62`, `AddDisputeMessage.cs:50-77`, `GetPagedDisputes.cs:18-38`, `DisputeFilter.cs:9`, `DisputeListItem.cs:5-16`, `Dispute.cs`, `DisputeMessage.cs:23`, `DisputeStatus.cs`.


---

I now have full ground truth. Key findings confirmed:

- Partner page: `PartnerGdprComponent {}` (empty class, line 22) renders only static legal text + a "go back" button.
- Backend fully supports it: `Cleansia.Web.Partner/Controllers/GdprController.cs` has all 5 endpoints (export, delete-account, consents GET/POST/withdraw), all gated by `Policy.CanXxx` that are "Authenticated (All roles)" (Policy.cs:189-193) — so partners are authorized.
- Reusable facade exists on customer side (`gdpr.facade.ts`) and the partner NSwag client already exposes `GdprClient`/`ConsentsClient` (partner-client.ts:4109, 4339) — so no NSwag regen needed.
- Partner i18n `pages.gdpr.*` has only the static legal keys + 3 error keys; the action/consent-label keys (`export_success`, `consent_updated`, `delete_confirm_*`, `consent_types.*`, etc.) used by the customer page do not exist in the partner locale yet → must be added in all 5 locales.
- One caveat I flag: the existing customer facade lives under `cleansia-customer-features` but imports `CustomerClient`/`CustomerAuthService`; the partner facade must use the partner client + partner auth equivalents, not copy those imports verbatim.

Here is the user story.

---

# US-partner-0007 — Partner GDPR self-service (export, erase, consent)

## Actor narrative

**As a** partner/cleaner (an authenticated data subject in the Partner app),
**I want** to export my personal data, request deletion of my account, and view and change my consents from the GDPR page,
**so that** I can exercise the same data-subject rights the platform already grants customers — instead of seeing a page that only displays static legal text.

## Context / grounding (why this is a real gap, not a feature request)

- The Partner GDPR page is inert: `libs/cleansia-partner-features/gdpr/src/lib/gdpr/gdpr.component.ts:22` is `export class PartnerGdprComponent {}`, and its template (`gdpr.component.html`) is static legal copy plus a single "go back" button.
- The backend is fully built and reachable for partners: `src/Cleansia.Web.Partner/Controllers/GdprController.cs:16-61` exposes `GET export`, `POST delete-account`, `GET consents`, `POST consents`, `POST consents/withdraw`, each gated by `Policy.CanExportOwnData` / `CanDeleteOwnAccount` / `CanViewOwnConsents` / `CanGrantConsent` / `CanWithdrawConsent` — all of which are "Authenticated (All roles)" in `Policy.cs:189-193`, i.e. partners are authorized.
- A working reference implementation exists on the customer side (`libs/cleansia-customer-features/gdpr/src/lib/gdpr/gdpr.facade.ts` + `gdpr.component.ts/.html`) and the partner NSwag client already exposes `GdprClient` and `ConsentsClient` (`libs/core/partner-services/src/lib/client/partner-client.ts:4109,4339`). So this is reuse/wiring, not new backend or client generation — consistent with the conventions "prime directive" (`agents/knowledge/conventions.md:16-30`).

## Acceptance criteria (Given/When/Then)

1. **Export**
   **Given** I am an authenticated partner on the GDPR page,
   **When** I activate "Export my data",
   **Then** the app calls the partner GDPR `export` endpoint and a `my-data-export.json` file containing my `GdprExportDto` downloads in the browser, and a success snackbar is shown; on failure an error snackbar is shown and no file downloads.

2. **View consents**
   **Given** I am an authenticated partner,
   **When** the GDPR page loads,
   **Then** my current consents (e.g. Terms of Service, Privacy Policy, Marketing Emails, Data Processing) are fetched from the partner `consents` endpoint and each is rendered with a toggle reflecting its granted/withdrawn state, with a loading state shown while fetching.

3. **Change a consent**
   **Given** my consents are displayed,
   **When** I toggle a consent on or off,
   **Then** the app calls the partner grant (`consents`) or withdraw (`consents/withdraw`) endpoint accordingly, shows a success snackbar, and the toggle reflects the persisted server state after a re-fetch (IP/user-agent are not sent from the client — they are captured server-side).

4. **Delete account (with confirmation)**
   **Given** I am an authenticated partner,
   **When** I activate "Delete my account",
   **Then** I must confirm in a dialog before anything happens; on confirm the app calls `delete-account`, and on success shows a success snackbar and logs me out / redirects; on a blocked deletion (active order or pending invoice) the partner-localized error message is shown and I remain logged in.

5. **Localization**
   **Given** the page exposes new action, consent-label, and outcome strings (export/delete/consent buttons, confirm dialog, success/error toasts, consent-type labels),
   **When** the app runs in any supported language,
   **Then** every user-visible string resolves to a translation key present in all 5 partner locale files (`en, cs, sk, uk, ru`) with no hardcoded text and no missing-key fallbacks. (Today the partner `pages.gdpr.*` block holds only the static legal copy plus 3 deletion error keys — the action/consent keys must be added.)

6. **Authorization parity / no unauthenticated leak**
   **Given** I open the GDPR page while not authenticated,
   **When** the page renders,
   **Then** no consents call is made and no data-subject actions are offered (the static legal sections may still render), matching the customer page's authenticated-gating behavior.

## Out of scope

- Any backend changes — controllers, MediatR handlers (`ExportUserData`, `DeleteUserAccount`, `GetUserConsents`, `GrantConsent`, `WithdrawConsent`), policies, DTOs, and services already exist and are unchanged.
- NSwag client regeneration — the partner `GdprClient`/`ConsentsClient` already exist; do **not** run `npm run generate-*-client` or hand-edit generated files. (If, and only if, a needed method is found missing during implementation, flag `manual_step: nswag-regen` rather than editing the client.)
- The Admin GDPR surface (`AdminGdprController`, `GetAllGdprRequests`, admin export/delete) and the GDPR request-tracking/admin-review workflow.
- The mobile apps (`Cleansia.Web.Mobile.Partner` GDPR controller / Android partner app GDPR screens).
- Customer GDPR page behavior — it is the reference, not a deliverable; do not modify it.
- Rewriting or restyling the existing static legal copy/sections; data-retention policy text changes.
- Refactoring the customer facade into a shared lib (a tempting but separate Architect/ADR decision — the partner facade should mirror the customer one using partner-side clients/auth, **not** import `CustomerClient`/`CustomerAuthService` as the current customer facade does).

## Layers touched

- **Frontend only** — `libs/cleansia-partner-features/gdpr/`:
  - New partner `GdprFacade` (mirror of the customer facade; `UnsubscribeControlDirective` + signals + `SnackbarService`, using the **partner** `GdprClient`/`ConsentsClient` and partner auth/session).
  - `PartnerGdprComponent` wired to the facade (OnPush, `ConfirmationService` for the delete confirm, PrimeNG `ToggleSwitch`) and its template extended with export/delete/consent UI while keeping the existing legal sections.
  - i18n: add the action/consent/outcome `pages.gdpr.*` keys to all 5 partner locale files.
- **No backend, no DB, no NSwag, no mobile.**

---

Relevant files (all absolute):
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-partner-features\gdpr\src\lib\gdpr\gdpr.component.ts` (empty class to wire)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-partner-features\gdpr\src\lib\gdpr\gdpr.component.html` (static template to extend)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\gdpr\src\lib\gdpr\gdpr.facade.ts` + `gdpr.component.ts` (reference to mirror)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Partner\Controllers\GdprController.cs` (existing partner endpoints)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Authentication\Policy.cs:189-193` (GDPR policies, all-roles)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\core\partner-services\src\lib\client\partner-client.ts:4109,4339` (existing `GdprClient`/`ConsentsClient`)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\apps\cleansia-partner.app\src\assets\i18n\en.json` (+ `cs/sk/uk/ru.json`) — i18n keys to add


---

I now have everything grounded. Key findings confirmed:

1. **All four membership commands** call Stripe with no `try/catch` — confirmed at the cited lines.
2. **CreateOrder.cs:362** is the exact reference pattern (narrow `catch (StripeException)` → `PaymentGatewayUnavailable`).
3. **The phase-2 gap is real and worse**: In `CreateMembershipSubscription` (PaymentMethodConfirmed=true), `CreateSubscriptionAsync` succeeds at Stripe (line 85), then `userMembershipRepository.Add` (94) only commits via the UnitOfWork pipeline. If that commit fails, the customer is subscribed/billed at Stripe with no local `UserMembership` row. Critically, the webhook handler's `ProvisionFromCreatedEventAsync` only fires on `customer.subscription.created` for the **web Checkout flow** — but `CreateSubscriptionAsync` (the SetupIntent/mobile flow) also emits `subscription.created`, so the webhook *could* reconcile it. However, there's no idempotency guard keyed on user/plan, and a synchronous retry of phase-2 would call `CreateSubscriptionAsync` again with a **fresh** `Guid` attempt id (line 84), creating a **second** Stripe subscription.
4. **F8 is confirmed** (consistency-violations.md:66) and names only `CreateMembershipSubscription` + `CreateOrder` — not Swap/Cancel/CheckoutSession.
5. `PaymentGatewayUnavailable = "order.payment_gateway_unavailable"` exists but is order-namespaced; there's no `membership.*` gateway key.
6. Rules **B8** (consistency.md:75-79), **S7** (security-rules.md:83), and the runtime-readiness Stripe matrix all back the finding.

Here is the user story.

---

# US-customer-0042 — Membership Stripe calls must fail gracefully and never double-subscribe

**Type:** bug-risk / reliability
**Size:** M
**Layers:** Backend (AppServices, MediatR handlers; possibly Stripe abstraction + domain), i18n (frontend locale files for the new error key). No frontend component logic, no mobile, no DB schema beyond the optional idempotency-guard read.
**Rules cited:** B8 (`consistency.md:75-79`), S7 (`security-rules.md:83-88`), runtime-readiness Stripe matrix (`runtime-readiness.md:44`). Tracked as **F8** (`agents/backlog/audits/consistency-violations.md:66`).

## Actor narrative

**As a** customer subscribing to, switching, or cancelling a Cleansia Plus membership,
**I want** a transient Stripe outage to surface as a clear "payment provider unavailable, try again" message rather than a generic server error,
**so that** I can safely retry without risking being billed for a membership that never appears in my account, or being charged twice for two parallel subscriptions.

## Grounding (read, not assumed)

- `CreateMembershipSubscription.cs:67,85,107-108`, `CreateMembershipCheckoutSession.cs:64,80`, `SwapMembershipPlan.cs:61`, `CancelMembershipSubscription.cs:38` — every Stripe call is unguarded; any `StripeException` bubbles to an unhandled 500.
- `CreateOrder.cs:357-372` is the canonical pattern to mirror: narrow `catch (StripeException)` → `BusinessResult.Failure(... PaymentGatewayUnavailable)`, with a comment that only transient/API-level Stripe failures map to the gateway error (anything else still bubbles).
- Phase-2 of `CreateMembershipSubscription` (`PaymentMethodConfirmed == true`, lines 85-94): `CreateSubscriptionAsync` charges at Stripe **before** the `UserMembership.Create` / `Add` is committed by the UnitOfWork pipeline. A commit failure leaves the customer billed with no local row, and a synchronous retry mints a **fresh** `idempotencyAttemptId` (line 84) → a **second** Stripe subscription.
- `BusinessErrorMessage.PaymentGatewayUnavailable = "order.payment_gateway_unavailable"` exists but is order-namespaced (`BusinessErrorMessage.cs:90`); there is no `membership.*` gateway key today.
- `IUserMembershipRepository` (interface doc, lines 5-11) states writes only happen via the webhook handler "there's no business operation that creates memberships locally" — which is exactly contradicted by `CreateMembershipSubscription` phase-2, the source of the reconciliation gap.

## Acceptance criteria (Given / When / Then)

1. **Given** I confirm a membership subscription (phase-2, `PaymentMethodConfirmed = true`) **and** Stripe throws a transient `StripeException` on `CreateSubscriptionAsync`, **When** the handler runs, **Then** it returns `BusinessResult.Failure` with a payment-gateway-unavailable business error (HTTP 4xx business failure, not a 500) and no `UserMembership` row is added.

2. **Given** Stripe `CreateSubscriptionAsync` **succeeds** but the subsequent commit fails, **When** I retry the subscribe action, **Then** an idempotency guard keyed on user + plan detects the already-created subscription (or the existing local/Stripe state) and the retry does **not** create a second Stripe subscription — I end with exactly one active subscription and exactly one local `UserMembership` row.

3. **Given** a Stripe outage during the web checkout flow, **When** I call `CreateMembershipCheckoutSession` (`CreateCustomerAsync` or `CreateMembershipCheckoutSessionAsync` throws `StripeException`), **Then** I receive the same payment-gateway-unavailable business failure rather than an unhandled 500.

4. **Given** I have an active membership and Stripe throws on `SwapSubscriptionPriceAsync` (swap) or `CancelSubscriptionAtPeriodEndAsync` (cancel), **When** the handler runs, **Then** the local `UserMembership` is left unchanged (no `ApplyPlanSwap` / `MarkCancellationRequested`) and I receive the payment-gateway-unavailable business failure.

5. **Given** a non-Stripe exception inside any of these handlers (DI misconfig, null reference, bad state), **When** it is thrown, **Then** it still bubbles as a 500 — the new catch is narrow to `StripeException` only and does **not** mask programming errors as "gateway down" (mirroring the comment at `CreateOrder.cs:364-367`).

6. **Given** the new failure is returned, **When** the customer UI maps the error key, **Then** a corresponding `errors.*` translation exists in all five locales (`en, cs, sk, uk, ru`) for the gateway-unavailable key surfaced by the membership flows.

## Out of scope

- **Refactoring `CreateOrder`'s** existing Stripe handling or adding its (separately-tracked under F8) order-side idempotency guard — this story is membership-only; the order half stays as-is.
- **The webhook reconciliation path** (`StripeSubscriptionWebhookHandler` / `ProvisionFromCreatedEventAsync`) — do not change webhook auto-provisioning logic. The idempotency guard here is the synchronous-retry guard in the command, not new webhook behavior.
- **Retry/backoff or circuit-breaker policies** against Stripe (no Polly, no automatic re-call) — we surface a retryable error to the client; the client re-invokes.
- **Changing the `IStripeClient` abstraction signatures** unless an idempotency-key parameter genuinely needs adding; the subscribe/swap/checkout calls already accept an `idempotencyAttemptId`, so prefer reusing/persisting a stable id over expanding the interface.
- **Any DB migration / schema change** beyond what a user+plan idempotency read requires; if a new persisted idempotency key is chosen over reusing existing state, flag it as a `MANUAL_STEP: ef-migration` for the owner rather than running migrations.
- **NSwag client regeneration** — if a new `membership.*` error key or DTO shape is added, flag `manual_step: nswag-regen`; do not regenerate clients.

## Notes for the implementer / PM

- Decide whether to introduce a `membership.payment_gateway_unavailable` key or reuse the existing order-namespaced `PaymentGatewayUnavailable`. The cleaner choice is a membership-namespaced key (the existing one lives under `// Order`), but either way AC-6 requires the five-locale translation.
- The idempotency guard for AC-2 can be grounded in existing state: before calling `CreateSubscriptionAsync`, the handler already checks `GetActiveForUserAsync` (line 57); the gap is that a Stripe-succeeded-but-commit-failed state leaves **no** local row, so re-entering re-charges. A guard keyed on (userId, planId) — e.g. a persisted in-flight attempt id reused across retries instead of `Guid.NewGuid()` at line 84, or a Stripe-side lookup of existing active subscriptions for the customer — is the architect-owned design decision.
- **Extend tracked F8** (`consistency-violations.md:66`) to enumerate all four membership commands, not just `CreateMembershipSubscription`.


---

I have all the grounding I need. The code confirms: `GetNextSequenceForYearAsync` (year-scoped sequence consumed at ReceiptService.cs:49), `receiptRepository.Add` is in-memory only (line 56), blob upload at line 93 — all before the single `CommitAsync` at GenerateReceiptFunction.cs:99. The `order.Receipt is not null` guard (line 66) only protects committed runs. `OrderReceipt` already exposes `OrderId`, `EmailSent`, `MarkEmailSent`, and the repo already has `GetByOrderIdAsync`/`GetByOrderIdAndLanguageAsync`. S7 names `LoyaltyService.GrantForCompletedOrderAsync` and `ReferralService.ProcessQualifyingOrderAsync` as the reference ledger-check patterns.

---

# US-admin-0042 — Make receipt generation idempotent so a retry never burns a fiscal sequence number or double-emails

> **Type:** Bug / Compliance hardening · **Security rule:** S7 (Idempotency on side-effecting commands) · **Severity:** High (legal fiscal-numbering integrity + duplicate customer email)

## Persona note
Filed under the **admin** persona because the harmed party is the operator of record: it is the *business* that must produce a gapless, non-duplicated, legally-sequenced register of receipts to the tax authority (CZ EET, SK eKasa, DE TSE, etc.), and the *business* that is exposed when a customer receives two receipts for one order. The trigger is an internal queue retry, not a customer or partner action.

## Actor narrative

**As an** admin / operator of record responsible for a legally-sequenced fiscal receipt register,
**I want** receipt generation for an order to be idempotent across uncommitted queue retries — consuming at most one fiscal sequence number and sending at most one customer email per order —
**so that** a transient failure (email send, fiscal authority, or the commit itself) cannot create a gap or a duplicate in the legal receipt numbering, nor send the customer a second receipt.

## Grounding (current behavior — read from code)

- `GenerateReceiptFunction.Run` has a single `CommitAsync` at the very end (`GenerateReceiptFunction.cs:99`), and re-throws on any exception to force a queue retry (`:107`).
- Before that commit, `ReceiptService.GenerateReceiptAsync` (`ReceiptService.cs:27-96`) performs three side-effecting / state-consuming steps with **no** prior commit:
  1. `GetNextSequenceForYearAsync(currentYear, …)` — consumes the next year-scoped fiscal sequence number (`ReceiptService.cs:49`).
  2. `receiptRepository.Add(receipt)` — **in-memory only**, not persisted until the function commits (`ReceiptService.cs:56`).
  3. `blobClient.UploadAsync(blobName, …)` — uploads the PDF to blob storage (`ReceiptService.cs:93`).
- The dedup guard `if (order.Receipt is not null)` (`GenerateReceiptFunction.cs:66`) reads committed state, so it only protects across *committed* runs. If `SendOrderReceiptEmailAsync` (`:95`) or `CommitAsync` (`:99`) throws, nothing is committed, the queue message is redelivered, the guard sees no receipt, and a **brand-new receipt with a new sequence number** is generated — yielding a gap/duplicate in the legal sequence and potentially a **second customer email**.
- The domain entity already supports an idempotent design: `OrderReceipt` exposes `OrderId`, `EmailSent`, and `MarkEmailSent(messageId)` (`OrderReceipt.cs:15,29,103`), and `IOrderReceiptRepository` already offers `GetByOrderIdAsync` / `GetByOrderIdAndLanguageAsync` (`IOrderReceiptRepository.cs:7-14`).
- Reference idempotency patterns named by S7 and present in the codebase: `LoyaltyService.GrantForCompletedOrderAsync` checks the loyalty ledger before granting (`LoyaltyService.cs:52-60`); `ReferralService.ProcessQualifyingOrderAsync` checks `Referral.Status`.

## Acceptance criteria (Given / When / Then)

1. **No second sequence number on uncommitted retry**
   **Given** an order whose first receipt-generation attempt reserved sequence number `N` and uploaded its PDF, but then failed before commit (so nothing is persisted),
   **When** the `generate-receipt` queue message is redelivered and reprocessed,
   **Then** the reprocess reuses the same receipt identity for that order and does **not** call `GetNextSequenceForYearAsync` a second time, so the fiscal register still shows exactly one receipt number for that order with no gap and no duplicate.

2. **At most one customer email per order**
   **Given** a receipt whose `EmailSent` is already `true`,
   **When** the same `generate-receipt` message is reprocessed for any reason (retry, manual re-trigger, double delivery),
   **Then** `SendOrderReceiptEmailAsync` is **not** invoked again and no second receipt email is delivered to the customer.

3. **Sequence reserved exactly once, committed before email**
   **Given** an order with no existing receipt,
   **When** receipt generation runs to completion,
   **Then** the sequence number is allocated exactly once and the receipt row is durably committed **before** the customer email is attempted, such that an email-send failure leaves the persisted receipt with `EmailSent = false` and the same `ReceiptNumber` available for a safe redelivery.

4. **Redelivery after email failure re-sends without re-numbering**
   **Given** a committed receipt for an order with `EmailSent = false` (email send previously threw),
   **When** the message is redelivered,
   **Then** the existing receipt (same `ReceiptNumber`, same `BlobName`) is looked up by `OrderId`, the email is sent, and `MarkEmailSent` is called — with no new sequence number consumed and no new blob name generated.

5. **Blocking-fiscal hold path stays idempotent**
   **Given** a BlockingOnline / BlockingWithOfflineCache country where the initial fiscal attempt did not yield a `FiscalCode` and the function takes the "hold the email" branch (`GenerateReceiptFunction.cs:83-90`),
   **When** the held receipt is later reprocessed or released by the retry job,
   **Then** it reuses the already-reserved receipt number and blob, and the customer email is sent at most once across the hold-then-release sequence.

6. **Existing committed-receipt fast path is preserved**
   **Given** an order that already has a fully committed receipt with `EmailSent = true`,
   **When** a `generate-receipt` message arrives,
   **Then** the function still short-circuits (as the current `order.Receipt is not null` guard does) without allocating a sequence, uploading a blob, or sending an email.

## Out of scope (explicit)

- The fiscal **retry job** logic (`RetryFiscalRegistrationAsync`, `ReceiptService.cs:200-282`) and its backoff schedule — except where AC 5 requires the held/released path to remain idempotent. No change to retry counts, backoff, or admin acknowledgement.
- Changing the **fiscal numbering scheme**, the `ReceiptNumberFormat.Pattern`, or making sequence allocation per-tenant/per-country (it is currently year-scoped) — only ensure it is consumed *once* per order.
- Concurrency/locking hardening for two **distinct** orders racing on the same sequence (DB-level uniqueness / sequence design) — this story targets the single-order, single-message retry duplication, not cross-order race conditions.
- Adding a generic queue-level dedup / idempotency-key infrastructure for all functions — scope is `GenerateReceiptFunction` + `ReceiptService.GenerateReceiptAsync` only.
- Invoice generation, payout/settlement records, and Stripe charge idempotency (separate S7 findings).
- EF migration authoring/application and NSwag client regeneration (owner-only `MANUAL_STEP`s; flag if a unique index on `OrderReceipt.OrderId`/`(OrderId, LanguageId)` is added to enforce idempotency at the DB level).
- Admin UI surfacing of duplicate/held receipts.

## Layers touched

- **Azure Functions** — `Cleansia.Functions/Functions/GenerateReceiptFunction.cs`: reorder so the receipt row is reserved/looked-up-by-`OrderId` and committed *before* the email step; guard the email with `receipt.EmailSent`; keep the early-exit fast path.
- **AppServices / domain service** — `Cleansia.Core.AppServices/Services/ReceiptService.cs` (`GenerateReceiptAsync`): split sequence-allocation + persistence from email; look up an existing receipt by `OrderId` before allocating a new sequence; mirror the ledger-check shape of `LoyaltyService` / `ReferralService`.
- **Domain** — `Cleansia.Core.Domain/Receipts/OrderReceipt.cs` and `IOrderReceiptRepository`: reuse existing `OrderId`, `EmailSent`, `MarkEmailSent`, `GetByOrderIdAsync`/`GetByOrderIdAndLanguageAsync` (no new fields expected; flag if one is needed).
- **Infra / DB (MANUAL_STEP, owner-only)** — optional unique constraint on `OrderReceipt(OrderId)` or `(OrderId, LanguageId)` to enforce single-receipt-per-order at the database boundary; requires an `ef-migration` manual step.
- **Tests** — `Cleansia.Tests`: retry-after-email-failure and retry-after-commit-failure scenarios asserting exactly one sequence consumed and at most one email sent.


---

`IHostAudienceProvider` confirms there's already a per-host resolution precedent (host-bound audience injected at startup) that a host/subdomain -> tenant resolver would mirror. I have grounded every claim. Writing the user story.

---

# US-admin-0042 — Resolve tenant on anonymous auth endpoints without bypassing the global filter

> Persona note: cited as `admin` because the affected actor is the platform/tenant operator whose multi-tenant deployment must work; the *symptom* is felt by every customer/partner end-user of a tenanted deployment, but the *value owner* and the configuration surface (per-host/subdomain tenant mapping) is the admin/operator. The story touches all three personas' login surfaces.

## Actor narrative

**As a** platform operator running Cleansia in multi-tenant mode (tenants whose users carry a non-null `TenantId`),
**I want** the anonymous authentication endpoints — Login, ConfirmUserEmail, ResendConfirmationEmail, and password reset — to resolve the correct tenant from the request's host/subdomain *before* the user lookup, and to scope that lookup to exactly that tenant via the existing global query filter,
**so that** tenanted users can actually log in, confirm their email, and reset their password, while a leaked or guessed confirmation/reset code can never match a user in a *different* tenant (no cross-tenant account-takeover primitive).

## Why this is broken today (grounded)

- `User : Auditable, ITenantEntity` — `src/Cleansia.Core.Domain/Users/User.cs:11`. So every read through the DbSet inherits the global tenant filter.
- The global filter — `src/Cleansia.Infra.Database/CleansiaDbContext.cs:111-179` — evaluates to `tenantProvider == null || (currentTenantId == null && e.TenantId == null) || e.TenantId == currentTenantId`. On an `[AllowAnonymous]` route there is no `tenant_id` claim, so `GetCurrentTenantId()` returns `null` (`src/Cleansia.Infra.Database/TenantProvider.cs:12-20`) and only the **single-tenant clause** (`e.TenantId == null`) can match.
- The anonymous consumers all read through the filtered `GetDbSet()`:
  - `Login.Handler` → `GetByEmailAsync` — `src/Cleansia.Core.AppServices/Features/Auth/Login.cs:80` (and the validator at lines 40, 55, 61).
  - `ConfirmUserEmail.Handler` → `GetByConfirmationCodeAsync` — `src/Cleansia.Core.AppServices/Features/Auth/ConfirmUserEmail.cs:65` (validator line 37).
  - `ResendConfirmationEmail` and password-reset request/confirm follow the same `GetByEmailAsync`/code pattern in `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:18-48`.
- Endpoints are confirmed `[AllowAnonymous]` — e.g. `src/Cleansia.Web.Customer/Controllers/AuthController.cs:34-73` (Login, ConfirmUserEmail, ResendConfirmationEmail), mirrored in the Partner/Admin/Mobile auth controllers.
- **Net effect:** for any user with a non-null `TenantId`, these four flows return "no user found" and silently fail. Single-tenant deployments (all `TenantId == null`) are unaffected, which is why this has not surfaced yet.
- **The tempting wrong fix is itself the security risk.** `agents/knowledge/security-rules.md:54-56` (S3): anonymous routes have no tenant claim so the global filter is bypassed and "must not return tenant-scoped data unless gated by a different shared secret (e.g. a confirmation code in the URL)." A blanket `IgnoreQueryFilters()` (already used narrowly and deliberately in `UserRepository.GetByIdIgnoringTenantAsync`, line 69-74) would make `GetByConfirmationCodeAsync` match a code across **all** tenants, converting a weak per-tenant code into a cross-tenant takeover — a direct S8 violation (`security-rules.md:93-101`).
- A host→value resolution precedent already exists: `IHostAudienceProvider` (`src/Cleansia.Core.AppServices/Authentication/IHostAudienceProvider.cs`) is injected per Web host at startup. A host/subdomain→tenant resolver should mirror that shape and feed `ITenantProvider.SetTenantOverride(...)`.

## Acceptance criteria

1. **Tenanted login works without bypassing the filter.**
   **Given** a multi-tenant deployment where user `u` has `TenantId = "T1"` and the request arrives on the host/subdomain mapped to `T1`,
   **When** an anonymous client calls `POST /Auth/Login` with `u`'s valid credentials,
   **Then** the tenant is resolved to `T1` and applied via `ITenantProvider.SetTenantOverride` *before* the lookup, the user is found through the normal (non-ignored) global filter, and a JWT is returned — with no occurrence of `IgnoreQueryFilters()` on the auth read path.

2. **Confirm-by-code is tenant-scoped, not global.**
   **Given** users in tenants `T1` and `T2` that happen to hold the same `ConfirmationCode` value (codes are unique per tenant, not globally — S8),
   **When** an anonymous client submits that code to `ConfirmUserEmail` on the host mapped to `T1`,
   **Then** only `T1`'s user is matched and confirmed, and the same request against the `T2` host can never resolve `T1`'s user (no cross-tenant match).

3. **Password reset and resend-confirmation behave consistently.**
   **Given** a tenanted user on their tenant's host,
   **When** they request a password-reset email / resend-confirmation, then complete reset with the emailed code/token,
   **Then** the user is located and updated within their own tenant only, and the flow succeeds end-to-end.

4. **Unresolvable / mismatched host fails closed.**
   **Given** an anonymous auth request whose host/subdomain maps to no known tenant (and the deployment is in multi-tenant mode),
   **When** any of the four flows runs,
   **Then** the request is rejected with a generic, non-enumerating auth failure (consistent with the existing `InvalidPassword`/`InvalidConfirmationCode` responses) and does **not** silently fall back to the single-tenant (`TenantId == null`) clause.

5. **Single-tenant mode is preserved and explicitly documented.**
   **Given** a single-tenant deployment where all users have `TenantId == null` and no host→tenant mapping is configured,
   **When** the four anonymous flows run,
   **Then** behavior is unchanged (the existing single-tenant clause at `CleansiaDbContext.cs:154-156` still matches), and the supported-modes decision is captured in code/docs so a future multi-tenant rollout doesn't silently ship broken auth.

6. **Regression guard exists.**
   **Given** the test suite,
   **When** it runs,
   **Then** there is a guard test proving (a) a tenanted user is found by Login/Confirm/Reset only when the resolved tenant matches, and (b) no auth-path repository method newly introduces `IgnoreQueryFilters()` on `GetByEmailAsync`/`GetByConfirmationCodeAsync` (so the cross-tenant leak cannot be reintroduced as a "fix").

## Out of scope

- Building or designing the actual tenant-onboarding / host-and-subdomain *registration* admin UI (mapping rows are assumed to exist; this story only *consumes* a host→tenant mapping).
- Custom-domain / wildcard-TLS / DNS provisioning for tenant subdomains.
- Migrating any existing single-tenant deployment's data to non-null `TenantId` (data backfill is a separate, owner-driven `ef-migration` concern — S9).
- `GoogleAuth`, `RefreshToken`, and `RegisterEmployee` flows except where they share the same lookup helper (covered only insofar as the shared `GetByEmailAsync`/code reads change; no new behavior for OAuth/refresh).
- Rate-limiting changes (S5 already covers these endpoints via the `"auth"` window) — not modified here.
- Changing the global filter expression itself in `CleansiaDbContext.cs` — the fix sets a tenant *override*, it does not alter or bypass the filter.
- Frontend/mobile UI changes — these clients already post to the same anonymous endpoints; no client contract change is expected (flag `nswag-regen` only if a request DTO actually changes, which it should not).

## Layers touched

- **Backend — host/middleware (Web):** a host/subdomain→tenant resolver, registered per Web host like `IHostAudienceProvider`, that calls `ITenantProvider.SetTenantOverride` early in the anonymous auth pipeline. Files: `src/Cleansia.Web.Customer`, `Cleansia.Web.Partner`, `Cleansia.Web.Admin`, `Cleansia.Web.Mobile.*` auth controllers/startup; `src/Cleansia.Infra.Database/TenantProvider.cs`.
- **Backend — AppServices (Auth):** `Features/Auth/Login.cs`, `ConfirmUserEmail.cs`, `ResendConfirmationEmail.cs`, password-reset request/confirm handlers + validators (must rely on the resolved tenant; must **not** add `IgnoreQueryFilters`).
- **Backend — Infra.Database:** `Repositories/UserRepository.cs` (the `GetByEmailAsync`/`GetByConfirmationCodeAsync` consumers; no filter-ignoring additions). The filter in `CleansiaDbContext.cs:111-179` is read/relied-on, not modified.
- **Tests:** `src/Cleansia.Tests` (and/or `Cleansia.IntegrationTests`) for the AC-6 guard tests.
- **Docs:** `docs/architecture` + `agents/knowledge/security-rules.md` (S3/S8) — record the single-vs-multi-tenant auth resolution decision.
- **Manual steps (owner-only):** none expected for schema/clients; if a host→tenant mapping store is added, flag `ef-migration`. No `nswag-regen` unless a request DTO changes (it should not).


---

I have everything grounded. Key findings confirmed in code:

- Five `[AllowAnonymous]` overview endpoints all do `Mediator.Send(...)` straight to a handler that hits Postgres every call (`ServiceController.cs:14-21`, `PackageController.cs:14-21`, `CountryController.cs:14-34` — note it also exposes `GetServiced` → `GetServicedCountries`, `LanguageController.cs:14-21`).
- Handlers are pure DB reads with no cache (`GetServiceOverview.cs:20-23`, `GetCountryOverview.cs:18-22`, `GetServicedCountries.cs:21`).
- `AppConfigurationProvider.cs:26-56` runs up to 3 `FeatureFlags` queries per `IsFeatureEnabledAsync` call; `CheckFeatureFlag` is `[AllowAnonymous]` and rate-limited (`FeatureFlagController.cs:14-22`).
- Frontend NgRx effects call `getOverview()` with no cache layer (`catalog.effects.ts:16,28`); customer app is SSR so this also runs server-side per render.
- Zero caching primitive anywhere in `src` (empty grep for `AddOutputCache|IMemoryCache|HybridCache|IDistributedCache`); the only "cache" convention is mobile `SessionScopedCache`.
- Tenant key source exists: `TenantProvider.GetCurrentTenantId()` from the `tenant_id` claim; flags/data are tenant-scoped.
- `conventions.md` explicitly states a genuinely new cross-cutting abstraction is an Architect/ADR decision, and the `UnitOfWorkPipelineBehavior` (commit-on-`Command`) is the natural seam for write-side invalidation.

Here is the user story.

---

# US-admin-0042 — Cache near-static catalog reads (services, packages, countries, currencies, languages, feature flags)

**Type:** Technical enabler / performance · **Theme:** Cost & scalability
**Persona note:** No end-user-visible behavior changes; the beneficiary is the platform operator (admin/owner) who carries the infrastructure cost and the SLA. Written from the operator's perspective per the persona convention.

> Requires an Architect decision + ADR before implementation. Per `agents/knowledge/conventions.md` ("If a genuinely new abstraction is needed, that's an Architect decision (an ADR), not an ad-hoc invention"), a server-side caching layer is a new cross-cutting abstraction. This story is the trigger for that ADR, not a license to add caching ad hoc inside a feature.

## Actor narrative

**As** the Cleansia platform operator (admin/owner),
**I want** the near-static catalog reads that the customer booking wizard fires on every render — service overview, package overview, serviced/overview countries, currency overview, language overview, and feature-flag checks — to be served from a short-TTL, tenant-scoped cache instead of hitting PostgreSQL on every request,
**so that** the single highest-volume, lowest-change read path stops generating one (or several) DB round-trips per page view and per SSR render, cutting recurring database/CPU cost at scale while keeping the catalog correct within seconds of an admin edit.

## Context grounded in code

- The overview endpoints are anonymous and go straight to the DB on every call: `Cleansia.Web.Customer/Controllers/ServiceController.cs:14-21`, `PackageController.cs:14-21`, `CountryController.cs:14-34` (both `GetOverview` and `GetServiced`), `LanguageController.cs:14-21`, plus the Currency overview controller.
- Handlers are unconditional Postgres reads with no memoization: `GetServiceOverview.cs:20-23`, `GetCountryOverview.cs:18-22`, `GetServicedCountries.cs:21`.
- `IsFeatureEnabledAsync` issues up to three `FeatureFlags` queries per call (`AppConfigurationProvider.cs:26-56`); exposed anonymously via `FeatureFlagController.cs:14-22`.
- The customer SSR app re-fetches these via NgRx effects with no client cache: `catalog.effects.ts:16,28` (`serviceClient.getOverview()` / `packageClient.getOverview()`).
- Confirmed zero caching primitive exists in `src` (no `AddOutputCache` / `IMemoryCache` / `HybridCache` / `IDistributedCache` match). The only existing "cache" convention is mobile-side `SessionScopedCache` (`consistency.md:134`) — not applicable server-side.
- A tenant key already exists: `TenantProvider.GetCurrentTenantId()` reading the `tenant_id` claim (`TenantProvider.cs:12-20`). Catalog data and feature flags are tenant-scoped, so the cache key **must** include tenant id (and country/scope for flags) to avoid cross-tenant leakage.
- The write-side invalidation seam already exists: `UnitOfWorkPipelineBehavior` commits on request types ending in `Command` (`conventions.md:93-95`), giving a single, consistent hook point for evicting cache entries on create/update/delete of the cached entities.

## Acceptance criteria

1. **Cache hit avoids the DB**
   **Given** the service/package/country/currency/language overview (and a feature-flag check) for a tenant has been read once and is within its TTL,
   **When** the same read is requested again for the same tenant (and same scope, for flags),
   **Then** the response is served from cache and **no** PostgreSQL query is executed for that read (verifiable via query logging / counter in an integration test), and the payload is byte-equivalent to the uncached response.

2. **Writes invalidate within the TTL window**
   **Given** a cached overview for a tenant,
   **When** an admin creates, updates, or deletes the corresponding entity (e.g. `UpdateService`, `CreateCountry`, `SetCountryServiced`, `ToggleFeatureFlag`) and the command commits,
   **Then** the next read for that tenant returns the new data **no later than** the configured TTL — and, if active invalidation is in scope, immediately after the commit — without requiring an app restart.

3. **Tenant and scope isolation (no cross-tenant leakage)**
   **Given** tenant A and tenant B have different catalog/feature-flag data,
   **When** the cached read is requested for each,
   **Then** each tenant receives only its own data; a cache entry populated for tenant A is never served to tenant B, and feature-flag entries are keyed so a `tenant` / `country` / `global` scope value is never returned for the wrong scope.

4. **TTL is configuration-driven, not hardcoded**
   **Given** the caching layer is enabled,
   **When** an operator sets the TTL (e.g. 60–300s) via configuration,
   **Then** the value comes from a named configuration home (no magic number inline, per `conventions.md` "no magic numbers"), and a TTL of `0`/disabled cleanly falls back to direct DB reads with identical responses.

5. **Correctness of filtered/anonymous reads is preserved**
   **Given** the customer-facing reads only expose active/serviced rows (`GetServiceOverview` filters `IsActive`; `GetServicedCountries` filters `IsServiced && IsActive`),
   **When** the read is served from cache,
   **Then** the cached payload reflects exactly the same filtering as the uncached handler — a deactivated service or un-serviced country never appears in a cached customer response.

6. **Observability**
   **Given** the cache is in operation,
   **When** reads occur over a period,
   **Then** cache hit/miss is observable (metric or log) so the operator can confirm the cost reduction and detect a degraded hit rate.

## Out of scope

- **HTTP-level / CDN / browser response caching** (e.g. `Cache-Control`, ETag, reverse-proxy, output caching at the edge) — this story is the application-tier data cache only; edge caching is a separate ticket.
- **Frontend / SSR client-side caching** of `getOverview()` in `catalog.effects.ts` or an SSR `TransferState` cache — separate frontend story; this story does not modify the NgRx effects.
- **Distributed cache infrastructure provisioning** (Redis cluster sizing, networking, failover). The ADR may *choose* `IDistributedCache`/`HybridCache` with a Redis backend, but standing up and operating Redis is an infra/deployment ticket.
- **Caching paged/admin list queries, order data, pay/payroll reads, or any per-user/mutable data.** Strictly the named near-static catalog + feature-flag reads.
- **Changing the auth posture** of the overview endpoints (they remain `[AllowAnonymous]`), or altering DTO shapes / NSwag contracts.
- **EF Core second-level query caching** as a global strategy — explicitly out; this is a targeted read-path cache.
- **The Architect ADR document itself** (cache primitive choice — `HybridCache` vs `IMemoryCache` vs `IDistributedCache` — single vs multi-instance topology, and active-eviction vs TTL-only invalidation) is a *prerequisite decision*, tracked separately; this story is blocked on it.

## Layers touched

- **Backend — App composition / DI** (`Cleansia.Config` / per-API `Program.cs`): register the chosen cache primitive (Architect's call) — the new cross-cutting abstraction.
- **Backend — read handlers** (`Cleansia.Core.AppServices/Features/{Services,Packages,Countries,Currencies,Languages,FeatureFlags}`): `GetServiceOverview`, `GetPackageOverview`, `GetCountryOverview`, `GetServicedCountries`, `GetCurrencyOverview`, `GetLanguageOverview`, `CheckFeatureFlag` → read-through cache, tenant-/scope-keyed.
- **Backend — config provider** (`Cleansia.Infra.Database/AppConfigurationProvider.cs`): cache `IsFeatureEnabledAsync` lookups.
- **Backend — invalidation seam** (`Cleansia.Core.AppServices/Behaviors/UnitOfWorkPipelineBehavior.cs` or a paired post-commit hook): evict on the matching create/update/delete/toggle commands.
- **Backend — tenant keying** (`Cleansia.Infra.Database/TenantProvider.cs`): source of the tenant component of the cache key (read-only use).
- **Config**: TTL + enable/disable flag in `appsettings`/options.
- **Tests** (`Cleansia.Tests` / `Cleansia.IntegrationTests`): hit-avoids-DB, invalidation-on-write, tenant isolation, filter-correctness, TTL fallback.
- **NOT touched:** frontend, mobile, database schema (no migration), NSwag contracts.

## Flags / manual steps

- `manual_step: architect-adr` — cache primitive + topology + invalidation-strategy decision must land first; this story is **blocked on** that ADR.
- No `ef-migration` and no `nswag-regen` expected (no schema or contract change). If the chosen design adds a config table or alters a DTO, re-flag accordingly.


---

I have everything I need. The code confirms: `DeleteDocument`, `UploadEmployeeDocument`, and `UploadNewDocumentVersion` exist as MediatR handlers but are referenced by no controller; `AdminEmployeeDocumentController` exposes only get-paged/approve/reject/versions/download; `DeleteDocument.Validator` has no ownership/status guard (unlike `DeleteMyDocument`); `CanDeleteEmployeeDocument` already exists in `Policy.cs` (currently only wired to the partner self-delete); and the file-bytes pipeline pattern lives in `SaveMyDocuments` (blob upload), whereas `UploadEmployeeDocument` takes a pre-resolved `FilePath` with no blob upload.

Here is the user story.

---

# US-admin-0042 — Admin manages cleaner (employee) documents

## Context / grounding (read before estimating)

Three MediatR handlers exist but are wired to **no controller** — they are dead code today:
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/UploadEmployeeDocument.cs`
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/UploadNewDocumentVersion.cs`
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/DeleteDocument.cs`

`AdminEmployeeDocumentController` (`src/Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs`) currently exposes only `get-paged`, `{id}/approve`, `{id}/reject`, `{id}/versions`, `{id}/download`. There is **no admin path to delete a document**. The only delete route in the system is the partner self-service `DeleteMyDocument` (`Cleansia.Web.Partner` + `Cleansia.Web.Mobile.Partner` `EmployeeController`), and its validator (`DeleteMyDocument.cs:46`) refuses to delete an `Approved` document: *"Cannot delete an approved document. Contact admin for assistance."* — yet that admin assistance is unreachable. Net product gap: a cleaner can upload a fraudulent/illegal/expired document, get it approved, and **no one can remove it**.

Two grounded risks the implementation must address:
1. `DeleteDocument.Validator` (`DeleteDocument.cs:22-35`) checks only existence — it has **no ownership and no status guard** (contrast `DeleteMyDocument`, which guards both). That is acceptable for a true admin actor but means this command must **never** be exposed on a partner/mobile-partner host. Per `security-rules.md` S2/S3, the gate must be the policy, and ownership/role logic must hold regardless of host.
2. `UploadEmployeeDocument.cs` takes a pre-resolved `FilePath` (lines 19, 42-43) and performs **no blob upload** — unlike `SaveMyDocuments`, which is the real file pipeline (base64 → `IBlobContainerClient.UploadAsync` → versioned `EmployeeDocument`, `SaveMyDocuments.cs:108-173`). Wiring `UploadEmployeeDocument` as-is would expose a raw server file path to the client and bypass blob storage. Admin upload-on-behalf must reuse the `SaveMyDocuments`-style base64/blob pipeline, not the bare `FilePath` contract.

`Policy.cs:72` already defines `CanDeleteEmployeeDocument` (commented *"Admin + Employee (own documents)"*) and `CanUploadEmployeeDocument` (`Policy.cs:68`). Decide whether to reuse these or introduce a dedicated admin-only `CanManageEmployeeDocument` policy (see open question).

## Actor narrative

**As an** admin (compliance/operations) reviewing a cleaner's documents,
**I want** to upload a document on a cleaner's behalf, upload a corrected new version, and delete (soft-delete) any document — including an already-approved one —
**so that** I can correct missing paperwork and remove fraudulent, illegal, expired, or mistakenly-approved documents that the cleaner themselves is blocked from removing.

## Acceptance criteria (Given / When / Then)

1. **Admin delete reaches an approved document**
   Given an `EmployeeDocument` in status `Approved`, and an admin holding the delete policy,
   When the admin issues delete for that document id via `AdminEmployeeDocumentController`,
   Then the document is **soft-deleted** (`SoftDelete(adminUserId)` — the existing `DeleteDocument` handler behaviour), it no longer appears in `get-paged` results, and the response is `200` with the deleted document id.

2. **Delete is admin-gated and host-isolated**
   Given the partner web host and the mobile-partner host,
   When their route tables are inspected,
   Then **no** route maps to `DeleteDocument` on either partner host; the admin delete route carries a single `[Permission(...)]` admin policy (`CanDeleteEmployeeDocument` or new `CanManageEmployeeDocument`), and a caller without that policy receives `403` (per `security-rules.md` S2).

3. **Admin upload-on-behalf uses the blob pipeline**
   Given an admin and a valid employee id with an allowed file (pdf/jpeg/png/doc/docx, ≤ 10 MB),
   When the admin uploads a document on that employee's behalf,
   Then the file is stored via the blob pipeline (as in `SaveMyDocuments`, not a client-supplied server `FilePath`), a new `EmployeeDocument` V1 is created against that employee with `CreatedBy = adminUserId`, and the response returns the new document id and metadata.

4. **Admin new-version upload preserves the version chain**
   Given an existing document for an employee and an admin uploading a replacement file for it,
   When the admin submits the new version,
   Then `EmployeeDocument.CreateNewVersion(...)` is invoked, version number increments, `DocumentType` is inherited from the previous version (per `UploadNewDocumentVersion.cs:108`), the prior version remains retrievable via `{id}/versions`, and the new version is the latest.

5. **Invalid file is rejected with a translated error**
   Given an admin upload whose content-type is not in the allow-list or whose size exceeds 10 MB,
   When the upload is submitted,
   Then the request fails validation with `FileTypeNotAllowed` / `FileSizeExceeded10MB` (existing `BusinessErrorMessage` keys), returns `400`, and no `EmployeeDocument` row is created.

6. **Non-existent employee/document is handled cleanly**
   Given an admin targeting an unknown employee id (upload) or unknown document id (delete / new version),
   When the request is submitted,
   Then it returns `NotFound` via the existing validators (`DeleteDocument` `MustAsync(ExistsAsync)`, `UploadEmployeeDocument` employee lookup) and no partial write occurs.

7. **Admin actions appear in the audit/version trail**
   Given the admin has uploaded, re-versioned, or deleted a document,
   When the document's version history (`{id}/versions`) or audit fields are inspected,
   Then the admin's user id is recorded as `CreatedBy` (uploads/versions) or the soft-delete actor (delete), distinguishing admin-originated changes from cleaner-originated ones.

## Out of scope

- New admin UI screens beyond a delete control and an upload/upload-version control on the existing employee-documents view (full document-management redesign is separate).
- Changing the **partner** self-service rules (e.g. allowing cleaners to delete approved documents) — `DeleteMyDocument` behaviour stays as-is.
- Hard delete / physical blob deletion / retention or purge policy — this story is soft-delete only, consistent with the current handler.
- Approve/reject flow changes — those endpoints already exist and are unchanged.
- Notifying the cleaner (email/push) when an admin deletes or replaces their document.
- Virus/malware scanning of uploaded files and any content-type sniffing beyond the existing allow-list.
- Multi-tenant scoping changes — relies on existing global query filter / `ITenantEntity` behaviour (S8); no new isolation logic.
- Deleting the dead `UploadEmployeeDocument` / `UploadNewDocumentVersion` / `DeleteDocument` handlers as the alternative resolution — this story chooses to wire them; the "delete dead code" path is the explicit fallback if product decides admin document management is not in scope.

## Layers touched

- **Backend — API host:** `Cleansia.Web.Admin` — new routes on `AdminEmployeeDocumentController` (delete, upload-on-behalf, upload-new-version) with admin `[Permission(...)]`. Explicitly **not** `Cleansia.Web.Partner` / `Cleansia.Web.Mobile.Partner`.
- **Backend — AppServices:** wire/adjust `DeleteDocument`, `UploadEmployeeDocument`, `UploadNewDocumentVersion`; rework the upload commands to a base64/blob contract reusing the `SaveMyDocuments` pipeline (`IBlobContainerClient`). Possible new `Policy.CanManageEmployeeDocument` and `PolicyBuilder` registration (`src/Cleansia.Core.AppServices/Authentication/Policy.cs`, `PolicyBuilder.cs`).
- **Frontend — admin app:** `cleansia-admin-features` employee-documents view — delete action + upload/upload-version controls; admin data-access store + facade; translations for new strings + any new error keys across all 5 locales (`en/cs/sk/uk/ru`).
- **Manual steps (owner-only, per CLAUDE.md):** `nswag-regen` for the admin client after the DTO/endpoint changes; no EF migration expected (no schema change — soft-delete and version columns already exist).

## Open questions (for PM/architect)

- **Policy choice:** reuse the existing admin-gated `CanDeleteEmployeeDocument` (`Policy.cs:72`) and `CanUploadEmployeeDocument` (`Policy.cs:68`), or introduce a single dedicated **admin-only** `CanManageEmployeeDocument`? The existing two are commented "Admin + Employee (own documents)"; if reused for admin-only routes, confirm the `PolicyBuilder` role mapping actually restricts the admin routes to admins.
- **Upload contract:** confirm admin upload should adopt the `SaveMyDocuments` base64 contract (recommended) and that the current `FilePath`-based `UploadEmployeeDocument`/`UploadNewDocumentVersion` command shapes can be changed (they are unreferenced, so no external consumer — safe to reshape).


---

I now have everything grounded in real code and real rules. I have confirmed:

- Controller is read-only, gated by `Policy.CanViewReferrals` (only `CanViewReferrals` exists under "Admin Referrals" in `Policy.cs:225`; no manage/mutate permission)
- Domain `Referral` exposes `MarkQualified`/`MarkExpired` but no reversal method; status enum has only Accepted/Qualified/Expired (no terminal "reversed" state)
- Only caller of those methods is `ReferralService` with `SystemActor = "system"`
- `ILoyaltyService.RevokePointsManuallyAsync` exists with exactly the right signature for the clawback
- Frontend facade only calls `getPaged`; table has 6 columns and zero row actions
- Rules: S2 (every endpoint needs a Permission), S7 (idempotency on grant/revoke), and the manual-grant idempotency reference patterns

Here is the user story.

---

```yaml
---
id: US-admin-0007
title: Intervene on referrals — reverse fraudulent qualifications and manually qualify legitimate ones
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As an **admin (fraud/operations reviewer)**, I want to **reverse a referral that qualified through abuse (clawing back the symmetric +150 points from both sides) and manually qualify a legitimate referral that is stuck in `Accepted`**, so that **I can remediate self-referral rings and refunded/disputed qualifying orders in-product, and unblock genuine customers whose qualifying order did not auto-trigger — instead of having no operational control once `ReferralService` (the only current actor) has run.**

### Grounding (current state in code)

- `src/Cleansia.Web.Admin/Controllers/AdminReferralController.cs` exposes only `get-paged` and `by-user/{userId}`, both gated by `[Permission(Policy.CanViewReferrals)]`. There is no mutating endpoint.
- `src/Cleansia.Core.Domain/Loyalty/Referral.cs` exposes `MarkQualified(...)` and `MarkExpired(...)`, but their **only** caller is `ReferralService` using `SystemActor = "system"` (`src/Cleansia.Core.AppServices/Services/ReferralService.cs:27`). There is no method to reverse a `Qualified` referral, and `ReferralStatus` (`ReferralStatus.cs`) has no terminal "Reversed" state — only `Accepted | Qualified | Expired`.
- `ILoyaltyService.RevokePointsManuallyAsync(userId, points, source, orderId, actorId, ct)` already exists (`Services/Interfaces/ILoyaltyService.cs:65`) and is the intended clawback path; `GrantPointsManuallyAsync` (line 48) is the grant path used today for the +150 (`ReferralPolicy.PointsPerSide`).
- The admin UI (`libs/cleansia-admin-features/loyalty-referrals/referrals-list.component.ts` + `.facade.ts`) renders 6 read-only columns and filters; the facade only calls `adminReferralClient.getPaged(...)`. There are **no** row actions.
- `Policy.cs:225` defines only `CanViewReferrals` under "Admin Referrals" — there is no manage/mutate permission to author these actions against (S2 requires a distinct `[Permission]` for new mutating endpoints).

## Acceptance criteria

- **AC1 — Reverse a fraudulent qualified referral** — Given a referral in `Qualified` status whose `PointsAwardedToReferrer`/`PointsAwardedToReferred` are recorded (e.g. 150/150), When an admin holding the new manage permission invokes "Reverse referral" with a required reason, Then the referral moves to a terminal reversed state, **both** the referrer's and the referred user's loyalty ledgers receive a negative entry equal to the originally recorded grant for each side (via `RevokePointsManuallyAsync`), and the action is attributed to the admin's `actorId` (not `"system"`) with the reason persisted.

- **AC2 — Reverse is idempotent (S7)** — Given a referral already in the reversed terminal state, When the reverse command is sent again (double-click, retry, or re-trigger), Then no additional negative ledger entries are written for either side and the response indicates the referral is already reversed — matching the idempotency contract in `security-rules.md` S7 and the `Referral.Status`-guard pattern in `ReferralService.ProcessOrderCompletedAsync` (`ReferralService.cs:173`).

- **AC3 — Manually qualify a stuck legitimate referral** — Given a referral in `Accepted` status within (or, per owner decision, regardless of) the 90-day window, When an admin invokes "Force qualify" with a reason, Then the referral becomes `Qualified`, both sides are granted `ReferralPolicy.PointsPerSide` (150) via `GrantPointsManuallyAsync` exactly once, `PointsAwardedToReferrer/Referred`, `PointsAwardedOn`, and `actorId` are recorded, and a referral already in `Qualified`/`Expired`/reversed state is rejected with a clear business error rather than double-granting.

- **AC4 — Authorization is a distinct permission (S2)** — Given an admin who has `CanViewReferrals` but **not** the new manage permission, When they call the reverse or force-qualify endpoint, Then the request is rejected with 403 and no points or status changes occur; the read endpoints (`get-paged`, `by-user`) remain available to view-only admins.

- **AC5 — Row actions surface only valid transitions in the UI** — Given the admin referrals table, When a row is in `Qualified`, Then a "Reverse" row action is offered (with a confirm dialog requiring a reason); When a row is in `Accepted`, Then a "Force qualify" action is offered; When a row is `Expired` or already reversed, Then no mutating action is offered. Actions are only rendered for admins holding the manage permission, and all labels/confirmations/errors use `TranslatePipe` keys present in all 5 locales (en, cs, sk, uk, ru).

- **AC6 — Outcome is observable in-list and auditable** — Given a successful reverse or force-qualify, When the table reloads, Then the row reflects the new status and updated points cells, and the new status value is selectable in the existing status filter (the filter currently maps only `accepted|qualified|expired` in `referrals-list.facade.ts`, so a new terminal state must be added to the filter mapping).

## Out of scope

- **Bulk remediation** of an entire self-referral ring in one action — this story is single-referral row actions only. (A ring-detection/bulk tool is a separate story.)
- **Automated fraud detection / scoring** (self-referral pattern heuristics, device/IP clustering). The admin decides; the system only executes the reversal/qualification.
- **Disabling or revoking the underlying `ReferralCode`** to stop future abuse — adjacent but distinct from acting on an individual `Referral` row.
- **Changing the auto-qualify pipeline** in `ReferralService.ProcessOrderCompletedAsync` (the 90-day window, the "first completed order only" rule, the `RecordUse` counter logic) — admin actions wrap these existing mechanics, they do not redefine them.
- **Email/notification to the affected customers** on reversal/qualification.
- **EF Core migration authoring** and **NSwag client regeneration** are owner-only `MANUAL_STEP`s (new `ReferralStatus` enum value + new admin endpoints/DTOs require both `ef-migration` and `nswag-regen` flags per S9); this story does not perform them.
- **A standalone referral detail/audit-history page** — surfacing the recorded `actorId`/reason beyond the existing list is a follow-up.

## Layers touched

- **Domain** (`Cleansia.Core.Domain/Loyalty/`): add a terminal reversed value to `ReferralStatus`; add `Referral` methods to reverse a qualified referral and to admin-qualify an accepted one, each taking the admin `actorId` and recording the reason (the existing `MarkQualified` only takes a `firstQualifyingOrderId` — force-qualify needs an actor/no-order path).
- **AppServices** (`Cleansia.Core.AppServices/Features/Referrals/Admin/`): new `ReverseReferral` and `ForceQualifyReferral` commands + handlers + validators (FluentValidation, `Cascade.Stop`), happy-path handlers calling `ILoyaltyService.Revoke/GrantPointsManuallyAsync` and the new domain methods, idempotency guard on status (S7), `BusinessResult<T>` returns, new `BusinessErrorMessage` keys.
- **Authentication** (`Cleansia.Core.AppServices/Authentication/`): new `Policy.CanManageReferrals` const in `Policy.cs` and its mapping in `PolicyBuilder.cs` (`AdminOnly`).
- **Web.Admin** (`AdminReferralController.cs`): two new mutating endpoints gated by `[Permission(Policy.CanManageReferrals)]`, actor id enriched from JWT (S1), rate-limit consideration (S5).
- **Frontend admin** (`libs/cleansia-admin-features/loyalty-referrals/`): row-action buttons + confirm-with-reason dialog in `referrals-list.component`, mutation methods in `referrals-list.facade.ts`, new terminal-status mapping in the status filter, and i18n keys in all 5 locale files.
- **Manual steps (owner):** `ef-migration` (new enum value persistence), `nswag-regen` (new admin endpoints/DTOs) — flagged, not executed.

### Open questions (default assumptions taken)

- **Q (90-day window on force-qualify):** Should "Force qualify" be allowed for referrals already past the 90-day window (i.e. ones the stale-expiry job would/did mark `Expired`)? **Default assumed:** yes — admin force-qualify is an override and may act on `Accepted` referrals regardless of window; acting on an already-`Expired` row is out of scope for v1 (AC3 rejects non-`Accepted`).
- **Q (clawback amount source):** Should the reversal claw back the **recorded** `PointsAwardedToReferrer/Referred` on the `Referral` row, or the current `ReferralPolicy.PointsPerSide` constant? **Default assumed:** the recorded per-side values on the row (so a historical grant reverses by exactly what was granted, even if the policy constant later changes).
- **Q (negative balance):** `RevokePointsManuallyAsync` may drive a user's tier-point balance negative if they have already spent the points. **Default assumed:** allow the negative ledger entry (the ledger is the source of truth) unless the owner specifies a floor-at-zero behavior — to be confirmed before implementation.

Files cited (absolute): `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Admin\Controllers\AdminReferralController.cs`, `...\src\Cleansia.Core.Domain\Loyalty\Referral.cs`, `...\Loyalty\ReferralStatus.cs`, `...\src\Cleansia.Core.AppServices\Services\ReferralService.cs`, `...\Services\Interfaces\ILoyaltyService.cs`, `...\Features\Orders\ReferralPolicy.cs`, `...\Authentication\Policy.cs`, `...\src\Cleansia.App\libs\cleansia-admin-features\loyalty-referrals\src\lib\referrals-list\referrals-list.component.ts`, `...\referrals-list.facade.ts`. Rules cited: `agents\knowledge\security-rules.md` S1, S2, S5, S7, S9.


---

I have everything grounded. Confirmed facts: 11 `new global::Stripe.StripeClient` in StripeClient.cs + the SendGridClientFactory line = the GAP's "12 sites" framing (Stripe + the SendGrid factory both new SDK clients without IHttpClientFactory). `new SendGridClient(...)` appears at EmailService.cs:348 and :390 (plus the factory at SendGridClientFactory.cs:15). DI uses `AddTransient`, never `AddHttpClient`. The contrast (`AddHttpClient<>().AddStandardResilienceHandler()` and `ConfigureHttpClientDefaults`+`AddHttpClientInstrumentation`) is confirmed. runtime-readiness.md:27-28 and :71 confirm the boundary error-classification rule. This is a backend/infra observability+resilience gap — admin persona owns platform reliability.

# US-admin-0007 — Route Stripe & SendGrid through `IHttpClientFactory` for resilience and tracing

## Context (grounded in code)

The Czech fiscal client is the in-repo gold standard: `FiscalServiceCollectionExtensions.cs:54-55` registers its transport with `services.AddHttpClient<CzechEet2FiscalService>().AddStandardResilienceHandler()`, and `ServiceDefaults/Extensions.cs:25-29` adds `ConfigureHttpClientDefaults` + `AddHttpClientInstrumentation` so *every* factory-managed client inherits the standard resilience handler and OpenTelemetry HTTP spans.

Stripe and SendGrid bypass this entirely:

- **Stripe** — `StripeClient.cs` news up a fresh SDK client per call at 11 sites (lines 42, 50, 83, 108, 140, 151, 165, 186, 225, 262, 312), each `new global::Stripe.StripeClient(config.SecretKey)`. DI registers it as `AddTransient<IStripeClient, StripeClient>()` (`StripeExtensions.cs:23`) — never `AddHttpClient`. There is **no** outbound resilience policy and **no** boundary error classification.
- **SendGrid** — `EmailService.cs:348` and `:390` do `new SendGridClient(sendGridConfig.ApiKey)` per send; `SendGridClientFactory.cs:15` does the same. `EmailService` has a **hand-rolled** Polly retry (`EmailService.cs:32-43`) — a parallel, inconsistent resilience path that the fiscal/standard handler would replace.

Consequence vs. `agents/knowledge/runtime-readiness.md`:
1. **No standard resilience** on Stripe (rule :71 — "every external call classifies its error and logs the boundary"); a transient Stripe blip surfaces raw.
2. **No distributed tracing** on Stripe/SendGrid spans (rule :27-28) — a Stripe slowdown is invisible in traces.
3. **Socket churn** — newing an SDK client per call risks a fresh `HttpClient`/handler each time (the classic `HttpClient` anti-pattern).

## User story

**As an** admin (platform operator responsible for reliability),
**I want** all outbound Stripe and SendGrid calls to flow through `IHttpClientFactory` with the same standard resilience handler and OpenTelemetry instrumentation the fiscal client already uses,
**so that** transient payment/email provider blips are absorbed and classified instead of surfacing as raw 500s, provider slowdowns appear in distributed traces before they become incidents, and connection reuse eliminates socket churn — giving the platform "runs safely with minimal manual intervention" (runtime-readiness.md:7).

## Acceptance criteria (Given / When / Then)

1. **Stripe transport via factory**
   **Given** the Stripe integration,
   **When** the app starts,
   **Then** the `Stripe.StripeClient`'s HTTP transport (`SystemNetHttpClient`) is built from an `HttpClient` obtained from `IHttpClientFactory`, registered via `AddHttpClient(...)` with `.AddStandardResilienceHandler()` — and **no** `StripeClient.cs` site news up a transport-owning `global::Stripe.StripeClient` with an implicit per-call `HttpClient`.

2. **SendGrid transport via factory**
   **Given** the SendGrid integration,
   **When** `EmailService` sends an email,
   **Then** the `SendGridClient` is constructed with an `HttpClient` injected from `IHttpClientFactory` (via `SendGridClientOptions.HttpErrorAsException`/`reliabilitySettings` left to the standard handler), and the `new SendGridClient(apiKey)` calls at `EmailService.cs:348` and `:390` and `SendGridClientFactory.cs:15` no longer create their own transport.

3. **Standard resilience replaces the hand-rolled policy**
   **Given** the registrations in AC1/AC2,
   **When** a Stripe or SendGrid call hits a transient failure (timeout / 5xx / 429),
   **Then** it is retried by `AddStandardResilienceHandler` with backoff, and the bespoke Polly policy in `EmailService.cs:32-43` is removed (one resilience mechanism, consistent with the fiscal client) — verified by a test asserting a transient response is retried and a permanent 4xx is not.

4. **OTel spans appear**
   **Given** a Stripe checkout/payment-intent call and a SendGrid send,
   **When** the operation runs with tracing enabled,
   **Then** each produces an outbound HTTP client span (host = `api.stripe.com` / `api.sendgrid.com`) under the current trace — observable because the client now flows through the `AddHttpClientInstrumentation` default wired in `ServiceDefaults/Extensions.cs:25-29`.

5. **Boundary error classification + structured log**
   **Given** any Stripe or SendGrid call,
   **When** it fails,
   **Then** the boundary logs outcome + duration + an explicit classification of `Transient | Permanent | Configuration | Unknown` (runtime-readiness.md:28,71), with no PII above Debug (S6) — verified by a test asserting a `StripeException`/`HttpRequestException` is mapped to the correct class.

6. **No behavior regression**
   **Given** the existing Stripe idempotency keys (e.g. `pi-{orderId}-{amountCents}`, `refund-{sessionId}-{amountCents}`) and SendGrid template sends,
   **When** the refactor lands,
   **Then** all existing public method signatures on `IStripeClient`/`IEmailService` and their idempotency/return semantics are unchanged, and the existing Stripe/email test suites pass.

## Out of scope

- Moving email sending to a queue + Function (the runtime-readiness.md:45 "email is a side effect, enqueue it" gap) — that is a separate story; this one only fixes the transport.
- The Firebase/FCM push transport (`FcmExtensions.cs`) — push is already `AddSingleton<IPushDispatcher, FcmPushDispatcher>`; not in this scope.
- Webhook idempotency / payment-state reconciliation (S7) — untouched.
- Adding new health-check probes for Stripe/SendGrid reachability (runtime-readiness.md:32) — separate story.
- Any change to Stripe API version, SendGrid template IDs, or the `RetryFailedFiscalRegistrations` path.
- Frontend, mobile, and admin-UI changes — this is transport-layer only.
- Secret-management changes for `SecretKey` / `ApiKey` (still sourced from `IStripeConfig`/`ISendGridConfig`).

## Layers touched

- **Backend / Infra only.**
  - `src/Cleansia.Infra.Clients/Stripe/` — `StripeExtensions.cs` (add `AddHttpClient`), `StripeClient.cs` (consume factory-built transport), `StripeClientFactory.cs`.
  - `src/Cleansia.Infra.Clients/SendGrid/` — `SendGridExtensions.cs`, `SendGridClientFactory.cs`.
  - `src/Cleansia.Core.AppServices/Services/EmailService.cs` — inject factory-built `SendGridClient`, remove the hand-rolled `policy` (lines 32-43, 358, 408).
  - New error-classification helper at each boundary (`Transient | Permanent | Configuration | Unknown`).
  - `src/Cleansia.Tests` — resilience/classification/tracing unit tests.
- **No** DB migration, **no** NSwag regeneration, **no** API contract change → no `MANUAL_STEP` required.

## Notes for the team

- The Stripe .NET SDK supports a custom transport via `new StripeClient(apiKey, httpClient: new SystemNetHttpClient(injectedHttpClient))`; SendGrid supports `new SendGridClient(injectedHttpClient, apiKey)`. Both let `IHttpClientFactory` own the handler lifetime — this is the mechanism that lets the standard resilience handler + OTel attach.
- Cross-check `consistency.md` and `conventions.md` during build: the fiscal client is the established pattern to mirror, so this is a consistency fix as much as a resilience one.


---

I now have everything I need. Let me write the user story, grounded in the cited code.

Key facts grounded:
- `CancelOrder.cs:119` — `refundAmount = order.TotalPrice * (1m - feeRate)` (untested computation)
- `ResolveDispute.cs:26-29` — `RefundAmount` validator with `InvalidRefundAmount` (untested)
- `BookingPolicy.cs:98` `CalculateCancellationFeeRate` is the only tested piece (`BookingPolicyTests.cs`)
- `HandlePaymentNotification.cs` — `switch` at :201 has no `charge.refunded` case (no refund-side webhook path)
- testing.md must-cover #3 names "refunds on cancellation (the fee-rate × refund-amount math)"; must-cover #8 requires every `BusinessErrorMessage` path tested; #6 idempotency for refunds

Here is the story.

---

# US-admin-0007 — Backend test coverage for refund money-math and dispute refund validation

> Type: Test-coverage / quality story (no behavior change)
> Source GAP: Refunds / dispute-resolution money math — ZERO tests (testing.md must-cover #3, also #8)
> Persona: admin (the refund-issuing actor in `ResolveDispute`; cancellation refunds are customer-triggered but the money-math under test serves admin oversight/settlement)

## Context (grounded in code)

The cancellation **fee-rate** function is well-tested — `BookingPolicy.CalculateCancellationFeeRate` has 16 cases in `src/Cleansia.Tests/Features/Orders/BookingPolicyTests.cs`. But the money that actually moves is computed and validated **outside** that function, and none of it is tested:

- **Refund-amount computation** lives at `src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:119` — `var refundAmount = order.TotalPrice * (1m - feeRate);` — and the refund-gate predicate at `CancelOrder.cs:130-133` (`PaymentType.Card && PaymentStatus.Paid && refundAmount > 0m && StripeSessionId` non-empty). Neither the multiplication nor the gate has a test.
- **Dispute refund validation** lives at `src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:26-29` — `RuleFor(x => x.RefundAmount).GreaterThanOrEqualTo(0).When(x => x.RefundAmount.HasValue).WithMessage(BusinessErrorMessage.InvalidRefundAmount)`. The `dispute.invalid_refund_amount` error path (`BusinessErrorMessage.cs:199`) is never triggered by a test, violating testing.md must-cover #8 (every `BusinessErrorMessage` path).
- **No refund-side webhook path exists** — `HandlePaymentNotification.cs:201-213` switches only on `ExpiredSession / CompletedSession / PaymentIntentSucceeded / PaymentIntentPaymentFailed / PaymentIntentCanceled`. There is no `charge.refunded` case, so a Stripe-side refund never reconciles back to `Order.PaymentStatus` / `CancellationRefundAmount` (`Order.cs:129,134`). This story pins the current behavior with a characterization test and flags the reconciliation gap; it does **not** add the missing webhook branch (that is a separate functional story — see out-of-scope).

This is a test-first / characterization effort per testing.md ("When you're changing existing untested code … write a characterization test first"). It changes no production code.

## Actor narrative

**As an** admin responsible for resolving disputes and overseeing cancellation refunds,
**I want** the refund-amount computation and the dispute refund-amount validation to be covered by automated backend unit tests that pin the exact money math and the `InvalidRefundAmount` rejection,
**so that** a future change to fee/refund logic cannot silently issue the wrong refund or accept a negative refund without a test going red, and so the refund-issuance error contract the frontend relies on is proven real.

## Acceptance criteria (Given/When/Then)

1. **Refund-amount computation is pinned**
   Given an order with a known `TotalPrice` and a fee rate of 0.0, 0.25, and 0.50,
   When the cancellation refund amount is computed as `TotalPrice * (1 - feeRate)`,
   Then a unit test asserts the exact decimal result for each rate (e.g. `1000 → 1000 / 750 / 500`) using `decimal` (no floating-point drift), matching `CancelOrder.cs:119`.

2. **Refund gate skips when not a paid card order**
   Given a cancellation whose order is Cash, or Card-but-not-`Paid`, or has a zero refund amount, or has an empty `StripeSessionId`,
   When the cancel handler runs (with a mocked `IStripeClientFactory`),
   Then no Stripe refund is attempted and the response reports `RefundInitiated == false`; and given a Card+`Paid` order with a positive refund and a session id, a refund **is** attempted and `RefundInitiated == true` — matching the gate at `CancelOrder.cs:130-144`.

3. **Stripe failure on refund is non-blocking**
   Given a Card+`Paid` cancellation where the mocked Stripe client throws `StripeException`,
   When the cancel handler runs,
   Then the result is still `IsSuccess`, `RefundInitiated == false`, payment status is **not** moved to `Refunded`, and no `OrderRefunded` push is enqueued — pinning the catch behavior at `CancelOrder.cs:146-151`.

4. **Dispute resolution rejects a negative refund**
   Given a `ResolveDispute.Command` with a `RefundAmount` of `-1`,
   When the validator runs (`new ResolveDispute.Validator().TestValidate(cmd)`),
   Then validation fails with `BusinessErrorMessage.InvalidRefundAmount`, satisfying must-cover #8 for the `dispute.invalid_refund_amount` key.

5. **Dispute resolution accepts a null or non-negative refund**
   Given a `ResolveDispute.Command` with `RefundAmount == null`, with `0`, and with a positive value (and valid `ResolutionNotes`),
   When the validator runs,
   Then the `RefundAmount` rule does **not** raise an error (the `.When(HasValue)` short-circuit and the `>= 0` boundary at `ResolveDispute.cs:27-28` are both exercised).

6. **Refund-reconciliation gap is documented by a characterization test**
   Given the current `HandlePaymentNotification` switch (`HandlePaymentNotification.cs:201-213`) which has no `charge.refunded` case,
   When the test suite documents the handled event types,
   Then a test (or an `xUnit` `[Fact(Skip=…)]` placeholder with a comment) records that no `charge.refunded` reconciliation path exists, so the gap is visible and traceable to the follow-up functional story rather than silently assumed-covered.

## Out of scope

- Adding a `charge.refunded` (or `refund.updated`) branch to `HandlePaymentNotification` — that is a **functional** story (refund-side reconciliation), not this test story.
- Adding the missing admin-initiated order-intervention / refund-issuance endpoint (coordinate with the prior "admin order intervention is missing" finding). If `ResolveDispute`'s refund is found unreachable from any admin route, raise a separate functional story; this story only tests the logic that exists.
- Wiring the dispute `RefundAmount` into an actual Stripe refund call — `Dispute.Resolve` (`Dispute.cs:82-90`) only **records** `RefundAmount`; it issues no money. This story does not add issuance.
- Changing fee rates, refund formula, the `Order.Cancel` signature, or any production code.
- Integration tests against a live Web host, EF/migrations, frontend Jest specs, i18n keys, and mobile.
- Idempotency tests for the refund webhook (must-cover #6) — not applicable until the `charge.refunded` path exists.

## Layers touched

- **Backend tests only** — `src/Cleansia.Tests` (xUnit). New unit tests under `Features/Orders` (cancel refund-amount + gate, using the existing `ValidatorTestHelpers.BuildOrder` builder and mocked `IStripeClientFactory`/`ILoyaltyService`/`IQueueClient`/`ICancellationPolicyResolver`) and `Features/Disputes` (a new `ResolveDisputeValidatorTests` following the `*ValidatorTests` + `TestValidate` pattern already used in `Features/Auth`).
- **No production code** in `Cleansia.Core.AppServices`, `Cleansia.Core.Domain`, or any Web project is modified.
- No DB migration, no NSwag regeneration, no frontend/mobile work → **no `MANUAL_STEP` required**.

## Cited code references

- `src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs:119` (refund-amount), `:130-151` (refund gate + Stripe catch), `:182-189` (response)
- `src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:26-29` (`InvalidRefundAmount` rule)
- `src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs:199` (`dispute.invalid_refund_amount`)
- `src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs:201-213` (no `charge.refunded` case)
- `src/Cleansia.Core.Domain/Orders/Order.cs:129,134` (`CancellationRefundAmount`/`CancellationFeeRate`), `:441-454` (`Cancel`)
- `src/Cleansia.Core.Domain/Disputes/Dispute.cs:82-90` (`Resolve` records refund, issues none)
- Existing tested piece (the fee-rate only): `src/Cleansia.Tests/Features/Orders/BookingPolicyTests.cs`
- Patterns to follow: `src/Cleansia.Tests/Features/Orders/ValidatorTestHelpers.cs`; `src/Cleansia.Tests/Features/Auth/*ValidatorTests.cs`
- Rules cited: `agents/knowledge/testing.md` must-cover **#3** (refunds: fee-rate × refund-amount math), **#8** (every `BusinessErrorMessage` path), and the "characterization test first" guidance for untested code.


---

There are write commands (`CreateFeatureFlag`, `DeleteFeatureFlag`, `ToggleFeatureFlag`) — so a cache must be invalidated on these writes to avoid stale flags. I have everything needed to ground the story. Here is the user story.

---

# US-admin-0042 — Cache feature-flag resolution to cut sequential per-check DB round-trips

## Actor narrative

**As an** admin (platform operator) who configures and relies on feature flags to gate platform behavior,
**I want** feature-flag checks to be served from an in-memory cache and resolved in a single database round-trip instead of up to three sequential ones,
**so that** flag-gated request paths and background jobs stay fast and cheap under load, while toggles I make in the admin tools still take effect promptly.

## Context / grounding (read code)

- `src/Cleansia.Infra.Database/AppConfigurationProvider.cs:26-56` — `IsFeatureEnabledAsync` runs up to **three sequential, awaited, uncached** `FirstOrDefaultAsync` queries: tenant scope (`:33-35`), then country scope (`:43-45`), then global (`:51-53`), each with `.AsNoTracking()`. There is no caching and the three lookups are not collapsed.
- `src/Cleansia.Core.AppServices/Features/FeatureFlags/CheckFeatureFlag.cs:24` — the `Handler` is a thin pass-through to `IsFeatureEnabledAsync`; the cost lives entirely in the provider.
- **Hot-path exposure:** `Cleansia.Web.Customer/Controllers/FeatureFlagController.cs:14-22` exposes `GET /api/featureflag/check` as `[AllowAnonymous]` (rate-limited `"interactive"`), so unauthenticated callers can each trigger 1–3 DB queries. Partner / mobile-partner / mobile-customer controllers expose the same endpoint behind `Policy.CanCheckFeatureFlag`.
- Also called by `DataRetention/DataRetentionBackgroundService.cs:28` (periodic job — low frequency, not the concern).
- **Index already exists:** `EntityConfigurations/FeatureFlagEntityConfiguration.cs:27` — unique index on `(Name, Scope, ScopeValue)`, so each individual query is cheap; the cost is **volume × no-cache × sequential awaits**, not a missing index.
- **No caching infrastructure exists today:** repo-wide search found zero usages of `IMemoryCache` / `IDistributedCache` / `HybridCache` under `src`. This story introduces the first one for this provider.
- **Writes that must invalidate:** `CreateFeatureFlag.cs`, `ToggleFeatureFlag.cs`, `DeleteFeatureFlag.cs` mutate flags; `FeatureFlag.cs:40-56` (`Toggle/Enable/Disable`). A cache without invalidation on these paths would serve stale flags.
- Conventions grounding: feature flags are read-mostly config; resolution order is documented in `FeatureFlag.cs:17-28` as tenant → country → global (most specific wins), which any single-query collapse must preserve.

## Acceptance criteria (Given / When / Then)

1. **Cache hit avoids DB round-trips**
   **Given** a feature flag has already been resolved once for a given `(featureName, countryId, tenantId)` triple,
   **When** the same triple is checked again within the cache TTL,
   **Then** the result is served from cache with **zero** database queries for that check (verifiable by an EF query interceptor / counter in a test).

2. **Single round-trip on cache miss**
   **Given** the requested flag is not in cache,
   **When** `IsFeatureEnabledAsync` resolves it,
   **Then** at most **one** database round-trip is issued (not three sequential ones), and the most-specific matching scope is selected in memory following the existing precedence **tenant → country → global**.

3. **Resolution result is unchanged vs. current behavior**
   **Given** any combination of tenant, country, and global flag rows (including missing rows),
   **When** a flag is checked,
   **Then** the returned `IsEnabled` value is identical to the pre-change behavior, including the default of **`false`** when no matching flag exists (per `AppConfigurationProvider.cs:55`).

4. **Writes invalidate the cache (no stale flags)**
   **Given** a flag is currently cached,
   **When** an admin creates, toggles, or deletes that flag via `CreateFeatureFlag` / `ToggleFeatureFlag` / `DeleteFeatureFlag`,
   **Then** the next check for the affected `(featureName, scope, scopeValue)` reflects the new value within the defined freshness window (immediately on the same instance via invalidation, or within the documented TTL).

5. **Tenant isolation preserved (S8)**
   **Given** flags cached for tenant A,
   **When** a check is performed for tenant B (or anonymous/global context),
   **Then** tenant A's tenant-scoped cache entry is never returned for tenant B — the cache key includes `tenantName/countryId/tenantId` so scopes cannot bleed across tenants.

6. **Anonymous endpoint stays bounded under load**
   **Given** the `[AllowAnonymous]` customer `GET /api/featureflag/check` endpoint,
   **When** the same flag is requested repeatedly,
   **Then** repeated requests for an already-resolved flag do not each issue fresh DB queries (cache absorbs the volume), and existing `[EnableRateLimiting("interactive")]` behavior is unchanged.

## Out of scope

- Changing the **flag precedence semantics** (tenant → country → global) or the default-`false` behavior.
- Adding, removing, or renaming any feature flags, or changing what they gate.
- The admin **UI** for managing flags (no Angular/admin-app changes) — this is provider/data-layer only.
- The `CheckFeatureFlag` **query/response contract** and the controller signatures — no NSwag regeneration is expected (flag this `manual_step: nswag-regen` only if the public contract ends up changing, which it should not).
- A **distributed/cross-instance** cache (Redis/`IDistributedCache`/`HybridCache`) — in-process `IMemoryCache` with TTL + write-path invalidation is sufficient for this story; cross-instance coherence is a separate ticket.
- The unrelated `GetTenantSettingAsync` and `GetCountryConfigurationAsync` methods on the same provider (`AppConfigurationProvider.cs:9-24`), unless trivially covered by the same cache abstraction.
- The broader **PERF-CAT-01** caching epic — this story may fold into that cache abstraction, but its scope is limited to feature-flag resolution.
- EF Core **migrations** (none required — index already exists) and any schema change.

## Layers touched

- **Backend / Infra.Database** — `AppConfigurationProvider.IsFeatureEnabledAsync` (caching + optional single-query collapse). *(primary)*
- **Backend / AppServices** — `CreateFeatureFlag`, `ToggleFeatureFlag`, `DeleteFeatureFlag` handlers gain cache-invalidation; possibly a small cache abstraction/interface.
- **Backend / Config (DI)** — register `IMemoryCache` (first usage in the solution) and any cache helper.
- **Tests** — `Cleansia.Tests` (unit: precedence, default-false, invalidation, query-count assertions; tenant-isolation).
- **Not touched:** Domain entity (`FeatureFlag` unchanged), database schema/migrations, frontend apps, Android apps, NSwag clients.


---

I now have everything grounded. Key confirmations:

- Backend `GetReferralsByUser` returns a `Response(AsReferrer, AsReferred)` — two lists (people they invited, and the row that brought them in), each `AdminReferralListItem` with `ReferrerEmail`, `ReferredEmail`, `Status` (ReferralStatus enum), `AcceptedOn`, `FirstQualifyingOrderOn`, `PointsAwardedToReferrer`, `PointsAwardedToReferred`. The handler doc comment literally says "the admin UI can render two stacked tables."
- Validator returns 404 (`UserNotFound`) for unknown users; `Required` for empty.
- `[Permission(Policy.CanViewReferrals)]` gates the endpoint.
- `user-loyalty-detail.facade.ts` loads account + activity, has `Policy` permission directive support, uses `CleansiaTableComponent`/`CleansiaSectionComponent`, and `navigateBack()` returns to `/admin-user-management` — confirming the user-detail screen is the natural home and already routes from user management with a `userId` param.
- `referrals-list.facade.ts` calls ONLY `getPaged`. `byUser` exists solely in the generated client (lines 9329/9445). Confirmed orphaned.

Here is the user story.

---

# US-admin-0042 — Surface a user's referral relationships on the loyalty user detail screen

## Context (grounded in code)

The backend is already live and complete:

- `AdminReferralController.GetReferralsByUser` exposes `GET /api/AdminReferral/by-user/{userId}`, gated by `[Permission(Policy.CanViewReferrals)]`, returning `404` for an unknown user.
  (`src/Cleansia.Web.Admin/Controllers/AdminReferralController.cs:37-50`)
- `GetReferralsByUser.Response` returns **two lists** — `AsReferrer` (people this user invited) and `AsReferred` (the single row that brought this user in, if any) — each an `AdminReferralListItem` carrying `ReferrerEmail`, `ReferredEmail`, `Status`, `AcceptedOn`, `FirstQualifyingOrderOn`, `PointsAwardedToReferrer`, `PointsAwardedToReferred`. The handler's own doc comment states the split is "so the admin UI can render two stacked tables."
  (`src/Cleansia.Core.AppServices/Features/Referrals/Admin/GetReferralsByUser.cs:15-67`)
- The generated admin client exposes `adminReferralClient.byUser(userId)`.
  (`src/Cleansia.App/libs/core/admin-services/src/lib/client/admin-client.ts:9329, 9445`)

The frontend never calls it. A grep across `libs/cleansia-admin-features` for `byUser` returns **only** the generated client; no feature consumes it. The referrals admin UI calls only `getPaged` (`referrals-list.facade.ts:36-43`), and the loyalty user detail screen loads only the loyalty account and points activity (`user-loyalty-detail.facade.ts:43, 62`) — never referrals. The detail screen already exists, already routes per `userId`, already renders points-activity via `CleansiaTableComponent` inside `CleansiaSectionComponent`, and already navigates back to `/admin-user-management` — making it the natural, ready home for this data.

The functional gap: an admin investigating a specific user (fraud check, dispute, "did my friend's referral actually count?") **cannot see that user's referral relationships anywhere in-app**, despite the backend being fully ready.

## User story

**As an** admin reviewing a specific user on the loyalty user detail screen,
**I want** to see that user's referral relationships — both the people they referred and the referral that brought them in, with each referral's status and awarded points —
**so that** I can investigate referral fraud, resolve "my referral didn't count" disputes, and verify referral rewards without leaving the user's detail page or cross-referencing the global referrals list by hand.

## Acceptance criteria

1. **Given** I am an admin with `CanViewReferrals` permission viewing the loyalty user detail screen for a user,
   **When** the page loads,
   **Then** a "Referrals" panel is shown that loads via `adminReferralClient.byUser(userId)` and renders two distinct sections — "Referred by this user" (`AsReferrer`) and "How this user joined" (`AsReferred`).

2. **Given** the `byUser` response for a user contains entries in `AsReferrer`,
   **When** the panel renders,
   **Then** each referred-person row shows the referred user's email, referral `Status`, `AcceptedOn`, `FirstQualifyingOrderOn`, and `PointsAwardedToReferrer` / `PointsAwardedToReferred`, with empty/null dates rendered as a placeholder (consistent with the existing activity table's `—`).

3. **Given** a user was themselves referred by someone,
   **When** the panel renders,
   **Then** the "How this user joined" section shows that single referral row with the referrer's email and the referral status; **and given** the user was not referred by anyone, that section shows an empty-state message instead of a blank table.

4. **Given** the `byUser` call returns no referral activity at all (both lists empty),
   **When** the panel finishes loading,
   **Then** an empty-state message is shown for the whole panel rather than two empty tables, and no error is surfaced.

5. **Given** I am an admin **without** the `CanViewReferrals` permission,
   **When** I view the loyalty user detail screen,
   **Then** the Referrals panel is not rendered and no `byUser` request is issued (mirroring the existing `CleansiaPermissionDirective` / `Policy` gating already used on this screen).

6. **Given** the `byUser` request fails or the user id is invalid (backend returns 404),
   **When** the panel attempts to load,
   **Then** the failure is caught and a non-blocking error/empty state is shown for the panel only, while the rest of the loyalty detail screen (account + points activity) continues to function — the three explicit data states (loading / loaded / empty-or-error) are all observable.

7. **Given** the new panel introduces user-visible strings,
   **When** the panel renders in any supported locale,
   **Then** all labels (section titles, column headers, empty states, status labels) resolve via `TranslatePipe`/`TranslateService` with keys present in **all five** locale files (en, cs, sk, uk, ru) and no hardcoded strings.

## Out of scope

- Any **new or modified backend** — `GetReferralsByUser` query, handler, controller, validator, and DTOs already exist and are unchanged.
- **NSwag client regeneration** — `byUser(userId)` is already present in the generated client; no regeneration is needed (and regeneration is owner-only regardless).
- **EF Core migrations** / schema changes (owner-only; none required here).
- Adding referral **drill-in from the global referrals list** (`referrals-list` / `getPaged`) — this story targets the loyalty user detail screen only.
- Adding referral data to the **partner or customer** apps, or to the Android apps.
- Any **mutation** of referrals (manually creating, voiding, re-qualifying, or re-awarding points for a referral) — this is read-only surfacing.
- Making the referred/referrer emails **clickable links** to navigate to that other user's detail screen (nice-to-have; can be a follow-up).
- Changing the **referral domain model, statuses, or points-award logic**.

## Layers touched

- **Frontend only** — Angular admin app (`cleansia-admin-features/loyalty-user-detail`):
  - `user-loyalty-detail.facade.ts` — add a `referrals` signal + loading/empty state and a `loadReferrals(userId)` calling `adminClient.adminReferralClient.byUser(userId)`, wired into the existing `loadAccount`/`loadActivity` flow and `refresh()`.
  - `user-loyalty-detail.component.ts` / `.html` — add the "Referrals" panel (two `CleansiaTableComponent` tables inside a `CleansiaSectionComponent`), gated by `CleansiaPermissionDirective` on `Policy.CanViewReferrals`, with column builders mirroring the existing activity-column pattern.
  - i18n: new keys under `pages.loyalty_user_detail.referrals.*` in all five locale files (`en, cs, sk, uk, ru`).
- **Backend**: none (already implemented).
- **DB / NSwag / Mobile**: none.


---

No existing user stories, so I'll start the numbering. The gap is about the anonymous catalog being unsafe for a real second tenant — the actor is the customer (booking visitor), with admin-side implications for whether per-tenant catalog is even allowed. The primary persona experiencing the broken outcome is the customer. I have all the grounding I need.

# US-customer-0001 — Anonymous catalog is tenant-correct (resolve or formalize before the second tenant)

## Persona / Actor narrative

**As a** prospective customer browsing a tenant's public booking wizard (no login, no `tenant_id` claim),
**I want** the anonymous services/packages catalog I am shown to be the catalog that actually belongs to the tenant whose site I am visiting,
**so that** I can see and book that tenant's real offerings instead of an empty list or another tenant's default catalog — and so onboarding a second tenant does not silently break or cross-leak the booking flow.

## Context (grounded in code)

- `GET /api/Service/GetOverview` and `GET /api/Package/GetOverview` are `[AllowAnonymous]` — `ServiceController.cs:14-21`, `PackageController.cs:14-21`. They delegate to `GetServiceOverview.Handler` / `GetPackageOverview.Handler` (`GetServiceOverview.cs:13-27`, `GetPackageOverview.cs:13-28`), which read `Services`/`Packages` (each filtered to `IsActive`) via the repositories — no tenant context is passed in.
- `Service`, `ServiceCategory`, and `Package` are all `ITenantEntity` (`Service.cs:9`, `ServiceCategory.cs:7`, `Package.cs` — confirmed `ITenantEntity`), so the global EF query filter in `CleansiaDbContext.ApplyTenantQueryFilters` (`CleansiaDbContext.cs:111-180`) applies.
- On an anonymous request there is no `tenant_id` claim, so `TenantProvider.GetCurrentTenantId()` returns `null` (`TenantProvider.cs:12-20`), and the filter collapses to the `singleTenantMatch` branch `currentTenantId == null && e.TenantId == null` (`CleansiaDbContext.cs:154-156`) — i.e. **only null-tenant rows are returned**.
- The only mechanism that exists to scope an un-authenticated read to a tenant is `ITenantProvider.SetTenantOverride(...)` (`TenantProvider.cs:22-25`), and it is used **exclusively by background jobs/webhooks** that already know the tenant from a row (`HandlePaymentNotification.cs:195`, `CleanupStalePendingOrders.cs:66`, `MaterializeRecurringBookings.cs:74`, the Functions, etc.). There is **no host/subdomain→tenant resolution anywhere** in `src/` for inbound HTTP (the lone "subdomain" reference is an unrelated CSRF comment in `CsrfTokenService.cs:19`).
- Rules cited: **S3** ("For `[AllowAnonymous]` endpoints there is no tenant claim, so the global filter is bypassed — anonymous routes must not return tenant-scoped data unless gated by a different shared secret", `security-rules.md:54-56`) and **S8** ("tenant isolation correctness", `security-rules.md:93-101`).

This is a latent (not yet live) defect: in today's single-tenant deployment every catalog row has `TenantId == null`, so it works. The first tenant that creates catalog rows with a non-null `TenantId` triggers both failure modes below.

## Acceptance Criteria (Given/When/Then)

1. **Tenant's own customers see that tenant's catalog (the outage today)**
   **Given** a tenant `T` exists whose active `Service`/`Package` rows have `TenantId = T`,
   **When** an anonymous visitor loads `GET /api/Service/GetOverview` and `GET /api/Package/GetOverview` for tenant `T`'s site,
   **Then** the response contains tenant `T`'s active services and packages (the booking wizard is populated), **not** an empty list.

2. **No cross-tenant leakage**
   **Given** tenants `T` and `U` each have their own active catalog rows,
   **When** an anonymous visitor loads the catalog endpoints for tenant `T`,
   **Then** the response contains **only** `T`'s rows and **no** rows belonging to `U` (and no null-tenant default rows are silently substituted for `T`'s own catalog).

3. **Tenant resolution for anonymous reads is deterministic and explicit**
   **Given** an anonymous catalog request,
   **When** the request carries the tenant signal the system has standardized on (host/subdomain mapping, or an explicit tenant parameter/header),
   **Then** the resolved tenant is applied before the catalog query runs (e.g. via `SetTenantOverride`), and an **unresolvable** tenant signal returns a deterministic, documented result (defined null-tenant default catalog **or** a 4xx) rather than a silently wrong/leaky one.

4. **`IsActive` filtering is preserved**
   **Given** tenant resolution is applied,
   **When** the catalog is returned,
   **Then** only `IsActive` services/packages are included (the existing `Where(IsActive)` behavior in the handlers is retained — S10) and deactivated rows never appear.

5. **The platform-global vs per-tenant decision is recorded**
   **Given** the team chooses between (a) resolving tenant per request or (b) formally declaring the catalog platform-global (null-tenant only) and forbidding per-tenant catalog rows,
   **When** the decision is made,
   **Then** it is captured as an ADR and reflected in `agents/knowledge/` (and, if option (b), enforced/validated so per-tenant `Service`/`Package`/`ServiceCategory` rows cannot be created).

6. **Regression guard**
   **Given** the resolution (or the formal global-catalog rule),
   **When** an automated test seeds null-tenant rows plus tenant-`T` rows and calls each anonymous catalog endpoint with and without a resolvable tenant signal,
   **Then** the test asserts the correct, non-leaking row set for each case — and the legacy single-tenant (all-null) deployment still returns its catalog unchanged.

## Out of scope

- Authenticated catalog reads (requests that already carry a `tenant_id` claim) — they already scope correctly via the existing filter; this story does not change them.
- The booking/checkout flow, cart, pricing, or order creation beyond the catalog (`GetOverview`) reads.
- The `singleTenantMatch` / null-tenant filter semantics in `CleansiaDbContext.cs:154-156` themselves — they stay as-is for single-tenant and background-job correctness; this story adds resolution *before* the query, it does not rewrite the global filter.
- A full subdomain/custom-domain tenant-routing system for the entire app — only the inbound tenant signal needed by the anonymous catalog endpoints is in scope (broader routing can be a follow-up).
- Other anonymous endpoints (auth, public order-lookup-by-confirmation-code, etc.) — auditing/fixing those is a separate ticket, though this story should establish the reusable resolution mechanism.
- Frontend redesign of the booking wizard; only the data it consumes must become tenant-correct.
- EF migrations / NSwag client regeneration are owner-only `MANUAL_STEP`s, not implemented here.

## Layers touched

- **Backend API** — `Cleansia.Web.Customer` (`ServiceController`, `PackageController`; likely middleware or a request-tenant resolver that calls `SetTenantOverride`).
- **Backend AppServices** — `GetServiceOverview` / `GetPackageOverview` handlers (consume resolved tenant; keep `IsActive`).
- **Infra/Database** — `TenantProvider` / `ITenantProvider` (anonymous-request override path), interaction with `CleansiaDbContext` global filter; no change to the filter expression itself.
- **Domain** — only if option (b) is chosen (constraint/validation forbidding per-tenant catalog rows).
- **Config/Auth** — request pipeline where the tenant signal (host/subdomain or explicit param/header) is read.
- **Frontend (customer app)** — only if an explicit tenant param/header is the chosen signal (the customer app may need to send it); none if host/subdomain resolution is used.
- **Docs/Knowledge** — ADR + `agents/knowledge/` note recording the platform-global-vs-per-tenant decision.
- **Tests** — `Cleansia.Tests` / `Cleansia.IntegrationTests` for the regression guard.

> Decision gate: this must be resolved **before the first real second tenant onboards** — until then it is latent. Recommend the PM route the platform-global-vs-resolve decision to the Architect (ADR) ahead of implementation.


---

I have everything grounded. The backend is confirmed complete (entity with scope semantics, 5 handlers, controller with 4 admin policies, generated `AdminFeatureFlagClient` with `adminFeatureFlagGet/Post/Delete`, `toggle`, `check`), the 4 admin policies exist in both backend `Policy.cs` and frontend `policy.ts`, and there is no admin UI under `cleansia-admin-features` (siblings like `country-management`, `currency-management`, `language-management` are the clear list+form archetypes to mirror). Note one grounded nuance: the generated client splits methods across two classes — CRUD-ish on one (`adminFeatureFlagGet/Post/Delete`) and `toggle`/`check` on `AdminFeatureFlagClient`.

Here is the user story.

---

```yaml
---
id: US-admin-0042
title: Manage platform feature flags from the admin console
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As an **admin operator**, I want **to view, create, toggle, and delete feature flags — scoped to global, country, or tenant — from a screen in the admin console**, so that **I can turn platform behavior on and off for the right audience without an engineer hand-crafting raw HTTP calls against the admin API**.

## Context (grounded in code)

The backend for this is already complete and reachable:

- Entity `FeatureFlag` (`src/Cleansia.Core.Domain/Configuration/FeatureFlag.cs:6`) with `Name`, `Description`, `IsEnabled`, and a `Scope` of `"global" | "country" | "tenant"` plus an optional `ScopeValue` (country ISO code or tenant id; null for global).
- Handlers under `src/Cleansia.Core.AppServices/Features/FeatureFlags/`: `GetAllFeatureFlags` (optional `scope` filter, ordered by name then scope), `CreateFeatureFlag` (validates scope is one of the three values, name ≤ 100, scopeValue ≤ 26, description ≤ 500, and rejects duplicate name+scope+scopeValue with `BusinessErrorMessage.FeatureFlagAlreadyExists`), `ToggleFeatureFlag`, `DeleteFeatureFlag` (both validate existence with `FeatureFlagNotFound`).
- Controller `AdminFeatureFlagController` (`src/Cleansia.Web.Admin/Controllers/AdminFeatureFlagController.cs`) exposing `GET`, `POST`, `POST {id}/toggle`, `DELETE {id}`, `GET check`, gated by `CanViewFeatureFlags` / `CanCreateFeatureFlag` / `CanToggleFeatureFlag` / `CanDeleteFeatureFlag` (all `AdminOnly`), defined in both backend `Policy.cs:167` and frontend `policy.ts:125`.
- The generated admin client already has the methods (`admin-client.ts`): `adminFeatureFlagGet(scope?)`, `adminFeatureFlagPost(body)`, `adminFeatureFlagDelete(id)`, and on `AdminFeatureFlagClient`: `toggle(id)`, `check(...)`.

There are **zero** references to feature flags under `libs/cleansia-admin-features` or the admin app — the only operator path today is hitting the API directly. This story builds the missing UI by mirroring the existing list+form admin archetypes (`country-management`, `currency-management`, `language-management`).

## Acceptance criteria

- **AC1 — List** — Given I am an admin with `CanViewFeatureFlags` and feature flags exist, When I open the Feature Flags screen, Then I see a table of all flags showing name, description, enabled state, scope, and scope value, ordered by name then scope (matching the backend ordering).
- **AC2 — Scope filter** — Given the Feature Flags list is open, When I choose a scope (Global, Country, or Tenant) from the filter, Then the table reloads showing only flags of that scope, and clearing the filter shows all flags again.
- **AC3 — Create (happy path)** — Given I have `CanCreateFeatureFlag`, When I submit the create form with a name, a scope, an optional scope value, an optional description, and an initial enabled state, Then the new flag is persisted and appears in the list without a full page reload.
- **AC4 — Create (duplicate rejected)** — Given a flag already exists with the same name + scope + scope value, When I submit a create with that same combination, Then the request is rejected and the translated `errors.*` message for `FeatureFlagAlreadyExists` is shown, and no duplicate row is added.
- **AC5 — Toggle** — Given a flag is shown in the list, When I toggle its enabled control, Then its `IsEnabled` flips server-side and the row reflects the new state; a failed toggle reverts the control and surfaces a translated error.
- **AC6 — Delete with confirmation** — Given I have `CanDeleteFeatureFlag`, When I delete a flag and confirm in the confirmation dialog, Then the flag is removed and disappears from the list; cancelling the dialog leaves the flag untouched.
- **AC7 — Permission gating** — Given my admin account lacks `CanViewFeatureFlags`, When I attempt to reach the Feature Flags screen, Then the navigation entry is hidden and the route is not accessible (consistent with how other `AdminOnly` admin features gate via `policy.ts`).

## Out of scope

- Any backend changes — handlers, controller, validators, entity, policies, and the generated `AdminFeatureFlagClient` already exist and are not modified. No NSwag regeneration is required (`manual_step` not triggered).
- The "check feature" evaluation endpoint (`GET check` / `adminFeatureFlagClient.check`) is not surfaced as an operator-facing tool in this story; this UI is for managing flag records, not for runtime evaluation.
- Editing an existing flag's name/scope/scopeValue/description in place (no `UpdateFeatureFlag` handler exists on the backend — only create, toggle, delete). Changing those fields is achieved by delete + recreate.
- A typed scope **enum / dropdown sourced from a shared enum**. The picker in AC2/AC3 uses the three known literal values (`global`/`country`/`tenant`); replacing the literals with a clean shared scope enum is deferred to its dependency (CAT-07, scope enum) and tracked as an open question below.
- Resolving `ScopeValue` into a friendly picker (selecting an actual country or tenant from a list); for now `ScopeValue` is a free-text field constrained to ≤ 26 chars, matching the backend.
- Mobile (Android) and the customer/partner web apps — feature-flag management is admin-web only.
- Audit-trail/history of who toggled a flag and when (beyond the entity's existing `Auditable` Created/Updated fields shown read-only in the list).

## Layers touched

- **Frontend — admin feature library**: new `feature-flag-management` module under `src/Cleansia.App/libs/cleansia-admin-features/` (component + facade + models, following the OnPush + signals + facade pattern), mirroring `country-management` / `currency-management`.
- **Frontend — admin app shell**: route registration and a navigation menu entry in `apps/cleansia-admin.app`, gated by the `CanViewFeatureFlags` policy.
- **Frontend — i18n**: new translation keys (screen labels, table headers, form labels, scope option labels) and the `errors.*` mapping for `FeatureFlagAlreadyExists` / `FeatureFlagNotFound` across all 5 locales (en, cs, sk, uk, ru).
- **Frontend — generated client (consumed, not modified)**: `AdminFeatureFlagClient` from `libs/core/admin-services`.
- **Backend / DB / Mobile**: none.

## Open questions

- **Q (scope enum, CAT-07)** — Should the scope picker bind to a shared, typed scope enum, or to the three string literals? Default assumption taken: use the three literal values (`global`/`country`/`tenant`) now and adopt the shared enum once CAT-07 lands. Raise as Q-NNNN in `agents/backlog/questions/open.md` for the owner.
- **Q (no Update path)** — Backend has no `UpdateFeatureFlag`; confirm delete+recreate is acceptable operator UX, or whether an Update handler should be a separate backend story.

---

Source-grounding (files read): `FeatureFlag.cs`, `AdminFeatureFlagController.cs`, `CreateFeatureFlag.cs`, `GetAllFeatureFlags.cs`, `ToggleFeatureFlag.cs`, `DeleteFeatureFlag.cs`, `FeatureFlagDto.cs`, `Policy.cs:167-170`, `PolicyBuilder.cs:140-143`, `policy.ts:125-128/278-282`, `admin-client.ts:3690-4377`, and directory listing of `libs/cleansia-admin-features` (no feature-flag module present). No code was modified.


---

I have fully grounded the story. All facts confirmed against real code and the B6/B7 conventions. Here is the user story.

---

# US-admin-0007 — Retire (deactivate) and restore Services & Packages without destroying history

## Context (grounded in code)

The system is already half-built for a soft-retirement model but the active half is unreachable:

- `BaseEntity.IsActive` defaults to `true` (`Cleansia.Core.Domain/Common/BaseEntity.cs:7`).
- `Auditable.Deactivated(deactivatedBy, deactivatedOn)` flips `IsActive=false` and stamps `DeactivatedBy`/`DeactivatedOn` (`Cleansia.Core.Domain/Common/Auditable.cs:35-42`); those columns are mapped (`EntityConfiguration.cs:46-49`) and `BaseRepository.Deactivate(entity)` exists (`Cleansia.Infra.Database/BaseRepository.cs:122-125`).
- The customer booking catalog **already filters on it**: `GetServiceOverview.cs:20-23` returns only `s => s.IsActive`, with a comment that deactivated services "must not appear in the booking wizard catalog."
- The query side already supports an active/inactive filter: `ServiceSpecification.cs:20-23` and `BaseSpecification.IsActive` (`BaseSpecification.cs:7,14`) apply `IsActive == value` when set.
- **But nothing ever sets it.** `Service`/`Package` domain entities expose no activate/deactivate method beyond the inherited `Deactivated()`, no command calls it, no admin endpoint exposes it (`AdminServiceController.cs` has only get-paged/categories/details/create/update/delete; `AdminPackageController.cs` similarly). The only removal path is the **hard delete** `DeleteService.cs:35` → `serviceRepository.Remove(service!)` (CC-02), which destroys the row and its translations/links.
- The admin list filter is search-only: `ServiceFilter.cs` and `PackageFilter.cs` carry just `SearchTerm`; `GetPagedServices.cs:26-28` never passes `isActive`; the admin facade `service-management.facade.ts` exposes only `searchTerm` and a `deleteService()` (hard delete). So an admin **cannot** retire a service safely, cannot tell active from retired, and cannot bring one back.

The pattern is proven elsewhere in the same codebase — `DeactivateAdminUser.cs:52` calls `user!.Deactivated(...)`, and an `ActivateAdminUser` counterpart already ships (the admin client has `processActivate`). This story brings Services & Packages up to that same B6 soft-delete standard.

**Cited rules this satisfies:** `consistency.md` **B6** (soft-delete via `repo.Deactivate` is the canonical default for business-facing entities; the current `Service`/`Package` hard-delete is explicitly "tracked as a violation") and **B7** (handlers call rich domain methods, never set properties directly).

## Persona / Actor narrative

**As an** admin (catalog manager),
**I want** to deactivate (retire) and re-activate a Service or a Package, and filter the admin catalog by active vs. retired,
**so that** I can pull an offering out of the customer booking catalog while preserving its history and links to past orders — making the destructive hard-delete the rare exception, not the only option.

## Acceptance Criteria (Given/When/Then)

1. **Deactivate a Service**
   **Given** an active Service that appears in the customer booking catalog,
   **When** the admin deactivates it (admin Service list/detail toggle → `Deactivate` endpoint, `CanUpdateService` policy),
   **Then** the Service row is preserved with `IsActive=false`, `DeactivatedBy`/`DeactivatedOn` stamped via the domain `Deactivated(...)` method (not direct property assignment), and `GetServiceOverview` (`:20-23`) no longer returns it — so it disappears from the booking wizard while all past order links remain intact.

2. **Re-activate a Service**
   **Given** a retired (inactive) Service,
   **When** the admin activates it,
   **Then** `IsActive=true` again, the deactivation stamps are cleared, and the Service reappears in the customer booking catalog on the next load.

3. **Package parity**
   **Given** an active or retired Package,
   **When** the admin deactivates or activates it,
   **Then** the Package behaves identically to criteria 1–2 (history preserved, hidden from / restored to customer-facing surfaces that filter on `IsActive`).

4. **Admin list filter by status**
   **Given** the admin Service-management (and Package-management) list, which today shows all rows with a search box only,
   **When** the admin selects an Active / Inactive / All status filter,
   **Then** the paged result returns only matching rows (`GetPagedServices` passing `isActive` through `ServiceSpecification.Create(isActive:...)`), and each row visibly indicates whether it is Active or Retired.

5. **Deactivate is the default, hard-delete the exception**
   **Given** the existing hard-delete path (`DeleteService` / `DeletePackage`),
   **When** the admin chooses the primary "remove from catalog" action,
   **Then** the UI invokes deactivate (reversible, history-preserving) as the default, and the irreversible hard-delete is presented as a separate, clearly-labeled, confirmation-gated action.

6. **Permissions & translations**
   **Given** an authenticated admin,
   **When** they call activate/deactivate,
   **Then** the endpoints are guarded (Service: `CanUpdateService`; Package: `CanUpdatePackage`, matching the existing controllers), invalid IDs fail validation with a `BusinessErrorMessage` key (e.g. `ServiceNotFound` / `PackageNotFound`), and every new user-visible string (status labels, filter options, toggle, success/error toasts) has keys in all 5 locales (en, cs, sk, uk, ru).

## Out of scope

- Changing or removing the existing hard-delete commands/endpoints (`DeleteService`/`DeletePackage`) — they stay as the rare escape hatch; only their default-vs-exception framing in the UI changes.
- A cascade/dependency policy (e.g. auto-deactivating Packages when a contained Service is retired, or blocking deactivation of a Service still referenced by active Packages) — flag as a follow-up question for the PM/architect; this story deactivates the single targeted entity only.
- Bulk activate/deactivate, scheduled/time-boxed retirement, or "draft/coming-soon" states beyond the binary `IsActive`.
- Customer- or partner-app UI changes beyond the **already-existing** `IsActive` filtering in `GetServiceOverview` (no new customer-facing screens).
- Hiding retired offerings from other read surfaces not currently `IsActive`-filtered (audit those separately; not introduced here).
- EF Core migration authoring/applying and NSwag client regeneration — owner-only **manual steps** (`manual_step: nswag-regen`; new audit columns already exist/mapped, but confirm no migration is needed for the controllers/filter change).

## Layers touched

- **Domain** (`Cleansia.Core.Domain`): optional intent-revealing `Service.Deactivate/Activate` & `Package.Deactivate/Activate` wrappers over the inherited `Auditable.Deactivated(...)` plus an `Activate()` that re-sets `IsActive=true` and clears stamps (none exists today).
- **AppServices** (`Cleansia.Core.AppServices`): new `DeactivateService` / `ActivateService` / `DeactivatePackage` / `ActivatePackage` commands (handler calls `repo.Deactivate(...)` / domain method per B7) with validators; extend `ServiceFilter`/`PackageFilter` with `bool? IsActive` and pass it into `ServiceSpecification.Create(isActive:)` from `GetPagedServices`/`GetPagedPackages`.
- **Web.Admin** (`Cleansia.Web.Admin`): new activate/deactivate endpoints on `AdminServiceController` / `AdminPackageController` under `CanUpdateService` / `CanUpdatePackage`.
- **Frontend admin** (`Cleansia.App` — `cleansia-admin-features/service-management` & package-management): status toggle + active/inactive/all filter in the facade (`service-management.facade.ts`) and list models; reframe delete as the secondary destructive action.
- **i18n**: new keys in all 5 locale files for both admin apps' affected features; new `BusinessErrorMessage`/`errors.*` keys if introduced.
- **Owner manual steps**: NSwag client regeneration (`manual_step: nswag-regen`); verify whether a migration is required.


---

The field is in the generated admin client but not consumed by any admin feature UI yet — confirming it's a phantom contract. I have enough grounding. Here is the user story.

---

# US-admin-0007 — Record and surface admin "last login" so the phantom `LastLoginAt` field reflects reality

## Persona & Narrative

**As an** administrator managing the platform's admin/staff accounts,
**I want** each user's *last successful login* timestamp to be actually recorded and shown on the admin user list and detail screens,
**so that** I can identify dormant or potentially compromised accounts and make informed activate/deactivate decisions — instead of relying on a field that is always blank.

## Context / Why this exists (grounded in code)

The admin user contract already *promises* this data but can never deliver it:

- `Features/AdminUsers/DTOs/AdminUserDetailDto.cs:18` and `AdminUserListItem.cs:16` both declare `DateTimeOffset? LastLoginAt`.
- `Mappers/AdminUserMappers.cs:22` and `:41` hardcode `LastLoginAt: null` for both mappers.
- `Core.Domain/Users/User.cs` (read in full) has **no** last-login field and **no** mutator to set one — the closest temporal fields are `ResetPasswordCodeExpiresAt`, `ConfirmationCodeExpiresAt`, and the audit `CreatedOn`.
- The field has already propagated into the generated frontend contract (`libs/core/admin-services/.../admin-client.ts:11722, :11787, :11800, :11859`) but **no** admin feature module consumes it (no matches under `libs/cleansia-admin-features`) — so today it is a guaranteed-`null` value shipped end-to-end.
- Login is recorded **nowhere**: the token-issuing handlers `Features/Auth/AdminLogin.cs:83`, `Login.cs:78`, and `PartnerLogin.cs:80` all call `tokenService.GenerateTokenAsync(...)` and return, without touching the user entity. (`GoogleAuth.cs` and `RefreshToken.cs` are the other token paths.)

This violates `agents/knowledge/conventions.md:60` ("**No dead code**") and the "production-ready, long-term" bar (`conventions.md:113`). The story closes the gap by making the field real; the explicit alternative (delete the field) is captured under Out of Scope as the fallback decision the team may take instead.

## Acceptance Criteria

1. **Login is recorded on the user entity**
   **Given** an active user successfully authenticates through any password login path (`AdminLogin`, `Login`, `PartnerLogin`)
   **When** the handler issues a JWT via `tokenService.GenerateTokenAsync(...)`
   **Then** that user's new `LastLoginAt` field is set to the current UTC instant and persisted (through the existing UnitOfWork pipeline — no manual `CommitAsync()` in the handler), via a `User` domain mutator, not direct property assignment.

2. **Google login is also recorded**
   **Given** a user authenticates via Google (`Features/Auth/GoogleAuth.cs`)
   **When** a token is issued for that user
   **Then** `LastLoginAt` is updated the same way as for password logins.

3. **Detail DTO reflects the real value**
   **Given** an admin opens a user's detail
   **When** `MapToAdminDetailDto` runs (`AdminUserMappers.cs:25`)
   **Then** `LastLoginAt` is populated from `user.LastLoginAt` (no longer the hardcoded `null` at `:41`), returning `null` only for a user who has genuinely never logged in.

4. **List DTO reflects the real value**
   **Given** an admin views the paged admin-user list
   **When** `MapToAdminListItem` runs (`AdminUserMappers.cs:8`)
   **Then** each row's `LastLoginAt` is populated from `user.LastLoginAt` (no longer the hardcoded `null` at `:22`).

5. **A never-logged-in user is unambiguous**
   **Given** a user created (e.g. via `CreateAdminUser`) who has not yet logged in
   **When** their detail/list entry is returned
   **Then** `LastLoginAt` is `null`, and the field is documented as "no login recorded" rather than "feature not implemented."

6. **Token refresh does not masquerade as a fresh login** *(boundary clarity)*
   **Given** a user's client silently refreshes its token (`Features/Auth/RefreshToken.cs`)
   **When** a new access token is issued without a credential re-entry
   **Then** `LastLoginAt` is **not** advanced by the refresh (it tracks interactive logins, not token rotation) — unless the team explicitly decides otherwise during refinement.

## Out of Scope

- **Fallback decision — deletion instead of population.** If the team decides login tracking is intentionally deferred, the alternative is to **remove** `LastLoginAt` from both DTOs and both mappers and regenerate the admin client. This story assumes the "make it real" path; the delete path is a separate, smaller change and is not implemented here.
- **Admin UI rendering** of the column/field (adding a "Last login" column to the admin user table or a row on the detail page). The data becomes available; surfacing it visually is a follow-up frontend story.
- **Login history / audit trail** (multiple timestamps, IP, device, geo). Only the single most-recent successful-login timestamp is in scope.
- **Failed-login tracking, lockout, or anomaly detection.** Out of scope here.
- **Automated dormant-account flagging or auto-deactivation.** This story enables the human decision; it does not automate it.
- **Customer-app login timestamps surfaced to customers themselves.** Scope is the admin-facing contract.
- **EF Core migration authoring/execution.** Per project rules, the owner adds and applies migrations manually — this is a `manual_step`, not Claude's work.
- **NSwag client regeneration.** Owner-only `manual_step` once the DTOs/mappers change.

## Layers Touched

- **Domain** — `Cleansia.Core.Domain/Users/User.cs`: add `DateTimeOffset? LastLoginAt { get; private set; }` plus a `RecordLogin()` mutator (and include it in any relevant factory/anonymization considerations).
- **App Services / CQRS** — token-issuing handlers `Features/Auth/AdminLogin.cs`, `Login.cs`, `PartnerLogin.cs`, `GoogleAuth.cs` call the new mutator on the resolved user before/after token issuance; `Mappers/AdminUserMappers.cs` (lines 22, 41) read the real value.
- **Infrastructure / Database** — EF entity config + **migration (owner-run `manual_step`)** adding the `LastLoginAt` column to the users table.
- **Frontend (contract only)** — admin NSwag client (`libs/core/admin-services/.../admin-client.ts`) regenerated (owner-run `manual_step`). No admin-feature UI changes in this story.
- **Tests** — `Cleansia.Tests`: unit coverage that a login path sets `LastLoginAt` and that both mappers project it; assert refresh does not advance it (AC 6).

## Manual Steps (owner-only)

- `manual_step: ef-migration` — add `LastLoginAt` column to the users table and apply it.
- `manual_step: nswag-regen` — regenerate the admin TypeScript client after the DTO/mapper change (the field already exists in the generated client, but the contract semantics change from always-null to populated).


---

Confirmed: translations are a plain dictionary keyed by language code with no default/fallback lookup — a missing key yields nothing, hence the blank-name risk for any locale without a translation. I have full grounding. Here is the user story.

---

# US-admin-0007 — Default/fallback language so a 6th language doesn't break every service & package

**Type:** Story (Owner-decision-driven) · **Persona:** admin · **Stack:** backend (primary), frontend-admin, db, docs · **Blocking question:** yes (raise to `questions/open.md` before build)

## Context (grounded in code)

The platform advertises 5 languages but the `Language` entity is a bare `Code`/`Name` row with **no `IsDefault` and no fallback concept** (`src/Cleansia.Core.Domain/Internationalization/Language.cs:6-26`). Its sibling i18n entities both model a primary row: `Currency.IsDefault` + `SetAsDefault()` (`src/Cleansia.Core.Domain/Internationalization/Currency.cs:21,39-42`), and the codebase already has a canonical "set default" command pattern in `SetDefaultSavedAddress` (`src/Cleansia.Core.AppServices/Features/SavedAddresses/SetDefaultSavedAddress.cs:9-59`, which clears the prior default then sets the new one). Language is the odd one out.

Service translation validation requires an **exact set-equality** against *all* language codes — `allLanguageCodes.SetEquals(providedCodes)` (`src/Cleansia.Core.AppServices/Features/Services/CreateService.cs:67-74` and `UpdateService.cs:71-78`). Consequences confirmed in code:
- Adding a 6th `Language` via `CreateLanguage` just inserts a row (`src/Cleansia.Core.AppServices/Features/Languages/CreateLanguage.cs:41-51`) and **backfills nothing**. The very next edit of any existing service then fails the `SetEquals` check with `MissingTranslationForLanguage` — there is no migration path.
- Translations are a plain dictionary with **no fallback read** (`src/Cleansia.Core.Domain/Services/Service.cs:29-30,59-61`); a missing locale key resolves to nothing → risk of a blank service name in that locale.
- The rule is **inconsistent**: `CreatePackage`/`UpdatePackage` do *not* enforce `SetEquals` at all (`src/Cleansia.Core.AppServices/Features/Packages/CreatePackage.cs`), so Services are "all-or-nothing" while Packages are "whatever you send." This is a real consistency defect not yet recorded in `agents/backlog/audits/consistency-violations.md`.

This violates `conventions.md` §"production-ready, long-term" bar ("solve the root cause… preserve seams… config-driven variation") and the §Localization rule that wording/business-impact i18n decisions go to the owner via `questions/open.md` rather than being invented silently.

> **MANUAL_STEP (owner):** This story begins with an **owner decision** (Option A: introduce `Language.IsDefault` + a `SetDefaultLanguage` flow with fallback; **or** Option B: formally document translations as mandatory-for-all and define the add-a-language workflow). The chosen option determines the ACs below; the analyst's recommendation is **Option A** for consistency with Currency/Country and to remove the blank-locale risk. Also flag `manual_step: ef-migration` (new `IsDefault` column, default-true backfill for the seeded primary language) and `manual_step: nswag-regen` once Language DTOs change.

## Actor narrative

**As an** admin managing the platform's languages and catalog,
**I want** one language marked as the default/fallback so that adding or removing a language never silently invalidates existing services and packages, and every locale always resolves a name,
**so that** the catalog stays editable and customer-facing text is never blank when a translation is missing.

## Acceptance Criteria (Given/When/Then)

*(Authored against recommended Option A; if the owner picks Option B, AC2–AC4 are replaced by a documented mandatory-completeness workflow + a backfill flow for new languages.)*

1. **Exactly one default exists**
   Given the language list, When the admin opens it, Then exactly one language is flagged as default; and When the admin sets a different language as default, Then the previous default is cleared and only the newly chosen one is default (mirroring `SetDefaultSavedAddress` clear-then-set).

2. **Default cannot be left in an invalid state**
   Given a language is the current default, When the admin attempts to delete it, Then the operation is rejected with a localized `BusinessErrorMessage` code (parallel to `DeleteCurrency` guarding `IsDefault`), and the language remains the default until another is promoted first.

3. **Adding a language does not break existing catalog edits**
   Given existing services and packages translated in the current languages, When the admin adds a new language, Then saving an unrelated edit to any existing service or package still succeeds (translations for the new language are not retroactively required), and the new language appears in the per-language translation editor as empty-but-optional.

4. **Default-language translation is the enforced minimum; others fall back**
   Given a service or package is created or updated, When the submitted translations include a non-empty entry for the default language, Then the save succeeds even if some non-default languages are omitted; and When a locale has no translation at read time, Then the API returns the default-language text for that field instead of an empty/blank value.

5. **Services and packages enforce the same rule**
   Given the completeness rule chosen for the default language, When a service is saved and when a package is saved, Then both apply the identical validation (removing today's Services-only `SetEquals` divergence), with the same `BusinessErrorMessage` code surfaced.

6. **Decision is captured, not assumed**
   Given this is an owner decision, When work starts, Then a `blocking: yes` entry exists in `agents/backlog/questions/open.md` stating Option A vs B, and the resolved choice is recorded in an ADR before any handler changes are merged.

## Out of scope

- **Auto-translation / machine translation** of missing locales — fallback returns the *default-language* text verbatim, it does not translate.
- **Adding or removing the actual 5 (or 6th) languages** as data — this story delivers the mechanism, not a catalog change.
- **`Country` and `Currency`** default handling — already modeled; not modified here beyond being cited as the precedent.
- **Email template translations** (`EmailTemplateTranslation`, `EmailTranslation`) — separate translation surface, not part of the Service/Package catalog rule.
- **Frontend i18n UI-string files** (`assets/i18n/*.json`) — this is about *catalog data* translations, not the app shell's static strings (only the new error key must be added to all 5 locales).
- **Mobile apps** — they consume the catalog API; no Kotlin change beyond inheriting the fallback API behavior. No new mobile screens.
- **Per-tenant default language** — single platform default for now; multi-tenant override is a follow-up if the owner requests it.

## Layers touched

- **Backend (primary):** `Language` entity (`IsDefault` + a `SetAsDefault`-style method), a `SetDefaultLanguage` command/handler/validator (pattern from `SetDefaultSavedAddress`), guards in `DeleteLanguage`, and aligned translation-completeness validation in `CreateService`/`UpdateService` **and** `CreatePackage`/`UpdatePackage`; new `BusinessErrorMessage` code(s) + 5-locale `errors.*` keys; fallback read in the Service/Package translation getter or mapper.
- **DB:** `manual_step: ef-migration` — add `Language.IsDefault` (non-null, default false) and backfill the seeded primary language to `true`.
- **Frontend-admin:** languages list/detail — a "Set as default" action and a default badge (reusing `cleansia-table`/`TableAction`); `manual_step: nswag-regen` after Language DTOs change.
- **Docs:** ADR recording Option A/B; update i18n/architecture notes on default-language fallback semantics.

## Files cited (audit evidence — no code changed)

- `src/Cleansia.Core.Domain/Internationalization/Language.cs:6-26` — no `IsDefault`/fallback
- `src/Cleansia.Core.Domain/Internationalization/Currency.cs:21,39-42` — `IsDefault` + `SetAsDefault()` precedent
- `src/Cleansia.Core.AppServices/Features/SavedAddresses/SetDefaultSavedAddress.cs:9-59` — canonical clear-then-set default command
- `src/Cleansia.Core.AppServices/Features/Services/CreateService.cs:67-74`, `UpdateService.cs:71-78` — `SetEquals` all-languages rule
- `src/Cleansia.Core.AppServices/Features/Packages/CreatePackage.cs` — **no** equivalent completeness rule (the inconsistency)
- `src/Cleansia.Core.AppServices/Features/Languages/CreateLanguage.cs:41-51` & `DeleteLanguage.cs:26-28` — add-row-only (no backfill); `IsInUse` delete guard exists but no default guard
- `src/Cleansia.Core.Domain/Services/Service.cs:29-30,59-61` — translation dictionary with no fallback read (blank-locale risk)
- Convention basis: `agents/knowledge/conventions.md` §Localization + §"production-ready, long-term"; `agents/knowledge/consistency.md` §B (commands) — not yet in `agents/backlog/audits/consistency-violations.md`.


---

I now have full grounding for the analysis. Let me synthesize the findings into the user story.

Key facts established from the code:
- The batch handler (`LookupOrderBatch.cs:23`) **does** cap at 10 and **does** filter per-item by email (`:42-47`). So the original GAP framing ("must enforce... a length cap; otherwise enumeration surface") is partially already satisfied — but the audit must verify the *secret semantics*, where I found a real discrepancy.
- The single `LookupOrder` matches on `DisplayOrderNumber` (`LookupOrder.cs:52`) — the human-facing `ORD-XXXXXXXX` shared secret the customer receives.
- The batch handler matches on `o.Id` (`LookupOrderBatch.cs:38,47`) — the internal **Ulid** primary key (`BaseEntity.cs:5`), which is also returned as `detail.Id` in every lookup response and cached client-side keyed by it. So the two variants key on **different fields**, and the batch leaks/consumes the internal id rather than the order number.
- Batch silently returns empty `Response([])` on a cap breach / empty input (`:24`) instead of a validation error — no `Validator` class exists for the batch query (violates the convention that validation lives in `Validator` classes, and the silent-success masks the cap from clients/tests).
- Both anonymous routes are rate-limited `"interactive"`. Per S3, an `[AllowAnonymous]` route returning tenant-scoped data must be gated by a shared secret in every item — confirmed present (email), but the *order-identifier half* of the secret pair diverges from the single-lookup contract.

Here is the user story.

---

# US-customer-0042 — Harden anonymous batch order lookup to match the single-lookup shared-secret contract

## Context (grounded in code)

- `src/Cleansia.Web.Customer/Controllers/OrderController.cs:29-37` — `[AllowAnonymous]` `POST api/Order/LookupBatch`, rate-limited `"interactive"`, sends `LookupOrderBatch.Query` straight to MediatR with no controller-side enrichment.
- `src/Cleansia.Core.AppServices/Features/Orders/LookupOrderBatch.cs`
  - `:12` `OrderLookupItem(string OrderId, string Email)` — the per-item secret pair.
  - `:23` caps the batch at 10 items (`> 10` short-circuits).
  - `:38` and `:47` match on **`o.Id`** (the internal `Ulid` primary key — see `BaseEntity.cs:5`) plus a case-insensitive email check.
  - `:24` returns `BusinessResult.Success(new Response([]))` for empty input **and** for a cap breach — a silent success, not a validation failure. There is no `Validator` class for this query.
- `src/Cleansia.Core.AppServices/Features/Orders/LookupOrder.cs:52` — the single lookup matches on **`DisplayOrderNumber`** (`ORD-XXXXXXXX`, the human-facing code the customer is given) plus email.

The divergence: the single lookup's shared secret is `(DisplayOrderNumber, Email)`; the batch's is `(internal Ulid Id, Email)`. The internal Ulid is also returned in every lookup `Response.Id` and cached client-side keyed by it (`guest-order-lookup-cache.service.ts:21`). Two anonymous variants of the *same* feature key on **different identifiers**, so they do not enforce the same shared-secret contract. Per **S3**, anonymous routes carry no tenant claim and bypass the global tenant filter, so they "must not return tenant-scoped data unless gated by a different shared secret" — the batch is gated, but on a non-canonical, machine-generated identifier rather than the order number the customer actually holds, and the cap is enforced via a silent empty success rather than observable validation.

## User story

**As a** customer who has looked up several of my orders as a guest (no account / no tenant claim),
**I want** the batch order-lookup endpoint to enforce exactly the same per-item shared-secret contract as the single lookup — the same order-identifier field, the same email check, and an explicit, observable batch-size limit —
**so that** my orders are retrievable in one call without the batch variant becoming a weaker, inconsistent enumeration surface over tenant-scoped data than the single-order lookup it mirrors.

## Acceptance criteria

1. **Given** the single lookup matches on `DisplayOrderNumber` and the batch matches on `o.Id`, **When** the two anonymous lookup handlers are compared, **Then** the team decides and documents a single canonical order-identifier field for both, and `LookupOrderBatch` matches on that same field so the `(orderIdentifier, email)` secret pair is identical across both variants.

2. **Given** a batch request, **When** any item's email does not match the order's `CustomerEmail` (case-insensitive), **Then** that order is excluded from the response (no row returned), and **no** order is ever returned for an item whose email does not match — confirmed by a test asserting a non-matching email yields zero results for that item while a sibling matching item still resolves.

3. **Given** a batch request whose item count exceeds the cap (currently 10) or is empty/null, **When** the handler runs, **Then** the cap is enforced and the outcome is observable to the client and to tests (e.g. a `Validator`-produced `BusinessResult.Failure` / 400 with a stable error key, rather than the current silent `Success(Response([]))` that is indistinguishable from "no orders matched").

4. **Given** a batch containing both items the caller is entitled to and items they are not, **When** the handler responds, **Then** the response contains **only** the entitled (email-matched) orders, and the count/shape of the response never reveals whether a non-entitled order id exists (no existence leak; consistent with the S3 NotFound-not-Forbidden stance).

5. **Given** the `[AllowAnonymous]` `LookupBatch` route, **When** it is called, **Then** it remains rate-limited (`[EnableRateLimiting("interactive")]`) and returns no field beyond what the single `LookupOrder.Response` already exposes (no `UserId`, `TenantId`, or other-customer PII — re-verified against S4).

6. **Given** the chosen identifier change affects the request DTO consumed by the customer web app and the generated Android client, **When** the contract changes, **Then** the change is flagged as a `manual_step: nswag-regen` (and any client model regen for Android) so callers are updated in lockstep and stale generated DTOs do not break deserialization (S9).

## Out of scope

- The single `LookupOrder` endpoint's existing behaviour, beyond being the reference contract the batch must match (no functional change to single lookup unless the canonical-identifier decision in AC-1 requires it).
- Authenticated order endpoints (`GetMyOrders`, `GetById`, `GetPaged`, `DownloadReceipt`, etc.) — those are policy-gated and out of this story.
- The broader audit findings (admin order intervention, payroll settlement lifecycle, hardcoded `CancelOrder` actor) — separate stories.
- Changing the rate-limit *window* values, adding CAPTCHA, or adding account-based auth to the lookup flow.
- Tenant-filter mechanics for authenticated paths (global query filter behaviour is unchanged; this story only concerns the anonymous, claim-less path).
- Any redesign of `GuestOrderLookupCacheService` beyond what the AC-1 identifier decision mechanically requires.

## Layers touched

- **Backend — AppServices/CQRS:** `LookupOrderBatch.cs` (identifier match + add a FluentValidation `Validator` for cap/empty, `Cascade.Stop`), possibly its `Query`/`OrderLookupItem` DTO shape; new `BusinessErrorMessage` key + 5-locale `errors.*` translations if a validation failure is surfaced.
- **Backend — Web.Customer:** `OrderController.cs` (no logic change expected; confirm `[AllowAnonymous]` + rate-limit are retained; possibly add `ProblemDetails` 400 `ProducesResponseType`).
- **Contracts / clients (owner-only, flagged):** NSwag customer client + Android generated model — `manual_step: nswag-regen`.
- **Frontend — customer-features:** `order-lookup.component.ts`, `guest-order-lookup-cache.service.ts`, `guest-order-detail.component.ts` if the batch request now keys on a different identifier.
- **Tests:** xUnit handler/validator tests for AC-2 / AC-3 / AC-4.

**Auditor note (not part of the story, for the order-subsystem audit queue):** the GAP's two stated risks resolve as — (a) **batch cap**: already enforced at `LookupOrderBatch.cs:23` but via a *silent* `Success([])`, which AC-3 upgrades to observable validation; (b) **per-item secret**: present (email) but keyed on the internal `Ulid` `Id` rather than the `DisplayOrderNumber` used by single lookup — a real contract inconsistency, captured by AC-1. No code was changed.


---

I have everything I need. The `AdminGdprClient` is already generated in `admin-client.ts` (4 methods at lines 4594, 4648, 4698, 4761), the four `AdminOnly` policies exist in both backend and frontend mirror, the four handlers + DTOs exist, the audit/consent entities exist — and there is zero admin feature lib consuming any of it. The story is purely a frontend feature (no backend or NSwag work needed, since the client is already generated).

Here is the grounded user story.

---

# US-admin-0042 — Admin "Data Protection" back-office (GDPR requests, consents, export & erasure)

## Context (grounded in code)

The platform already records a GDPR audit surface and exposes an admin API for it, but **no admin UI consumes it**:

- **Endpoints exist** — `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs:15-49`: `GET export/{userId}`, `POST delete-account/{userId}`, `GET consents/{userId}`, `GET requests` (paged), each gated by `Policy.CanAdminExportUserData` / `CanAdminDeleteUserAccount` / `CanAdminViewUserConsents` / `CanViewGdprRequests` (all map to `PhysicalPolicy.AdminOnly`, confirmed in `libs/core/services/src/lib/auth/policy.ts:299-302`).
- **Handlers + DTOs exist** — `Features/Gdpr/GetAllGdprRequests.cs`, `AdminGetUserConsents.cs`, `AdminExportUserData.cs`, `AdminDeleteUserAccount.cs`; DTOs `GdprRequestDto`, `UserConsentDto`, `GdprExportDto`.
- **Audit data is being written** — `AdminExportUserData.Handler` creates a `GdprRequest` audit row (Article-30 style) before building the export and marks it Completed/Failed (`AdminExportUserData.cs:39-52`); `GdprRequest` (`Core.Domain/Users/GdprRequest.cs`) and `UserConsent` (`UserConsent.cs`) persist with `GdprRequestStatus` {Pending, Processing, Completed, Failed} and `ConsentType` {TermsOfService, PrivacyPolicy, MarketingEmails, DataProcessing}.
- **The generated client already exposes them** — `AdminGdprClient` in `libs/core/admin-services/src/lib/client/admin-client.ts:4580` (methods at lines 4594, 4648, 4698, 4761). **No NSwag regeneration is required.**
- **Nothing consumes any of it** — `libs/cleansia-admin-features/` has 20 feature libs; none is GDPR/data-protection, and a repo-wide search for the four handler names returns only the policy mirror, never a facade or component.

Result: a DPO/admin cannot view the GDPR request log, fulfil a data-subject access request (export) for another user, inspect a user's consents, or erase an account from the back office — a compliance-blocking gap for an EU PROD launch where the data and API already exist.

## Actor narrative

**As an** admin / Data Protection Officer,
**I want** a "Data Protection" section in the admin app where I can browse the GDPR request audit log, inspect any user's consent history, and run an export or account erasure for a given user,
**so that** I can fulfil and evidence data-subject rights (access, erasure, consent transparency) under GDPR without direct database access.

## Acceptance criteria (Given / When / Then)

1. **GDPR request audit list (paged)**
   **Given** I am an admin with `CanViewGdprRequests` and I open the Data Protection page,
   **When** the list loads,
   **Then** a `cleansia-table` shows GDPR requests from `GET /AdminGdpr/requests` with columns User Id, Request Type, Status (translated `GdprRequestStatus`), Processed By, Completed At, Notes, Created On, server-side paged (offset/limit), newest first — and the table renders distinct empty, loading, and loaded states.

2. **Per-user consent viewer**
   **Given** I am an admin with `CanAdminViewUserConsents` and I supply/select a user,
   **When** I open that user's consents,
   **Then** the list from `GET /AdminGdpr/consents/{userId}` shows each `ConsentType` (translated) with Granted/Withdrawn state, Granted At, Withdrawn At, and Created On.

3. **Admin export (data-subject access request)**
   **Given** I am an admin with `CanAdminExportUserData`,
   **When** I trigger "Export data" for a user and the call to `GET /AdminGdpr/export/{userId}` succeeds,
   **Then** I receive the `GdprExportDto` payload as a downloadable artifact and see a success confirmation; **and** a new `GdprRequest` row (type Export) subsequently appears in the requests list (proving the audit trail), reflecting the handler that already writes it.

4. **Admin account erasure (gated + confirmed)**
   **Given** I am an admin with `CanAdminDeleteUserAccount`,
   **When** I choose "Erase account" for a user,
   **Then** a `ConfirmationService` dialog requires explicit confirmation before `POST /AdminGdpr/delete-account/{userId}` is called, a success confirmation is shown on completion, and the action is hidden when I lack the policy.

5. **Permission gating end-to-end**
   **Given** an admin missing one of the four GDPR policies,
   **When** the page renders,
   **Then** each control is hidden via `*cleansiaPermission="Policy.CanXxx"` (list, consents, export, erase gated independently), and any API 403 surfaces through `SnackbarService.showApiError` rather than crashing the view.

6. **Consistency & i18n compliance**
   **Given** the feature is implemented,
   **When** reviewed against C1–C8,
   **Then** the facade extends `UnsubscribeControlDirective` (C1), state is signals with `loading`/`initialLoading`/`totalRecords` (C2), every client call uses the `takeUntil → catchError(of(null)) → finalize` pipe (C3), errors go through `SnackbarService` (C4), paging is server-side only (C5), the table is one `getGdprRequestsTableDefinition()` returning `{ columns, actions }` (C6), the component is `standalone` + `OnPush` exposing `protected readonly Policy` (C7), and no NgRx is introduced for this single-feature state (C8); **and** every user-visible string (including new enum labels and statuses) has keys in all five locales (en, cs, sk, uk, ru).

## Out of scope

- **No backend changes.** Handlers, DTOs, controller, and the four `AdminOnly` policies already exist and are correct; do not modify `Features/Gdpr/*` or `AdminGdprController.cs`.
- **No NSwag regeneration.** `AdminGdprClient` is already present in `admin-client.ts`; this is consume-only. (If, contrary to expectation, a method is found missing, flag `manual_step: nswag-regen` — do not hand-edit the client.)
- **No new audit/consent semantics.** Do not add request statuses, consent types, transitions, or change what `GdprDeletionService`/`GdprExportService` do.
- **Customer/partner self-service GDPR** (`GdprController` on customer/partner/mobile APIs, the customer/partner `gdpr` feature libs) — already shipped; untouched.
- **Filtering/search/sorting beyond paging** on the requests list, CSV/PDF formatting of the export beyond returning the existing `GdprExportDto`, retention scheduling, and bulk operations — future enhancements.
- **A `GdprRequest`-detail drill-down screen** and any new "reprocess/retry failed request" action (the API exposes no such endpoint).
- **EF Core migrations** — none required; entities already exist.

## Layers touched

- **Frontend only.** New admin feature lib `libs/cleansia-admin-features/data-protection/` (component + facade + `*.models.ts` + `lib.routes.ts`), wired into the admin app routing/nav, following the `employee-management` structure.
- **Consumes existing** `AdminGdprClient` (`libs/core/admin-services`) and existing `Policy` constants (`libs/core/services/src/lib/auth/policy.ts`).
- **i18n:** new keys in `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` (feature labels + `GdprRequestStatus`/`ConsentType` translations).
- **Backend / NSwag / DB:** **untouched.**


---

This is the final confirming detail. The Partner host validates `ValidAudience = JwtAudiences.Partner` (line 146) and registers `AdminOnly` requiring the Administrator/Admin role (lines 194-195, 209-210), but the Partner host only issues Partner-audience tokens to Employee-profile users via its `AuthController`. The CustomerOnly policy isn't even registered in this Partner host's `AddUserAuthorization` (only Authenticated, EmployeeOrAdmin, AdminOnly, OwnerOrElevated are registered) — so the duplicated `CanCreateDispute`/`CanViewDispute`/`CanViewDisputeList` routes (CustomerOnly) would fall back / fail to authorize and are dead. And `CanRespondToDispute` → Authenticated (the registered default-ish policy) is the only one any Partner Employee can hit — the SEC-DSP-01 amplification path.

I now have everything grounded. Writing the user story.

---

# US-admin-0042 — Relocate admin dispute operations to the Admin host and remove the dead/duplicated dispute surface from the Partner host

## Context (grounded in code)

The Partner API host (`Cleansia.Web.Partner`) carries a full `DisputeController` (`src/Cleansia.Web.Partner/Controllers/DisputeController.cs:1-89`) whose routes are, in practice, **dead or dangerous**:

- **Admin-only mutations on a host with no Admin principal.** `Resolve` (`:66-76`) and `UpdateStatus` (`:78-88`) require `Policy.CanResolveDispute` / `Policy.CanUpdateDisputeStatus`, which map to `PhysicalPolicy.AdminOnly` (`PolicyBuilder.cs:77-78`). The Partner host validates `ValidAudience = JwtAudiences.Partner` (`ServiceExtensions.cs:146`) and only issues Partner-audience tokens to Employee-profile users; `AdminOnly` requires the Administrator role (`ServiceExtensions.cs:209-210`), which is never present. These routes are **unreachable**.
- **Customer-only routes duplicated from the Customer host.** `Create` / `GetById` / `GetPaged` (`:18-52`) require `CanCreateDispute` / `CanViewDispute` / `CanViewDisputeList` → `PhysicalPolicy.CustomerOnly` (`PolicyBuilder.cs:73-75`). `CustomerOnly` isn't even registered in the Partner host's `AddUserAuthorization` (`ServiceExtensions.cs:200-231` registers only Authenticated/EmployeeOrAdmin/AdminOnly/OwnerOrElevated). They are **dead duplicates** of `Cleansia.Web.Customer/Controllers/DisputeController.cs`.
- **The one live route is the SEC-DSP-01 hole.** `AddMessage` (`:54-64`) requires `CanRespondToDispute` → `PhysicalPolicy.Authenticated` (`PolicyBuilder.cs:76`), so **any** authenticated Partner Employee can call it. The handler trusts a client-supplied `IsStaffMessage` flag and skips the ownership check when it is `true` (`AddDisputeMessage.cs:34-54`), so a partner can post a staff message into **any** dispute — exploiting SEC-DSP-01 from the Partner host.
- **No admin home exists.** The Admin host (`Cleansia.Web.Admin/Controllers/`) has **no** dispute controller at all, so legitimate admin dispute resolution is currently unreachable anywhere.

The latent risk: if an Admin role is ever issued on the Partner host's token issuer, `Resolve`/`UpdateStatus` silently go live with no further review.

## User story

**As an** admin (platform support operator),
**I want** dispute resolution and status-change operations to live on — and only be reachable through — the Admin API, with the Partner API exposing no dispute endpoints,
**so that** I can actually resolve and re-status customer disputes through an authorization surface that matches who is allowed to use it, and partners can no longer reach (or amplify exploits against) dispute endpoints that were never meant for them.

## Acceptance criteria (Given/When/Then)

1. **Admin can resolve a dispute on the Admin host.**
   **Given** an admin authenticated against the Admin API and an existing dispute,
   **When** the admin calls the Admin-host dispute *Resolve* endpoint with a valid command,
   **Then** the dispute is marked resolved and a `200`/success `BusinessResult` is returned, gated by `Policy.CanResolveDispute` (AdminOnly).

2. **Admin can update dispute status on the Admin host.**
   **Given** an admin authenticated against the Admin API and an existing dispute,
   **When** the admin calls the Admin-host dispute *UpdateStatus* endpoint,
   **Then** the dispute status changes and a success result is returned, gated by `Policy.CanUpdateDisputeStatus` (AdminOnly).

3. **Partner host exposes no dispute routes.**
   **Given** the Partner API is running,
   **When** any client requests `POST /api/Dispute/Create`, `GET /api/Dispute/GetById/{id}`, `GET /api/Dispute/GetPaged`, `POST /api/Dispute/AddMessage`, `POST /api/Dispute/Resolve`, or `POST /api/Dispute/UpdateStatus`,
   **Then** the Partner API returns `404 Not Found` (the routes no longer exist), and the Partner Swagger document lists no `Dispute` operations.

4. **A non-admin (partner Employee) cannot resolve or re-status disputes anywhere.**
   **Given** a partner Employee with a valid Partner-audience token,
   **When** they attempt to reach any dispute resolve/status endpoint on the Admin host,
   **Then** the request is rejected with `401`/`403` (wrong audience / not AdminOnly) and no dispute state changes.

5. **The Partner-host SEC-DSP-01 amplification path is closed.**
   **Given** a partner Employee authenticated on the Partner host,
   **When** they attempt to add a staff message to a dispute they do not own (the former `AddMessage` route),
   **Then** there is no Partner-host endpoint to call (request returns `404`), eliminating the cross-host `IsStaffMessage` ownership-bypass surface from the Partner API.

6. **Customer dispute behavior is unchanged.**
   **Given** a customer authenticated on the Customer host,
   **When** they create / view / list / message / upload-evidence on their own disputes,
   **Then** behavior is identical to before this change (the Customer host `DisputeController` remains the sole customer-facing dispute surface).

## Out of scope

- **Fixing SEC-DSP-01 itself** (the `IsStaffMessage` client-trust / ownership-bypass logic in `AddDisputeMessage.cs:34-54`). This story only removes the Partner-host *exposure*; the underlying handler defect is tracked separately and must be fixed before any host re-enables staff messaging. Re-confirm `AddMessage` stays removed/locked on the Partner host once SEC-DSP-01 is closed.
- Any new admin dispute UI/screen in the admin web app or its data-access store beyond wiring the two relocated endpoints into the existing admin API client surface.
- Changes to dispute domain logic, notifications, or evidence upload flow (`UploadEvidence` is Customer-host only and stays there).
- Adding an Admin role to the Partner host token issuer (explicitly **not** desired — host audience segregation is the control being preserved).
- Mobile dispute surface (`Cleansia.Web.Mobile.Customer/Controllers/DisputeController.cs`) — unaffected; verify only that it is not regressed.
- Broader policy-map audit of other features; this story is dispute-scoped.

## Layers touched

- **Backend / API hosts:** add an `AdminDisputeController` on `Cleansia.Web.Admin` exposing Resolve + UpdateStatus (AdminOnly); delete `Cleansia.Web.Partner/Controllers/DisputeController.cs`. No change required to `PolicyBuilder`/`Policy` mappings (existing `CanResolveDispute`/`CanUpdateDisputeStatus` → AdminOnly are reused on the correct host).
- **Backend / AppServices:** none expected — reuses existing `ResolveDispute` / `UpdateDisputeStatus` commands in `Cleansia.Core.AppServices/Features/Disputes`.
- **Contract / generated clients:** Partner OpenAPI loses `Dispute` operations and Admin OpenAPI gains them → **`manual_step: nswag-regen`** for the partner and admin clients (owner-only; do not run `generate-*-client`).
- **Frontend (admin app):** wire the relocated endpoints into the admin dispute facade/data-access if/where an admin dispute view consumes them (verification that nothing references the removed Partner client methods).
- **Tests:** xUnit coverage asserting Partner host returns 404 for all former dispute routes, and Admin host enforces AdminOnly on Resolve/UpdateStatus.

**Reviewer note (cited rules):** authorization mapping is governed by `src/Cleansia.Core.AppServices/Authentication/Policy.cs` + `PolicyBuilder.cs` (the dispute block, lines 73-79) and the `PhysicalPolicy` role semantics (`PhysicalPolicy.cs:5-10`); per the root `CLAUDE.md`, NSwag regeneration and EF migrations are owner-only and must be flagged as `MANUAL_STEP`, and no client files may be hand-edited.


---

I now have everything I need. The repository interface even documents the gap ("admins manage future plans through a back-office surface that doesn't exist yet"). The admin sidebar has no Membership entry. The `Loyalty` group is the natural sibling. Policies are declared in `Policy.cs` + mapped in `PolicyBuilder.cs` — new ones needed. The repo needs a `GetPagedAsync`/`GetByIdAsync` analog (only `GetByCodeAsync`/`GetActivePlansAsync` exist today).

Here is the grounded user story.

---

# US-admin-0014 — Manage Membership Plans from the Admin back office

**Status:** Ready for refinement
**Source GAP:** `MembershipPlan` is a full domain entity with `Create` / `UpdatePricing` / `UpdateBenefits` / `Deactivate` behavior (`src/Cleansia.Core.Domain/Memberships/MembershipPlan.cs:122-167`), seeded with two plans carrying placeholder Stripe price ids (`sql-scripts/insert_seed_data.sql:2436-2470`, comment: *"StripePriceId values are placeholders — replace with the actual Price ids ... before deploying"*). All membership feature code under `src/Cleansia.Core.AppServices/Features/Memberships/` is customer-facing (`GetMembershipPlans`, `GetMyMembership`, `SwapMembershipPlan`, `CreateMembershipSubscription`, `CreateMembershipCheckoutSession`). There is **no** `Features/Memberships/Admin/*`, **no** `AdminMembershipController` (confirmed absent from `src/Cleansia.Web.Admin/Controllers/`), and **no** admin sidebar entry (`apps/cleansia-admin.app/src/app/app.component.ts:85-131`). `IMembershipPlanRepository` itself documents the gap: *"admins manage future plans through a back-office surface that doesn't exist yet"* (`src/Cleansia.Core.Domain/Repositories/IMembershipPlanRepository.cs:5-10`).

## Actor narrative

**As an** admin (Cleansia operations / pricing owner),
**I want** a back-office screen to list, create, edit, and deactivate Membership Plans — including pricing, billing interval, discount %, trial length, free-cancellation window, express-upgrade benefit, and the Stripe Price id —
**so that** I can launch a Plus plan, run a price change or promo trial, deactivate a retired plan, and replace the placeholder Stripe price ids myself, without engineering effort or hand-editing the production database.

## Acceptance criteria (Given/When/Then)

1. **Plan list is visible**
   **Given** I am an authenticated admin with the membership-view permission,
   **When** I open the Memberships entry in the admin sidebar,
   **Then** I see a paged table of all membership plans (active and inactive) showing Code, Name, billing interval, price (CZK), monthly-equivalent price, discount %, trial days, free-cancellation window, express-upgrade flag, and active status — and an admin without the permission neither sees the nav entry nor can reach the route.

2. **Create a plan with an explicit Stripe Price id**
   **Given** I have created the Stripe Product/Price in the Stripe dashboard and copied the Price id,
   **When** I submit the create form with a unique Code, Name, price, billing interval, discount %, free-cancellation window, trial days, express-upgrade flag, and that `StripePriceId`,
   **Then** a new active plan is persisted, it appears in the list, and the customer plan switcher (`GetMembershipPlans`) immediately offers it — with no SQL or code change required.

3. **Duplicate code is rejected with a localized error**
   **Given** a plan with code `PLUS_MONTHLY` already exists in my tenant,
   **When** I try to create another plan with the same code (case-insensitively; codes are upper-cased),
   **Then** the request is rejected with a `BusinessErrorMessage` code that resolves to a translated message in all five locales, and no row is created.

4. **Edit pricing and benefits, including fixing the placeholder Stripe id**
   **Given** a plan exists with a placeholder `StripePriceId`,
   **When** I update its price, Stripe Price id, discount %, free-cancellation window, trial days, and express-upgrade flag and save,
   **Then** the changes persist (via the entity's `UpdatePricing` / `UpdateBenefits` methods), the audit fields (`UpdatedBy`/`UpdatedOn`) are stamped, and the new pricing/benefits are reflected on the next customer plan fetch.

5. **Deactivate a plan**
   **Given** an active plan that I no longer want to offer,
   **When** I deactivate it,
   **Then** `IsActive` becomes false (via `Deactivate()`), it disappears from the customer-facing active-plans list (`GetActivePlansAsync`), but existing `UserMembership` rows referencing it keep working — consistent with the entity's soft-delete contract.

6. **Invalid input is blocked before persistence**
   **Given** the create or edit form,
   **When** I submit a negative price, a discount % outside 0–100, a negative trial or cancellation window, an empty `StripePriceId`, or an out-of-range billing interval,
   **Then** validation fails with field-level localized errors (FluentValidation, `Cascade.Stop`) and nothing is written.

## Out of scope

- **Stripe Product/Price creation/registration.** `StripePriceId` is captured as an explicit admin-entered text input; the platform does **not** call the Stripe API to create products/prices. (Owner step — the seed comment and `MembershipPlan.Create` doc both state the caller registers the Stripe Price first.)
- Migrating or re-pricing **existing active subscriptions** when a plan's price changes (Stripe remains source of truth for active subscriptions; reprice/grandfather logic is a separate story).
- The **monthly↔yearly upgrade/swap path** (`SwapMembershipPlan`) and any customer subscribe/checkout flow — already implemented, untouched here.
- **Membership benefit-usage tracking** (express-upgrade caps), referenced as "future" in the entity docs.
- **Editing the immutable `Code`** of an existing plan (Code is the stable in-code reference; treat as create-only).
- **Multi-currency** plan pricing — `MonthlyPriceCzk` stays CZK-only as today.
- **Editing the DB seed file** (`insert_seed_data.sql`) — owner-only per conventions; this story replaces seed edits with a UI, it does not modify the seed.

## Layers touched

- **Domain** — none (entity, factory, and mutators already exist). May add read methods to `IMembershipPlanRepository` (a paged/by-id lookup; today it only exposes `GetByCodeAsync` + `GetActivePlansAsync`) and their `MembershipPlanRepository` implementation.
- **AppServices** — new `Features/Memberships/Admin/`: `GetPagedMembershipPlans` (query + filter), `GetMembershipPlanById` (query + detail DTO), `CreateMembershipPlan`, `UpdateMembershipPlan`, `DeactivateMembershipPlan` (commands + validators + responses), plus a mapper and any new `BusinessErrorMessage` codes (e.g. duplicate code / not found). Follow the `Features/PromoCodes/Admin/*` archetype exactly.
- **Authentication** — new `Policy` constants (`CanViewMembershipPlans`, `CanCreateMembershipPlan`, `CanUpdateMembershipPlan`, `CanDeactivateMembershipPlan`) in `Policy.cs` and their `AdminOnly` mapping in `PolicyBuilder.cs`.
- **Web.Admin** — new `AdminMembershipController` mirroring `AdminPromoCodeController` (`get-paged` / `details/{id}` / `create` / `update/{id}` / `deactivate/{id}`), `[Permission(...)]`-guarded, returning `PagedData<T>` / `HandleResult<T>`.
- **Frontend (admin app)** — new feature lib under `cleansia-admin-features` (list + create/edit form, facade + signals, `cleansia-table`/`cleansia-*` controls), route, generated-client method calls (no hand-editing), `*cleansiaPermission` gating, and a sidebar entry in `app.component.ts` (natural sibling of the existing `Loyalty`/`Marketing` groups).
- **i18n** — new `sidebar.*`, page, form-label, and `errors.*` keys in all five locales (`en, cs, sk, uk, ru`).
- **Tests** — xUnit for the validators and create/update/deactivate handlers (duplicate-code, soft-delete, range validation); facade-logic tests on the frontend.

## Manual steps (owner-only, flag — do not run)

- `manual_step: nswag-regen` — new `AdminMembershipController` endpoints change the admin OpenAPI contract; regenerate the admin TypeScript client before the frontend work.
- `manual_step: stripe-price-registration` — owner creates the real Stripe Product/Price and supplies the Price id entered in the admin form (replaces the placeholders at `insert_seed_data.sql:2453,2467`).
- No EF migration expected — the `MembershipPlans` table and all columns already exist; flag `manual_step: ef-migration` only if a repository read path forces a schema/index change (not anticipated).


---

No existing stories, so `US-admin-0001` is the first. The persona for "lookup-any-user-by-id is an admin/support capability" is **admin**. Here is the grounded user story.

---

```markdown
---
id: US-admin-0001
title: Restrict full-user-detail lookup to admins so cleaners cannot harvest customer PII
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---

## Narrative
As an **admin (support operator)**, I want the "look up any user by id" endpoint to return a user's
full contact details **only to admins** — and never to a cleaner who merely happens to be logged in —
so that an Employee cannot enumerate ids to harvest every customer's email, phone, name and birth
date across the tenant, and our PII-handling stays within the S3/S4 security laws.

**Grounding (current behaviour, audited):**
- `GET /api/User/GetById` (`src/Cleansia.Web.Partner/Controllers/UserController.cs:28-39`) is gated by
  `[Permission(Policy.CanViewUserDetail)]`.
- `CanViewUserDetail` maps to `PhysicalPolicy.OwnerOrElevated`
  (`src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:48`).
- `OwnerOrElevated` (`src/Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:211-228`) returns
  `true` for **any** user in the `Employee` *or* `Administrator` role; the owner-self branch only runs
  for non-elevated callers and reads a route value `"id"` that this endpoint does not have (the
  parameter is `query.UserId` from the query string), so the self-check is effectively dead code.
- `GetUser.Handler` (`src/Cleansia.Core.AppServices/Features/Users/GetUser.cs:31-41`) performs a bare
  `GetByIdAsync(query.UserId)` with **no ownership or relationship check** and returns the full
  `UserItem`.
- `UserItem` (`.../Features/Users/DTOs/UserItem.cs`) exposes `Email`, `FirstName`, `LastName`,
  `PhoneNumber`, `BirthDate`, `ProfilePhoto`, `Profile`, `Id`, `IsActive` — i.e. more PII than just
  email/phone. The validator's `ExistsAsync` filter keeps results in-tenant, so the boundary is the
  whole tenant's user base, not the whole platform.
- The `Policy.cs:50` comment (`// Authenticated (All roles) + Admin + Employee`) **mismatches** the
  actual `OwnerOrElevated` mapping and is misleading per `conventions.md` ("comments explain WHY";
  no stale/incorrect comments).

This violates **S3** (resource-by-id endpoints must check ownership) and **S4** (DTO leak prevention —
"email / phone / full name of non-self users", with the *only* sanctioned exception being a cleaner's
view of a first name on an order they are assigned to).

## Acceptance criteria
- **AC1 — Cleaner is denied another user's detail.**
  Given an authenticated Employee (cleaner) who is **not** the user being requested,
  When they call `GET /api/User/GetById?UserId={someOtherUserId}` for a valid in-tenant user id,
  Then the response is **403 Forbidden** (or **404 NotFound**, per the S3 don't-leak-existence
  convention) and **no** `UserItem` PII is returned.

- **AC2 — Admin retains full lookup.**
  Given an authenticated Administrator,
  When they call `GET /api/User/GetById?UserId={anyInTenantUserId}`,
  Then they receive **200 OK** with the full `UserItem`, exactly as today.

- **AC3 — Enumeration is closed.**
  Given an authenticated Employee,
  When they iterate over a sequence of arbitrary user ids calling `GetById`,
  Then **every** call is rejected (no id returns another user's `Email`/`PhoneNumber`/name), so the
  bulk-harvest path is closed.

- **AC4 — Tenant boundary still holds.**
  Given an Administrator in Tenant A,
  When they request a `UserId` belonging to Tenant B,
  Then the result is **NotFound** (the existing in-tenant `ExistsAsync`/global-filter behaviour is
  preserved, not regressed).

- **AC5 — Self-read remains available through its own route.**
  Given any authenticated user requesting **their own** profile,
  When they call the current-user endpoint (`GET /api/User/GetCurrent`, `Policy.CanGetCurrentUser`),
  Then they receive their own profile unchanged — i.e. tightening `GetById` does not remove a caller's
  ability to read their own data.

- **AC6 — Policy intent is documented truthfully.**
  Given the `Policy.cs` constant for this capability and its `PolicyBuilder` mapping,
  When a developer reads them,
  Then the comment accurately states the gate (admin-only) and matches the physical policy — the
  current `// Authenticated (All roles) + Admin + Employee` comment no longer mismatches the mapping.

## Out of scope
- **A new narrow, order-scoped "contact info for my assigned job" endpoint.** If cleaners legitimately
  need a customer's contact details for an order they are assigned to, that is a **separate** story —
  exposing only the minimal, order-scoped fields (per the documented S4 exception), **not** the full
  `UserItem`. This story only *removes* the over-broad access; it does not build that replacement.
- **`GetPaged` users** (`Policy.CanViewPagedUser` = `EmployeeOrAdmin`) — whether cleaners should list
  users at all is a related but distinct finding; not changed here.
- The dead/incorrect owner-self route-value branch in `OwnerOrElevated` and any other endpoints that
  share that physical policy — auditing/repairing `OwnerOrElevated` broadly is its own ticket; this
  story may simply stop routing `CanViewUserDetail` through it.
- Decisions about which API host exposes the endpoint, NSwag client regeneration, and any DB/schema
  change (none expected — this is an authorization mapping change).
- Logging/rate-limiting changes (S5/S6) for this endpoint.

## Layers touched
- **Backend — Authorization mapping:** `PolicyBuilder.cs:48`
  (`CanViewUserDetail` → `AdminOnly` instead of `OwnerOrElevated`), and the corresponding comment in
  `Policy.cs:50`. *(Primary fix; no handler change strictly required if mapped to `AdminOnly`.)*
- **Backend — Handler (alternative/defence-in-depth):**
  `src/Cleansia.Core.AppServices/Features/Users/GetUser.cs` — add an explicit caller-vs-resource check
  via `IUserSessionProvider` if the capability must remain available to non-admins for self only.
- **Backend — Tests:** `src/Cleansia.Tests` (and/or `IntegrationTests`) — cover AC1–AC5 (cleaner
  denied, admin allowed, enumeration closed, cross-tenant NotFound, self-read intact).
- **No frontend/mobile contract change expected** (response shape unchanged for the allowed caller);
  if the endpoint's reachable roles change in any consumed client, flag `manual_step: nswag-regen`.

## Open questions
- **Q (default assumption taken):** Do any non-admin flows legitimately call `GET /api/User/GetById`
  for *another* user today (e.g. an admin support screen surfaced to elevated Employees)? Default
  assumption until the owner answers: **no** — `GetById`-any-user is an admin/support capability, so it
  is mapped to `AdminOnly`; self-reads already have `GetCurrent`. If a legitimate cleaner→customer
  contact need exists, it is satisfied by the separate order-scoped endpoint listed under Out of scope,
  not by reopening this one.
```


---

I have everything I need. The evidence confirms and extends the GAP:

- `ContractStatus`: `Pending=1, Active=2, Terminated=3, Approved=4, Rejected=5`. The gate `!= Pending` admits **Rejected, Terminated, Active, and Approved** — only `Pending` is blocked.
- `Employee.Reject()` sets `ContractStatus = Rejected`; a rejected cleaner passes the gate in both `TakeOrder.cs:117` and `CompleteOrder.cs:139`.
- The method is named `HasUploadedDocumentsAsync` but reads `ContractStatus`, with error key `EmployeeDocumentsMissing` ("employee.documents_missing") — the name and message both lie about what is checked.
- **Wider finding:** `StartOrder.cs` (the InProgress transition, lines 47–52) gates only on assignment and "no other in-progress order" — it has **no document/approval gate at all**. So a rejected/terminated cleaner can also start work.
- `Approve()` is the only thing that sets `Approved`; `UpdateContractStatus()` can set any value.

Here is the user story.

---

# US-partner-0042 — Block non-approved cleaners from taking, starting, or completing orders

## Persona
partner (cleaner / employee acting on jobs)

## Actor narrative

**As a** Cleansia admin responsible for cleaner vetting and platform liability,
**I want** the order-action gates (take, start, complete) to admit only cleaners whose contract is in an explicitly approved state,
**so that** a cleaner I have rejected, terminated, or not yet approved cannot claim, begin, or finish customer jobs — closing the trust/liability hole where the approval workflow can be silently bypassed.

## Background / grounding (read-only evidence)

- `src/Cleansia.Core.Domain/Enums/ContractStatus.cs:8-12` — `Pending = 1, Active = 2, Terminated = 3, Approved = 4, Rejected = 5`.
- `src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs:111-118` — `HasUploadedDocumentsAsync` returns `employee?.ContractStatus != ContractStatus.Pending`. This admits `Rejected (5)`, `Terminated (3)`, `Active (2)` and `Approved (4)` — it blocks only `Pending`.
- `src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs:133-140` — identical `HasUploadedDocumentsAsync` check, same defect.
- `src/Cleansia.Core.Domain/Users/Employee.cs:243-255` — `Reject()` sets `ContractStatus = ContractStatus.Rejected`; line 229-241 `Approve()` is the only method that sets `Approved`.
- `src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs:47-52` — the InProgress (start work) transition gates only on `EmployeeIsAssignedToOrderAsync` + `EmployeeHasNoOrderInProgressAsync`; **it has no document/approval gate at all**. This is the wider finding: a rejected/terminated cleaner who already holds an assignment can still start the job.
- Error key: `src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs:109` — `EmployeeDocumentsMissing = "employee.documents_missing"`. The rule method `HasUploadedDocumentsAsync`, and this message, both describe documents while the code actually reads `ContractStatus` — the name lies about what it enforces.
- Convention basis: `agents/knowledge/security-rules.md` S2/S3 (authorization must be enforced in the handler/domain, not assumed) and the project rule that approval is the vetting gate. An open question (EMP-GAP-02) is whether `Active` should count alongside `Approved`; this story forces that decision to be made explicitly rather than left implicit in a `!= Pending` check.

## Acceptance criteria (Given/When/Then)

1. **Rejected cleaner cannot take an order**
   **Given** an available order with an open spot and a cleaner whose `ContractStatus` is `Rejected`,
   **When** that cleaner calls TakeOrder,
   **Then** the command fails with a dedicated "not approved" business error (not `employee.documents_missing`), and the order gains no assigned employee and no status change to `Confirmed`.

2. **Rejected cleaner cannot complete an order**
   **Given** an order already in `InProgress` to which a `Rejected` cleaner is somehow still assigned,
   **When** that cleaner calls CompleteOrder with valid after-photos,
   **Then** the command fails with the "not approved" business error, and the order stays `InProgress` (no `Completed` status, no receipt/pay/loyalty side effects fire).

3. **Rejected/terminated cleaner cannot start an order**
   **Given** a `Confirmed` order assigned to a cleaner whose `ContractStatus` is `Rejected` or `Terminated`,
   **When** that cleaner calls StartOrder,
   **Then** the command fails with the "not approved" business error, and the order does not transition to `InProgress`. (StartOrder must gain the approval gate it currently lacks.)

4. **Approved cleaner is unaffected (happy path preserved)**
   **Given** a cleaner whose `ContractStatus` is `Approved` and who meets all existing gates (complete profile, available spot, within weekly limit, no time conflict),
   **When** they take, start, then complete an order,
   **Then** all three commands succeed exactly as they do today.

5. **The "Active vs Approved" decision is made explicit and consistent**
   **Given** the EMP-GAP-02 open question on whether `Active` counts as work-eligible,
   **When** the gate is implemented,
   **Then** the set of work-eligible `ContractStatus` values is defined in exactly one shared place and the take/start/complete gates all reference that same definition (no per-feature divergence), so a cleaner who is eligible for one action is eligible for all three.

6. **The rule and its error message tell the truth**
   **Given** the renamed gate,
   **When** a non-approved cleaner is blocked,
   **Then** the rule method name reflects "approved" (not "uploaded documents"), the failure returns a new `BusinessErrorMessage` key whose dot-notation name reflects approval (e.g. `employee.not_approved`), and that key resolves to a translation under `errors.*` in all five locales (en, cs, sk, uk, ru).

## Out of scope

- Changing the document-upload requirement itself or how documents are stored/validated — the existing `HasCompletedProfile`/document checks stay; this story only fixes the contract-status gate that was mislabeled as a documents check.
- Retroactively unassigning or cancelling orders already held by a now-rejected cleaner (orders taken before rejection). That cleanup/intervention is a separate admin-intervention concern.
- Admin UI to reject/approve cleaners (that workflow already exists via `ApproveEmployee` / `Reject()`).
- Mobile/partner-app UI copy changes beyond surfacing the new error key's translation.
- Resolving EMP-GAP-02's policy intent itself (whether `Active` *should* be work-eligible is a product decision); this story only requires the chosen answer to be applied consistently and in one place.
- The unrelated gaps noted in the prior audit (admin order intervention, payroll settlement reachability, customer-hardcoded CancelOrder).

## Layers touched

- **Backend — AppServices (CQRS validators):** `Features/Orders/TakeOrder.cs`, `CompleteOrder.cs`, and `StartOrder.cs` — replace/add the approval gate; rename the rule away from "documents".
- **Backend — Common:** `BusinessErrorMessage.cs` — add the new `employee.not_approved` (or equivalent) key; likely a shared helper/extension defining the work-eligible `ContractStatus` set so the three features share one source of truth.
- **Backend — Domain (read-only reference):** `ContractStatus` enum and `Employee` status methods (no behavior change expected, but the eligibility definition keys off them).
- **Frontend i18n:** `errors.*` translation entries for the new key in `en/cs/sk/uk/ru` JSON across the partner (and any app that surfaces order-action errors).
- **Tests:** backend unit tests for the three validators covering each `ContractStatus` value (xUnit).
- **Manual steps (owner-only, flag in spec):** NSwag client regeneration if a new error key/enum surfaces in generated contracts (`manual_step: nswag-regen`). No DB migration expected (no schema change).


---

I have full grounding. Key confirmations:

- `Dispute.cs:64-68` `UpdateStatus` free-sets any status, no guard; `Resolve` (82-90), `Close` (92-96), `Escalate` (98-102), `LinkStripeDispute` (104-108) exist.
- Grep proves `Close`/`Escalate`/`LinkStripeDispute`/dispute-`Resolve` have **zero callers** except `ResolveDispute.cs:53`. So `Close`/`Escalate`/`LinkStripeDispute` are genuinely unreachable.
- `UpdateDisputeStatus.cs:45` calls `dispute.UpdateStatus(request.NewStatus, actorId)` with only an `IsInEnum` shape check — no transition guard.
- `DisputeStatus.cs`: Pending=1, UnderReview=2, WaitingForResponse=3, Resolved=4, Closed=5, Escalated=6.
- Stripe webhook (`Constants.cs:21-53`) handles only checkout/payment-intent/subscription/invoice events — **no `charge.dispute.*`**, so `StripeDisputeId`/`LinkStripeDispute` are never populated.
- Policies (`PolicyBuilder.cs:73-78`): create = CustomerOnly; resolve & update-status = AdminOnly. The single `DisputeController` lives in `Cleansia.Web.Partner` (admin-gated by policy); no admin-API controller.
- House rule B7 (`consistency.md:73`, `patterns-backend.md:279`): handlers call rich intent-named domain methods; `BusinessResult.Failure` is the canonical failure path.

Here is the user story.

---

# US-admin-0042 — Enforce a guarded Dispute status lifecycle (and wire chargeback linking)

## Context (grounded in code)

`Dispute` (`src/Cleansia.Core.Domain/Disputes/Dispute.cs`) models a six-state machine — `DisputeStatus` Pending(1), UnderReview(2), WaitingForResponse(3), Resolved(4), Closed(5), Escalated(6) (`src/Cleansia.Core.Domain/Enums/DisputeStatus.cs`). The domain exposes intent-named transitions `Resolve` (`Dispute.cs:82`), `Close` (`Dispute.cs:92`), `Escalate` (`Dispute.cs:98`) and `LinkStripeDispute` (`Dispute.cs:104`), plus the generic `UpdateStatus` (`Dispute.cs:64`).

Grep confirms the only caller of any of these is `ResolveDispute.cs:53` (`dispute.Resolve(...)`). `Close`, `Escalate`, and `LinkStripeDispute` have **zero callers in `src`** — they are unreachable. The single write path for arbitrary status changes is `UpdateDisputeStatus.cs:45` (`dispute.UpdateStatus(request.NewStatus, actorId)`), whose validator only enforces `IsInEnum` (`UpdateDisputeStatus.cs:20-22`) — **no legal-transition guard**. So an admin can drive `Pending → Closed` skipping resolution, or re-open a `Resolved` dispute, with no rule rejecting it.

`StripeDisputeId` (`Dispute.cs:38`) is never populated: the webhook event catalog (`Constants.cs:21-53`) handles only checkout / payment-intent / subscription / invoice events — there is no `charge.dispute.*` handling — so chargeback-driven reasons (`UnauthorizedCharge`, `IncorrectAmount` in `DisputeReason.cs:12-13`) have no Stripe correlation. This violates house rule **B7** (handlers call rich, intent-named domain methods, returning `BusinessResult.Failure` for illegal operations — `agents/knowledge/consistency.md:73`, `agents/knowledge/patterns-backend.md:279`): half the modeled lifecycle is dead code and the live path enforces no invariants.

## Actor narrative

**As an** admin handling customer disputes,
**I want** dispute status changes to be validated against a defined legal-transition graph (via the intent-named domain transitions) and chargeback-originated disputes to be auto-linked to their Stripe dispute,
**so that** a dispute can never skip or reverse its lifecycle (e.g. jump to Closed without resolution, or re-open after Resolved), and chargeback cases stay correlated with Stripe instead of drifting out of sync.

## Acceptance criteria (Given / When / Then)

1. **Legal transition is accepted**
   **Given** a dispute in `Pending`,
   **When** an admin calls `UpdateStatus` with `UnderReview`,
   **Then** the status becomes `UnderReview`, the change is attributed to the calling admin's user id, and the call returns a success `BusinessResult`.

2. **Illegal transition is rejected (skip-ahead)**
   **Given** a dispute in `Pending`,
   **When** an admin calls `UpdateStatus` with `Closed` (or any target not reachable from `Pending` in the agreed graph),
   **Then** no status change is persisted and the call returns a `BusinessResult.Failure` carrying a `BusinessErrorMessage` key for an illegal dispute transition (e.g. `dispute.invalid_status_transition`), with a matching `errors.dispute.*` entry in all five locales.

3. **Terminal states cannot be reopened**
   **Given** a dispute in a terminal state (`Resolved` or `Closed`),
   **When** an admin attempts any `UpdateStatus` that moves it back to an active state (e.g. `UnderReview`),
   **Then** the transition is rejected with the illegal-transition failure and the stored status, `ResolvedBy`/`ResolvedOn`/`RefundAmount` remain unchanged.

4. **Intent transitions are the single source of truth**
   **Given** the guarded `UpdateStatus` path,
   **When** a status change to `Closed`, `Escalated`, or `Resolved` is requested,
   **Then** the change is performed via the corresponding intent-named domain method (`Close`/`Escalate`/`Resolve`) rather than a free-set assignment, so the transition graph is enforced in one place and `Close`/`Escalate` are no longer unreachable.

5. **Chargeback disputes are linked to Stripe**
   **Given** a `charge.dispute.created` (or equivalent) Stripe webhook event arrives for an order that has an open Cleansia dispute,
   **When** the webhook is processed,
   **Then** `LinkStripeDispute` is invoked so `StripeDisputeId` is populated on that dispute, the operation is idempotent (replaying the same event does not duplicate or error per the existing `ProcessedStripeEvent` pattern), and the dispute's `Reason`-based correlation (`UnauthorizedCharge`/`IncorrectAmount`) is preserved.

6. **Definition of the legal graph is explicit and observable**
   **Given** the implemented transition table,
   **When** the test suite runs,
   **Then** there is an xUnit test enumerating every `(from, to)` pair, asserting allowed pairs succeed and disallowed pairs return the illegal-transition failure, documenting the agreed graph (proposed allowed edges, to be confirmed during refinement: `Pending → UnderReview | Escalated`; `UnderReview → WaitingForResponse | Resolved | Escalated`; `WaitingForResponse → UnderReview | Resolved | Escalated`; `Escalated → Resolved | UnderReview`; `Resolved → Closed`; `Closed` = terminal).

## Out of scope

- Building any admin-app or partner-app dispute **UI** (status dropdown, transition buttons). This story is backend/domain + the webhook only; the existing `DisputeController` endpoints in `Cleansia.Web.Partner` are reused unchanged in signature.
- Adding a **second** dispute controller in `Cleansia.Web.Admin`; access stays governed by the existing `AdminOnly` policies (`PolicyBuilder.cs:77-78`).
- Initiating, issuing, or reconciling **refunds** with Stripe — `RefundAmount` remains a recorded number set by `ResolveDispute`, not an executed payment action.
- Outbound notifications / emails to customer or partner on status change.
- Changing `DisputeReason`, the `DisputeMessage`/`DisputeEvidence` sub-entities, or the create/list/detail/evidence flows.
- Any **EF migration** authoring/applying and any **NSwag client regeneration** — owner-only manual steps (flag as `manual_step: ef-migration` if a new error constant needs no schema change, and `manual_step: nswag-regen` only if a DTO/endpoint contract actually changes; the current plan changes neither the `Command` shape nor response).
- Defining the Stripe webhook signature-verification/endpoint plumbing itself (assumed to reuse the existing webhook entry point that already dispatches by `Type`).

## Layers touched

- **Domain** — `Dispute.cs`: add a guarded transition table; route `UpdateStatus` through `Close`/`Escalate`/`Resolve`; keep `LinkStripeDispute`. (No schema change; `StripeDisputeId` column already exists per `Initial` migration.)
- **AppServices (CQRS)** — `UpdateDisputeStatus.cs` handler returns `BusinessResult.Failure` on illegal transitions (B5/B7); a new constant in `BusinessErrorMessage.cs` (`dispute.invalid_status_transition`). A new/extended Stripe webhook handler path for `charge.dispute.*` that resolves the order's open dispute and calls `LinkStripeDispute` (idempotent via existing `ProcessedStripeEvent`). New `StripeEventType` constant(s) in `Constants.cs`.
- **Infra** — Stripe webhook dispatch wiring (reuse existing endpoint and `IProcessedStripeEventRepository`); `IDisputeRepository` lookup-by-order if not already present (`GetOpenDisputeForOrderAsync` already exists, `CreateDispute.cs:54`).
- **i18n (frontend assets only, no UI)** — add `errors.dispute.invalid_status_transition` to `en/cs/sk/uk/ru` under all three apps' `assets/i18n` so the existing error-toast pipeline can render the new key.
- **Tests** — xUnit transition-table tests (AC6) and a webhook-linking idempotency test.


---

I have everything grounded. Confirmed facts:

- `Policy.CanViewUserDetail` → `PhysicalPolicy.OwnerOrElevated` (PolicyBuilder.cs:48), used by exactly one endpoint: `UserController.GetById` in `Cleansia.Web.Partner` (lines 28-34). Customer/Mobile.Customer UserControllers don't expose `GetById` at all.
- The `OwnerOrElevated` assertion (ServiceExtensions.cs:211-228, duplicated across all 5 hosts) returns true for Employee/Admin; for everyone else it reads `http.Request.RouteValues["id"]`.
- But `GetById` binds `GetUser.Query(UserId)` via `[FromQuery]` (`?UserId=`), and the route `api/User/GetById` has no `{id}` segment, so `RouteValues["id"]` is always null → owner branch always false. The elevated (Employee OR Admin) branch is the only thing granting access.
- Governing knowledge rules: **S3** (resource-by-id ownership must be checked, in handler/domain) and **S2** (authorization on every endpoint). IDA-SEC-04 is the prior-audit finding id, not a knowledge rule.

Here is the user story.

---

# US-partner-0042 — Fix the dead owner-branch in the `OwnerOrElevated` authorization policy

## Persona
`partner` (the finding lives in the Partner API host, which is the only host that exposes the consuming endpoint; the access-model defect is owned by the platform/partner side, not by a customer-facing flow).

## Actor narrative

> **As a** Cleansia platform owner responsible for the authorization model,
> **I want** the `OwnerOrElevated` policy to evaluate ownership against the same identifier the action actually binds (`GetUser.Query.UserId`), instead of a route value (`RouteValues["id"]`) that the route never supplies,
> **so that** the "owner may read their own user detail" rule the code claims to implement is the rule that is actually enforced — and a future `{id}` route or a relaxation of the elevated branch can't silently grant or deny access in a way nobody reviewed.

## Context / grounding (read before estimating)

- `src/Cleansia.Web.Partner/Extensions/ServiceExtensions.cs:211-228` — the `OwnerOrElevated` assertion. Non-elevated callers are checked via `http.Request.RouteValues.TryGetValue("id", out var routeId)` then `routeId?.ToString() == sub`.
- `src/Cleansia.Web.Partner/Controllers/UserController.cs:28-34` — the **only** consumer of `Policy.CanViewUserDetail`. `GetById([FromQuery] GetUser.Query query)` binds from the query string; route is `api/User/GetById` (no `{id}` segment).
- `src/Cleansia.Core.AppServices/Features/Users/GetUser.cs` — `record Query(string UserId)`; the bound field is `UserId`, not `id`.
- `src/Cleansia.Core.AppServices/Authentication/PolicyBuilder.cs:48` — `[Policy.CanViewUserDetail] = PhysicalPolicy.OwnerOrElevated`.
- Identical `OwnerOrElevated` assertion blocks exist in all five hosts: `Cleansia.Web.Partner` (211), `Cleansia.Web.Admin` (211), `Cleansia.Web.Customer` (225), `Cleansia.Web.Mobile.Customer` (226), `Cleansia.Web.Mobile.Partner` (214). Only the Partner host currently wires an endpoint to it.
- Governing rules: **security-rules.md S3** (resource-by-id endpoints must verify the caller owns the resource — *in the handler/domain*, "regardless of which API host exposes it"; return **NotFound**, not Forbidden, for cross-user access) and **S2** (every endpoint carries exactly one authorization decision). The prior-audit id IDA-SEC-04 (elevated branch grants every employee) is the adjacent finding this work must not mask.

**Net effect today:** because the owner branch is unreachable, a customer/self caller is always denied and only `Employee | Admin` are admitted. This is *deny-only* today (no active data exposure), so the story is a **correctness / defense-of-the-access-model** fix, not an open hole. Per S3 the durable ownership check belongs in the `GetUser` handler, with the policy aligned to whatever id the action binds.

## Acceptance Criteria (Given / When / Then)

1. **Owner-self read is reachable**
   **Given** a caller whose JWT `sub` equals the `UserId` they request, and who is neither Employee nor Admin,
   **When** they call `GET api/User/GetById?UserId={their own id}`,
   **Then** the request is authorized and returns `200` with their own `UserItem` — i.e. the owner branch now actually evaluates against the bound `UserId`, not `RouteValues["id"]`.

2. **Cross-user read by a non-elevated caller is refused without leaking existence**
   **Given** a non-elevated caller,
   **When** they call `GET api/User/GetById?UserId={a different user's id}`,
   **Then** access is refused, and per S3 the response does **not** confirm the other user exists (the handler-level ownership guard returns **NotFound**, not a `200` and not a Forbidden that distinguishes "exists but not yours").

3. **The defect is provably gone (regression lock)**
   **Given** the corrected policy/handler,
   **When** the bound id and the route value disagree (or no route value is present, as is the case on the real route),
   **Then** the ownership decision is driven by the **bound** id (`GetUser.Query.UserId`), demonstrated by a unit/integration test that fails against the current `RouteValues["id"]` implementation and passes after the fix.

4. **Elevated access remains governed by the intended rule, not masked by this fix**
   **Given** an Employee or Admin caller,
   **When** they call `GetById` for any user,
   **Then** their access is determined by the explicit elevated rule (the IDA-SEC-04 resolution), and a test documents that an Employee's access to *another* user's detail follows that intended rule rather than being incidentally allowed by a broken branch.

5. **No silent behavior change on the currently-shipping route**
   **Given** the existing `GET api/User/GetById?UserId=` contract (query-string bound, no `{id}` segment, NSwag clients unchanged),
   **When** the fix ships,
   **Then** the route shape, the bound parameter name (`UserId`), and the OpenAPI/NSwag contract are unchanged — the fix is internal to the authorization decision and the handler guard, requiring no `manual_step: nswag-regen`.

6. **Hosts stay consistent**
   **Given** the five duplicated `OwnerOrElevated` assertion blocks,
   **When** the policy logic is corrected,
   **Then** every host that defines `OwnerOrElevated` reflects the same corrected ownership logic (no host left reading a stale `RouteValues["id"]`), so a future endpoint wired in another host inherits the intended behavior.

## Out of scope

- **Resolving IDA-SEC-04 itself** (whether *every* Employee should be able to read *any* user's detail). This story must not *widen or narrow* the elevated rule beyond what its own resolution decides; it only stops the owner branch from masking it and adds the test that pins the intended elevated behavior. If IDA-SEC-04 is unresolved, AC #4's "intended rule" is its current documented behavior.
- Adding a new `{id}`-route variant of `GetById`, or changing the query-string contract to a path parameter.
- Any change to other policies (`EmployeeOrAdmin`, `AdminOnly`, `Authenticated`, `CustomerOnly`) or to other endpoints using them.
- Customer/Partner/Admin frontend or Android changes — no UI consumes a behavior change here (the route and contract are unchanged).
- NSwag client regeneration / OpenAPI contract changes (owner-only, and not needed — see AC #5).
- EF migrations or any data/schema change.
- The unrelated prior-audit items (admin order intervention, payroll settlement lifecycle, customer-hardcoded `CancelOrder`).

## Layers touched

- **Backend — Auth/policy (host composition):** `OwnerOrElevated` assertion in `…/Extensions/ServiceExtensions.cs` across the 5 hosts (Partner:211, Admin:211, Customer:225, Mobile.Customer:226, Mobile.Partner:214) — read ownership from the bound id (or have the action pass it explicitly) instead of `RouteValues["id"]`.
- **Backend — AppServices (durable guard, per S3):** `Cleansia.Core.AppServices/Features/Users/GetUser.cs` — the owner check belongs here (handler/domain), returning **NotFound** for cross-user access, so it holds regardless of host.
- **Backend — Tests:** `Cleansia.Tests` (handler-level ownership unit test) and `Cleansia.IntegrationTests` (route-level auth: owner-self `200`, cross-user `NotFound`, elevated-caller per intended rule) — the regression-lock for AC #3/#4.
- **Not touched:** Domain entities/EF config, DB/migrations, OpenAPI/NSwag clients, all 3 Angular apps, both Android apps.


---

I have everything I need. The rule is confirmed: patterns-frontend.md:183 — "No raw HTML form controls" and conventions.md:30 — "No hand-rolled HTTP, no raw HTML controls."

Now I have full ground truth:

1. **The GAP as written is a false positive.** A complete web saved-address management UI exists — it's the "Addresses" section of the customer Profile page. `SetDefault` and `Delete` are both reachable (template lines 269 and 276 → facade → store → client). The grep for a `*address*` feature folder missed it only because the feature is named `profile`.

2. **A real, smaller observation surfaces in the same code:** the address-card action buttons use raw `<button>` elements (template lines 269, 273, 276), violating the documented rule (patterns-frontend.md:183).

Per the task, I must turn a *confirmed functional gap* into one user story. Since the headline gap is disproven, the honest, grounded deliverable is a correction plus the genuinely-confirmed (minor, refactor-only) finding cast as a story.

---

# Audit finding: GAP is a FALSE POSITIVE — web saved-address management already exists

**Verdict:** The premise — "`SetDefaultSavedAddress` / `DeleteSavedAddress` exist on the backend but have no web saved-address management UI" — does **not** hold up against the code. A full web saved-address management UI exists; it is just **embedded in the Profile feature**, not in a folder named `*address*`. The originating grep (`no cleansia-customer-features/*address*`) produced a false negative because the feature is called `profile`.

### Evidence (file:line)

- **Backend endpoints exist** (as the GAP states): `src/Cleansia.Web.Customer/Controllers/SavedAddressController.cs` — `SetDefault` (line 41), `Delete/{id}` (line 63), plus `GetMine`/`Add`/`Update`. Mirrored in `src/Cleansia.Web.Mobile.Customer/Controllers/SavedAddressController.cs`.
- **Web store fully implements all five operations:** `libs/data-access/customer-stores/src/lib/saved-addresses/saved-address.store.ts` — `setDefault()` (line 83) calls `savedAddressClient.setDefault`; `delete()` (line 101) calls `savedAddressClient.delete`.
- **Web facade exposes them:** `libs/cleansia-customer-features/profile/src/lib/profile/profile.facade.ts` — `deleteSavedAddress` (line 158), `setDefaultSavedAddress` (line 167), plus add/update.
- **Web UI renders and wires them:** `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.html` — "Addresses" section (lines 226–295): list with default badge, **Set-default** button `(click)="setDefaultAddress(addr.id!)"` (line 269), **Edit** (line 273), **Delete** `(click)="deleteAddress(addr.id!)"` (line 276), plus an add/edit dialog with Mapbox autocomplete (lines 419–476). Handlers in `profile.component.ts`: `deleteAddress` (line 394), `setDefaultAddress` (line 398).
- **Route is live:** `libs/cleansia-customer-features/profile/src/lib/lib.routes.ts` (line 8) mounts `ProfileComponent` at `/profile`.

So both endpoints are reachable from the web today. **No new management-screen story is warranted.** Mobile parity is fine (`customer-app/.../features/addresses/AddressManagerScreen.kt`); web is in the Profile page rather than a standalone screen — a product/IA choice, not a functional gap.

### The one genuinely-confirmed defect in this code (refactor-only)

The three address-card action controls are **raw `<button>` elements** (`profile.component.html` lines 269, 273, 276), violating the documented rule: patterns-frontend.md:183 "No raw HTML form controls" and conventions.md:30 "no raw HTML controls." This is the only confirmed, code-grounded issue in the SavedAddress web flow. It is cast as the story below.

---

# US-customer-0418 — Replace raw `<button>` controls in Profile address cards with `<cleansia-button>`

**As a** customer using the web app's Profile → Addresses section,
**I want** the per-address Set-default / Edit / Delete actions to use the platform's standard button component,
**so that** they have consistent styling, focus/disabled/loading states, and accessibility with the rest of the app (and the codebase honors its own "no raw HTML controls" rule).

> Note: this is a UI-consistency refactor of an **already-working** feature, not new functionality. It exists only because the originally-reported "missing feature" turned out to be present; this is the real, smaller finding underneath it.

### Acceptance criteria

1. **Given** I am on `/profile` viewing a saved address card, **when** the page renders, **then** the Set-default, Edit, and Delete actions are rendered via `<cleansia-button>` (no raw `<button>` elements remain in the address-card markup at `profile.component.html` lines ~269/273/276).
2. **Given** a saved address that is **not** the default, **when** I click its Set-default action, **then** `setDefaultAddress(addr.id)` fires exactly as today, the address becomes default, and the default badge moves — behavior identical to the current implementation.
3. **Given** any saved address, **when** I click Edit, **then** the existing add/edit dialog opens pre-filled (`openEditAddress`), and **when** I click Delete, **then** `deleteAddress(addr.id)` runs and the card is removed with the existing success snackbar.
4. **Given** the default address, **when** the card renders, **then** the Set-default action is hidden (the current `@if (!addr.isDefault)` condition is preserved).
5. **Given** each action button, **when** rendered, **then** it has an accessible label/tooltip sourced via `TranslatePipe` (reuse existing keys `pages.profile.set_default`, and add equivalent keys for Edit/Delete if missing, populated in all five locales en/cs/sk/uk/ru).
6. **Given** the refactor is complete, **when** `npx nx lint cleansia-customer-features-profile` and the customer app build run, **then** they pass with no new warnings, and `ChangeDetectionStrategy.OnPush` is retained.

### Out of scope

- Any backend change (`SavedAddressController`, the MediatR `SetDefaultSavedAddress`/`DeleteSavedAddress`/`Add`/`Update` handlers, DTOs, or EF config) — all correct and unchanged.
- The store and facade (`saved-address.store.ts`, `profile.facade.ts`) — no signature or logic changes.
- Creating a **new standalone** `cleansia-customer-features/saved-addresses` (or `*address*`) feature folder or a dedicated `/addresses` route — the feature intentionally lives in Profile; do not relocate it.
- The add/edit dialog internals, Mapbox autocomplete, lat/lng capture, and the `setAsDefault` default-on-first-address logic.
- Mobile (`AddressManagerScreen.kt`) and any mobile API.
- NSwag client regeneration (no contract change) — flag nothing for `manual_step: nswag-regen`.
- Visual redesign of the address cards beyond swapping the control element and wiring icon/tooltip props.

### Layers touched

- **Frontend only** — single library `libs/cleansia-customer-features/profile`:
  - `src/lib/profile/profile.component.html` (address-card action buttons; the dialog already uses `<cleansia-button>`).
  - i18n: `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` only if new Edit/Delete tooltip keys are introduced.
- No backend, no DB/migration, no NSwag, no mobile.


---

I now have everything I need, fully grounded:

- **Web facade** (`membership.facade.ts`) has methods: `refresh/getMine`, `loadPlans/getPlans`, `cancel`, `swapPlan`, `createCheckoutSession` — **no `subscribe()`**. New subscriptions go through Stripe-hosted Checkout (`window.location.href = response.checkoutUrl`).
- **Android** (`MembershipViewModel.kt` + `MembershipRepository.kt`) uses the two-phase `subscribe` endpoint: `subscribePhase1` (`paymentMethodConfirmed=false` → SetupIntent + EphemeralKey for PaymentSheet) and `subscribePhase2` (`paymentMethodConfirmed=true` → creates the Stripe subscription + `UserMembership`).
- **Both controllers** (`Cleansia.Web.Customer` and `Cleansia.Web.Mobile.Customer`) expose identical `Subscribe` endpoints. The web one is reachable by URL but never called by the web client → the orphan-from-web appearance.
- Consistency catalog confirms the two cited debts cite `CreateMembershipSubscription` by name: **B5** (it uses `nameof(Command)` instead of the offending field) and **B8** (Stripe calls with no `try/catch`/idempotency guard).

Here is the user story.

---

# US-customer-0042 — Document and align the two-path membership subscribe design (web Checkout vs mobile PaymentSheet)

## Persona
`customer` (the subscribing end-user) — but the story's deliverable is primarily a **maintainability / contract-clarity** story for the engineers who own the membership feature. The customer-facing behavior must remain unchanged.

## Actor narrative

> **As a** developer maintaining the Cleansia membership subscribe flow,
> **I want** the intentional split between the web subscribe path (Stripe-hosted Checkout via `CreateMembershipCheckoutSession`) and the mobile subscribe path (native SetupIntent + PaymentSheet via `CreateMembershipSubscription`) to be explicitly documented and the two `Subscribe` endpoints' provenance to be unambiguous,
> **so that** the web-exposed `MembershipController.Subscribe` endpoint no longer looks orphaned, future contributors don't "fix" a non-bug by wiring web into the native-SDK path (or deleting a live mobile endpoint), and the customer's subscribe experience on web and mobile stays exactly as it is today.

### Context (grounded in code)
- `src/Cleansia.App/.../membership/membership.facade.ts` exposes `refresh`, `loadPlans`, `cancel`, `swapPlan`, and `createCheckoutSession` — and **no `subscribe` call**. New web subscriptions redirect the browser to a Stripe Checkout URL (`membership.facade.ts:141-142`).
- `src/cleansia_android/.../membership/MembershipViewModel.kt` (`startSubscribe`/`confirmSubscribe`) and `MembershipRepository.kt:55-78` (`subscribePhase1`/`subscribePhase2`) are the **only** callers of the `Subscribe` endpoint, driving Stripe PaymentSheet in setup mode.
- `Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs` is a deliberate two-phase handler keyed on `PaymentMethodConfirmed`: phase 1 returns `SetupIntentClientSecret` + `EphemeralKey` (lines 107-114); phase 2 creates the Stripe subscription + `UserMembership` (lines 79-105).
- `Subscribe` is declared on **both** `Cleansia.Web.Customer/Controllers/MembershipController.cs:15-25` **and** `Cleansia.Web.Mobile.Customer/Controllers/MembershipController.cs:15-25`. The **web** copy is reachable by URL but has no web-client caller — the source of the "orphaned" appearance.

## Acceptance Criteria (Given / When / Then)

1. **Design is documented at the feature.**
   **Given** a developer opens `CreateMembershipSubscription.cs` or the membership feature docs,
   **When** they read the type/feature summary,
   **Then** it states that the web client subscribes via `CreateMembershipCheckoutSession` (Stripe Checkout redirect) and the mobile client subscribes via this two-phase SetupIntent/PaymentSheet handler, and that both paths converge on a `UserMembership` row.

2. **The web-exposed `Subscribe` endpoint's audience is unambiguous.**
   **Given** the web Customer API surface (`Cleansia.Web.Customer/Controllers/MembershipController.cs`),
   **When** a developer inspects the `Subscribe` action,
   **Then** there is an explicit, discoverable signal of whether it is intentionally retained for non-web clients or should not be present on the web API — resolved as a deliberate decision (documented retention, or removal) rather than left as an undocumented orphan.

3. **No behavioral change for the web customer.**
   **Given** a signed-in web customer on the subscribe page,
   **When** they tap "Subscribe"/"Start trial,"
   **Then** they are still redirected to Stripe-hosted Checkout exactly as today, and on return their membership shows as active — with no new `subscribe()` call introduced in `membership.facade.ts`.

4. **No behavioral change for the mobile customer.**
   **Given** a signed-in Android customer on the subscribe screen,
   **When** they complete subscribe via PaymentSheet,
   **Then** `subscribePhase1` → PaymentSheet → `subscribePhase2` still runs against the **Mobile** Customer `Subscribe` endpoint and a `UserMembership` is created, unchanged from today.

5. **Cross-references are bidirectional.**
   **Given** a developer reading the web `createCheckoutSession` path or the mobile `subscribePhase1/2` path,
   **When** they look for the sibling platform's subscribe mechanism,
   **Then** each path references the other (e.g., a short note in `membership.facade.ts` and in the handler), so the split is discoverable from either side.

6. **The active idempotency guard is preserved and noted.**
   **Given** a customer who already has an active membership,
   **When** any client calls `Subscribe`,
   **Then** the existing `MembershipAlreadyActive` rejection (`CreateMembershipSubscription.cs:57-62`) still fires, and the documentation calls out that both platforms rely on this single guard.

## Out of scope (explicit)

- **Fixing consistency debt B5** — `CreateMembershipSubscription` uses `nameof(Command)` instead of the offending field name (`consistency.md` B5; `CreateMembershipSubscription.cs:47`). Tracked separately; do **not** bundle here.
- **Fixing consistency debt B8** — the Stripe calls in this handler have no narrow `try/catch` / idempotency wrapping (`consistency.md` B8; `CreateMembershipSubscription.cs:67,85,107,108`). Tracked separately.
- **Unifying the two payment UX paths** — no migrating web to PaymentSheet or mobile to Checkout; the split is intentional and stays.
- **Any change to Stripe pricing, plans, trial logic, swap, or cancel** flows.
- **Removing the mobile `Subscribe` endpoint** — it is live and required.
- **New i18n keys, UI redesign, or new endpoints.**
- **NSwag client regeneration** — owner-only; only flag `manual_step: nswag-regen` if AC#2 is resolved by changing the web endpoint signature/removing it.

## Layers touched

- **Docs / feature annotations (primary):** `Cleansia.Core.AppServices/Features/Memberships/CreateMembershipSubscription.cs` summary; membership feature docs; bidirectional code comments.
- **Backend (conditional, only if AC#2 → removal):** `Cleansia.Web.Customer/Controllers/MembershipController.cs` — if the team decides the web API should not expose `Subscribe`, removing the action is a backend + API-contract change (triggers `manual_step: nswag-regen`). The Mobile controller is untouched.
- **Frontend (docs only):** a clarifying note in `membership.facade.ts`; **no logic change**.
- **Mobile (docs only):** a clarifying note in `MembershipRepository.kt` / `MembershipViewModel.kt`; **no logic change.**

## Notes for the PM / sizing
- **Severity:** Low (maintainability/clarity). Both subscribe paths function today; this is debt-of-understanding, not a runtime defect.
- **Cheapest viable slice:** AC#1, #3, #4, #5, #6 are documentation/comment-only (no contract change, no NSwag). AC#2 is the only one that *may* require a backend decision; recommend defaulting to **documented retention** to keep the slice owner-free, unless product confirms no non-web client will ever hit the web API.


---

I now have everything I need to ground the story. Note the relevant rules: runtime-readiness.md:26-28, :55-57, :63, :71; consistency.md B8 (narrow try/catch, not broad `catch(Exception)`); conventions.md "no magic strings/constants in a named home" and "root cause not symptom". I've confirmed the four code citations and that EmailService throws `EmailDeliveryException` after Polly exhausts.

Here is the user story.

---

# US-admin-0014 — Classify external-integration errors (Transient / Permanent / Configuration) so retries are targeted and the owner is alerted on permanent/config failures

## Persona / Actor
`admin` (the platform owner / operator — the single person who runs Cleansia and is on the hook when an external provider misbehaves in PROD).

## Actor narrative
**As** the platform owner operating Cleansia,
**I want** every outbound call to an external provider (SendGrid, Stripe, Firebase/FCM, Mapbox) to classify its failure as **Transient**, **Permanent**, **Configuration**, or **Unknown**, retry **only** transient failures, and emit a distinct, alertable signal for Permanent/Configuration failures,
**so that** a rotated API key or a permanently-bad request stops burning retry budget, surfaces immediately as a config/permanent problem I can act on, and I am not blindsided by a silent provider outage.

## Grounding (the gap, in the real code)
- **`EmailService.cs:32-43`** — the Polly policy is `HandleResult<Response>(r => !r.IsSuccessStatusCode).Or<HttpRequestException>()` with 3 retries. It retries on **any** non-2xx, so a permanent `4xx` (bad template id, invalid recipient) and a configuration `401` (rotated SendGrid key) each burn 3 attempts that can never succeed, then throw `EmailDeliveryException` (`:365`, `:415`) with no Permanent/Configuration distinction and no alertable metric.
- **`MapboxGeocodingService.cs:68-74`** — a single `catch` collapses `HttpRequestException`/`TaskCanceledException`/`JsonException`/`InvalidOperationException` to `LogWarning` + `return null`. A bad/rotated access token (config) and a transient network blip (transient) are indistinguishable; both are silently downgraded to "no coordinates".
- **`FcmPushDispatcher.cs:95-101`** — per-token failures are classified into dead-token vs transient (good), **but** the outer `catch (Exception)` at **`:75-81`** treats any init/transport failure as "all-failed, prune nothing" with a single `LogError`, and `EnsureInitialized` (`:147-158`, `:170-179`) folds a missing-config terminal case and a transient credential-refresh failure into the same `null` return — no classification crosses the boundary to the caller.
- **`StripeClient.cs`** — no try/catch and no classification on any call (`CreateCheckoutSessionAsync`, `CreatePaymentIntentAsync`, refunds, subscriptions); raw `StripeException` propagates unclassified. (Consistent with consistency.md **B8** flagging `CreateMembershipSubscription`/`CreateOrder` Stripe handling.)

## Cited rules this satisfies
- **runtime-readiness.md:26-28** — every external call logged at its boundary with outcome + duration + **error classification (`Transient | Permanent | Configuration | Unknown`)**.
- **runtime-readiness.md:55-57** — retries read the classification: `Transient` → retry; `Permanent` → stop + flag; `Configuration` → alert, don't retry forever.
- **runtime-readiness.md:63, :71** — alert on a spike in `Permanent`/`Configuration` external errors; the readiness checklist requires every external call to classify and log the boundary.
- **consistency.md B8** — side-effecting external calls wrapped in a **narrow** provider-specific try/catch, never a broad `catch (Exception)` for control flow.
- **conventions.md** — the classification taxonomy and the status-code→class mappings live in a **named home** (no magic numbers/strings inline), and we solve the **root cause** (one shared classifier reused by all four clients), not per-client symptom patches.

## Acceptance Criteria (Given / When / Then)

1. **Given** a shared error classifier exists that maps a provider failure to `Transient | Permanent | Configuration | Unknown`, **when** any of the four integration clients (Email/SendGrid, Stripe, FCM, Mapbox) handles a failure, **then** it uses that single shared classifier (no per-client ad-hoc mapping) and logs the boundary at Error/Warning with the resolved classification, the provider name, and the outcome.

2. **Given** the SendGrid Polly policy in `EmailService`, **when** a send fails with a **Permanent** result (e.g. `400` bad template id / invalid recipient) or a **Configuration** result (`401`/`403` rotated or invalid key), **then** the policy does **not** retry (0 retry attempts), the failure is logged **once** at `Error` with its classification, and a Configuration/Permanent metric/counter is incremented — while a **Transient** failure (`429`/`5xx`/`HttpRequestException`) still retries up to 3 times with backoff.

3. **Given** `MapboxGeocodingService` fails, **when** the failure is a **Configuration** error (invalid/rotated access token), **then** it is logged at `Error` with the `Configuration` classification and the config metric is incremented — distinct from a **Transient** network/timeout failure which remains a `Warning` "continuing without coordinates"; in both cases geocoding still degrades to `null` (a non-core dependency must not block the core action, per the degradation matrix).

4. **Given** `FcmPushDispatcher`'s outer failure path (`:75-81`) and `EnsureInitialized` (`:147-179`), **when** initialization or transport fails, **then** the failure is classified — a missing/invalid service-account or project config is logged once as **Configuration** (and not retried as if transient), while a transient credential-refresh/transport failure is classified **Transient** — and the existing per-token dead-token pruning at `:95-101` is unchanged.

5. **Given** any `StripeClient` call (checkout/payment-intent/refund/subscription), **when** a `StripeException` is raised, **then** it is run through the shared classifier (e.g. `card_error`/`invalid_request_error` → Permanent, `authentication_error` → Configuration, `api_connection_error`/`rate_limit_error` → Transient) and the boundary is logged with that classification before the error propagates to the caller, using a **narrow** Stripe-specific catch (consistency.md B8), with no change to existing idempotency-key behavior.

6. **Given** Permanent and Configuration failures occur across any of the four clients, **when** the owner inspects telemetry, **then** an observable, alertable count exists per classification and provider (a metric/log field the owner can threshold on), so a spike in `Configuration`/`Permanent` errors (e.g. a rotated key) is surfaceable per runtime-readiness.md:63 — and **no** test asserts on a hardcoded literal status code that lives outside the classifier's named mapping.

## Out of scope
- **EF Core migrations / schema** — this is observability/control-flow only; no new tables. (No persisted "external-failures" admin table is introduced here; if owner wants a durable failures screen, that is a separate ticket.)
- **A new admin UI screen / dashboard** to browse external-call failures — this story emits the alertable signal (metric/structured log), not a frontend surface. No NSwag regen.
- **Changing the graceful-degradation behavior itself** — Mapbox still returns `null`, email is still a side effect, push is still best-effort, orders are not newly blocked or unblocked. Only classification/retry-targeting/logging change.
- **Replacing Polly / introducing a new resilience framework**, or adding a circuit breaker — retry counts and backoff stay as-is except for the transient-only gating in AC-2.
- **Webhook idempotency, the order/payment-state reconciliation, and Stripe webhook handling** (S7) — untouched here.
- **Touching FCM's existing per-token dead-token pruning logic** (`:95-101`) beyond reusing the shared taxonomy — its behavior is already correct.
- **Frontend, Android, iOS** — backend-only.
- **Inventing a new shared abstraction without ADR sign-off** — if the classifier is deemed a genuinely new cross-cutting abstraction, the Architect blesses it via ADR per conventions.md ("Reuse the real types"); the developer does not fork a parallel result/error type.

## Layers touched
- **Backend — `Cleansia.Core.AppServices`**: `Services/EmailService.cs` (Polly policy + send paths).
- **Backend — `Cleansia.Infra.Services`**: `Geocoding/MapboxGeocodingService.cs`.
- **Backend — `Cleansia.Infra.Clients`**: `Fcm/FcmPushDispatcher.cs`, `Stripe/StripeClient.cs`.
- **Backend — shared/common** (likely `Cleansia.Infra.Common` or `Cleansia.Config`): the new `ExternalErrorClassification` enum + classifier helper + metric/counter home (named constants for status-code→class mapping), reused by all four clients.
- **Tests — `Cleansia.Tests`**: unit tests for the classifier mapping and the Polly transient-only retry gating (TDD per conventions.md "develop test-first" for pure logic).
- **Not touched**: Domain entities, DB schema/migrations, MediatR handlers (handlers call these clients but their signatures don't change), any frontend/mobile app, NSwag clients.

**Manual steps:** none (`manual_step: ef-migration` not required; `manual_step: nswag-regen` not required — no DTO/endpoint contract change).


---

The recursive grep across `src/` returned zero `EnableRateLimiting` attribute usages — confirming definitively that no controller in the codebase applies the rate-limit policy, including the two send-test endpoints, and the Admin host has no global limiter. The policies are defined in `CleansiaStartupBase` but never attached.

I have everything I need. The findings are fully grounded in the cited code and the real S5/S7 rules.

---

# US-admin-0007 — Rate-limit and constrain the email-template "send test" endpoints

## Context (grounded in code)

Two Admin endpoints send a **real outbound email via SendGrid** to a fully caller-supplied `RecipientEmail`, gated only by the `CanUpdateEmailTemplate` permission:

- `POST api/AdminEmailTemplate/types/{emailType}/send-test` — `AdminEmailTemplateController.cs:42-59`, handler `SendTestEmailByType.cs:43-108`
- `POST api/AdminEmailTemplate/{emailTemplateId}/send-test` — `AdminEmailTemplateController.cs:110-128`, handler `SendTestEmail.cs:44-116`

`RecipientEmail` is validated **only** for non-empty + email format (`SendTestEmail.cs:30-35`, `SendTestEmailByType.cs:31-36`). Each handler unconditionally fires a genuine `ConfirmationEmail` / `ResetPassword` / `OrderReceipt` / `PeriodClosed` / `PeriodEndReminder` template from the production domain — **no idempotency key, no dedup, every call sends another email.**

Rate-limit policies (`"auth"` 10/min, `"interactive"` 60/min) are defined in `CleansiaStartupBase.cs:69-93` but a repo-wide search found **zero `[EnableRateLimiting]` usages anywhere in `src/`**, and the Admin host registers **no global limiter**. So these two endpoints have no throttle at all.

This violates **S5** (`security-rules.md:70-75` — mutations that send email get a narrower per-user limit; "decide the limit whenever you add a side-effecting mutation") and **S7** (`security-rules.md:83-91` — any command that sends an email must be idempotent; protects against double-clicks and admin re-triggers).

---

## User Story

**As an** admin (email-template editor),
**I want** the "send test email" actions to be rate-limited per user and constrained to a safe set of recipients, with repeat sends de-duplicated,
**so that** a holder (or thief) of a `CanUpdateEmailTemplate` token cannot turn our SendGrid sender into an unlimited spam / phishing relay aimed at arbitrary third-party addresses, and so that an accidental double-click does not fire duplicate mail or burn SendGrid quota/reputation.

---

## Acceptance Criteria (Given / When / Then)

1. **Per-user throttle exists and is enforced (S5)**
   **Given** a narrow `"send-test"` fixed-window rate-limit policy is registered (a few requests per minute, partitioned per authenticated user/tenant)
   **When** an admin calls either `send-test` endpoint more times than the window permits within one minute
   **Then** the excess requests are rejected with HTTP `429 Too Many Requests` and **no** SendGrid call is made for the rejected requests.

2. **Both endpoints carry the policy**
   **Given** the `"send-test"` policy is defined
   **When** the application starts
   **Then** both `POST types/{emailType}/send-test` and `POST {emailTemplateId}/send-test` resolve the `"send-test"` limiter (verifiable by attribute/route binding), and no other behavior of unrelated endpoints changes.

3. **Recipient is restricted, not "any email" (S5)**
   **Given** the configured recipient policy (the calling admin's own verified email and/or a configured allow-list)
   **When** an admin submits a `RecipientEmail` that is a well-formed but non-allowed third-party address
   **Then** the request is rejected with a `400`-class business error (a `BusinessErrorMessage` key with a translation in all 5 locales) and **no** email is sent.

4. **Allowed recipient still works**
   **Given** an admin submits a `RecipientEmail` that is on the allow-list (e.g. their own address)
   **When** the request is within the rate-limit window
   **Then** exactly one email is sent and the response returns the SendGrid `MessageId` and `RecipientEmail` as today.

5. **Repeat sends are de-duplicated (S7)**
   **Given** an idempotency mechanism (idempotency key or short-lived dedup on template/type + recipient + admin)
   **When** the identical send-test request is submitted twice in quick succession (e.g. a double-click)
   **Then** only one outbound email is sent and the second response is the same successful result, not a second SendGrid dispatch.

6. **Authorization gate is unchanged**
   **Given** a caller without `CanUpdateEmailTemplate`
   **When** they call either endpoint
   **Then** they still receive `403 Forbidden` (the new controls add to, and never replace, the existing permission check).

---

## Out of Scope

- Any global Admin-host rate limiter or applying `[EnableRateLimiting]` to other Admin controllers (audited separately; this story touches only the two `send-test` endpoints).
- Changing the email template content, the `EmailType` set, or the `IEmailService` send signatures.
- Reworking the SendGrid client, retry/backoff, or webhook handling.
- Adding rate limiting to the *production* (non-test) transactional email flows (order receipts, period emails) triggered by real lifecycle events.
- SendGrid-side suppression lists, sender authentication (SPF/DKIM), or quota/billing alerting.
- A frontend redesign of the email-template admin screen beyond surfacing the new `429` / recipient-rejected / dedup error states.

---

## Layers Touched

- **Backend — Web.Admin host config:** register a `"send-test"` fixed-window policy in `CleansiaStartupBase.cs:69-93` (or the Admin startup) and apply `[EnableRateLimiting("send-test")]` to both actions in `AdminEmailTemplateController.cs:42` and `:110`.
- **Backend — AppServices:** recipient allow-list/self check + idempotency/dedup in the `SendTestEmail` / `SendTestEmailByType` validators and handlers; new `BusinessErrorMessage` key(s) for the rejected-recipient case.
- **i18n:** matching `errors.*` keys in all 5 locales (en, cs, sk, uk, ru).
- **Frontend (admin app):** handle the new `429` and recipient-rejected responses on the email-template test-send UI (no new business logic in components — facade-level).
- **Manual steps (owner-only):** `manual_step: nswag-regen` if a new error contract/DTO field is added to the admin client. No DB migration anticipated.


---

The convention is `US-admin-NNNN` (4-digit). The owner-facing pattern for failures (`fiscal-failures admin area`, the `RetryFailedFiscalRegistrations` Function) is the precedent to mirror — admin-visible failures table with replay. This is an admin-persona story (the human who needs the visible dead-end is the platform operator/admin).

I have everything grounded. Here is the story.

---

# US-admin-0042 — Poison-queue dead-letter consumer with admin-visible failed-message replay

## Actor narrative

**As an** admin/platform operator,
**I want** every background-queue message that exhausts its retries to be captured in a visible, alert-raising dead-letter pipeline (instead of vanishing into an unconsumed `<queue>-poison` queue),
**so that** a permanently-failing receipt, invoice, push, pay calculation, or promo fan-out becomes a human-actionable dead-end I can see, get alerted on, and replay once the root cause is fixed — rather than being lost silently.

## Grounding (confirmed in code)

- Five queue-trigger Functions exist, one per queue, all on the same default retry policy:
  - `generate-receipt` — `GenerateReceiptFunction.cs:25`, re-throws on failure to force retry (`:107`)
  - `notifications-dispatch` — `SendPushNotificationFunction.cs:32`, re-throws (`:120`)
  - `sitewide-promo-fanout` — `SendSitewidePromoFanoutFunction.cs:57`, re-throws "poison-message pipeline retries the whole campaign" (`:162`)
  - `generate-invoice` — `GenerateInvoiceFunction.cs:13`
  - `calculate-order-pay` — `CalculateOrderPayFunction.cs:33`
  - Queue name constants: `QueueNames.cs:5-9`
- `host.json:22` sets a single global `"maxDequeueCount": 5` with **no per-queue override**. After 5 dequeues Azure auto-moves the message to `<queue>-poison`.
- **No `-poison` queue trigger exists anywhere** in `src` (confirmed: only the 5 base-queue triggers above). The `<queue>-poison` queues accumulate with **nothing consuming them and no alert**.
- Three code comments cite a "poison-message pipeline" as if it handles this — it does not exist: `SendSitewidePromoMessage.cs:23`, `SendSitewidePromoFanoutFunction.cs:28` and `:162`, `CalculateOrderPayFunction.cs:59`.
- This violates `agents/knowledge/runtime-readiness.md`:
  - Checklist **item 6** (line 75): "There's a visible dead-end for failures (a human can see what's stuck)."
  - "Background jobs & retries" (lines 58-59): "Every retry path has a dead-end ... a visible place a human can see what's stuck ... so nothing retries silently forever."
  - "What to alert on" (line 66): "A queue backing up (messages not being processed)."
- Precedent to mirror (same doc, line 47 + the existing `RetryFailedFiscalRegistrationsFunction`): the fiscal-failures admin area already establishes the "Permanent → stop + flag for the owner" pattern this story generalizes.

## Acceptance criteria

1. **Given** a message has been dequeued and re-thrown `maxDequeueCount` times on any of the 5 queues (`generate-receipt`, `generate-invoice`, `notifications-dispatch`, `sitewide-promo-fanout`, `calculate-order-pay`), **when** Azure moves it to the corresponding `<queue>-poison` queue, **then** a poison consumer picks it up (no `-poison` queue is left unconsumed), logs at `Critical` with structured properties (queue name, correlation id, tenant id, original dequeue count, raw payload), and the message is **not** silently discarded.

2. **Given** a message lands in any `<queue>-poison` queue, **when** the poison consumer processes it, **then** it raises an observable alert signal (Application Insights / Sentry per the existing telemetry stack) that the owner can configure an alert on, satisfying runtime-readiness "alert when a queue backs up" / item 6.

3. **Given** a poison message is consumed, **when** it is recorded, **then** a `FailedMessage` row is persisted (queue name, original message payload, failure reason/last exception, source correlation id + tenant id, first-seen and dead-lettered timestamps, status = `Failed`), so a human has a durable, queryable dead-end — mirroring the existing fiscal-failures pattern.

4. **Given** an admin is authenticated in the Admin app, **when** they open the failed-messages area, **then** they can list and inspect dead-lettered messages (filterable by queue and status) with enough context to diagnose the failure, and no failed message exists only in an invisible `-poison` queue.

5. **Given** an admin views a recorded `FailedMessage` whose root cause is resolved (e.g. company-info corrected, fiscal authority back up), **when** they trigger "replay", **then** the original payload is re-enqueued onto its source queue, the row's status reflects the replay (e.g. `Replayed`), and replaying is **idempotent/safe** (re-processing a receipt/invoice/pay that may have partially completed does not double-charge, double-generate, or duplicate — consistent with S7 and the existing in-handler "already exists / already calculated" guards at `GenerateReceiptFunction.cs:66-70` and `CalculateOrderPayFunction.cs:57-65`).

6. **Given** the receipt queue specifically (where the GAP notes 5 dequeues is too low for a fiscal-outage window), **when** per-queue retry policy is configured, **then** `generate-receipt` uses a `maxDequeueCount` (and/or visibility/backoff) tuned for realistic outage windows rather than the shared global `5`, while the other queues retain a deliberate, documented value — and the three stale "poison-message pipeline" comments are corrected to reference the real consumer.

## Out of scope

- Building a generic outbox / transactional-enqueue pattern for the **producer** side (the enqueue-before-commit concern at runtime-readiness line 48) — this story is the **consumer/dead-letter** side only.
- Automatic/scheduled auto-replay or self-healing retries — replay in this story is **admin-initiated** only.
- Changing the business logic, retry semantics, or re-throw behavior **inside** the 5 existing happy-path Functions (beyond correcting the misleading comments).
- Bulk replay, replay scheduling, or per-message editing of payloads before replay — single-message admin replay only.
- Customer- or partner-facing surfacing of failed messages — this is an internal admin/operator capability.
- Partner/customer mobile (Kotlin) app changes — none required.
- New alerting infrastructure or dashboards beyond emitting the `Critical` log + telemetry signal the owner can alert on (alert-rule wiring is owner/infra config).

## Layers touched

- **Azure Functions** (`Cleansia.Functions`): new generic `<queue>-poison` consumer(s) covering all 5 queues; `host.json` per-queue `maxDequeueCount` override (esp. `generate-receipt`); correct the 3 stale comments.
- **Backend — Domain** (`Cleansia.Core.Domain`): `FailedMessage` entity + status enum + repository abstraction.
- **Backend — Infra/Database** (`Cleansia.Infra.Database`): EF entity config + `MANUAL_STEP: EF migration` (owner-applied) for the `FailedMessage` table; `MANUAL_STEP` if a new tenant-scoped column needs a global filter.
- **Backend — AppServices** (`Cleansia.Core.AppServices`): CQRS handlers — `GetPagedFailedMessages` query, `ReplayFailedMessage` command + validator; uses existing `IQueueClient` to re-enqueue; `BusinessErrorMessage` keys for replay failures.
- **Backend — Admin API** (`Cleansia.Web.Admin`): admin endpoints for list + replay → `MANUAL_STEP: nswag-regen` (owner-only) for the admin client.
- **Frontend — Admin app** (`cleansia-admin-features` + admin data-access): failed-messages list/detail feature (facade + `cleansia-*`/PrimeNG table) with a replay action; `errors.*` i18n keys across all 5 locales (en/cs/sk/uk/ru).
- **Observability**: `Critical` structured logs + App Insights/Sentry signal from the poison consumer (no new infra).


---

I now have everything needed. Key grounded facts:

- `loyalty/users` route (app.routes.ts:163-168) loads `loyaltyUserRoutes`, whose only child is `:userId` (lib.routes.ts:4-10). The component (`user-loyalty-detail.component.ts:132-137`) hard-requires `userId` and redirects to `/admin-user-management` if absent — so a bare `/loyalty/users` sidebar link would bounce.
- The route's only caller is `admin-user-management.component.ts:203-208` (`viewLoyalty`), navigating `/loyalty/users/{id}` with id + email already in hand.
- The Loyalty sidebar group (app.component.ts:100-120) lists only Promo Codes / Tiers / Referrals — no user-loyalty entry.
- Sidebar i18n keys live under `sidebar.*` in all 5 admin locales (en.json:217-220 etc.); adding one means adding to all 5 (conventions.md:107-111).
- Story template at `agents/templates/story.md` defines the expected shape.

Here is the user story.

---

```yaml
---
id: US-admin-0001
title: Look up any user's loyalty account directly from the Loyalty menu
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As an **admin**, I want **to reach a user's loyalty account from the Loyalty section of the sidebar** (by searching/picking the user), so that **I can inspect tier, points, and activity — and grant/revoke points — without first remembering that the only door is the "View loyalty" row action buried in Admin User Management.**

## Context (grounded in code)

The destination already exists and works: route `loyalty/users` -> `loyaltyUserRoutes` (`apps/cleansia-admin.app/src/app/app.routes.ts:163-168`), whose single child path is `:userId` (`libs/cleansia-admin-features/loyalty-user-detail/src/lib/lib.routes.ts:4-10`), rendering `UserLoyaltyDetailComponent`. That component **requires** a `userId` and redirects to `/admin-user-management` when it is missing (`user-loyalty-detail.component.ts:132-137`). The **only** navigator to it today is `viewLoyalty()` in `admin-user-management.component.ts:203-208`, which already holds the user's `id` (and passes `email` as a query param). The Loyalty sidebar group (`app.component.ts:100-120`) lists only Promo Codes, Tiers, and Referrals — there is no entry that lets an admin start from "I want this user's loyalty." This is a discoverability gap, not a dead route.

Because the detail page is a per-user drill-in (it needs a `userId`), the fix is **not** a bare `/loyalty/users` menu link (that would bounce per lines 132-137). The menu entry must land the admin somewhere they can choose a user first. The lowest-risk, convention-consistent landing is the existing Admin User Management list (which already carries the "View loyalty" row action), surfaced under the Loyalty group as a "Look up a user's loyalty" entrypoint.

## Acceptance criteria

- **AC1** — Given an authenticated admin viewing the sidebar, When they expand the **Loyalty** group, Then a new item (e.g. `sidebar.loyalty_user_lookup`, "Look up user") appears alongside Promo Codes, Tiers, and Referrals, with an icon, following the existing child-item shape in `app.component.ts:100-120`.
- **AC2** — Given the admin clicks the new Loyalty "Look up user" item, When navigation completes, Then they land on a page that lets them select a user (the existing Admin User Management list, which exposes the "View loyalty" row action) — never on a blank/redirected page.
- **AC3** — Given the admin selects a user from that landing page, When they invoke "View loyalty", Then they arrive at the existing `UserLoyaltyDetailComponent` for that `userId` with the user's loyalty account, tier, and activity loaded — i.e. the existing drill-in path (`admin-user-management.component.ts:203-208`) continues to work unchanged.
- **AC4** — Given any of the 5 supported admin locales (en, cs, sk, uk, ru), When the sidebar renders the new item, Then its label resolves to a real translation under `sidebar.*` (no raw key shown, no missing-key fallback), consistent with `conventions.md` "adding a key means adding it to all five."
- **AC5** — Given a non-admin (or unauthenticated) actor, When they attempt to reach the lookup entry or `loyalty/users/:userId`, Then access is still gated by `adminGuard` exactly as the other Loyalty routes are (`app.routes.ts:147-177`) — the new entry introduces no new unguarded surface.
- **AC6** — Given the Loyalty group already had three items, When the new item is added, Then the existing Promo Codes / Tiers / Referrals entries and their routes are unchanged and still reachable (no reordering regressions, no duplicate routes).

## Out of scope

- **No backend changes.** No new query, command, endpoint, or DTO; the loyalty account/activity APIs and `BusinessResult` flows are untouched (so **no `manual_step: nswag-regen`**).
- **No new "loyalty users" list page or list endpoint.** We are not building a paginated directory of loyalty accounts; we reuse the existing Admin User Management list as the picker. (Building a dedicated loyalty-user list would be a separate, larger story.)
- **No change to `UserLoyaltyDetailComponent`, its facade, the grant/revoke dialog, or the `:userId` route contract** — the drill-in stays `userId`-driven.
- **No change to the existing `viewLoyalty()` row-action behavior** in `admin-user-management.component.ts` (it remains the primary drill-in).
- **No deletion of any route** (the route is reachable today; this is purely additive discoverability).
- Marketing, Referrals, Tiers, and Promo Codes menu items and pages — untouched.
- No customer/partner app or Android changes (admin web only).

## Layers touched

- **Frontend — admin app only.** Primarily `apps/cleansia-admin.app/src/app/app.component.ts` (`sidebarMenuItems`, Loyalty `children` array, lines 100-120) and the 5 i18n files `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` (new `sidebar.*` key). If the chosen landing differs from the existing `/admin-user-management` route, a small additive route may be needed in `app.routes.ts`, guarded by `adminGuard`.
- **Backend:** none.
- **Mobile:** none.
- **DB / migrations:** none.

## Open questions

- **Q (product / owner):** Should the Loyalty "Look up user" entry land on the **existing Admin User Management list** (zero new surface, reuse the current "View loyalty" row action) — the default assumption taken here — **or** does product want a **dedicated loyalty-user search/list** (larger, needs a backend paged query per `consistency.md` §A)? Default until answered: reuse Admin User Management as the picker. *(Note: a bare `/loyalty/users` link is explicitly rejected because `user-loyalty-detail.component.ts:132-137` redirects away without a `userId`.)*
- **Q (wording, all 5 locales):** Exact label for the new item ("Look up user" vs "User loyalty" vs "Find user") — a tone/wording call; a placeholder key is added and flagged per `conventions.md:107-111` rather than invented silently.


---

I have everything I need. `RecomputeTier` is only called from `GrantPoints`/`RevokePoints` (never in bulk after a threshold edit), which is the second half of the GAP: even if `RecomputeTier` read config, an admin threshold edit wouldn't retroactively reclassify existing users until their next grant/revoke — and the `UpdateTierConfig` comment and PreviewTierThresholdImpact both falsely imply it does ("Threshold edits retroactively reclassify users"). This belongs in the story's scope decision. Here is the grounded user story.

---

# US-admin-0042 — Tier thresholds the admin edits must actually drive tier assignment

## Context (grounded in code)

`LoyaltyAccount.RecomputeTier()` assigns a customer's `CurrentTier` from **hardcoded literals** — `>= 5000` Platinum, `>= 2000` Gold, `>= 500` Silver, else Bronze (`src/Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs:100-106`). The editable per-tenant `LoyaltyTierConfig.LifetimePointsThreshold` (`LoyaltyTierConfig.cs:16`) is **never read** on the assignment path. Three things are broken as a result:

1. **The admin editor is dead.** `UpdateTierConfig` persists a new `LifetimePointsThreshold` (`UpdateTierConfig.cs:89-94`) and `AdminLoyaltyTierController` exposes `PUT /update/{tierConfigId}`, but no code consumes that value to assign tiers. The admin changes a number that changes nothing.
2. **The preview lies.** `PreviewTierThresholdImpact.Handler` computes "how many users move tier" by running `ResolveTier(points, proposed)` against the proposed config thresholds (`PreviewTierThresholdImpact.cs:60-108`). Since the real engine uses different hardcoded numbers, the preview's "current" and "new" counts are both fiction relative to what users actually hold.
3. **Discount and threshold are silently inconsistent.** `LoyaltyService.ResolveTierDiscountForOrderAsync` correctly loads the config discount **by `account.CurrentTier`** (`LoyaltyService.cs:163`), but `CurrentTier` came from the hardcoded ladder. So a customer can sit at a tier (and get its discount) that contradicts the config thresholds the admin set and that `GetLoyaltyTiers` shows them on the Rewards ladder (`GetLoyaltyTiers.cs:33-43`).

This violates the project's own bars: "**No magic numbers** … discounts, … all come from a named home" and "prefer the design that makes the next change … config-driven" (`conventions.md`), and the rich-domain rule that handlers/domain operate from real config rather than baked-in constants. The infrastructure to fix it already exists: `ILoyaltyTierConfigRepository.GetAllForTenantAsync` / `GetByTierAsync` (`ILoyaltyTierConfigRepository.cs:11-16`) and the `ResolveTier(points, thresholds)` switch already written in the preview handler.

## Actor narrative

**As an** admin (loyalty-program operator),
**I want** the tier thresholds I configure per tenant to be the single source of truth that actually assigns customers to tiers,
**so that** editing a threshold genuinely re-tiers customers (and the resulting discounts follow), and the impact preview I rely on before saving reflects reality instead of a hidden second ladder.

## Acceptance Criteria

**AC1 — Config drives assignment (root cause)**
**Given** a tenant whose `LoyaltyTierConfig` rows set Silver=300, Gold=1500, Platinum=4000
**When** a customer's `LifetimePoints` is recomputed after a grant or revoke
**Then** `CurrentTier` is resolved from those configured `LifetimePointsThreshold` values (e.g. 1600 points → Gold), and **no** `5000 / 2000 / 500` literal participates in the decision.

**AC2 — Threshold edit re-tiers existing customers**
**Given** existing customers already assigned tiers under the old thresholds
**When** an admin saves a `LoyaltyTierConfig` threshold change via `PUT /api/AdminLoyaltyTier/update/{tierConfigId}`
**Then** affected customers' `CurrentTier` is recomputed against the new thresholds (their `TierAchievedOn` updating only when the tier actually changes), so the editor's documented promise that "threshold edits retroactively reclassify users" becomes true rather than aspirational.

**AC3 — Preview matches the engine**
**Given** the admin opens `PreviewTierThresholdImpact` with proposed thresholds
**When** the preview computes `CurrentCount` per tier
**Then** those current counts equal what the live engine would assign for the same customers under the saved config (preview and assignment use one shared threshold-resolution function), so the preview no longer disagrees with reality.

**AC4 — Discount and assigned tier are consistent**
**Given** a customer assigned to a tier by AC1's config-driven logic
**When** `ResolveTierDiscountForOrderAsync` loads the discount by `CurrentTier`
**Then** the discount applied corresponds to the **same** config row whose threshold qualified the customer — the tier shown on the Rewards ladder (`GetLoyaltyTiers`), the tier on the order, and the discount all reference one config.

**AC5 — Missing / partial config is safe**
**Given** a tenant is missing one or more `LoyaltyTierConfig` rows
**When** a tier is recomputed
**Then** the customer falls back to the lowest tier (Bronze) for any threshold that cannot be resolved (no crash, no negative/`int.MaxValue` leakage to the client), consistent with the null-safe defaulting already used in the preview handler.

**AC6 — Bronze floor is honored without a literal**
**Given** the Bronze tier is the entry tier
**When** a customer with 0 lifetime points is recomputed
**Then** they resolve to `BronzeCleaner` from configured thresholds (Bronze threshold treated as the 0/floor entry), with the previous behavior preserved and the hardcoded `_ => BronzeCleaner` literal removed.

## Out of scope

- Changing tier **discount** math, `MinimumOrderAmountForDiscount` behavior, or the points-earning formula (`TotalPrice / 10`).
- Adding, removing, or renaming `LoyaltyTier` enum values, or making the number of tiers configurable.
- New admin UI screens — the editor, preview, and ladder UIs already exist; only their behavior must become truthful. Any DTO/contract change that ripples to the generated client is flagged `manual_step: nswag-regen`, not done here.
- A scheduled/background re-tiering job or notifications (`loyalty.tier_upgrade` push) firing for AC2's bulk reclassification — bulk re-tier is a data correction, not a per-user promotion event; if a push is desired on bulk demotion/promotion, raise a separate ticket.
- Migrations/seed edits: if a tenant seed lacks a full set of `LoyaltyTierConfig` rows, flag `manual_step: ef-migration`/seed rather than editing seeds here.
- The two unrelated tier-as-`Earn`-source / referral grant paths.

## Layers touched

- **Domain** — `Cleansia.Core.Domain/Loyalty/LoyaltyAccount.cs`: `RecomputeTier` must resolve from configured thresholds instead of literals. Per project rule B7 (domain stays rich, handlers don't reach into the DB), thresholds are **passed into** the domain method (e.g. an ordered threshold set / a small value object) rather than the entity loading a repository.
- **AppServices** — `LoyaltyService` (the grant/revoke and discount caller) supplies the tenant's thresholds to the domain on recompute; extract the existing `ResolveTier(points, thresholds)` switch from `PreviewTierThresholdImpact` into one shared helper so preview and engine cannot diverge (AC3). A new command/handler for AC2 bulk re-tier on threshold save, returning a `Response` per convention B1.
- **Repositories (read-only reuse)** — `ILoyaltyTierConfigRepository.GetAllForTenantAsync` / `GetByTierAsync` already exist; no new query shape needed.
- **Web.Admin** — `AdminLoyaltyTierController.UpdateTierConfig` triggers the AC2 recompute (behavioral wiring; endpoint/permissions unchanged).
- **Tests** — `Cleansia.Tests`: TDD-first for `RecomputeTier` from config (AC1, AC5, AC6), preview/engine parity (AC3), and bulk re-tier (AC2), per `conventions.md` "develop test-first" for pure domain logic.


---

I have everything I need to ground the story in real conventions. The codebase has a canonical paged-query pattern (consistency.md A1-A8, patterns-backend.md `GetPagedOrders`), and conventions.md:121 confirms the "empty/loading/error states are part of done" rule. Now writing the user story.

---

# US-admin-0042 — Eliminate the country "serviced" N+1 in admin Service-Area management

## Actor narrative

**As an** admin managing the platform's service area (`/admin` → Service area page),
**I want** the country catalog and each country's *serviced* flag to load in a single backend round-trip (not one extra call per country),
**so that** the page loads predictably as the country catalog grows, and the admin country list follows the same paged, consistent contract as every other admin list in Cleansia.

## Grounding (what the code does today)

- `service-area-management.facade.ts:47-81` (`loadCountries`) calls `adminCountryClient.getOverview()` to get the catalog, then **fans out one `adminCountryClient.details(id)` call per country** (lines 64-68 `ids.map(... .details(id))`, executed via `forkJoin` at line 69) **purely to read `IsServiced`**. The in-code comment at lines 40-46 explicitly admits this is acceptable only for ~50 countries and anticipates a not-yet-existing `/admin-countries/serviced-ids` endpoint.
- The N+1 exists because the list DTO lacks the flag: `CountryListItem` (`DTOs/CountryListItem.cs:5-9`) has `Id, IsoCode, Name, Translations` but **no `IsServiced`**, whereas `CountryDetailDto` (`DTOs/CountryDetailDto.cs:3-7`) carries `IsServiced`. So the only way to learn "is this country serviced" from the list is to open every detail.
- The backing query `GetCountryOverview.cs:9-24` returns a plain `IEnumerable<CountryListItem>` (no paging, no `IsServiced`). There is **no `GetPagedCountries`**, unlike the canonical paged admin queries (`GetPagedOrders`, `GetPagedDisputes`, etc.; pattern in `agents/knowledge/consistency.md` A1–A8 and `patterns-backend.md:112+`).
- ISO-code uniqueness audit (per the GAP): `CreateCountry.cs:28-30` **does** enforce uniqueness via `ExistsWithIsoCodeAsync` → `BusinessErrorMessage.CountryIsoCodeAlreadyExists`. `UpdateCountry.cs` **does not accept or mutate `IsoCode`** (command is `(CountryId, Name)`, handler only calls `UpdateName`, lines 11-13, 50) — so ISO code is effectively immutable and there is no update-time uniqueness gap. This is recorded so the story's scope is deliberate, not an oversight.

## Acceptance criteria

1. **Given** the admin opens the Service-area page with N serviced/active countries, **when** the page loads the country list, **then** the *serviced* state for every country is available from a single list response — observable as exactly **one** country-list network request (no per-country `details(id)` calls) in the browser network panel.

2. **Given** the admin country-list response, **when** it is inspected, **then** each row exposes the `IsServiced` flag directly (i.e. the toggle in `service-area-management.component` reflects serviced state without any follow-up detail call), and `servicedCountryIds` is derived from that single response.

3. **Given** the admin toggles a country's serviced flag, **when** the change succeeds, **then** the existing `setCountryServiced` behavior and success snackbar are unchanged, and re-loading the list reflects the new state from the single-call source of truth.

4. **Given** the country list endpoint, **when** it is implemented/extended, **then** it conforms to the project's admin-list contract — either `CountryListItem` gains `IsServiced` (preferred minimal fix) and/or a `GetPagedCountries` query is added following the canonical `DataRangeRequest → PagedData<CountryListItem>` shape (consistency.md A1–A8, `GetPagedOrders` template) — and the customer-facing `GetServicedCountries`/`GetCountryOverview` semantics are not altered.

5. **Given** a backend DTO/endpoint change lands, **when** the frontend consumes it, **then** the NSwag admin client is regenerated by the owner (flagged `manual_step: nswag-regen`) — Claude must not run `npm run generate-*-client` or hand-edit the generated client.

6. **Given** the loaded page, empty catalog, and a failed list call, **when** each occurs, **then** the page renders the three explicit data states (loaded / empty / error) per conventions.md:121 — the current code silently swallows the error into `of([])` (facade line 53), which the new state handling must surface rather than show a blank "0 countries" page.

## Out of scope

- Editing or re-validating a country's **ISO code on update** — `UpdateCountry` intentionally does not expose `IsoCode`; no update-time uniqueness rule is to be added here. (The create-time uniqueness rule already exists and stays.)
- **ServiceCity** CRUD (`loadCities`/`createCity`/`updateCity`/`deleteCity`, facade lines 105-186) — unchanged.
- The customer/partner-facing `GetServicedCountries` and the customer `GetCountryOverview` consumption — no behavior change.
- Search/sort/filter UX on the country list, and any pagination UI controls, **unless** option B (`GetPagedCountries`) is chosen; if so, keep it to wiring the canonical paged contract, not new filter affordances.
- Country create/delete flows, translations management, and any new `serviced-ids` endpoint beyond what AC-4 requires (a standalone `serviced-ids` endpoint is an acceptable implementation but not mandated).
- DB migrations and `IsServiced` storage changes — `IsServiced` already exists on the domain/`CountryDetailDto`; no schema change is anticipated (if the chosen implementation needs one, flag `MANUAL_STEP` for the owner).

## Layers touched

- **Backend — AppServices** (`Features/Countries/`): add `IsServiced` to `CountryListItem` mapping and/or add `GetPagedCountries` (query + handler + filter/sort/spec per the paged pattern). Touches `DTOs/CountryListItem.cs`, the country mapper (`MapToDto`), `GetCountryOverview.cs`, and possibly the admin Country controller/repository (`ICountryRepository`).
- **Backend — Web.Admin**: controller endpoint surface for the list/paged query.
- **Contract — NSwag** (owner-only `manual_step: nswag-regen`): regenerate `@cleansia/admin-services` (`CountryListItem`, client wrapper).
- **Frontend — admin** (`libs/cleansia-admin-features/country-management/service-area-management`): rewrite `loadCountries` to drop the `forkJoin` detail fan-out (facade.ts:47-81), derive `servicedCountryIds` from the single response, and implement the three explicit data states in the component.
- **i18n**: only if a new error/empty-state string is introduced — add keys across all 5 locales (`en/cs/sk/uk/ru`).
- **Tests**: backend handler/mapper unit test for `IsServiced` on the list (and paged query if added); frontend facade test asserting a single list call and correct `servicedCountryIds` derivation plus the empty/error states.


---

I have everything I need. Key confirmations grounded in code:

- `EmployeeDocument` (domain entity) has **no content-hash field** — only `FileSizeBytes`, `FileName`, `FilePath`, `Version`, `Status`, `PreviousVersionId` (`EmployeeDocument.cs:8-41`). So there is no existing column to de-dup on.
- `SaveMyDocuments.Handler` uploads first (`client.UploadAsync`, line 135) **then** versions, with no idempotency/content check — exactly the S7 gap. The same defect exists in the admin sibling `UploadEmployeeDocument.Handler` (`UploadEmployeeDocument.cs:106-138`).
- `GetLatestByFileNameAsync` already filters `IsActive` + orders by `Version` desc (`EmployeeDocumentRepository.cs:71-77`) — the natural place to compare a hash against the latest active version.
- S7 explicitly names this pattern (idempotency on side-effecting commands; check before acting) and cites `LoyaltyService`/`ReferralService` ledger-check reference patterns.

Now I'll write the user story.

---

# US-partner-0042 — Make employee document upload idempotent (no duplicate blobs / spurious versions)

## Actor narrative

**As a** partner (employee) uploading my onboarding/compliance documents from the partner web or mobile app,
**I want** the system to recognise when I re-submit a file I have already uploaded (because of a double-tap, a flaky-network retry, or a pipeline retry) and treat it as the same document,
**so that** I do not silently create duplicate blobs and bogus new "versions" of an identical file, and the admin reviewing my documents sees one entry per real file instead of near-identical duplicates.

(Secondary beneficiary: the **admin** who reviews documents and the platform owner who pays for blob storage. Same fix must cover the admin-initiated upload path.)

## Context / grounding

- `SaveMyDocuments.Handler` (partner self-service): `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs:112-184`. For each document it builds a GUID-suffixed unique blob name (`{employeeId}_{DocumentType}_{timestamp}_{guid}{ext}`, lines 119-123), unconditionally calls `client.UploadAsync(...)` (line 135), then looks up the latest active row by **filename only** via `GetLatestByFileNameAsync` (lines 139-142) and either bumps `Version` (`CreateNewVersion`) or creates V1 — with **no request-level or content-level de-dup**.
- The identical defect exists on the admin path: `UploadEmployeeDocument.Handler` at `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/UploadEmployeeDocument.cs:106-138` (same "filename exists → new version" logic, no content check).
- The domain entity has **no content-hash column** today: `src/Cleansia.Core.Domain/Documents/EmployeeDocument.cs:8-41` exposes `FileName`, `FilePath`, `ContentType`, `FileSizeBytes`, `Version`, `PreviousVersionId`, `Status` — there is nothing to compare content against yet.
- The lookup repo method already scopes by `EmployeeId + FileName + IsActive` and orders by `Version` desc: `src/Cleansia.Infra.Database/Repositories/EmployeeDocumentRepository.cs:71-77` — the natural seam for a hash comparison.
- **Rule cited:** S7 (Idempotency on side-effecting commands) in `agents/knowledge/security-rules.md:84-91` — "*check whether the side effect already happened … before doing it again*", with `LoyaltyService.GrantForCompletedOrderAsync` / `ReferralService.ProcessQualifyingOrderAsync` as the ledger-check reference pattern. Blob upload + new DB row is a doublable side effect. Also B8 in `agents/knowledge/consistency.md:76-79` ("Side-effecting commands are idempotent (S7)").

## Acceptance criteria

1. **Re-upload of identical content is a no-op version-wise (partner path)**
   **Given** an employee already has an active `EmployeeDocument` for filename `X` whose stored content hash equals the content being submitted,
   **When** the same employee calls `SaveMyDocuments` again with filename `X` and byte-identical content,
   **Then** no new `EmployeeDocument` row is created and `Version` is **not** incremented, and the response returns the existing document (its current `DocumentId` and `Version`).

2. **No orphan/duplicate blob is left when content is unchanged**
   **Given** the re-upload in AC1,
   **When** the handler determines the content is identical to the latest active version,
   **Then** it does **not** persist a second blob for that content (either it does not upload, or any speculative upload is cleaned up), so the blob count for that `(employeeId, filename)` does not grow on a duplicate submit.

3. **A genuinely changed file still versions correctly**
   **Given** an employee has an active `EmployeeDocument` for filename `X`,
   **When** they upload filename `X` again with **different** content (different hash),
   **Then** a new version is created with `Version = previous + 1`, `Status = Pending`, and `PreviousVersionId` pointing at the prior version — i.e. legitimate re-submission/versioning is preserved.

4. **Admin upload path is equally idempotent**
   **Given** the same `(employeeId, filename, content-hash)` already exists as the latest active version,
   **When** `UploadEmployeeDocument` is invoked (admin-side),
   **Then** it behaves identically to AC1 (no duplicate row, no version bump, returns the existing document).

5. **Distinct files are unaffected**
   **Given** an employee uploads multiple documents in one `SaveMyDocuments` call,
   **When** the files differ from each other and from anything already stored,
   **Then** each is saved exactly once as a new V1 (or correct next version), with no false-positive de-dup collapsing distinct documents.

6. **Idempotency outcome is observable, not silent corruption**
   **Given** a duplicate submission is detected,
   **When** the handler returns,
   **Then** the result is a `BusinessResult.Success` referencing the existing document (the caller cannot tell a retry from the original "save"), and no error is surfaced to the partner.

## Out of scope

- Client-supplied idempotency-key header/infrastructure (a request-id ledger). This story de-dups on **content hash of the latest active version**; an explicit idempotency-key mechanism is a separate, larger ticket.
- Rate limiting on the upload endpoints (S5) — separate concern.
- Cross-employee or cross-tenant de-duplication / global blob de-dup — de-dup is scoped to a single `(employeeId, filename)` chain only.
- Retroactive cleanup of already-duplicated blobs/rows in existing data (a one-off data-migration/cleanup job, if wanted, is its own ticket).
- Changing the document review/approval lifecycle, `DocumentStatus` transitions, or admin review UI.
- File-type/size validation changes (already handled in the validators).
- The `Address.State` and other unrelated entities.

## Layers touched

- **Domain** — `Cleansia.Core.Domain/Documents/EmployeeDocument.cs`: add a content-hash field (e.g. `ContentHash`) set in `Create` / `CreateNewVersion`, plus an equality/idempotency check helper.
- **Backend (AppServices)** — `Features/EmployeeDocuments/SaveMyDocuments.cs` and `UploadEmployeeDocument.cs`: compute the content hash, compare against the latest active version **before** uploading the blob / adding a row; short-circuit on match (mirroring the S7 ledger-check pattern).
- **Infra (Database)** — `EntityConfigurations/EmployeeDocumentEntityConfiguration.cs` and the `EmployeeDocumentRepository` (the `GetLatestByFileNameAsync` seam) for the new column / optional hash lookup.
- **Infra (Blobs)** — the blob upload path (`IBlobContainerClientFactory` usage) only if AC2 requires "upload-then-rollback" cleanup; preferred design avoids the speculative upload entirely.

## Manual steps (owner-only, flag in spec)

- `manual_step: ef-migration` — new **nullable** `ContentHash` column on `EmployeeDocument` (nullable keeps it S9-safe; legacy rows simply won't de-dup until re-uploaded). No backfill required.
- `manual_step: nswag-regen` — only if the `SavedDocument` / `Upload...Response` DTO shape changes (e.g. an added "was this a duplicate" indicator). If the response contract is left identical, no regen is needed — call this out in the spec so the owner knows which case applies.


---

`Referral` is `ITenantEntity` — so a system-level sweep must read cross-tenant (the membership sweep uses `GetQueryableIgnoringTenant()`, but `GetExpirableAsync` uses the plain `GetDbSet()` which applies the tenant filter). This is a real implementation consideration worth flagging in the story. I have everything needed.

---

# US-admin-0042 — Stale referrals never expire (background expiry sweep is unreachable)

## Context / Grounded Findings

| Claim | Verified at |
|---|---|
| `ExpireStaleReferralsAsync` exists | `src/Cleansia.Core.AppServices/Services/ReferralService.cs:236` |
| It has **zero callers** (only the definition + interface decl) | `ReferralService.cs:236`, `IReferralService.cs:74` — repo-wide grep returns only those two lines |
| 90-day window enforced lazily only on order completion | `ReferralService.cs:180-185` (inside `ProcessOrderCompletedAsync`) |
| `GetExpirableAsync` filters `Status == Accepted && AcceptedOn < cutoff` | `src/Cleansia.Infra.Database/Repositories/ReferralRepository.cs:50-56` — **matches the proposed-fix assumption** |
| `QualifyingWindowDays = 90` | `src/Cleansia.Core.AppServices/Features/Orders/ReferralPolicy.cs:22` |
| Admin "Expired" status filter exists and queries the persisted `Status` column | `libs/cleansia-admin-features/loyalty-referrals/src/lib/referrals-list/referrals-list.facade.ts:93-94`; `referrals-list.component.ts:90-91,224-225` |
| Reference timer Function to mirror | `src/Cleansia.Functions/Functions/SendMembershipLifecycleNotificationsFunction.cs` |

**Confirmed defect:** A referred user who signs up (`Referral` row created in `Accepted` via `AcceptAsync`, `ReferralService.cs:145-151`) but never completes an order never triggers `ProcessOrderCompletedAsync`, so the row stays `Accepted` forever. The admin **Expired** filter therefore returns rows only in the (impossible-today) case where the timeout path ran, and conversion/expiry reporting is permanently skewed.

**Two convention nuances surfaced while grounding (carried into AC + out-of-scope):**
1. The reference function dispatches a **MediatR Command** (`mediator.Send(new SendMembershipLifecycleNotifications.Command())`) so the **UnitOfWork pipeline auto-commits** — it does NOT resolve a service and call `CommitAsync` manually. `ReferralService` handlers per `agents/knowledge` "never call `CommitAsync()` in handlers — UnitOfWork pipeline handles it" (the one exception, `EnsureCodeForUserAsync`, is explicitly documented as commit-here). `ExpireStaleReferralsAsync` does **not** commit. So the sweep must run on a commit-bearing path.
2. `Referral` is `ITenantEntity` (`Referral.cs:15`), and `GetExpirableAsync` uses the tenant-filtered `GetDbSet()` (not `GetQueryableIgnoringTenant()`). A system-level (no-JWT) Function has no tenant context, so the sweep would silently match **zero** rows unless tenant scoping is bypassed — exactly the pattern the membership sweep uses. This must be resolved or the new Function is a no-op.

---

## Story

**ID:** US-admin-0042
**Title:** Stale referrals automatically expire on a daily background sweep

> **As an** admin operating the referral program,
> **I want** referrals that pass the 90-day qualifying window without the invitee completing their first order to be automatically flipped from `Accepted` to `Expired` on a recurring schedule,
> **so that** the "Expired" filter and the conversion/expiry reporting on the referrals dashboard reflect reality instead of leaving abandoned referrals stuck in `Accepted` forever.

---

## Acceptance Criteria (Given / When / Then)

**AC1 — Sweep runs on a schedule and expires overdue referrals**
- **Given** a `Referral` in `Accepted` status whose `AcceptedOn` is more than `ReferralPolicy.QualifyingWindowDays` (90) days in the past, and the invitee has never completed a qualifying order,
- **When** the scheduled referral-expiry sweep runs,
- **Then** that referral's `Status` becomes `Expired` (via `MarkExpired`), the change is committed to the database, and the run is logged with the count of referrals expired.

**AC2 — In-window referrals are left untouched**
- **Given** a `Referral` in `Accepted` status whose `AcceptedOn` is within the last 90 days,
- **When** the sweep runs,
- **Then** the referral remains `Accepted`, no points are granted, and it is not counted in the expired total.

**AC3 — Already-resolved referrals are never touched (idempotent / status-respecting)**
- **Given** referrals already in `Qualified` or `Expired` status (regardless of age),
- **When** the sweep runs, possibly multiple times in a day,
- **Then** their status is unchanged and no duplicate logging/side effects occur (sweep only selects `Status == Accepted && AcceptedOn < cutoff`, per `ReferralRepository.cs:54`).

**AC4 — No reward side effects on expiry**
- **Given** a stale `Accepted` referral being expired by the sweep,
- **When** it transitions to `Expired`,
- **Then** **no** loyalty points are granted to either side and the inviter's referral-code "friends qualified" counter is **not** incremented (expiry is data hygiene only — contrast with the qualify path at `ReferralService.cs:207-233`).

**AC5 — Admin "Expired" filter reflects swept rows**
- **Given** the sweep has expired one or more referrals,
- **When** an admin opens the referrals list and selects the **Expired** status filter (`referrals-list.facade.ts:93-94`),
- **Then** the newly-expired referrals appear in the result set, and the program's expiry/conversion counts move accordingly. *(No frontend code change is expected — this AC asserts the existing UI becomes correct once data flows.)*

**AC6 — Sweep operates across all tenants**
- **Given** stale `Accepted` referrals exist under more than one `TenantId`,
- **When** the system-level sweep runs (no JWT / no tenant context),
- **Then** stale referrals from **every** tenant are expired — i.e. the implementation must not silently no-op due to the EF global tenant query filter (`Referral` is `ITenantEntity`, `Referral.cs:15`).

---

## Out of Scope

- Notifying users (push/email) that their referral expired — expiry is silent/cosmetic per the `IReferralService` doc comment ("No points granted; cosmetic data hygiene only", `IReferralService.cs:71-74`).
- Any change to the **qualify** path or point-grant amounts (`ReferralPolicy.PointsPerSide = 150`).
- Changing the 90-day window value or making it configurable.
- One-time backfill of historically-stuck `Accepted` referrals — call out as a **separate** decision (the daily sweep will catch them on first run since `AcceptedOn < cutoff` already holds; an explicit backfill ticket is not required by this story but should be acknowledged).
- New admin UI, columns, charts, or an "expire now" manual button — AC5 only asserts the existing filter becomes correct.
- Frontend changes of any kind; NSwag client regeneration.
- Reworking how `ProcessOrderCompletedAsync` enforces the window inline (`ReferralService.cs:180-185`) — it stays as the lazy fast-path; the sweep is the safety net.
- Production cron hardening — match the existing dev cadence convention (`SendMembershipLifecycleNotificationsFunction` uses `0 */2 * * * *`); a tightened production schedule is a follow-up, consistent with that function's own comment.

---

## Layers Touched

| Layer | Change |
|---|---|
| **Azure Functions** (`src/Cleansia.Functions/`) | New timer-triggered function mirroring `SendMembershipLifecycleNotificationsFunction.cs` (daily/dev-cadence `TimerTrigger`). |
| **AppServices** (`src/Cleansia.Core.AppServices/`) | Preferred: wrap the sweep in a **MediatR Command + Handler** so the UnitOfWork pipeline auto-commits (matches the membership pattern and the "no manual `CommitAsync` in handlers" rule). If instead resolving `IReferralService` directly per the proposed fix, the function MUST commit via `IUnitOfWork` since `ExpireStaleReferralsAsync` does not. |
| **Infra.Database** (`src/Cleansia.Infra.Database/`) | Likely a tweak so the sweep reads **cross-tenant** (the membership sweep uses `GetQueryableIgnoringTenant()`; `GetExpirableAsync` currently uses tenant-filtered `GetDbSet()`, `ReferralRepository.cs:53`). Needed to satisfy AC6. |
| **DI / `Program.cs`** (`src/Cleansia.Functions/Program.cs`) | Ensure the chosen entry point (MediatR command handler or `IReferralService`) is resolvable in the Functions host. `AddCoreBindings` already registers core services; confirm `IReferralService`/handler are included. |
| **Domain** | None — `Referral.MarkExpired` and `ReferralStatus.Expired` already exist. |
| **Frontend / Mobile / Tests-UI** | None (AC5 verifies existing behavior; backend unit tests for the new handler/sweep are expected but no UI work). |

**Suggested MANUAL_STEP flags for the spec:** none for NSwag (no contract change). Flag a decision point on the **cross-tenant read** approach (AC6) and on whether a one-time historical backfill ticket is opened separately.


---

I now have everything grounded in real code and real rules (S7 names push as a doublable side effect; B8 flags the broad-catch-without-idempotency-guard anti-pattern; LoyaltyService is the canonical ledger pattern to mirror). Here is the user story.

---

# US-admin-0042 — Make push dispatch idempotent so queue re-delivery never double-notifies users

## Persona / Actor narrative

**As an** admin who triggers user-facing notifications (transactional order events and, critically, the **sitewide promo** fan-out to the entire opted-in user base),
**I want** push dispatch to be idempotent — so a single logical event delivers **at most one** push per device even when the notification queue re-delivers a message (retry on `throw;`, cold-start init race, partial-success-then-exception, or a poison-message campaign retry),
**so that** users are not spammed with duplicate notifications, the marketing fan-out cannot re-notify the whole user base on a transient hiccup, and operators get an accurate dispatch log instead of a silent "all-failed, nothing pruned" cold-start drop.

> Persona is `admin` because the admin is the actor whose action (`SendSitewidePromo` → `SendSitewidePromoFanoutFunction`) carries the highest blast radius of the gap; the fix protects every push-receiving customer/partner device downstream.

## Grounding (real code + real rules)

- **No dedup key anywhere in the send path.** `SendPushNotificationMessage` (`src/Cleansia.Core.Queue.Abstractions/Messages/SendPushNotificationMessage.cs:18-22`) carries only `UserId, EventKey, Args, TenantId` — no dispatch/idempotency id. `IPushDispatcher.SendAsync` and `PushDispatchResult` (`src/Cleansia.Core.Clients.Abstractions/Fcm/*.cs`) have no notion of "already sent."
- **Queue re-delivery is guaranteed on any failure.** `SendPushNotificationFunction.cs:120` `throw;` re-queues; `SendSitewidePromoFanoutFunction.cs:162` `throw;` re-pages the **entire** opted-in user set and re-enqueues a fresh per-user push for everyone — no campaign-level ledger.
- **Partial-success-then-failure re-sends.** In `FcmPushDispatcher.SendAsync` (`FcmPushDispatcher.cs:70-81`), `SendEachForMulticastAsync` can partially succeed before an exception; the broad catch returns `(0, count, [])`, the Function logs all-failed and re-throws, and redelivery re-pushes tokens that already received it. A failure of `CommitAsync` after pruning (`SendPushNotificationFunction.cs:107`) has the same effect.
- **Cold-start init race silently drops one event.** `EnsureInitialized` returns `null` on transient init failure leaving `_initAttempted=false` (`FcmPushDispatcher.cs:170-179`) — good for next-time retry — but the current dispatch reports `(0, allFailed, [])` (`:52`) and the Function logs all-failed with **nothing pruned** at Warning only.
- **Rules cited:** **S7** (`agents/knowledge/security-rules.md:84-91`) lists push among doublable side effects that *must* be idempotent (check a ledger before re-doing the side effect), with `LoyaltyService.GrantForCompletedOrderAsync` (`src/Cleansia.Core.AppServices/Services/LoyaltyService.cs:52-60`) as the canonical "bail if ledger entry exists" pattern. **Consistency B8** (`agents/knowledge/consistency.md:75-79`) explicitly flags "try/catch but no idempotency guard" and "broad `catch (Exception)` for control flow" — both present in `FcmPushDispatcher.cs:75-81`.

## Acceptance Criteria (Given / When / Then)

1. **AC1 — Redelivery of the same logical event does not re-push (per-user transactional)**
   **Given** a `SendPushNotificationMessage` for a `(UserId, EventKey)` whose push was already delivered to a device,
   **When** the queue re-delivers the identical message (e.g. the consumer previously threw after a partial success or after a failed post-prune commit),
   **Then** the dispatcher recognizes the event was already dispatched for that device (via a persisted dispatch marker / sent-ledger) and does **not** send a second push to that device, and the dispatch log records the event as a no-op duplicate rather than a fresh send.

2. **AC2 — Sitewide promo fan-out cannot double-notify the user base on retry**
   **Given** an admin-triggered sitewide promo campaign identified by a stable campaign id,
   **When** `SendSitewidePromoFanoutFunction` is re-delivered/poison-retried after partially fanning out,
   **Then** users who were already enqueued+delivered for that campaign id are not re-pushed (campaign-scoped sent-ledger), so re-running the fan-out converges to "every opted-in user notified exactly once," not "everyone notified again."

3. **AC3 — Partial FCM success is not reported as all-failed**
   **Given** a multicast send where some tokens succeed and the call then raises an exception (or `SendEachForMulticastAsync` returns mixed results),
   **When** the dispatcher returns its `PushDispatchResult`,
   **Then** the already-succeeded tokens are recorded as sent (so redelivery skips them) and the result reflects the true per-token outcome — the broad catch at `FcmPushDispatcher.cs:75-81` no longer collapses a partial success into `(0, count, [])`.

4. **AC4 — Transient init failure is observable, not a silent drop**
   **Given** Firebase init transiently fails on cold start (`EnsureInitialized` returns `null` with `_initAttempted=false`),
   **When** the dispatch runs before init succeeds,
   **Then** the outcome is **distinguishable from "all tokens dead"**: the Function does **not** log a misleading "all-failed / 0 pruned" line, no `Device` rows are pruned, and the event is surfaced for retry (error-level/clearly-marked) so the cold-start race re-delivers and ultimately sends rather than being lost behind a single Warning.

5. **AC5 — No dead-token pruning on a non-permanent failure**
   **Given** a dispatch that failed for transient or init reasons (no genuine `Unregistered / InvalidArgument / SenderIdMismatch` from FCM),
   **When** the Function processes the result,
   **Then** zero `Device` rows are removed (pruning happens only on FCM-confirmed permanent token failures), preserving today's correct prune-only-on-`InvalidTokens` behavior at `SendPushNotificationFunction.cs:100-108`.

6. **AC6 — Documented delivery semantics**
   **Given** the dispatch path,
   **When** a developer reads the `IPushDispatcher` / Function / message-contract docs,
   **Then** the intended guarantee is stated explicitly (e.g. "at-least-once delivery at the queue layer, de-duplicated to effectively-once per device via the dispatch ledger"), so the at-most-once vs at-least-once intent is no longer ambiguous.

## Out of Scope

- Changing the queue from at-least-once to exactly-once, or replacing Azure Storage Queues / the poison-message pipeline.
- Reworking notification **content**, localization (`strings.xml` / data-only payloads), category gating (`UserNotificationPreferences`), or event-key catalog.
- Adding new notification events, channels (email/SMS/in-app), or a user-facing notification center/history UI.
- Device-token **registration/rotation** flows; the dead-token **pruning** rule itself is unchanged (AC5 only protects it from firing on the wrong signal).
- Per-employee pay config (IMP-3), Google OAuth (IMP-1), and the separately-tracked admin order-intervention / payroll-settlement / `CancelOrder` gaps — not touched here.
- Backfilling a ledger for historically-sent pushes; the guarantee applies from rollout forward.
- A retry/backoff policy redesign for the Functions host (max-dequeue, visibility timeout) beyond making outcomes correctly distinguishable per AC4.

## Layers Touched

- **Backend — Domain** (`Cleansia.Core.Domain`): new dispatch/sent-ledger entity (e.g. `PushDispatch` keyed by `(TenantId, UserId, EventKey/DeduplicationKey, DeviceToken)` and, for fan-out, campaign id), mirroring the `LoyaltyTransaction` ledger shape; `ITenantEntity` per **S8**.
- **Backend — Infra.Database** (`Cleansia.Infra.Database`): EF entity config + unique index on the dedup tuple → **MANUAL_STEP: `ef-migration`** (owner-only per CLAUDE.md / **S9**).
- **Backend — Clients.Abstractions + Infra.Clients.Fcm**: extend `IPushDispatcher`/`PushDispatchResult` to carry a dispatch/idempotency key and per-token sent outcome; fix the broad catch (`FcmPushDispatcher.cs:75-81`, B8) and the init-failure result distinction (`FcmPushDispatcher.cs:52`, `:170-179`).
- **Backend — Queue.Abstractions** (`Messages/SendPushNotificationMessage.cs`, `SendSitewidePromoMessage`): add a stable `DispatchKey` / `CampaignId` so redelivery is recognizable. Note this is a **queue-contract change**, not an HTTP DTO — no `nswag-regen` is required (no NSwag client touches it), but call it out in the spec.
- **Backend — Functions** (`SendPushNotificationFunction.cs`, `SendSitewidePromoFanoutFunction.cs`): consult/write the ledger before/after send; correct the all-failed-vs-init-failure logging and the post-prune commit ordering.
- **Backend — AppServices** (`Features/Marketing/SendSitewidePromo.cs`): generate and propagate the campaign id.
- **Tests** (`Cleansia.Tests` / integration): redelivery-no-double-send, partial-success, init-race, and fan-out-retry-convergence cases.
- **Not touched:** Frontend (Angular), Android, public HTTP DTOs/NSwag clients.


---

I have everything needed. The gap is fully grounded:

- **Handler** (`GetAllGdprRequests.cs:32-38`): calls `GetPaged(offset, pageSize)` then `.OrderByDescending(r => r.CreatedOn)` — ordering applied **after** Skip/Take. Returns `List<GdprRequestDto>`, no total.
- **BaseRepository.cs:48-52**: `GetPaged(offset, limit)` does `query.Skip(offset).Take(limit)` with **no `OrderBy`** — so the Skip/Take runs on an unordered set (Postgres returns arbitrary rows), and the handler's later `OrderByDescending` only re-sorts the already-truncated page.
- **Entity config** (`GdprRequestEntityConfiguration.cs:35`): only `HasIndex(e => e.UserId)` — no index on `CreatedOn` or `(Status, CreatedOn)`.
- **Controller** (`AdminGdprController.cs:42-49`): returns `List<GdprRequestDto>` with no total — admin client can't compute page count.
- Canonical shape exists (`GetPagedDisputes.cs`, `DisputeSort.cs`, `PagedData`, `MapToDto(total, request)`), so the fix is to conform to consistency.md A1-A6.

Here is the user story.

---

# US-admin-0417 — GDPR audit list must paginate correctly and report a total

> **Type:** Bug / Correctness (compliance surface)
> **Persona:** admin
> **Source GAP:** `GetAllGdprRequests` paginates before ordering and returns `List` instead of `PagedData`
> **Grounding:** `src/Cleansia.Core.AppServices/Features/Gdpr/GetAllGdprRequests.cs:30-40`; `src/Cleansia.Infra.Database/BaseRepository.cs:48-52`; `src/Cleansia.Infra.Database/EntityConfigurations/GdprRequestEntityConfiguration.cs:35`; `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs:42-49`

## Narrative

**As an** administrator reviewing the GDPR request audit log (an Article-30 records-of-processing surface),
**I want** the request list to be sorted deterministically by creation date *before* it is split into pages, and to receive the total number of matching requests with each page,
**so that** every page shows the correct, stable set of rows, no GDPR request can silently disappear between pages, and the admin UI can show an accurate page count and total — making the audit trail trustworthy and complete.

## Context (why this is a defect, not a nicety)

The handler asks the repository for `GetPaged(offset, pageSize)` — which executes `Skip(offset).Take(limit)` on an **unordered** query (`BaseRepository.cs:48-52`, no `OrderBy`) — and only then applies `OrderByDescending(r => r.CreatedOn)`. Because the ordering is applied **after** the database has already truncated to a page, PostgreSQL returns an arbitrary, non-deterministic slice of rows; the trailing `OrderByDescending` merely re-sorts whatever arbitrary rows happened to come back. Result: pages beyond page 1 show **wrong / overlapping / missing** rows, and there is **no total** in the response (`List<GdprRequestDto>`, `AdminGdprController.cs:44-48`), so the client cannot compute the number of pages. There is also **no index** backing the `CreatedOn` sort (`GdprRequestEntityConfiguration.cs:35` indexes only `UserId`). The codebase already has a canonical paged-query shape (`GetPagedDisputes.cs` + `DisputeSort.cs` + `PagedData` + `MapToDto(total, request)`); this feature simply doesn't follow it (consistency.md A1–A6).

## Acceptance Criteria

**AC1 — Deterministic ordering before paging**
**Given** there are more GDPR requests than fit on one page
**When** the admin requests any page of the GDPR request list
**Then** the rows are ordered by `CreatedOn` descending (newest first) **in the database query, before the offset/limit is applied**, so the same row never appears on two different pages and no row is skipped.

**AC2 — Total count returned**
**Given** the admin requests a page of GDPR requests
**When** the response is returned
**Then** it is a `PagedData<GdprRequestDto>` carrying the **total count of all matching requests** (independent of page size), the current page/offset, and the page items — so the client can compute the number of pages.

**AC3 — Complete, non-overlapping coverage across pages**
**Given** N GDPR requests exist and the admin walks through every page sequentially at a fixed page size
**When** the union of all returned pages is taken
**Then** it contains **exactly** the N requests with **no duplicates and no omissions**, and the global order across pages is strictly descending by `CreatedOn`.

**AC4 — Canonical paged-query shape (consistency.md A1–A6)**
**Given** the GDPR list query is implemented
**When** the code is reviewed
**Then** it uses `GetPagedSort<GdprRequestSort>(offset, limit, filter, sort)` + `GetCountAsync(filter, ct)`, projects the DTO **in the query** via `AsNoTracking().Select(...)`, and returns through `items.MapToDto(totalItems, request)` — matching the `GetPagedDisputes` reference, rather than the `GetPaged` + post-hoc `OrderBy` + `List` shape.

**AC5 — Index-backed sort**
**Given** the default and most common access pattern is "newest first" (optionally narrowed by status)
**When** the query executes against a populated table
**Then** the sort is served by a database index on `(CreatedOn)` (or `(Status, CreatedOn)`), declared in `GdprRequestEntityConfiguration`, so the query does not do an unindexed full-table sort.

**AC6 — Page-size guard preserved**
**Given** the existing defense-in-depth bound on page size (`PageSize` between 1 and 100, `Page ≥ 1`)
**When** the query is migrated to the canonical shape
**Then** an equivalent bound still rejects oversized/invalid paging inputs, so an admin (or compromised admin token) cannot dump the entire audit table in one request.

## Out of Scope

- Filtering/searching GDPR requests by `UserId`, `RequestType`, or `Status` (a `GdprRequestFilter` may be introduced **only** as the no-op/empty filter required by the canonical `GetPagedSort`/`GetCountAsync` signature; building real filter UI/criteria is a separate story).
- Any change to GDPR request **creation, processing, or status transitions** (`Create`, `MarkProcessing`, `MarkCompleted`, `MarkFailed`) or the other `AdminGdprController` endpoints (export, delete-account, consents).
- Fixing the same unordered-`GetPaged` anti-pattern in **other** features that use `BaseRepository.GetPaged(offset, limit)` — those are separate canonicalization tickets (do **not** change the shared `GetPaged` overload's behavior here; migrate this feature off it).
- Front-end GDPR admin UI work beyond consuming the new `PagedData` shape (e.g. new columns, export buttons, redesign).
- Multi-tenancy / scoping changes — the existing tenant query filter on `GdprRequest` (`ITenantEntity`) is assumed correct and untouched.
- Soft-delete / `IsActive` semantics for GDPR requests (B6).

## Layers Touched

| Layer | Change | Files (grounded) |
|---|---|---|
| **Domain — Sorting** | New `GdprRequestSort : BaseSort<GdprRequest>` with `DefaultSort => x => x.CreatedOn` (mirrors `DisputeSort`) | `src/Cleansia.Core.Domain/Sorting/GdprRequestSort.cs` (new) |
| **AppServices — Query** | Rewrite to canonical shape: `Request : DataRangeRequest, IRequest<PagedData<GdprRequestDto>>`; `GetPagedSort<GdprRequestSort>` + `GetCountAsync`; `AsNoTracking().Select(...)` in-query; `MapToDto(total, request)`. Preserve the page-size guard. | `src/Cleansia.Core.AppServices/Features/Gdpr/GetAllGdprRequests.cs:10-43` |
| **Infra.Database — Index** | Add `HasIndex(e => e.CreatedOn)` or `HasIndex(e => new { e.Status, e.CreatedOn })` | `src/Cleansia.Infra.Database/EntityConfigurations/GdprRequestEntityConfiguration.cs:35` |
| **Web.Admin — Controller** | Return/declare `PagedData<GdprRequestDto>`; accept `DataRangeRequest`-style offset/limit/sort instead of `page`/`pageSize`, consistent with other admin paged endpoints | `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs:42-49` |
| **DTO** | `GdprRequestDto` shape unchanged; only its container becomes `PagedData<…>` | `src/Cleansia.Core.AppServices/Features/Gdpr/DTOs/GdprRequestDto.cs` (no change) |

## Manual Steps (owner-only — flag, do not perform)

- **`MANUAL_STEP: ef-migration`** — adding the `(CreatedOn)` / `(Status, CreatedOn)` index requires a new EF Core migration created and applied by the owner.
- **`MANUAL_STEP: nswag-regen`** — the response contract changes from `List<GdprRequestDto>` to `PagedData<GdprRequestDto>` (and the request gains offset/limit/sort), so the admin NSwag client must be regenerated by the owner before the admin GDPR UI is wired to the new shape.


---

I now have everything needed, grounded in real code and cited rules. The interceptor convention in this app is `api.*` (prefix added at http-error.interceptor.ts:29,41), while the rule text says `errors.*` — the actual app uses `api.*`, and the missing keys are exactly under `api.*`. I'll write the story to match the real code (`api.*`) while citing the all-5-locales and "one way" rules.

Here is the user story.

---

# US-customer-0042 — Customer-facing API errors render localized text, not raw keys

## Actor narrative

**As a** Cleansia customer using the web app (cleansia.app),
**I want** every error the backend returns on a customer action (cancelling a booking, hitting my weekly booking limit, submitting a review, exporting/deleting my data, saving an address, picking an unserviced country/city) to appear as a clear, translated message in my language,
**so that** when an action is rejected I understand *why* and what to do next — instead of seeing a cryptic literal string like `api.order.cancellation_window_closed`.

## Context / grounding (why this is a real gap)

- The customer HTTP error interceptor takes the first backend error value, prefixes it with `api.`, and runs it through `translate.instant` — `libs/core/services/src/lib/interceptors/http-error.interceptor.ts:29` and `:41` (`translate.instant(\`api.${errorKey}\`)`). ngx-translate returns the key verbatim when it is missing, and this app registers **no** `MissingTranslationHandler`, so the raw key is what reaches the snackbar.
- The customer app's `api` i18n block contains only **7 keys** across 3 namespaces (`common`, `address`, `service_area`) — `apps/cleansia.app/src/assets/i18n/en.json:1302-1316`.
- The backend exposes far more customer-reachable keys via `BusinessErrorMessage` (`src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs`), e.g. `order.cancellation_window_closed` (:41), `order.already_cancelled` (:38), `order.in_progress_cannot_cancel` (:40), `order.weekly_limit_reached` (:52), `order.review.already_exists`/`order.review.rating_invalid` (:93-94), `address.not_owned_by_user` (:42), `country.not_serviced`/`city.not_serviced` (:183/:189), `gdpr.export_failed`/`gdpr.deletion_failed`/`gdpr.deletion_blocked_by_*` (:282-286). The customer `POST /Order/Cancel` endpoint (`src/Cleansia.Web.Customer/Controllers/OrderController.cs:166-175`) returns the `CancelOrder.Response`/order error keys, and `CreateOrder` can return `order.weekly_limit_reached` — all reachable, none translated.
- Today the app surfaces customer errors through **three inconsistent mechanisms**:
  1. interceptor → `api.<key>` (the 7-key path),
  2. `SnackbarService.showApiError(err, fallbackKey)` with fallback keys in **bespoke per-feature namespaces** — e.g. `membership.facade.ts:87` uses `'membership.not_found'` and `:112` uses `'membership.swap_same_plan'` (NOT under `api.*`), plus `order-detail.facade.ts:127` uses `'pages.order_detail.review.error'`,
  3. fully hand-rolled static-key handlers ignoring the server reason — `gdpr.facade.ts:104-109` always shows `'pages.gdpr.export_error'` regardless of whether the backend said `gdpr.export_failed`, `deletion_blocked_by_order`, etc.
- For contrast, the **Android** customer app already ships the full localized set (`error_order_cancellation_window_closed`, `error_order_weekly_limit_reached`, `error_gdpr_export_failed`, …) in all 5 locale `strings.xml` files — so the web app is the outlier.

**Cited rules this violates:**
- `agents/knowledge/consistency.md:52-53` — "Every backend error key has a matching frontend error key in **all 5 locales** (en, cs, sk, uk, ru)."
- `agents/knowledge/consistency.md` (title) — "**One Way To Do Each Thing**"; **C4** (line 94) — "Errors surface via `SnackbarService` (`showError`/`showApiError`)."
- `agents/knowledge/conventions.md:50-53` — no hardcoded user-facing strings; backend dot-notation keys must have matching translated frontend keys.

## Acceptance criteria

1. **Given** I attempt an action the backend rejects with an `order.*` key that is currently unmapped (e.g. `order.cancellation_window_closed`, `order.already_cancelled`, `order.in_progress_cannot_cancel`, `order.weekly_limit_reached`), **when** the interceptor surfaces it, **then** the snackbar shows a human-readable localized sentence in my active language and **never** the literal `api.order.*` key.

2. **Given** the customer app's `api.*` i18n subtree, **when** it is audited against every `BusinessErrorMessage` constant reachable from a `Cleansia.Web.Customer` controller endpoint (cancellation, order limits/empty/price, review, address ownership, country/city not-serviced, gdpr export/delete/blocked, membership, recurring_booking, promo, referral, dispute), **then** each such key has a corresponding `api.<key>` entry.

3. **Given** the 5 supported locales, **when** any new `api.*` key is added, **then** the identical key set exists in `en.json`, `cs.json`, `sk.json`, `uk.json`, and `ru.json` for the customer app (no locale missing a key) — satisfying consistency.md:52-53.

4. **Given** a customer-feature flow that previously caught the error with a bespoke per-feature fallback namespace (membership `membership.facade.ts:87,112`, review `order-detail.facade.ts:127`, gdpr `gdpr.facade.ts:104-109,127-133`), **when** the error-display approach is unified, **then** either (a) those flows resolve through the single interceptor/`api.*` mechanism and the bespoke per-feature error maps are removed, **or** (b) the deliberate split is formally documented in `agents/knowledge/consistency.md` as an approved exception — exactly one of these outcomes is delivered, not both left ambiguous.

5. **Given** a backend error whose key is *not* present in the `api.*` subtree (e.g. a brand-new key shipped before i18n catches up), **when** the interceptor cannot resolve it, **then** the user sees the generic localized `api.common.error_occurred` fallback rather than a raw key string (graceful degradation; no regression in observable behavior).

6. **Given** the GDPR export/delete flow, **when** the backend rejects with a specific reason (`gdpr.export_failed`, `gdpr.deletion_blocked_by_order`, `gdpr.deletion_blocked_by_invoice`, `gdpr.deletion_already_pending`), **then** the snackbar reflects that specific reason in my language, not a single generic "couldn't export/delete" string that masks why.

## Out of scope

- Backend changes: no new `BusinessErrorMessage` codes, no controller/handler/validator edits, no change to the wire shape of error responses.
- NSwag client regeneration — owner-only; not triggered by this story (no DTO/endpoint changes).
- Translation **copy quality / professional review** of the new strings — initial translations may be developer-provided; a linguist pass is a separate task.
- The **partner** and **admin** web apps' `api.*` blocks — this story is scoped to `apps/cleansia.app` (customer) only.
- The **Android** customer/partner apps — already localized; no work here.
- Building a brand-new customer **cancel-order UI** if none exists in the web app — this story fixes *error rendering* for cancellation (and the other listed flows); it does not add a missing cancel button/screen.
- Registering a custom ngx-translate `MissingTranslationHandler` as the *primary* fix — acceptable only as the AC-5 fallback safety net, not as a substitute for adding the actual keys (AC-2/AC-3).
- EF migrations / database changes — none required.

## Layers touched

- **Frontend i18n (primary):** `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — add the full `api.*` subtree for customer-reachable keys.
- **Frontend services (conditional, AC-4 option a):** `libs/core/services/src/lib/services/snackbar.service.ts` and/or `libs/core/services/src/lib/interceptors/http-error.interceptor.ts` — only if unifying onto one mechanism; plus removal of bespoke fallback keys in `libs/cleansia-customer-features/{profile/membership, orders/order-detail, gdpr}`.
- **Docs (conditional, AC-4 option b):** `agents/knowledge/consistency.md` — record the approved split if the bespoke mechanism is kept.
- **No backend, no database, no mobile, no NSwag-generated client layers.**

**Reference files (absolute paths):**
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\apps\cleansia.app\src\assets\i18n\en.json` (lines 1302-1316)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\core\services\src\lib\interceptors\http-error.interceptor.ts` (lines 29, 41)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\core\services\src\lib\services\snackbar.service.ts` (lines 37-57, 100-165)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Core.AppServices\Common\BusinessErrorMessage.cs` (full file; customer-reachable keys at lines 36-94, 168-189, 281-320)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.Web.Customer\Controllers\OrderController.cs` (lines 166-175)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\profile\src\lib\membership\membership.facade.ts` (lines 87, 112)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\gdpr\src\lib\gdpr\gdpr.facade.ts` (lines 104-109, 127-133)
- `c:\Users\cmisa\Desktop\Mike\Projects\cleansia\src\Cleansia.App\libs\cleansia-customer-features\orders\src\lib\order-detail\order-detail.facade.ts` (line 127)


---

I now have everything grounded. Key facts confirmed from the code:

- `DeviceDto.cs` (record `Id, UserId, Platform, DeviceId, LastActiveAt`) is referenced only by its own declaration — dead code (violates conventions.md "No dead code" rule).
- Only `RegisterDevice.cs` and `UnregisterDevice.cs` exist in `Features/Devices/`. No query.
- `IDeviceRepository.GetByUserIdAsync` already exists (the read-side data access the missing query would call) — used by `GdprDeletionService` and `SendPushNotificationFunction`, but never surfaced to a user.
- 3 `DeviceController`s (customer web `:5003`, mobile customer, mobile partner) expose only Register/Unregister.
- `Logout.cs` revokes one refresh token; `RefreshToken.cs` rotates; neither enumerates sessions.
- `CleanStaleDevicesAsync` silently bulk-deletes inactive devices past a cutoff — the only device lifecycle event, invisible to users.
- Note: `DeviceDto.UserId` would violate S4 (DTO leak — client's own id is allowed but other users' ids are not), so the story must address admin-side carefully.

Here is the user story.

---

# US-customer-0142 — See and revoke my signed-in devices / push sessions

> Status: Ready for refinement · Type: Functional gap (half-built feature) · Persona: customer (primary), partner (shared mobile surface), admin (secondary/optional)
> Source: Audit finding — `Features/Devices/` read side modeled but never built.

## Actor narrative

**As a** signed-in Cleansia app user (customer or partner)
**I want** to see the list of devices currently registered to my account and revoke any one of them
**so that** if I lose, sell, or replace a phone I can cut off that device's push notifications and refresh-token session myself, instead of having no visibility until a background sweep silently deletes it weeks later.

## Why this is a real gap (grounded in code)

- The read side is already half-built and orphaned. `src/Cleansia.Core.AppServices/Features/Devices/DTOs/DeviceDto.cs` declares `record DeviceDto(string Id, string UserId, string Platform, string DeviceId, DateTimeOffset LastActiveAt)` and a repo-wide search shows it is referenced **only by its own declaration** — dead code, which `agents/knowledge/conventions.md` ("No dead code") treats as a hard fail. Its shape is exactly a "list my devices" row.
- The data access already exists: `IDeviceRepository.GetByUserIdAsync` (`src/Cleansia.Core.Domain/Repositories/IDeviceRepository.cs:9`) is implemented and used by `GdprDeletionService.cs:193` and `SendPushNotificationFunction.cs:76` — but never surfaced to the user who owns the devices. The query handler that would call it (`GetMyDevices`) does not exist anywhere (confirmed: no `GetMyDevices`/`GetDevices`/`ListDevices` symbol in the codebase).
- `Features/Devices/` contains only `RegisterDevice.cs` and `UnregisterDevice.cs`. `UnregisterDevice` requires the caller to already know the `DeviceId` string of the handset — fine for the app self-unregistering on sign-out, useless from a *different* phone for a device you no longer hold.
- The 3 `DeviceController`s (`Cleansia.Web.Customer`, `Cleansia.Web.Mobile.Customer`, `Cleansia.Web.Mobile.Partner`) expose only `Register` / `Unregister`. No list endpoint.
- Session side: `Logout.cs` revokes a single refresh token from the current cookie/body; `RefreshToken.cs` rotates the current token. Neither lets a user enumerate or revoke *other* sessions.
- The only device lifecycle a user benefits from today is `DataRetentionBackgroundService.CleanStaleDevicesAsync` (`DataRetentionBackgroundService.cs:91`), which bulk-deletes inactive devices past a configurable cutoff — silently and invisibly.

## Acceptance criteria

**AC1 — List my devices (query + endpoint)**
**Given** I am an authenticated user with one or more registered devices
**When** my app requests my device list
**Then** I receive a list of `DeviceDto` rows (the existing dead record, now wired up — not a new parallel type), each showing platform, a device label/id and `LastActiveAt`, scoped to *my* `UserId` derived from the JWT (per security-rules S1 — never from request input), and the current device is identifiable in the response.

**AC2 — Revoke a device I no longer hold**
**Given** I see a device in my list that I want to disconnect (e.g. a lost phone)
**When** I revoke it by its server-side id
**Then** that `Device` row is removed and will receive no further push notifications, and the operation succeeds only if the device belongs to me — a device id I do not own returns **NotFound**, not Forbidden, and does not reveal that the row exists (per security-rules S3).

**AC3 — Revoke also ends that device's session, not just its push token**
**Given** I revoke a device that has an active sign-in
**When** the revoke completes
**Then** the refresh token(s) associated with that device are revoked (reusing the existing `IRefreshTokenService.RevokeAsync` path that `Logout` already uses), so the lost handset can no longer silently mint new access tokens on next refresh.

**AC4 — Mobile self-service surface**
**Given** I am in the partner or customer mobile app profile/settings area
**When** I open "Your devices" (or equivalent)
**Then** I see my devices with platform and last-active, can revoke any device other than the one I'm on, and the list reflects the change after revoke; empty, loading, and error states are all handled (per conventions.md "production-ready bar").

**AC5 — No leaked identifiers / dead code resolved**
**Given** the new query returns `DeviceDto`
**When** the response is serialized to a client
**Then** it carries no `TenantId` and no foreign user identifiers (per security-rules S4); `DeviceDto.UserId` is either removed from the returned shape or justified as the caller's own id, **and** `DeviceDto` is no longer dead code (it is now produced by the new query) — satisfying the "either wire it up or delete it" disposition.

**AC6 (optional / admin) — Admin can view a user's registered devices**
**Given** I am an admin viewing a user's detail
**When** I open that user's devices panel
**Then** I see the user's registered devices (read-only is acceptable for v1) sourced from the same `GetByUserIdAsync` data, with the admin endpoint gated by an admin `Permission` policy (security-rules S2) and the DTO carrying no cross-user PII beyond what admins are already authorized to see (S4).

## Out of scope

- Renaming/relabelling devices, or letting users set a friendly device name.
- Showing geolocation, IP address, browser/OS fingerprint, or a full "login activity / audit history" timeline per device (the `Device` entity stores no IP/location; `RefreshToken` metadata like `DeviceLabel`/`IpAddress` is a separate, larger story).
- A "revoke ALL other sessions / sign out everywhere" bulk action (valuable, but a distinct story; v1 is per-device).
- Changing the `DataRetentionBackgroundService.CleanStaleDevicesAsync` cutoff behaviour or notifying users before the sweep deletes a device.
- Web (Angular customer/partner/admin SPA) self-service device management UI beyond the optional admin read panel in AC6 — web push session management is not in this story.
- Any change to `RegisterDevice` / `UnregisterDevice` semantics or the FCM push dispatch path (`SendPushNotificationFunction`).
- Two-factor, trusted-device, or "remember this device" flows.

## Layers touched

- **Backend — AppServices** (`src/Cleansia.Core.AppServices/Features/Devices/`): new `GetMyDevices.cs` query + handler (read-only, calls existing `IDeviceRepository.GetByUserIdAsync`, derives `UserId` from `IUserSessionProvider`); a new revoke-by-id command + handler (ownership-checked, also revokes the device's refresh tokens via `IRefreshTokenService`); a `DeviceMapper` to `DeviceDto`; wire/clean up `DeviceDto`.
- **Backend — Domain/Repositories**: likely a `GetByIdAndUserAsync` (or reuse `GetByUserIdAsync` + in-memory filter) on `IDeviceRepository` for the ownership-checked revoke; possibly a helper to find refresh tokens by device. No entity schema change expected (`Device` already has all needed fields) — if a link from `RefreshToken` to a device is required and not present, flag `manual_step: ef-migration`.
- **Backend — Controllers**: add `GET` (list) and revoke endpoints to the existing `DeviceController`s in `Cleansia.Web.Mobile.Customer` and `Cleansia.Web.Mobile.Partner` (and `Cleansia.Web.Customer`), each with the correct `[Permission]` policy (S2). Optional admin endpoint on the Admin API for AC6.
- **Mobile** (`src/cleansia_android/`, `:customer-app` + `:partner-app`, sharing `:core`): a "Your devices" screen/section in profile — Compose screen, `@HiltViewModel` with sealed `UiState`/`ActionState`, repository method on the existing devices repo, strings in resources (no hardcoded text).
- **Frontend — Admin** (optional, AC6 only): a read-only devices panel on the admin user-detail page using the generated client.
- **i18n**: any new user-facing strings added to all 5 locales (en, cs, sk, uk, ru) on web, and to Android string resources.
- **Owner-only manual steps to flag**: `manual_step: nswag-regen` (new endpoints/DTO change the mobile/customer/admin API contract); `manual_step: ef-migration` only *if* a `RefreshToken→Device` association turns out to be needed and is missing.

## Notes for the team

- Disposition of `DeviceDto`: the audit's "either wire it to the new query or delete it" is resolved here by **wiring it up** (AC1/AC5). It is the natural output shape; deleting it would just mean re-creating an identical record.
- Security review is required (`security_touching`): this story directly engages S1 (UserId from JWT), S2 (policy on new endpoints), S3 (ownership-checked revoke returns NotFound), and S4 (no leaked ids in `DeviceDto`).
- Persona id is `customer` because the customer mobile app is the primary beneficiary and the gap is most acute there; the partner mobile app shares the identical surface, and admin (AC6) is explicitly optional.


---

The 3 matches are unrelated (admin routes, profile sub-routes, admin loyalty-referrals). Confirmed: no `r/:code` route exists in the customer app. I have grounded every claim. Here is the user story.

---

# US-customer-0042 — Web referral landing route `/r/:code`

## Context (grounded in code)

The customer rewards page builds and shares a public link to `/r/{code}`:
- `libs/cleansia-customer-features/rewards/src/lib/rewards/rewards.component.ts:207-209` — `referralUrl()` returns `${window.location.origin}/r/${code}`, used by `shareReferralCode()` (line 219) and the share text at line 222.

But the customer app has **no route that matches `r/...`**:
- `apps/cleansia.app/src/app/app.routes.ts` defines no `r/:code` entry; the trailing wildcard `apps/cleansia.app/src/app/app.routes.ts:141-144` (`path: '**' → redirectTo: NOT_FOUND`) swallows it. Every shared referral link 404s.
- `CleansiaCustomerRoute` (`libs/core/services/src/lib/enums/routes.enum.ts:36-54`) has no referral-landing member — the route enum that the share URL should be kept in lock-step with.

The downstream signup machinery already exists and only needs to be *fed*:
- `RegisterFacade` (`register.facade.ts`) already owns a `referralCode` signal (line 47), a `referralCode` form control (line 167), `validateReferralCodeNow()` (line 55), and passes the code into `authService.register(...)` (line 110). **Nothing reads a URL param to pre-fill it** — the only entry point is the manual "Add a referral code" dialog (`register.component.ts:98`).
- The backend already accepts it fail-soft: `Cleansia.Core.AppServices/Features/Auth/Register.cs:58-61,91-111` (`ReferralCode = null`, bad code never blocks signup). So **no backend change is required.**
- The guest guard `customerGuestGuard` (`libs/core/customer-services/src/lib/guards/guest.guard.ts:6-13`) redirects already-logged-in users away from `register` → `orders`; the landing route must account for the logged-in invitee.

Parity gap: mobile already handles this. The customer Android app has `ReferralCodeBottomSheet.kt`, a `ReferralRepository.validate()` passthrough (`core/referral/ReferralRepository.kt:98-102`), and deep-link plumbing (`MainActivity.kt` `pendingDeepLink`). Web has none.

## Actor narrative

**As a** prospective Cleansia customer who clicked a friend's referral link (`/r/{code}`),
**I want** that link to open a working page that automatically applies the referral code to my signup,
**so that** I can register with the credit already attached without hunting for the signup form and re-typing a code by hand — and so my friend's referral viral loop actually completes on the web.

## Acceptance criteria (Given / When / Then)

1. **Route resolves instead of 404**
   **Given** a referral share URL of the form `/r/{code}` produced by `referralUrl()` (`rewards.component.ts:207`),
   **When** an unauthenticated visitor opens it,
   **Then** the app routes to the registration experience (not the `not-found` page reached via the `**` wildcard at `app.routes.ts:141-144`), and the share URL pattern and the new route are defined from the same single source (a new `CleansiaCustomerRoute` member), so they cannot drift.

2. **Code is captured and pre-applied to the signup form**
   **Given** the visitor landed via `/r/{code}`,
   **When** the registration view initializes,
   **Then** the captured `code` is placed into `RegisterFacade.referralCode` and the `referralCode` form control (mirroring `setReferralCode()` at `register.facade.ts:93-96`) without the visitor opening the manual "Add a referral code" dialog.

3. **Code is validated and its state is shown**
   **Given** a pre-applied code from the URL,
   **When** the form initializes,
   **Then** `validateReferralCodeNow()` (`register.facade.ts:55`) runs once and the result drives the same `ReferralUiState` machine the dialog uses (`valid` shows the referrer's first name, `invalid`/network failure shows the localized message via `REFERRAL_ERROR_KEYS` in `register.component.ts:25-30`), with no auto-validation debounce loop.

4. **Bad or unknown codes never block signup**
   **Given** the URL code is invalid, expired, self-referral, or unverifiable (network failure),
   **When** the page loads and the visitor proceeds,
   **Then** registration is still fully usable and submittable, consistent with the fail-soft contract in `register.facade.ts:106-110` and `Register.cs:91-111` (a bad code is silently skipped, never a hard error).

5. **Code survives through to account creation**
   **Given** a valid pre-applied code,
   **When** the visitor completes signup,
   **Then** the code is sent to `authService.register(...)` (`register.facade.ts:110`) and accepted by the backend `Register` command, so the referral is recorded against the new account.

6. **Already-logged-in invitee is handled sanely**
   **Given** an already-authenticated customer opens `/r/{code}` (the `customerGuestGuard` would otherwise bounce `register` → `orders`, `guest.guard.ts:13`),
   **When** the route activates,
   **Then** the user is redirected to a sensible signed-in destination (e.g. rewards/orders) rather than the 404 page, and is not shown a broken/blank registration form.

7. **All visitor-facing strings are localized in 5 locales**
   **Given** any new copy introduced by the landing route (e.g. a "applying your referral…" state or redirect notice),
   **When** it renders,
   **Then** it uses `TranslatePipe` keys present in **all five** locale files `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — no hardcoded strings (conventions.md "No hardcoded user-facing strings", lines 50-53).

## Out of scope

- **Any backend change.** `Register.cs` and the `referralClient.validate` endpoint already accept and validate codes; no new command/DTO/endpoint — therefore **no NSwag regeneration** (`manual_step: nswag-regen`) is expected. If the implementer finds one is needed, flag it rather than editing generated clients.
- **Mobile / Android.** Deep-link and bottom-sheet handling already exist there (`ReferralCodeBottomSheet.kt`, `MainActivity.kt`). This story is web-only parity.
- **Partner and Admin apps.** Referral acquisition is a customer-app concern; no `/r/` route is added to `cleansia-partner.app` or `cleansia-admin.app`.
- **A dedicated marketing landing page** (hero, referrer avatar, value-prop copy). This story delivers a functional capture-and-redirect into registration; a richer landing page is a separate ticket.
- **Changing the referral reward/credit logic, points, or `ReferralService.AcceptAsync` rules.**
- **Persisting the code across a later return visit** (cookies/localStorage survival, attribution windows, OG/social preview meta tags). Capture is in-session through signup only.
- **Self-referral / abuse hardening** beyond the existing fail-soft backend validation.
- **Editing the NSwag-generated client, EF migrations, or DB seed data.**

## Layers touched

- **Frontend — routing:** `apps/cleansia.app/src/app/app.routes.ts` (new `r/:code` route ahead of the `**` wildcard) and `libs/core/services/src/lib/enums/routes.enum.ts` (`CleansiaCustomerRoute` member to keep share URL + route in lock-step).
- **Frontend — register feature:** `libs/cleansia-customer-features/register/` — read the route param and feed `RegisterFacade` (`register.facade.ts`), reusing the existing `referralCode` signal/control + `validateReferralCodeNow()` + `ReferralUiState`. (A small route-param-aware guard/resolver or component init; **logic in the facade, not the component**, per consistency.md C1.)
- **Frontend — guards:** interplay with `customerGuestGuard` (`libs/core/customer-services/src/lib/guards/guest.guard.ts`) for the logged-in case (AC 6).
- **i18n:** `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` for any new copy.
- **Tests:** facade/route-param logic test-first per conventions.md (lines 123-125) — capture, pre-apply, validate, fail-soft.
- **No backend, no mobile, no generated-client changes.**

Cited rules: conventions.md (no hardcoded strings 50-53; owner-only nswag-regen 100-101; production-ready "happy path is not done" 121-122; TDD 123-125); consistency.md C1 (facade owns logic, not component) and C3 (canonical client-call pipe for the validate call).


---

I now have full grounding. Notable confirmed facts:

- `ResolveDispute` is exposed in `Cleansia.Web.Partner` (not Admin), gated by `Policy.CanResolveDispute`. The GAP's "admin" persona is really the staff/partner-side resolver. I'll frame the actor as `admin` (staff resolver) since this is back-office dispute resolution, matching the GAP's intent and the persona naming rule.
- `Dispute.Resolve` (Dispute.cs:82-90) only sets `Status=Resolved` + `RefundAmount` + notes; no money moves.
- `Dispute.UpdateStatus` (line 64-68) and `Resolve` have no state-machine guard — any status to any status; re-resolving overwrites `RefundAmount`.
- `StripeDisputeId`/`LinkStripeDispute` (line 104) is dead code — never called; no inbound Stripe-dispute webhook.
- A real refund capability already exists: `IStripeClient.RefundCheckoutSessionAsync` (used by `CancelOrder.cs:142`), with the `PaymentStatus.Refunded` transition and non-blocking try pattern.
- S7 (security-rules.md:84-91) explicitly requires idempotency and names the exact reference patterns (`LoyaltyService`, `ReferralService`) the GAP cites.
- `Order` already has `StripeSessionId` and `PaymentStatus`.

I have everything needed to write a grounded, single user story.

---

# US-admin-0042 — Dispute resolution must actually issue the refund it records

## Actor narrative

**As an** admin/staff dispute resolver,
**I want** resolving a dispute "with a refund of X" to actually pay the customer that amount back (once, safely) and to be blocked from making illegal status jumps or silently overwriting a refund that already went out,
**so that** the `RefundAmount` we show is a real promise of money returned — not a number stored in a row that nobody ever acts on — and the customer is actually made whole.

## Context / grounding (why this story exists)

- `ResolveDispute.Handler` (`src/Cleansia.Core.AppServices/Features/Disputes/ResolveDispute.cs:53-57`) calls `dispute.Resolve(...)` and returns success.
- `Dispute.Resolve` (`src/Cleansia.Core.Domain/Disputes/Dispute.cs:82-90`) only sets `Status = Resolved`, `RefundAmount`, `ResolvedBy/On`, and notes. **No code anywhere consumes `Dispute.RefundAmount`** to move money (grep confirms the only non-generated readers are the mapper/DTO/seed). The customer is never paid → silent financial/correctness gap and a UX lie.
- The refund capability already exists and is proven: `IStripeClient.RefundCheckoutSessionAsync(stripeSessionId, amount, ct)` (`Cleansia.Core.Clients.Abstractions/Stripe/IStripeClient.cs:13`), used by the customer cancel path `CancelOrder.cs:142` with the `PaymentStatus.Refunded` transition and a non-blocking `StripeException` try. The `Order` carries `StripeSessionId` + `PaymentStatus`. This story wires the *same proven path* into dispute resolution.
- No state-machine guard exists: `Dispute.Resolve` (`Dispute.cs:82`) and `Dispute.UpdateStatus` (`Dispute.cs:64-68`) both set `Status` unconditionally, so any status → any status is allowed and re-resolving overwrites the prior `RefundAmount` (and would re-trigger a payout).
- `Dispute.StripeDisputeId` + `Dispute.LinkStripeDispute` (`Dispute.cs:38, 104`) are dead — never called; there is no inbound Stripe-dispute (chargeback) webhook.
- This is the exact scenario S7 (`agents/knowledge/security-rules.md:84-91`) governs: a command that issues a Stripe refund / writes a financial record **must be idempotent**, keyed off an existing ledger/transaction, with `LoyaltyService.GrantForCompletedOrderAsync` and `ReferralService.ProcessQualifyingOrderAsync` named as the reference patterns. The webhook-idempotency precedent is `ProcessedStripeEvent` + `HandlePaymentNotification.cs:144`.

## Acceptance criteria (Given / When / Then)

1. **Refund is actually issued on resolve-with-refund**
   Given a dispute on a card-paid order (`PaymentType == Card`, `PaymentStatus == Paid`, non-empty `StripeSessionId`),
   When an authorized resolver resolves it with `RefundAmount = X` (`0 < X ≤ order.TotalPrice`),
   Then a Stripe refund of exactly X is issued against the order's `StripeSessionId`, the order's `PaymentStatus` becomes `Refunded`, the dispute moves to `Resolved` with `RefundAmount = X` persisted, and the customer receives the existing `OrderRefunded` notification.

2. **Idempotent — never double-refunds (S7)**
   Given a dispute that has already been resolved-with-refund and the payout recorded,
   When the same resolve is retried (double-click, pipeline retry, admin re-trigger, webhook re-delivery),
   Then no second Stripe refund is issued and no second financial record is written — the operation short-circuits on the existing refund-ledger entry / transaction id keyed to the dispute (mirroring the `LoyaltyService`/`ReferralService` ledger check), and the response reflects the already-completed state.

3. **Status transitions are gated by a state machine**
   Given a dispute in a terminal state (`Resolved` or `Closed`),
   When a resolver attempts to resolve it again or move it to an illegal target status via `Resolve`/`UpdateStatus`,
   Then the transition is rejected with a `BusinessResult.Failure` carrying a `BusinessErrorMessage` dispute-status code, the prior `RefundAmount` and status are left unchanged, and no refund is attempted.

4. **Resolve-with-no-refund still works and moves no money**
   Given a dispute resolved with `RefundAmount` null or `0`,
   When it is resolved,
   Then it moves to `Resolved` with the notes saved, **no** Stripe call is made, and `PaymentStatus` is untouched.

5. **Refund failure does not falsely report success**
   Given a card order whose Stripe refund call fails (`StripeException`),
   When resolve-with-refund runs,
   Then the failure is logged without PII (S6) and surfaced so the resolver/UI does **not** see "refunded" — the dispute is not left claiming a payout that didn't happen (mirror `CancelOrder.cs:139-151` non-blocking handling, but the resolution must make the unpaid-refund state observable, not silent).

6. **Inbound Stripe-dispute (chargeback) link is wired**
   Given a Stripe `charge.dispute.created` webhook for an order we can resolve to a local order,
   When it is processed through the existing idempotent webhook handler (`ProcessedStripeEvent` gate),
   Then a local `Dispute` is created or matched and `Dispute.LinkStripeDispute(stripeDisputeId, ...)` is called so `StripeDisputeId` is populated (retiring the dead method).

7. **UI must not present an un-issued refund as paid**
   Given the partner/admin dispute resolution screen,
   When a resolver enters a refund amount,
   Then the UI labels and confirms it as "issue refund of X" and reflects the real post-resolve outcome (refunded vs. failed/pending), never showing "refunded" purely because `RefundAmount` was stored.

## Out of scope

- Changing the dispute **reasons**, evidence/message threads, or the `Escalate`/`Close` flows beyond adding the missing transition guard.
- Refunds for **cash** orders, manual/offline refunds, or partial-then-additional top-up refunds (only a single refund up to `TotalPrice` per dispute is in scope).
- Refunding via `PaymentIntent` (mobile PaymentSheet) orders that have **no** `StripeSessionId` — these are flagged for manual handling, not auto-refunded, in this story.
- Refund-to-loyalty-credit or store-credit alternatives instead of card refund.
- Moving the dispute endpoints from the **Partner** API host to the Admin API, or any policy/permission redesign of `Policy.CanResolveDispute` / `CanUpdateDisputeStatus`.
- A full chargeback **adjudication/representment** workflow (submitting evidence back to Stripe); AC #6 only links the inbound dispute id.
- EF migration and NSwag client regeneration execution — **owner-only**, flagged as `manual_step: ef-migration` (refund-ledger / dispute columns) and `manual_step: nswag-regen` (changed `ResolveDispute` response/DTO).

## Layers touched

- **Domain** — `Cleansia.Core.Domain/Disputes/Dispute.cs`: add a guarded state machine to `Resolve`/`UpdateStatus`; introduce the refund-ledger/transaction concept (new value/entity or a `RefundIssued` marker + transaction id) keyed to the dispute; activate `LinkStripeDispute`.
- **AppServices (CQRS)** — `Features/Disputes/ResolveDispute.cs` handler + validator: orchestrate the idempotent Stripe refund via `IStripeClient.RefundCheckoutSessionAsync`, set `PaymentStatus.Refunded`, enqueue `OrderRefunded` notification, return real outcome; new `BusinessErrorMessage` dispute-status/refund codes. New/extended webhook handling for `charge.dispute.created` alongside `HandlePaymentNotification`.
- **Infra.Database** — new EF config + migration for the refund-ledger entry / dispute columns (`manual_step: ef-migration`).
- **Infra.Clients (Stripe)** — confirm/extend `StripeClient` refund path; add inbound dispute-event parsing if not covered.
- **Web.Partner** — `Controllers/DisputeController.cs` (`Resolve`/`UpdateStatus`): response contract reflecting refund outcome; webhook route if the chargeback event needs an entry point.
- **Frontend (partner + admin dispute features)** — resolution screen wording/labels and three explicit states (success-refunded / failed / no-refund); i18n keys for the new `errors.dispute.*` codes in all 5 locales. Flag `manual_step: nswag-regen`.
- **Tests** — `Cleansia.Tests`: state-machine guard, refund idempotency (double-resolve issues one refund), no-refund path moves no money, refund-failure surfacing; integration test for the chargeback webhook link.


---

Failure source fully confirmed: line 358-359 executes the SendGrid send inside a Polly `policy.ExecuteAsync`, and line 365 throws `EmailDeliveryException` on non-success after the policy exhausts retries. This propagates uncaught out of the four auth handlers, while the order handlers wrap it.

All claims are grounded against real code and the cited rule (runtime-readiness.md:45). Here is the user story.

---

# US-partner-0142 — Make transactional auth emails a durable, non-blocking side effect

> **Status:** Ready for refinement
> **Type:** Reliability / Tech-debt (functional gap)
> **Source:** Audit GAP — "Email sent inline & synchronously on the critical path; SendGrid outage hard-fails registration and password-reset"
> **Persona note:** Filed under `partner` because the most severe blast radius is partner/employee onboarding (`RegisterEmployee`), but the defect is shared by customer registration and all-persona password reset — see *Affected actors*.

## Actor narrative

**As a** new or returning Cleansia user (customer signing up, employee/partner being onboarded, or anyone who clicked "forgot password"),
**I want** my account to be created — and my password-reset to be accepted — even when Cleansia's email provider (SendGrid) is temporarily degraded, down, or mis-keyed,
**so that** a transient third-party email outage never blocks me from registering or recovering my account, and my confirmation/reset email reliably arrives once email service recovers instead of being lost.

## Context (grounded in code)

- All four auth handlers call `IEmailService` **inline, synchronously, on the request's critical path**, with no surrounding try/catch:
  - `Features/Auth/Register.cs:89` → `SendEmailConfirmationAsync(...)`
  - `Features/Auth/RegisterEmployee.cs:84` → `SendEmailConfirmationAsync(...)`
  - `Features/Auth/ResendConfirmationEmail.cs:59` → `SendEmailConfirmationAsync(...)`
  - `Features/Users/RequestPasswordChange.cs:43` → `SendResetPasswordEmailAsync(...)`
- The failure originates in `Cleansia.Core.AppServices/Services/EmailService.cs:358-365`: the SendGrid call runs inside a Polly `policy.ExecuteAsync(...)`; once retries are exhausted (or the response is non-success), it `throw new EmailDeliveryException(...)`. That exception propagates uncaught out of the four handlers, so the command fails — either the new `User`/`Cart`/`Employee` rows never commit, or they commit and the request still 500s, leaving registration broken and password-reset un-actionable during the outage.
- This **violates the readiness matrix** in `agents/knowledge/runtime-readiness.md:45`: *"Email is a side effect — enqueue it; a send failure is logged + retried by the queue/Function, it does not fail the command."*
- The order handlers already do the right thing locally — e.g. `Features/Orders/CompleteOrder.cs:241-250` wraps the same `IEmailService` call in `try/catch` + `logger.LogWarning(...)` (also `StartOrder.cs`, `TakeOrder.cs`). The auth flows are the inconsistent outliers.
- **Important trade-off (drives the chosen solution):** for confirmation and reset flows the email *is the point* of the action, so a silent fail-soft swallow (the order-handler pattern) is **wrong here** — the user would get no email and no error. The correct answer is **enqueue-with-independent-retry**, mirroring the existing durable side-effect pattern: `Cleansia.Core.Queue.Abstractions/Messages/SendPushNotificationMessage.cs` (the message contract) consumed by `Cleansia.Functions/Functions/SendPushNotificationFunction.cs` (queue-trigger, cross-tenant override, re-throw-to-retry). A new `SendEmailMessage` + email-dispatch Function should follow that template, and handlers dispatch post-commit like `CompleteOrder.cs:227-238` does for push.

## Affected actors

- **Customer** — `Register.cs` (customer signup) and password reset.
- **Partner / Employee** — `RegisterEmployee.cs` onboarding and password reset.
- **All personas** — `RequestPasswordChange.cs` (account recovery) and `ResendConfirmationEmail.cs`.

## Acceptance criteria

1. **Registration survives an email outage (customer)**
   **Given** SendGrid is down / returning errors / configured with an invalid API key,
   **When** a customer submits a valid registration via the `Register` command,
   **Then** the account (`User` + `Cart`) is committed and the API returns a success result (no 500), **and** the confirmation email is durably queued for delivery (not dropped, not sent inline).

2. **Employee onboarding survives an email outage (partner)**
   **Given** SendGrid is unavailable,
   **When** `RegisterEmployee` runs for a valid new employee,
   **Then** the `User` + `Cart` + `Employee` rows are committed and the command returns success, **and** the confirmation email is enqueued rather than thrown.

3. **Password-reset request survives an email outage (all personas)**
   **Given** SendGrid is unavailable,
   **When** a user submits `RequestPasswordChange` for an existing account,
   **Then** the reset token is persisted and the command returns success, **and** the reset email is enqueued for independent retry.

4. **Resend confirmation survives an email outage**
   **Given** SendGrid is unavailable,
   **When** an unconfirmed user submits `ResendConfirmationEmail`,
   **Then** the refreshed confirmation code is persisted and the command returns success, **and** the email is enqueued.

5. **Queued email is actually delivered with independent retry**
   **Given** an email was enqueued while SendGrid was down, **and** SendGrid later recovers,
   **When** the email-dispatch Function processes the queued message,
   **Then** the confirmation/reset email is delivered to the correct recipient in the user's preferred language, the Function logs start/outcome, **and** a transient send failure causes a queue retry (re-throw) rather than a silent drop — mirroring `SendPushNotificationFunction.cs:115-121`.

6. **No silent data loss on a poison/exhausted message**
   **Given** a queued email message that permanently fails (e.g. invalid recipient) after max retries,
   **When** the Function exhausts its retry budget,
   **Then** the message lands in a visible dead-end (poison/dead-letter queue or a logged, alertable failure) so a human can see what is stuck — per `runtime-readiness.md:58-59` (every retry path has a dead-end).

## Out of scope

- Changing **what** the confirmation/reset emails contain (templates, copy, branding, subject lines, SendGrid template IDs).
- Reworking the **order/status** email call-sites (`CompleteOrder`, `StartOrder`, `TakeOrder`) — their existing fail-soft try/catch is acceptable for non-critical notifications; this story does not migrate them (a follow-up may unify them onto the same queue, but not here).
- Push-notification, marketing/promo, membership, or dispute email/notification flows — only the **four cited auth/account email call-sites** are in scope.
- Polly retry-policy tuning inside `EmailService.cs` (retry counts, backoff) — the in-process policy stays; this story moves the *invocation* off the request path, it does not re-tune the policy.
- Frontend/UI changes to the registration, resend, or password-reset screens (no new error states or copy required — behavior becomes success instead of 500).
- Authentication semantics, token formats, confirmation-code/reset-token generation, and validators (`Validator` classes are unchanged).
- Multi-tenancy model changes — the new message must carry `TenantId` exactly like `SendPushNotificationMessage`, but no broader tenancy work.

## Layers touched

- **Backend — AppServices (handlers):** `Register.cs`, `RegisterEmployee.cs`, `ResendConfirmationEmail.cs`, `RequestPasswordChange.cs` — replace the inline `IEmailService` call with a post-commit enqueue.
- **Backend — Queue abstractions:** new `SendEmailMessage` record in `Cleansia.Core.Queue.Abstractions/Messages/` (sibling of `SendPushNotificationMessage.cs`); add the queue name to `QueueNames`.
- **Backend — Functions:** new email-dispatch Function in `Cleansia.Functions/Functions/` that consumes the message and calls `IEmailService` (template after `SendPushNotificationFunction.cs`).
- **Email service:** `Cleansia.Core.AppServices/Services/EmailService.cs` / `IEmailService.cs` — reused by the Function; no signature change strictly required, though the enqueue payload must carry enough to reconstruct the send (recipient, name, code, language, email type, tenant).
- **Infra / config:** queue registration for the new email-dispatch queue + its poison/dead-letter handling (Aspire/Azure Storage Queue wiring).
- **Tests:** unit tests asserting the four handlers commit + enqueue (and do **not** throw) when email is unavailable; Function test for delivery + retry/poison behavior.
- **Manual steps (owner-only, flag in ticket):** none for NSwag (no DTO/endpoint contract change) and no EF migration expected — confirm during refinement. New queue/Function may need owner infra provisioning (queue + connection string).

**Frontend / Mobile layers: not touched.**


---

I now have full grounding. Key confirmations:

- **Controller** (`AdminMarketingController.cs:19-31`): no `[EnableRateLimiting]` attribute (violates S5 for a side-effecting mass-send mutation), no campaign id in the route/body.
- **Handler** (`SendSitewidePromo.cs:67-91`): enqueues exactly one fan-out message, no campaign id derived/persisted, returns `Success()`. A retry/double-submit enqueues a second full fan-out.
- **Fan-out function** (`SendSitewidePromoFanoutFunction.cs:55-164`): no dedupe — on any thrown exception it `throw`s and the poison pipeline "retries the whole campaign" (line 162), so even a single transient failure mid-fan-out replays the entire send. No `ProcessedMessage`/campaign-ledger check exists anywhere (no dedupe infra found in repo).
- **Frontend** (`sitewide-push-form.component.ts:131-155`): a raw `http.post`; the only guard against re-send is a client-side `submitting` signal and a confirm dialog — both bypassable by a second browser tab, a network-retry, or a back/forward resubmit. No client-supplied campaign id.
- **S7** (idempotency) and **S5** (rate limiting) are the cited laws. The GAP's "LG-SEC-07" maps to the catalog's **S7 — Idempotency on side-effecting commands** (sends a push to the entire user base = a side effect that must be idempotent), and the rate-limit angle maps to **S5**.

Here is the user story.

---

# US-admin-0042 — Make sitewide promo campaigns idempotent (campaign id + dedupe + rate limit)

## Actor narrative

**As an** admin sending a sitewide marketing push notification,
**I want** each "send sitewide promo" action to be tied to a single, persisted campaign that the system will dispatch at most once,
**so that** an accidental double-click, a browser/network retry, or a queue poison-message replay never push-spams the entire opted-in user base a second time.

## Context (grounded in code)

- The admin form (`src/Cleansia.App/libs/cleansia-admin-features/marketing/src/lib/sitewide-push-form/sitewide-push-form.component.ts:131-155`) does a raw `http.post` to `/api/AdminMarketing/send-sitewide-promo`. Its only re-send guards are a client-side `submitting` signal and a confirm dialog — both are lost on a new tab, an HTTP-layer retry, or a browser resubmit.
- The endpoint (`src/Cleansia.Web.Admin/Controllers/AdminMarketingController.cs:19-31`) has `[Permission(Policy.CanSendSitewidePromo)]` but **no** `[EnableRateLimiting(...)]`, despite being a mass side-effecting mutation (S5).
- The handler (`src/Cleansia.Core.AppServices/Features/Marketing/SendSitewidePromo.cs:67-91`) builds one `SendSitewidePromoMessage`, enqueues it, and returns `BusinessResult.Success()`. There is no campaign id, nothing is persisted, and nothing records that a send already happened (violates S7 — Idempotency on side-effecting commands).
- The fan-out consumer (`src/Cleansia.Functions/Functions/SendSitewidePromoFanoutFunction.cs:55-164`) has no dedupe check; on any thrown exception it `throw`s so "the poison-message pipeline retries the whole campaign" (line 162) — replaying the full fan-out.
- `SendSitewidePromoMessage` (`src/Cleansia.Core.Queue.Abstractions/Messages/SendSitewidePromoMessage.cs:26-36`) carries no campaign identifier. No campaign entity or message-dedupe/`ProcessedMessage` infrastructure exists in the repo today.

## Acceptance criteria

1. **Campaign identity is established and persisted**
   **Given** an admin submits a valid sitewide promo with a campaign id (client-supplied idempotency key, or server-derived if absent),
   **When** the command handler runs,
   **Then** a campaign record is persisted with that id, the tenant, the locale title/body payload, and an initial status (e.g. `Queued`), and the enqueued `SendSitewidePromoMessage` carries the campaign id.

2. **Duplicate submit of the same campaign does not re-enqueue a fan-out**
   **Given** a campaign with id `X` has already been accepted and enqueued,
   **When** the admin (or a retry) POSTs the same campaign id `X` again,
   **Then** the endpoint returns success **without** enqueuing a second fan-out message, and exactly one fan-out message for `X` exists on the queue.

3. **Fan-out consumer skips an already-dispatched campaign**
   **Given** the fan-out function receives a `SendSitewidePromoMessage` whose campaign id is already marked dispatched/in-progress,
   **When** the function executes (including on a poison-message replay of the same message),
   **Then** it short-circuits without re-paging users or re-enqueueing per-user push messages, and logs that the campaign was skipped as already-dispatched.

4. **First-time fan-out still delivers to every opted-in user exactly once**
   **Given** a brand-new campaign id that has never been dispatched,
   **When** the fan-out runs to completion,
   **Then** it marks the campaign dispatched and each opted-in user (`Promo = true`) receives exactly one push in their resolved locale — i.e. no regression to current behaviour for the happy path.

5. **The send endpoint is rate-limited (S5)**
   **Given** the same admin principal/tenant calls `send-sitewide-promo` repeatedly within the configured window,
   **When** the call count exceeds the per-admin/per-tenant limit,
   **Then** further calls receive HTTP 429 and no additional fan-out is enqueued.

6. **Observable success/failure stays unchanged for the admin**
   **Given** a successful first submit,
   **When** the response returns,
   **Then** the admin still sees the existing `pages.sitewide_push.send_success` confirmation; a deduped/rate-limited resubmit must not surface a misleading error that implies the first send failed.

## Out of scope

- Editing, scheduling, pausing, or **cancelling** an in-flight campaign (the form comment notes "once enqueued the fan-out runs unstoppable" — that stays true).
- A campaign history / analytics UI (open/delivery rates, per-recipient delivery receipts).
- Per-user idempotency at the `notifications-dispatch` / FCM layer beyond what exists today (this story dedupes at the *campaign/fan-out* level, not per push).
- Changing locale resolution, the 5-locale validation rules, or the FCM payload shape.
- Audience targeting/segmentation changes (still "all users with `Promo = true`").
- A general-purpose queue `ProcessedMessage` dedupe framework — scope is the sitewide-promo campaign only (a reusable abstraction may be proposed but is not required).
- Retroactive dedupe of campaigns sent before this change.

## Layers touched

- **DB / Domain** (`Cleansia.Core.Domain`, `Cleansia.Infra.Database`): new campaign entity (`ITenantEntity`, unique `(TenantId, CampaignId)` per S8) + EF config + migration → **MANUAL_STEP: `ef-migration`** (owner-only).
- **AppServices** (`Cleansia.Core.AppServices/Features/Marketing/SendSitewidePromo.cs`): campaign id on `Command`, persist-and-dedupe in the handler (S7), validator update for the id.
- **Queue abstractions** (`Cleansia.Core.Queue.Abstractions/Messages/SendSitewidePromoMessage.cs`): add campaign id to the message.
- **Functions** (`Cleansia.Functions/SendSitewidePromoFanoutFunction.cs`): dedupe check + mark-dispatched.
- **Admin API** (`Cleansia.Web.Admin/Controllers/AdminMarketingController.cs`): `[EnableRateLimiting]` (S5).
- **Frontend** (`cleansia-admin-features/marketing/.../sitewide-push-form.component.ts`): generate/send a campaign id (idempotency key) on submit → **MANUAL_STEP: `nswag-regen`** once the `Command` DTO changes (owner-only; the form currently uses a raw `http.post` pending regen).
- **i18n**: any new error/info key (e.g. rate-limited / already-sent) added to all 5 locales per the i18n rule.


---

I have full grounding. No existing consistency-violation entry covers this, no cron AppSettings exist yet, and peer functions prove the codebase already hardcodes real production crons elsewhere. Here is the user story.

---

# US-admin-0041 — Promote recurring/notification timer crons from 2-minute dev placeholders to documented production cadences

## Persona & Actor Narrative

**As a** platform operator/admin responsible for the Cleansia Functions runtime and its Azure execution + DB cost,
**I want** the four recurring/notification timer Functions to run on their documented production cadences (and in their documented order) instead of the every-2-minute dev placeholder,
**so that** the Materialize→Reminder ordering guarantee actually holds, cross-tenant table scans drop from ~720 runs/day to the intended handful, and idempotency stamps return to being a safety net rather than the only thing preventing duplicate sends.

> Persona rationale: there is no end-user UI surface here. The observable owner of timer cadence, Function cost, and the runtime-readiness bar (`agents/knowledge/runtime-readiness.md` — "Functions are observable… emits a metric or log the owner can alert on") is the operator/admin. Customer/partner impact (duplicate or mis-ordered pushes) is a *downstream* consequence, not the actor.

## Grounding (read, file:line)

The four flagged Functions each carry a doc-comment stating a daily/30-min production cadence but bind a 2-minute (or 30-second-class) dev timer:

| Function | Documented cadence (its own XML doc) | Actual cron in code |
|---|---|---|
| `MaterializeRecurringBookingsFunction.cs:19` | "Daily at 02:00 UTC" (lines 8-13) | `0 */2 * * * *` (every 2 min) |
| `SendRecurringOrderRemindersFunction.cs:23` | "Daily at 02:30 UTC… Runs 30min after Materialize" (lines 8-17) | `0 */2 * * * *` (every 2 min) |
| `SendMembershipLifecycleNotificationsFunction.cs:24` | "Daily sweep… can be tightened to a daily slot (e.g. 03:00 UTC)" (lines 8-18) | `0 */2 * * * *` (every 2 min) |
| `SendNewJobsDigestTimerFunction.cs:24` | "every 30 minutes (cron `0 0,30 * * * *`)" (lines 7-17) | `0 0/2 * * * *` (every 2 min) |

**Peer Functions in the same folder already ship real production crons hardcoded** — proving the codebase's intended end-state and that these four are the outliers:
- `AutoCancelStaleRecurringOrdersFunction.cs:24` → `0 0 * * * *` (hourly)
- `PayPeriodTimerFunction.cs:12` → `0 0 2 * * *` (daily 02:00)
- `DataRetentionTimerFunction.cs:12` → `0 0 3 * * 0` (weekly Sun 03:00)

**Ordering guarantee is real and currently false:** `SendRecurringOrderReminders.cs:16-32` documents pairing with `MaterializeRecurringBookings` and a 24h window so "a sweep at 02:00 UTC catches everything roughly 24h out." With both on the same 2-min tick, the reminder sweep can run *before* that tick's materialization commits, so a same-day spawned order can be missed until a later tick.

**Idempotency is load-bearing today, not by design:** `SendRecurringOrderReminders.cs:63-91` filters `RecurringReminderSentAt == null` and stamps via `MarkRecurringReminderSent` — the only reason 720 runs/day don't spam customers. `GetQueryableIgnoringTenant()` (line 63) confirms each run is a cross-tenant scan.

**Convention basis (so this is a real rule violation, not taste):**
- `conventions.md:116-117` — "No 'temporary' workarounds that become permanent." These are exactly that.
- `conventions.md:52-54` — "No magic numbers/strings… window durations… all come from a named home." A literal cron string with no config home is the magic-string smell; `%AppSetting%` binding gives it a named home.
- `runtime-readiness.md:34` — "Functions are observable… emits a metric or log the owner can alert on."
- No `*Cron` / `RunOnStartup` / AppSetting cron keys exist in `local.settings.json` today, so this is net-new config, not an edit to existing wiring.

## Acceptance Criteria (Given / When / Then)

1. **Production cadence — Materialize**
   **Given** the Functions app is running with production app settings,
   **When** the host evaluates the `MaterializeRecurringBookings` timer schedule,
   **Then** it resolves to once daily at 02:00 UTC (not every 2 minutes), and the schedule is supplied via a `%AppSetting%`-bound cron expression whose production value is `0 0 2 * * *`.

2. **Production cadence — Reminders, after Materialize**
   **Given** production app settings,
   **When** the host evaluates the `SendRecurringOrderReminders` timer schedule,
   **Then** it resolves to once daily at 02:30 UTC (`0 30 2 * * *`), i.e. 30 minutes after Materialize, restoring the documented "newly-spawned orders for tomorrow are eligible immediately" ordering.

3. **Production cadence — Membership lifecycle & New-jobs digest**
   **Given** production app settings,
   **When** the host evaluates the `SendMembershipLifecycleNotifications` and `SendNewJobsDigest` schedules,
   **Then** membership-lifecycle resolves to a single daily slot (03:00 UTC, `0 0 3 * * *`) and the new-jobs digest resolves to every 30 minutes (`0 0,30 * * * *`) — each matching its own XML-doc statement.

4. **Dev/prod differ without code change**
   **Given** a developer running the Functions app locally,
   **When** no override app setting is present (or the local override sets the dev cadence),
   **Then** the code contains no hardcoded `0 */2 * * * *` literal — the schedule is read from configuration, so the dev fast cadence is achievable purely via local settings while production uses the documented values, with the four `%AppSetting%` keys defaulted/documented in `local.settings.json`.

5. **Ordering observable in logs**
   **Given** a day with at least one recurring template materializing an order due ~24h out,
   **When** the daily run completes,
   **Then** the existing start/finish log lines show `MaterializeRecurringBookings` completing before `SendRecurringOrderReminders` starts on that day, and the reminder run's `Considered`/`RemindersSent` counts (`SendRecurringOrderReminders.cs:104`) include that newly-materialized order (no same-day miss).

6. **No behavioral regression in idempotency / window**
   **Given** the new daily cadence,
   **When** a reminder has already been sent for an order (`RecurringReminderSentAt` stamped),
   **Then** a subsequent run does not re-send (existing stamp filter still honored), and the reminder lead-window default (`LeadHoursLow = 6`, `LeadHoursHigh = 26` per `SendRecurringOrderReminders.cs:36`) is unchanged by this story.

## Out of Scope

- **No handler/business-logic changes.** `MaterializeRecurringBookings`, `SendRecurringOrderReminders`, `SendMembershipLifecycleNotifications`, and the `INewJobsDigestService` handlers are untouched — this is schedule/config only.
- **No change to the reminder lead-window** (`LeadHoursLow`/`LeadHoursHigh`) or the 7-day materialization horizon. (Note for the analyst trail: the `SendRecurringOrderReminders` XML doc on lines 28-32 says the default window is `[22,26]` but the code default is `[6,26]` — that doc/code drift is a *separate* finding, explicitly excluded here.)
- **No new metrics/alerting/dashboards.** Re-using existing start/finish logs is sufficient for AC5; adding the alert thresholds described in `runtime-readiness.md:62-66` is a separate ticket.
- **No migration / no schema change** — no `manual_step: ef-migration`.
- **No NSwag regeneration** — no API contract change; no `manual_step: nswag-regen`.
- **No frontend, mobile, or admin-UI surface.** No screen lets an admin edit cron values; building a cadence-config UI is out of scope.
- **Other timers** (`CleanupStalePendingOrders`, `RetryFailedFiscalRegistrations`, `SendSitewidePromoFanout`, `SendPushNotification`, `GenerateInvoice/Receipt`, etc.) are not part of this story unless they also carry a 2-minute placeholder; only the four cited Functions are in scope.

## Layers Touched

- **Azure Functions host layer (`src/Cleansia.Functions/Functions/`)** — the four `[TimerTrigger(...)]` attributes change from literal dev crons to `%AppSetting%`-bound expressions.
- **Functions configuration (`src/Cleansia.Functions/local.settings.json`, plus prod app settings owned by the operator)** — four new cron app-setting keys (dev values local, prod values in the deployment environment). Prod app-setting values are an **owner/operator step** (`conventions.md:104` — real config lives in env vars on prod), flagged `manual_step: prod-app-settings`.
- **Tests (`src/Cleansia.Tests`)** — schedule-resolution / config-binding assertions for AC1-4 where the timer expression is testable; no handler tests change.
- **Docs (`docs/architecture/`, the Functions' own XML doc-comments)** — XML docs already state the target cadence; confirm they match the new config and the architecture infra page references the production schedule.

No backend domain/AppServices, database, frontend, or mobile layers are touched.


---

I now have everything grounded. This GAP maps directly onto two cited rules: **S7 (Idempotency on side-effecting commands — webhook re-delivery)** and **S8 (tenant-scoped unique indexes are `(TenantId, X)`)**. The webhook provisioning path violates S7 (no check-before-create, no guard against the side effect already having occurred) and lacks the S8-shaped unique index backstop. Let me write the user story.

# US-customer-0042 — Webhook auto-provisioning must enforce the one-active-membership invariant

## Persona / id
`US-customer-0042` (persona = **customer** — the affected aggregate is the customer's `UserMembership`; the actor experiencing the harm is a subscribing customer, and the reconciliation/correctness owner is the platform on their behalf).

## Actor narrative

> **As a** Cleansia customer who subscribes to a membership plan,
> **I want** the system to provision exactly one active membership row no matter how many Stripe Checkout sessions I start or how my `customer.subscription.created` webhooks are delivered,
> **so that** my benefits (e.g. the once-per-period free express upgrade), renewal/cancellation reminders, and membership-aware pricing are computed against a single, truthful enrollment — and I am never double-billed-shaped against Stripe or double-granted benefits.

### Why this is real (grounded in code)

- The web Checkout flow (`CreateMembershipCheckoutSession.Handler`, `CreateMembershipCheckoutSession.cs:37-91`) only creates a Stripe **Session**; it never writes the local row. Its active-membership guard (`:54-59`, `GetActiveForUserAsync` → `BusinessErrorMessage.MembershipAlreadyActive`) gates *session creation*, not Stripe-side reality. A stale tab, the dashboard, or two near-simultaneous checkouts can each pass this guard and reach Stripe.
- The local `UserMembership` row is created **exclusively** by the webhook: `StripeSubscriptionWebhookHandler.ProvisionFromCreatedEventAsync` (`StripeSubscriptionWebhookHandler.cs:102-167`). That path validates user, plan, and metadata but **never calls `GetActiveForUserAsync`** before `UserMembership.Create` (`:154-160`) — so a second active row is inserted with no guard.
- The entity itself documents the invariant as handler-enforced only: `UserMembership.cs:12-15` ("at most one active membership… enforced in handler code, not by a unique index") and `Create`'s own contract `:84-88` ("Caller is responsible for ensuring no other Active membership exists").
- There is **no database backstop**: `UserMembershipEntityConfiguration.cs:56-61` has a unique index on `StripeSubscriptionId` and a **non-unique** composite `(UserId, Status)` index — nothing prevents two `Active` rows for one user.
- The duplicate is then silently masked, not surfaced: `UserMembershipRepository.GetActiveForUserAsync` (`UserMembershipRepository.cs:10-22`) does `OrderByDescending(CurrentPeriodEnd).FirstOrDefaultAsync` — it returns one arbitrary winner and hides the drift from every downstream reader (`QuoteOrder.cs:133`, `OrderFactory.cs:64`, `CancellationPolicyResolver.cs:33`, `GetMyMembership.cs:35`, `SwapMembershipPlan.cs:38`, `GdprDeletionService.cs:109`), all of which assume singularity.

### Rules this violates (cited)
- **S7 — Idempotency on side-effecting commands** (`security-rules.md:84-91`): provisioning a Stripe subscription mirror is exactly a "writes a financial record / creates a subscription" side effect that "must be idempotent — check whether the side effect already happened before doing it again… protects against webhook re-delivery (Stripe retries on 5xx/socket reset)." `ProvisionFromCreatedEventAsync` performs no such check.
- **S8 — Tenant isolation correctness** (`security-rules.md:94-101`): "Unique indexes on tenant-scoped tables are `(TenantId, X)`, not `(X)`." The required backstop is a filtered unique index on `(TenantId, UserId) WHERE Status = Active`, which is absent.
- **B8 — Side-effecting commands are idempotent** (`consistency.md:76-79`) reinforces S7 at the consistency layer and already names `CreateMembershipSubscription` as a known idempotency gap — the webhook path is the same class of defect.

## Acceptance criteria (Given / When / Then)

1. **Webhook check-before-create (the core fix)**
   **Given** a customer already has one `Active` `UserMembership` row,
   **When** a `customer.subscription.created` webhook arrives for a *different* `StripeSubscriptionId` for that same user,
   **Then** `ProvisionFromCreatedEventAsync` does **not** insert a second row, logs a reconciliation warning (including both subscription ids and the user id, no PII per S6), and the webhook returns `2xx` so Stripe does not retry.

2. **Same-subscription re-delivery stays idempotent (S7)**
   **Given** a `customer.subscription.created` webhook has already provisioned the row for a subscription id,
   **When** Stripe re-delivers that *same* event (retry / at-least-once delivery),
   **Then** exactly one row exists for that `StripeSubscriptionId`, no duplicate is created, and the existing row is updated via `UpdateFromStripeWebhook` (current behavior preserved — `GetByStripeSubscriptionIdAsync` short-circuits provisioning at `StripeSubscriptionHandler.cs:42-58`).

3. **Database backstop rejects concurrent duplicates (S8)**
   **Given** the new filtered unique index on `(TenantId, UserId) WHERE Status = Active` is applied,
   **When** two `customer.subscription.created` webhooks for the same user are processed concurrently and both pass the in-memory check,
   **Then** at most one `Active` row is committed; the second insert fails on the unique constraint and is handled fail-soft (logged + `2xx`, no `500`), leaving the data consistent.

4. **Cancelled-then-new still works (invariant is "one *active*", not "one ever")**
   **Given** a customer's prior membership is `Cancelled`,
   **When** they start a new subscription and its `customer.subscription.created` webhook arrives,
   **Then** a new `Active` row is provisioned successfully and the filtered index does not block it (consistent with `UserMembership.cs:13-14` — "cancelled+new is allowed and would violate a non-filtered index").

5. **Downstream readers see exactly one active membership**
   **Given** the fix is in place,
   **When** any consumer calls `GetActiveForUserAsync` (pricing in `QuoteOrder`/`OrderFactory`, `CancellationPolicyResolver`, `GetMyMembership`),
   **Then** it resolves a single deterministic active row per user, and benefits/reminders/pricing are computed once — no double-grant.

6. **Reconciliation observability**
   **Given** a webhook is suppressed because an active membership already exists (AC-1),
   **Then** a distinct, queryable log/marker is emitted identifying the user and both subscription ids so an operator can manually reconcile the orphaned Stripe subscription against Cleansia.

## Out of scope (explicit)

- **Reconciliation tooling / admin UI** to cancel or merge the orphaned Stripe subscription created in the AC-1 scenario — this story only *detects and refuses to duplicate*; it does not Stripe-side cancel, refund, or migrate.
- **Backfill / cleanup of any pre-existing duplicate active rows** already in production — a separate data-remediation ticket (the filtered unique index creation may surface them; deduping them is not this story).
- **Closing the request-side TOCTOU window** in `CreateMembershipCheckoutSession`/`CreateMembershipSubscription` beyond what the webhook fix + DB index already cover (e.g. pre-creation Stripe-side checks, locking the user). The fix here makes the *commit* safe; hardening the session-creation guard is separate.
- **The unrelated idempotency gaps** already tracked for `CreateMembershipSubscription` (no Stripe try/catch — `consistency.md:78`) and `CreateOrder` (B8) — same rule, different commands.
- **Stripe API/SDK version changes** or altering the `Stripe.net 50.x` period-extraction shape (`StripeSubscriptionHandler.cs:80-99`).
- **Mobile and Admin apps** — no UI work; the membership Checkout/provisioning path is web + webhook only.

## Layers touched

- **Backend — AppServices**: `StripeSubscriptionWebhookHandler.ProvisionFromCreatedEventAsync` (`src/Cleansia.Core.AppServices/Services/StripeSubscriptionWebhookHandler.cs`) — add `GetActiveForUserAsync` check + reconcile/log branch; fail-soft handling of the unique-constraint violation.
- **Backend — Domain (docs only)**: `UserMembership.cs` XML comments (`:12-15`, `:84-88`) updated to reflect that the invariant is now also enforced by a filtered DB index.
- **Backend — Infra.Database**: `UserMembershipEntityConfiguration.cs` — add the filtered unique index `(TenantId, UserId) WHERE Status = Active`. Optionally tighten `GetActiveForUserAsync`'s duplicate-masking (out of scope to *change* behavior, but a `TODO`/comment is acceptable).
- **Database / migration**: new filtered unique index. → **`manual_step: ef-migration`** (owner-only per CLAUDE.md and S9 `:112`). Note S9: this is a *new index*, not a column rename/drop — additive and safe, but must verify no pre-existing duplicate active rows would block index creation (ties into the out-of-scope backfill ticket).
- **Tests**: `Cleansia.Tests` (and/or `Cleansia.IntegrationTests`) — unit cover AC-1/2/4; an integration/DB test covers AC-3 (constraint enforcement) since a filtered unique index can only be verified against a real PostgreSQL provider.
- **No** frontend, mobile, NSwag-client, or i18n changes — no DTO/endpoint contract change, so **no `nswag-regen`**. (`MembershipAlreadyActive` error key already exists and is unchanged.)


---

There's no admin frontend feature consuming this endpoint yet — confirming the endpoint exists but the admin UI list does not. The `IRepository<GdprRequest, string>` base supplies `GetPagedSort`/`GetCountAsync` (as used by `GetPagedDisputes`), and the current `GetPaged(offset, pageSize)` is a non-canonical bespoke method. I now have everything grounded to write the story.

---

# US-persona-0041 — Canonicalize the admin GDPR-requests paged query so the list paginates correctly

> Note on the id: the persona is **admin**, so the canonical id is **US-admin-0041**. (`0041` is a placeholder sequence number for the PM to slot into `agents/backlog/INDEX.md`; renumber on intake.)

## Actor narrative

**As an** admin (data-protection officer) reviewing GDPR export/deletion requests,
**I want** the admin GDPR-requests list endpoint to return a properly paged result with a total count, server-side sort, and a specification-backed filter — built the same canonical way as every other admin list,
**so that** the admin UI can render correct pagination (page count, "X of N", next/prev) instead of being handed an unbounded, count-less page that silently caps at one screen and diverges from every other list I use.

## Context / grounding (read before implementing)

- The broken read: `src/Cleansia.Core.AppServices/Features/Gdpr/GetAllGdprRequests.cs` — uses `record Query(int Page = 1, int PageSize = 20) : IQuery<List<GdprRequestDto>>` with manual 1-based offset (`var offset = (request.Page - 1) * request.PageSize;`, line 30), inline-projects the DTO inside the handler (`.Select(r => new GdprRequestDto(...))`, lines 35-37), returns `BusinessResult.Success(List<GdprRequestDto>)` with **no total count**, and has **no Specification**.
- The controller leaks the same shape: `src/Cleansia.Web.Admin/Controllers/AdminGdprController.cs:42-49` — `GetAllGdprRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20, …)`, `ProducesResponseType(typeof(List<GdprRequestDto>), …)`.
- The bespoke repository call: `GdprRequestRepository.GetPaged(offset, pageSize)` (custom paging) instead of the base `IRepository<GdprRequest, string>.GetPagedSort<TSort>(...)` / `GetCountAsync(...)` that the canonical archetype uses.
- This is a **real, confirmed** divergence from the codebase's documented rules: it violates `agents/knowledge/consistency.md` rules **A1** (record `Query` with inline page/pageSize instead of `Request : DataRangeRequest`), **A2/A5** (returns `BusinessResult<List<T>>` not `PagedData<T>`, no `MapToDto(total, request)`), **A3/A4** (no Specification, no `GetPagedSort<XxxSort>` + `GetCountAsync`), and **B9** (inline DTO projection in the handler).
- Canonical reference to mirror exactly: `src/Cleansia.Core.AppServices/Features/Disputes/GetPagedDisputes.cs` (Request : `DataRangeRequest`, IRequest<`PagedData<DisputeListItem>`>, Specification → `SatisfiedBy()`, `GetPagedSort<DisputeSort>(...)`, `GetCountAsync(...)`, `items.MapToDto(totalItems, request)`). Supporting types: `Shared/DTOs/RequestModels/DataRangeRequest.cs`, `Mappers/PageDataMapper.cs` (the `MapToDto<T>(this IEnumerable<T>, int total, DataRangeRequest)` extension), `Core.Domain.Sorting`.
- Entity / sortable fields available: `GdprRequest` (`UserId`, `RequestType`, `GdprRequestStatus Status`, `ProcessedBy`, `CompletedAt`, `Notes`, `CreatedOn`); `GdprRequestStatus { Pending, Processing, Completed, Failed }`.

## Acceptance criteria (Given/When/Then)

1. **Total count is returned (the core defect).**
   **Given** there are 25 GDPR requests for the tenant and the admin requests `Offset=0, Limit=20`,
   **When** `GET /api/v{version}/AdminGdprController/requests` is called,
   **Then** the response is a `PagedData<GdprRequestDto>` whose `Total = 25`, `PageSize = 20`, `PageNumber = 1`, and whose `Data` holds the first 20 items — so the UI can compute "page 1 of 2".

2. **Canonical request shape replaces the bespoke one (A1).**
   **Given** the refactored feature,
   **When** I open `GetAllGdprRequests.cs`,
   **Then** the query is a `public class` with a nested `Request : DataRangeRequest, IRequest<PagedData<GdprRequestDto>>` (carrying `Offset`/`Limit`/`Sort`), the old `record Query(int Page, int PageSize)` and the manual `(Page - 1) * PageSize` offset math are gone, and the handler returns `PagedData<GdprRequestDto>` directly (not `BusinessResult<List<…>>`).

3. **Specification + server-side sort drive the read (A3/A4).**
   **Given** a `GdprRequestSpecification` and a `GetPagedSort<GdprRequestSort>` exist,
   **When** the handler executes,
   **Then** it counts via `repository.GetCountAsync(filter, ct)` and pages via `repository.GetPagedSort<GdprRequestSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())`, and a request for `Sort` by `CreatedOn` descending returns the newest request first.

4. **DTO mapping is an extension, not inline (B9/A5/A6).**
   **Given** the handler read path,
   **When** I inspect it,
   **Then** the projection uses a `MapToDto()` extension on `GdprRequest` and the result is wrapped with `items.MapToDto(totalItems, request)` (the `PageDataMapper` extension) — there is no `new GdprRequestDto(...)` constructed inside the handler and no hand-built `new PagedData<T>(...)`.

5. **Controller advertises the paged contract (consistent with other admin lists).**
   **Given** `AdminGdprController.GetAllGdprRequests`,
   **When** the action is refactored,
   **Then** it binds `[FromQuery] GetAllGdprRequests.Request` (Offset/Limit/Sort), its `[ProducesResponseType]` is `typeof(PagedData<GdprRequestDto>)`, it still carries `[Permission(Policy.CanViewGdprRequests)]`, and the inline `page`/`pageSize` query params are removed.

6. **The hard safety cap survives the refactor.**
   **Given** the existing defense-in-depth that an admin cannot dump the whole audit table,
   **When** a request arrives with an out-of-range page size,
   **Then** the page size is still bounded (the `DataRangeRequest.Limit` `[Range(1, 100000)]` is tightened — or a validator retained — so the effective max stays at the current ceiling of **100**, preserving the intent of the original `InclusiveBetween(1, 100)` rule).

## Out of scope

- Building or restyling the **admin GDPR-requests UI list** (table/facade/store). No admin frontend feature currently consumes this endpoint; wiring up a new admin list page is a separate story. (Once the contract changes, **`manual_step: nswag-regen`** must be flagged so the owner regenerates the admin client before any frontend work — Claude does not run `npm run generate-admin-client`.)
- Any change to **filtering semantics beyond what a `GdprRequestSpecification` needs to exist** (e.g. new filters by status/date/user) — add only the minimal spec to satisfy the archetype; richer GDPR-request filters are a follow-up.
- The **GDPR request *workflow*** (creating, processing, completing/failing requests; `MarkProcessing`/`MarkCompleted`/`MarkFailed`; export/deletion services) — read-list canonicalization only.
- **EF Core migrations / DB schema** — this is a read-path refactor; no schema change is expected. If one is somehow required, raise it as a `MANUAL_STEP` (owner-only).
- Canonicalizing the **other tracked paged-query deviations** (`GetPagedPromoCodes`, `GetPagedReferrals`, etc.) — separate tickets.
- Touching the customer/partner/mobile GDPR controllers — only the **Admin** `requests` endpoint is in scope.

## Layers touched

- **Backend — AppServices** (`Cleansia.Core.AppServices/Features/Gdpr/`): rewrite `GetAllGdprRequests.cs` to the canonical archetype; add `GdprRequestSpecification`, `GdprRequestSort`, and a `GdprRequest.MapToDto()` mapper extension (mirroring the Disputes feature layout). Reuse `DataRangeRequest`, `PagedData<T>`, `PageDataMapper.MapToDto`.
- **Backend — Domain** (`Cleansia.Core.Domain/`): add `GdprRequestSort` enum under `Sorting`; the `IGdprRequestRepository`/`IRepository<GdprRequest, string>` base already provides `GetPagedSort`/`GetCountAsync`, so the bespoke `GetPaged(offset, pageSize)` on the repository should be removed/replaced.
- **Backend — Infra.Database** (`Cleansia.Infra.Database/Repositories/GdprRequestRepository.cs`): drop the custom `GetPaged` in favor of the base paged-sort path (matching how `DisputeRepository` works).
- **Backend — Web.Admin** (`Cleansia.Web.Admin/Controllers/AdminGdprController.cs:42-49`): bind `GetAllGdprRequests.Request`, update `ProducesResponseType` to `PagedData<GdprRequestDto>`, keep the `Policy.CanViewGdprRequests` permission.
- **Contract / generated client (owner-only, flag only):** the admin OpenAPI spec changes shape → `manual_step: nswag-regen` for `generate-admin-client`. **No frontend code in this story.**


---

No existing stories, so `US-customer-0001` is free. I have grounded every claim in the cited code:

- `profile.component.ts:115` — `emailNotifications = signal(true)`, never loaded, never persisted, no facade call (the only mutation is the template's local `.set($event)`).
- `profile.component.html:349-358` — single `p-toggleSwitch` bound only to that local signal.
- `NotificationPreferencesController.cs` (Customer API) — `GetMine` (GET) + `Update` (PUT), both `Policy.Authenticated`, upsert-on-read.
- `NotificationPreferencesDto` / `UpdateNotificationPreferences.Command` — exactly **11** customer-facing boolean categories (the 12th enum value `NewJobsAvailable` is partner-side and excluded).
- `NotificationPreferencesRepository.kt` — Android already consumes this with proper load/error/optimistic-revert states (the three-data-states bar web fails).
- Grounded the rule citation in `conventions.md` ("Empty, loading, error... are part of the work", line 121-122; reuse the generated client + `cleansia-*` + `SnackbarService`, line 29-30).

Here is the user story.

---

```yaml
---
id: US-customer-0001
title: Manage my push/notification preferences from the web profile
persona: customer
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---
```

## Narrative

As a **customer using the Cleansia web app**, I want **the "Email Notifications" control in my profile to actually load and save my real per-category notification preferences on the server**, so that **the choices I make on the web are honored by the platform (and stay in sync with the Android app), instead of being a fake switch that always reads "on" and changes nothing**.

## Context (grounded in code)

- The web profile renders a single PrimeNG toggle (`profile.component.html:349-358`) bound only to a local signal `emailNotifications = signal(true)` (`profile.component.ts:115`). It is never loaded from the server, never persisted, and defaults to `true` on every page load — a dead-end control with no real data state.
- The backend already exposes the full, customer-facing contract: `NotificationPreferencesController` on the Customer API (`GetMine` GET + `Update` PUT, both `Policy.Authenticated`, upsert-on-read), backed by `GetMyNotificationPreferences` / `UpdateNotificationPreferences`, over **11 categories** in `NotificationPreferencesDto` (`OrderUpdates, CleanerOnTheWay, OrderCompleted, OrderCancelled, RefundIssued, MembershipExpiring, MembershipCancelled, TierUpgrade, Promo, DisputeReply, RecurringScheduled`). The 12th enum value `NotificationCategory.NewJobsAvailable` is partner-side and is intentionally **not** in the customer DTO.
- The Android customer app already consumes this contract correctly with load / error / optimistic-revert states (`NotificationPreferencesRepository.kt`). Web is the only orphaned consumer.
- This violates the `conventions.md` "production-ready" bar — "Empty, loading, error, and edge states are part of the work" (lines 121-122) — and the prime directive to reuse the generated client wrapper + `cleansia-*`/PrimeNG components + `SnackbarService` rather than a hand-rolled local signal (lines 29-30).

## Acceptance criteria

- **AC1 — Load real state** — Given an authenticated customer opens their web profile, When the notification-preferences section initializes, Then it calls `GetMine` and renders each of the 11 categories from the server response (no category is hardcoded to `true`/`on`), reflecting the user's actual saved values.

- **AC2 — Persist a change** — Given the preferences have loaded, When the customer toggles any category and the change is committed, Then `Update` is called with the full 11-category payload (replace-all semantics) and, on success, the UI reflects the server's returned values; reloading the page shows the persisted state, not the default.

- **AC3 — Loading state** — Given the GET is in flight, When the section is shown, Then a loading affordance (e.g. skeleton/disabled toggles, consistent with the existing profile skeletons) is displayed and the toggles are not interactive until data arrives — there is no flash of a fabricated "all on" state.

- **AC4 — Error state** — Given the GET fails (network/401/server), When the section renders, Then an error state is shown via `SnackbarService` (no silent fallback to "all on"), and the customer is not presented with toggles implying a saved state that does not exist.

- **AC5 — Failed save reverts** — Given a customer toggles a category, When the `Update` call fails, Then the toggle reverts to its last server-confirmed value and an error is surfaced via `SnackbarService` (no local-only "stuck" state that disagrees with the server) — matching the optimistic-revert behavior the Android repo already implements.

- **AC6 — No dead local switch** — Given the new section exists, When the code is reviewed, Then the fake local-only `emailNotifications` signal and its lone toggle are removed (no dead code per `conventions.md`), and every visible label/category name resolves through `TranslatePipe` keys present in all 5 locales (en, cs, sk, uk, ru).

## Out of scope

- **Email vs. push channel split.** The existing model is a single per-category preference (the entity/DTO has one bool per category, surfaced via push on Android). This story wires the web UI to that existing model; it does **not** introduce separate email/SMS/push channel toggles. (If product wants per-channel granularity, raise a Q in `questions/open.md` and an ADR — it's an Architect decision, not an ad-hoc field.)
- **The 12th category, `NewJobsAvailable`.** It is partner-side (digest) and absent from the customer DTO; the customer web surface must show exactly the 11 customer categories and nothing more.
- **Partner and Admin web apps.** This story covers only the customer web app (`cleansia.app`). Partner/admin preference surfaces, if needed, are separate stories.
- **Backend changes.** The controller, handlers, DTO, entity, and the lazy-create-on-read behavior already exist and are correct; no backend work is in scope. (If NSwag types for these endpoints are missing from the customer client, flag `manual_step: nswag-regen` — do not hand-edit the generated client.)
- **Notification delivery/triggering logic.** This is preference management only; it does not touch how/when notifications are actually sent (functions, channels, fan-out).
- **New backend categories or i18n wording sign-off.** Adding categories or finalizing tone/wording is owner-gated per `conventions.md` localization rules.

## Layers touched

- **Frontend (web, customer app — primary):** new notification-preferences section + facade in `libs/cleansia-customer-features/profile/...` (`profile.component.ts` / `.html`, plus a `profile.facade.ts`/dedicated facade), using the generated `@cleansia/customer-services` client, `cleansia-*`/PrimeNG toggles, signal state with explicit loading/error/loaded states, and `SnackbarService`.
- **i18n:** category labels + section copy added to all 5 locale files (`apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`).
- **Generated client (NSwag):** verify `GetMine`/`Update` + `NotificationPreferencesDto` are present in the customer client; if not, **owner-only** `manual_step: nswag-regen` (no hand edits).
- **Backend:** none expected (contract already complete) — read-only reference: `NotificationPreferencesController` (Customer), `GetMyNotificationPreferences.cs`, `UpdateNotificationPreferences.cs`, `NotificationPreferencesDto.cs`, `NotificationCategory.cs`.
- **Mobile / DB:** none — existing Android consumer and `UserNotificationPreferences` entity/migration are unchanged.

## Open questions

- **Q (presentation granularity):** Show all 11 categories as individual toggles, or group them (e.g. "Order updates", "Membership", "Promotions") with the single legacy toggle promoted to a section? Default assumption taken until owner answers: render all 11 as individual labeled toggles (1:1 with the DTO), grouped under headings for readability, mirroring the Android `NotificationsScreen` layout. Raise as Q-NNNN in `agents/backlog/questions/open.md` if a different grouping is desired.


---

Even the i18n keys already exist: `errors.service.in_use` (line 1765) and `errors.package.in_use` (line 1813) are translated and present. So the entire user-facing surface (error constant + translation) was scaffolded for the in-use guard, but the validator/handler never call it and the FK is cascade. This is a half-wired feature — the story is finishing it the soft-delete way.

I have all the grounding I need. Here is the user story.

---

# US-admin-0042 — Soft-delete catalog services & packages instead of cascade-destroying order history

## Context (grounded in code)

`DeleteService.Handler` (`src/Cleansia.Core.AppServices/Features/Services/DeleteService.cs:33-37`) and `DeletePackage.Handler` (`src/Cleansia.Core.AppServices/Features/Packages/DeletePackage.cs:33-37`) call `repository.Remove(entity)` — an unconditional hard delete. The validator only checks `NotEmpty` + `ExistsAsync`; there is **no in-use check**.

Because `FK_OrderServices_Services_ServiceId` is `onDelete: Cascade` (`src/Cleansia.Infra.Database/Migrations/20260519203658_Initial.cs:1734-1738`) and `FK_PackageServices_Services_ServiceId` is likewise `Cascade` (`:1166-1170`), one admin "Delete" click silently deletes the matching line-item row from **every historical order** that used that service/package — including `Completed`, invoiced, and receipted orders — corrupting financial and booking history with no warning. Simultaneously `EmployeePayConfig → Service`/`→ Package` are `Restrict` (`src/Cleansia.Infra.Database/EntityConfigurations/EmployeePayConfigEntityConfiguration.cs:61,66`), so if a pay config references the entity the same delete throws a raw DB exception → unhandled 500.

This contradicts the sibling catalog deletes: `DeleteCountry`/`DeleteCurrency`/`DeleteLanguage` all call `IsInUseAsync` and block with a `*.InUse` business error (e.g. `DeleteCountry.cs:26-28,45-49`). `IServiceRepository`/`IPackageRepository` are bare interfaces with no such method (`src/Cleansia.Core.Domain/Repositories/IServiceRepository.cs:5`, `IPackageRepository.cs:5`).

This violates two named rules:
- **Consistency B6** (`agents/knowledge/consistency.md:65-72`): prefer soft-delete via `Deactivate` (sets `IsActive=false`, preserves history); `Remove` is only for true join/scratch rows that carry no history and are never referenced. Services and packages carry both.
- **Security S10** (`agents/knowledge/security-rules.md:114-121`): `IsActive` is the soft-delete flag, and the customer overviews already filter it (`GetServiceOverview.cs:21`, `GetPackageOverview.cs:21`), so deactivation already hides the entity from the booking catalog — no destructive delete is needed to "retire" a service.

Notably, the user-facing surface for the safe behavior **already exists but is unwired**: `BusinessErrorMessage.ServiceInUse = "service.in_use"` and `PackageInUse = "package.in_use"` (`BusinessErrorMessage.cs:223,230`) are defined and translated in admin i18n (`errors.service.in_use` en.json:1765, `errors.package.in_use` :1813) yet referenced by no validator or handler.

## Story

**As an** admin managing the service/package catalog,
**I want** deleting a service or package to retire it safely — preserving every historical order, invoice, and receipt that referenced it — instead of cascade-erasing those records,
**so that** I can clean up the catalog without silently destroying financial and booking history or hitting an unexplained 500 error.

## Acceptance criteria

1. **Given** a service that appears on at least one existing order line item (`OrderServices`), **when** an admin deletes it, **then** the service is retired (`IsActive` set to false via the soft-delete path) and **all** `OrderServices` rows — on `Completed`, invoiced, and receipted orders — remain intact and unchanged.

2. **Given** a retired (soft-deleted / inactive) service, **when** a customer loads the booking catalog (`GetServiceOverview`), **then** the retired service does not appear (the existing `Where(s => s.IsActive)` filter already enforces this), **and** historical orders still resolve and display that service's line item correctly.

3. **Given** a service referenced by an `EmployeePayConfig` (the `Restrict` FK), **when** an admin attempts to delete it, **then** the request fails with the `BusinessErrorMessage.ServiceInUse` (`"service.in_use"`) business error — a clean validation failure, **not** an unhandled 500 / raw DB exception.

4. **Given** a service with no order, package, or pay-config references at all, **when** an admin deletes it, **then** it may be hard-removed (or soft-retired — implementation's choice, but consistent with the chosen strategy) and the operation succeeds with the existing `Response` shape.

5. **Given** any of the above failure cases, **when** the error reaches the admin UI, **then** it surfaces the already-present translated message (`errors.service.in_use` / `errors.package.in_use`) in all 5 locales (en, cs, sk, uk, ru) — no new untranslated key.

6. **Given** a package that is referenced by orders (`PackageServices`) or an `EmployeePayConfig`, **when** an admin deletes it, **then** behavior is identical to the service case (soft-retire or `PackageInUse` block) — `DeletePackage` and `DeleteService` stay symmetric, matching the `DeleteCountry`/`DeleteCurrency`/`DeleteLanguage` pattern.

## Out of scope

- Changing the FK `onDelete` behavior on `OrderServices`/`PackageServices` in a migration (a defense-in-depth follow-up; this story fixes the application-layer path that triggers the cascade — the owner owns any migration via `MANUAL_STEP`).
- A "restore / un-retire" admin action for previously deactivated services/packages.
- Backfilling or repairing order history already corrupted by prior hard deletes.
- Per-employee pay-config work (IMP-3), admin order intervention, payroll settlement, and the `CancelOrder` hardcoding noted in the prior audit — separate findings/stories.
- Any frontend redesign of the catalog delete UX beyond surfacing the existing error message; the confirmation dialog already exists.
- Hard-delete cleanup of `Currency`/`Country`/`Language` (already guarded; not this story).

## Layers touched

- **Domain — repositories** (`src/Cleansia.Core.Domain/Repositories/IServiceRepository.cs`, `IPackageRepository.cs`): add `IsInUseAsync` to mirror `ICountryRepository`, **or** rely on the `Deactivate` path on the base repository if soft-delete is chosen.
- **Infra — database / repositories** (`src/Cleansia.Infra.Database/Repositories/ServiceRepository.cs`, `PackageRepository.cs`): implement `IsInUseAsync` checking `OrderServices` / `PackageServices` / `EmployeePayConfig` references (model: `CountryRepository.IsInUseAsync`).
- **AppServices — commands** (`DeleteService.cs`, `DeletePackage.cs`): wire the validator + handler to either `Deactivate` (B6 soft-delete) or block with `ServiceInUse`/`PackageInUse` (`BusinessErrorMessage.cs:223,230`), following the `DeleteCountry` validator/handler shape.
- **Frontend — admin app**: no code change required beyond confirming the existing translated `errors.service.in_use` / `errors.package.in_use` keys render; verify in all 5 locale files.
- **Tests** (`src/Cleansia.Tests`): handler/validator unit tests for in-use-blocked, retire-preserves-history, and not-referenced-succeeds cases.
- **MANUAL_STEP (owner-only):** NSwag regen is **not** required if the `Response` contract is unchanged; flag `nswag-regen` only if a new field/error shape is added. Any FK `onDelete` migration (out of scope here) would be `ef-migration`.


---

I now have everything grounded in real code and real rules. Key findings confirmed:

- Customer `UserController.cs:39-61` and Partner `UserController.cs:52-72` expose `RequestPasswordChange`/`ChangePassword` as `[AllowAnonymous]` + `[EnableRateLimiting("auth")]` (customer) — these key off `Email` + reset `Code` (token-based forgot-password flow), not an authenticated "change my password."
- `ChangePassword.Command(Email, NewPassword, Code)` requires a valid reset token; there is no "old password" authenticated path anywhere.
- `AdminAuthController` exposes only Login/RefreshToken/Logout. No `GetCurrentUser`/`UpdateCurrentUser` exists on the Admin API at all (grep returned nothing).
- `AdminUserController` manages OTHER admins (create/update/activate/deactivate) gated by `CanViewAdminUsers`/`CanCreateAdminUser` etc. — no self-credential path.
- No `profile`/`account`/`security` feature lib in `cleansia-admin-features`.
- Relevant rules: S2 (authorize on every endpoint — note the `[Authorize]`-only "GetMyProfile" pattern is explicitly sanctioned), S1 (userId is server-truth from JWT), S5 (rate-limit auth endpoints), S6 (no PII in logs), B2 (identity from `IUserSessionProvider.GetUserId()` in handler), and frontend D1-D3 form conventions.

Here is the user story.

---

# US-admin-0007 — Admin self-service password change

## Actor narrative

**As an** authenticated admin user (Administrator or admin-Employee role) of the Cleansia admin app,
**I want** to change my own account password from within the admin app while signed in,
**so that** I can practice basic credential hygiene (routine rotation, or reacting to a suspected leak) without depending on another admin re-creating my account or on direct database access.

## Context / grounding (why this is a gap)

- The customer and partner web apps both ship a password surface; the admin app ships none. The customer/partner password endpoints (`Cleansia.Web.Customer/Controllers/UserController.cs:39-61`, `Cleansia.Web.Partner/Controllers/UserController.cs:52-72`) are the **token-based forgot-password** flow: `[AllowAnonymous]`, keyed off `Email` + reset `Code`, mapping to `RequestPasswordChange.Command(Email, Language)` and `ChangePassword.Command(Email, NewPassword, Code)` (`Cleansia.Core.AppServices/Features/Users/RequestPasswordChange.cs`, `ChangePassword.cs:73-77`). There is **no** authenticated "verify my current password, then set a new one" path anywhere in the backend.
- `AdminAuthController` (`Cleansia.Web.Admin/Controllers/AdminAuthController.cs`) exposes only `Login`, `RefreshToken`, `Logout`. A repo-wide search for `GetCurrentUser`/`CanGetCurrentUser` in `Cleansia.Web.Admin` returns nothing — the Admin API has no "my profile / my credentials" surface at all.
- `AdminUserController` (`Cleansia.Web.Admin/Controllers/AdminUserController.cs`) manages **other** admins (create/update/activate/deactivate), each gated by management policies (`CanCreateAdminUser`, `CanUpdateAdminUser`, etc.). None of these let a caller change their **own** password.
- `cleansia-admin-features` has `admin-login` and `admin-user-management` libs but no `profile`/`account`/`security` feature lib.

The product decision this story resolves (per the proposed fix): the admin self-service path is an **authenticated** "change my password" that verifies the caller's **current** password, derives identity from the JWT (S1 / B2), and does **not** reuse the anonymous email+reset-code flow. Admin password *recovery* when locked out (forgot-password by email) is explicitly out of scope below.

## Acceptance criteria

1. **Authenticated entry point exists**
   **Given** I am signed in to the admin app,
   **When** I open my account/security area,
   **Then** I see a "Change password" form with Current password, New password, and Confirm new password fields, reachable only while authenticated (unauthenticated access redirects to login).

2. **Successful change with correct current password**
   **Given** I enter my correct current password and a new password meeting the platform rule (min 8 chars, at least one letter and one digit — the same `^(?=.*[a-zA-Z])(?=.*\d).{8,}$` rule enforced by `ChangePassword.Validator`),
   **When** I submit,
   **Then** my password is updated, I see a success confirmation, and I can authenticate with the new password and no longer with the old one.

3. **Wrong current password is rejected**
   **Given** I enter an incorrect current password,
   **When** I submit,
   **Then** the change is rejected with a field-level error, my password is unchanged, and the error message does not disclose whether any other field was also invalid (current-password failure short-circuits via `Cascade.Stop`).

4. **New password must differ and be well-formed**
   **Given** I enter a new password that is identical to my current one **or** that fails the format rule,
   **When** I submit,
   **Then** the change is rejected with the corresponding field-level error keyed to the offending field (`SameResetPassword` / `InvalidPasswordFormat` style messages), and no change is persisted.

5. **Server derives identity from the session, not the request**
   **Given** any submission,
   **When** the backend processes it,
   **Then** the target account is resolved from the JWT in session (per S1 / B2 — `IUserSessionProvider.GetUserId()` in the handler), the endpoint carries `[Authorize]` (the sanctioned "any authenticated user / GetMyProfile" form in S2), and a request body that names a different user/email cannot change another account's password.

6. **Abuse resistance and log hygiene**
   **Given** repeated submissions,
   **When** they hit the endpoint,
   **Then** the endpoint is rate-limited under the shared `"auth"` window (`[EnableRateLimiting("auth")]`, S5), and no password value, JWT, or PII is written to logs at Information level or above (S6).

7. **Localized UI**
   **Given** the admin app is set to any supported locale,
   **When** I use the change-password form,
   **Then** all labels, validation messages, and the success/error toasts are rendered via `TranslatePipe` with keys present in all five locale files (en, cs, sk, uk, ru); no hardcoded strings.

## Out of scope

- **Admin forgot-password / locked-out recovery** (the email + reset-code flow). This story is the *authenticated* change only; recovery-when-locked-out is a separate story (and a separate product/security decision about whether admins may receive reset emails).
- **Editing any other profile field** (name, email, avatar, language, notification prefs). No `GetCurrentUser`/`UpdateCurrentUser` surface is being added to the Admin API here — password only.
- **Changing another admin's password / admin-initiated reset of a subordinate** (would live in `AdminUserController` under a management policy, not self-service).
- **MFA / 2FA, session-wide forced logout of other devices, password-history (>1 previous) or expiry policies, "must change on next login"** — not introduced by this story.
- **Customer and partner apps** — their existing flows are unchanged; no refactor of the shared `ChangePassword`/`RequestPasswordChange` commands' anonymous behavior.
- **EF migration / schema changes** — none expected (reuses existing `User.Password` and domain `UpdatePassword`); if any column proves necessary it is an owner-only `MANUAL_STEP` (S9), not part of delivery.
- **NSwag client regeneration** — owner-only `manual_step: nswag-regen` once the new admin endpoint's DTO exists.

## Layers touched

- **Backend — AppServices (CQRS):** new authenticated `ChangeMyPassword` command + validator + handler under `Cleansia.Core.AppServices/Features/AdminUsers/` (or `Features/Users/`), identity from `IUserSessionProvider.GetUserId()` (B2), current-password verification + new-password format/difference rules in the validator (B3/B4), domain `user.UpdatePassword(...)` call (B7). New `BusinessErrorMessage` key(s) for the wrong-current-password case.
- **Backend — Web.Admin:** new authenticated endpoint on `AdminAuthController` (or a new self-service controller) with `[Authorize]` + `[EnableRateLimiting("auth")]` (S2/S5).
- **Backend — Domain:** reuses existing `User.UpdatePassword`; verify a current-password check helper exists (mirrors `CheckIfPasswordSame`).
- **Frontend — admin app:** new `cleansia-admin-features` feature lib (e.g. `account-security`) — facade (extends `UnsubscribeControlDirective`, `loading`/`saving` signals, C3 pipe, D1), `standalone` + `OnPush` component with `fb.nonNullable.group(...)` and `cleansia-*` inputs (D2/D3), route + nav entry, calling the regenerated admin client.
- **i18n:** new keys under `errors.*` and the feature's labels in all five admin locale files.
- **Manual steps (owner-only):** `nswag-regen` (new admin DTO/endpoint); EF migration only if a schema change turns out to be required (not anticipated).


---

I now have everything to ground the story. The findings cite real rules:

- **S6 (Logging hygiene)** directly governs the proposed fix: if we log a skipped orphan, we must log the `SavedAddressId`/`userId` only — never the address contents (PII) — at the appropriate level.
- **S10** is the *adjacent* rule that the read path already (mostly) honors elsewhere — "list my saved addresses must exclude deactivated" — but `GetSavedAddresses` does NOT filter `IsActive` and instead does a different, undocumented `.Where(s => s.Address != null)` orphan-drop. That silent drop is the gap.
- The FK is `DeleteBehavior.Restrict` (SavedAddressEntityConfiguration.cs:23) and `AddressRepository` exposes no delete method, so an orphan is an *invariant violation* (data-integrity bug), not a normal state — which is exactly why silently swallowing it is the wrong behavior. **conventions.md** "production-ready bar": *solve root cause, surface deeper structural problems rather than paper over them.*

Here is the user story.

---

# US-admin-0042 — Surface (don't silently drop) orphaned saved addresses

## Persona
`admin` — the operator/support staff responsible for platform data integrity. The *symptom* is felt by a `customer` (an address vanishes from their list), but the *actionable* outcome — a problem that must be detected and triaged — lands on the admin/operations side via logs and observability. The customer is a secondary beneficiary.

## Actor narrative
**As an** operations admin,
**I want** the system to detect and log when a user's saved address references an Address row that no longer resolves (orphaned `AddressId` FK), instead of silently omitting it from the list,
**so that** a referential-integrity breach is surfaced and triaged rather than masked, and a customer never loses a saved address without anyone being able to see why.

## Context / grounding (code & rules)
- `src/Cleansia.Core.AppServices/Features/SavedAddresses/GetSavedAddresses.cs:22` — the handler does `.Where(s => s.Address != null)` before projecting to `SavedAddressDto`, dropping any orphan with **no error and no log**.
- `src/Cleansia.Infra.Database/EntityConfigurations/SavedAddressEntityConfiguration.cs:23` — the `SavedAddress → Address` FK is `OnDelete(DeleteBehavior.Restrict)`; `AddressRepository` (`src/Cleansia.Infra.Database/Repositories/AddressRepository.cs`) exposes **no** delete method. So an orphan is an **invariant violation**, not a normal state — meaning the silent filter masks a real data-integrity bug.
- `Address` (`src/Cleansia.Core.Domain/Users/Address.cs`) has **no soft-delete** and mutates in place via `Anonymize()`; there is no IsDeleted flag the read could legitimately be filtering on.
- This contradicts `agents/knowledge/conventions.md` "production-ready, long-term bar" (*solve root cause; surface deeper structural problems rather than paper over them*) and the `agents/knowledge/security-rules.md` **S6** logging-hygiene law (log identifiers, never address PII).

> Note: `GetSavedAddresses` does not filter `IsActive` either (S10), and the existing `.Where` is **not** that filter — it is an undocumented orphan-drop. This story is scoped to the orphan-drop behavior only (see out-of-scope).

## Acceptance criteria

1. **Given** a user with three saved addresses all resolving to valid Address rows, **when** they request their saved-address list, **then** all three are returned (no behavior change for the healthy path) and nothing is logged about orphans.

2. **Given** a user has a saved address whose `AddressId` references an Address that no longer resolves (orphaned FK), **when** `GetSavedAddresses` runs, **then** the system emits exactly one structured log event at **Warning** level for that orphan, containing the `SavedAddress.Id`, the dangling `AddressId`, and the `userId`, and **no** address content fields (Street, City, ZipCode, State, lat/long) — per S6.

3. **Given** the same orphaned saved address, **when** the list is returned, **then** the orphan is excluded from the returned DTO list (the response stays valid and never throws an NRE), so the customer-facing list still renders the remaining valid addresses.

4. **Given** a list containing two orphaned saved addresses, **when** the handler runs, **then** two distinct Warning events are logged (one per orphan, each with its own `SavedAddress.Id`/`AddressId`) so each integrity breach is independently traceable.

5. **Given** a list with no orphans, **when** the handler runs repeatedly, **then** no Warning-level orphan log is ever produced (no false positives / log noise on the healthy path).

6. **Given** any orphan log is produced, **when** an admin inspects logs/observability, **then** the event is greppable by a stable marker (e.g. an event name/category like `SavedAddress.OrphanedAddressSkipped`) so an alert can be wired to it, and it carries `userId` rather than user email/name (S6).

## Out of scope
- Enforcing the FK invariant at the schema level or adding a soft-delete column to `Address` (that is an **Architect/ADR + `manual_step: ef-migration`** decision, raised separately — this story only makes the existing breach *visible*).
- Adding/changing any `IsActive` (S10) filtering in `GetSavedAddresses` — tracked as its own finding.
- Backfilling, repairing, or deleting existing orphaned rows (data remediation job).
- Any change to `AddSavedAddress` / `UpdateSavedAddress` / `DeleteSavedAddress` / `SetDefaultSavedAddress` write paths.
- Surfacing the orphan to the end customer's UI (web `SavedAddressController` or Android `SavedAddressApi.kt`); the customer experience is unchanged — the address simply stays absent. No new `BusinessErrorMessage` code, no i18n keys, no NSwag regen.
- Building an admin screen/report for orphans (an alert wired to the log marker, per AC-6, is sufficient for this story).

## Layers touched
- **Backend — AppServices only:** `src/Cleansia.Core.AppServices/Features/SavedAddresses/GetSavedAddresses.cs` (inject `ILogger<GetSavedAddresses.Handler>`; replace the silent `.Where(s => s.Address != null)` with a partition that logs each skipped orphan at Warning, then projects the survivors).
- **Tests:** `src/Cleansia.Tests` — handler unit tests covering the healthy path (AC-1/5), single orphan (AC-2/3), and multiple orphans (AC-4), asserting on logger invocations and that no address PII is logged (TDD per conventions.md).
- **No DTO/contract change** → no `manual_step: nswag-regen`. **No schema change** in this story → no `manual_step: ef-migration` (the invariant-enforcement migration is the separate out-of-scope ADR).
- Not security-touching in the S1–S3 sense (read path already derives `userId` from `IUserSessionProvider`, GetSavedAddresses.cs:19); the only security rule in play is **S6** (PII-safe logging), which AC-2/6 enforce.


---

Everything is confirmed precisely. Key grounding facts for the story:

- **Admin facade** (`employee-documents.facade.ts`) has only `loadEmployeeDocuments`, `approveDocument`, `rejectDocument`, `downloadDocument`, `previewDocument` — no upload, new-version, or delete methods. It only calls `getPaged`, `approve`, `reject`, `download` on `adminEmployeeDocumentClient`.
- **Admin controller** has only get-paged/approve/reject/versions/download — no POST upload, no PUT/POST new-version, no DELETE.
- **Permissions already exist** and are admin-capable: `CanUploadEmployeeDocument` and `CanDeleteEmployeeDocument` both map to `PhysicalPolicy.EmployeeOrAdmin` (PolicyBuilder.cs:66,70). So admins are *already authorized* — the endpoints just don't exist. No new policy needed.
- **The blob inconsistency is real and load-bearing**: `SaveMyDocuments.DocumentToSave` carries `BlobFileDto File` (base64) and the handler uploads to blob storage (lines 109-136, container `Constants.BlobContainers.EmployeeDocuments`). `UploadEmployeeDocument.Command.FilePath` (line 19) and `UploadNewDocumentVersion.Command.FilePath` (line 18) are pre-uploaded paths with no producer — wiring them as-is persists DB rows pointing at non-existent blobs.

Now I have enough to write the grounded user story.

---

# US-admin-0007 — Admin upload, replace, and delete of employee documents

## Summary
Give Admin operators write access to an employee's document set (upload-on-behalf, upload-new-version, delete) from the employee detail screen, closing the gap where back-office document management is currently read-only (view / approve / reject / download) despite three fully-built but unwired command handlers.

## Actor narrative
**As an** Admin operator vetting and onboarding a cleaner,
**I want** to upload a document on the employee's behalf, replace a document with a corrected new version, and delete a wrong or duplicate file directly from the employee detail screen,
**so that** I can complete a cleaner's compliance file (e.g. attach a signed contract, supply a missing work permit) and keep it clean without asking the cleaner to re-upload through the partner app.

## Grounding (confirmed in code, audit-only)
- `Cleansia.Web.Admin/Controllers/AdminEmployeeDocumentController.cs` exposes only `get-paged`, `{id}/approve`, `{id}/reject`, `{id}/versions`, `{id}/download`. There is **no** upload, new-version, or delete endpoint (lines 17-96).
- Three handlers exist and are wired nowhere: `EmployeeDocuments/UploadEmployeeDocument.cs`, `UploadNewDocumentVersion.cs`, `DeleteDocument.cs`. A grep for these three across all `*Controller*.cs` returns nothing; only the partner-self `SaveMyDocuments` / `DeleteMyDocument` are wired (`Cleansia.Web.Partner/Controllers/EmployeeController.cs:51,73` and the mobile-partner controller).
- The admin facade `employee-documents.facade.ts` has only load / approve / reject / download / preview methods — no upload, new-version, or delete, and never calls a write client method beyond approve/reject.
- **Authorization already permits admins**: `Policy.CanUploadEmployeeDocument` and `Policy.CanDeleteEmployeeDocument` both map to `PhysicalPolicy.EmployeeOrAdmin` (`Authentication/PolicyBuilder.cs:66,70`; mirrored in `libs/core/services/.../policy.ts`). No new permission is required.
- **Blob-contract inconsistency (must be resolved in this story):** the partner path `SaveMyDocuments.DocumentToSave` carries a `BlobFileDto File` (base64) and the handler uploads to blob storage itself (`SaveMyDocuments.cs:109-136`, container `Constants.BlobContainers.EmployeeDocuments`, path `Constants.VirtualDirectories.EmployeeDocuments`). By contrast `UploadEmployeeDocument.Command.FilePath` (line 19) and `UploadNewDocumentVersion.Command.FilePath` (line 18) expect a **pre-uploaded** path that no caller produces. Wiring them unchanged would persist a DB document row pointing at a non-existent blob.

## Acceptance criteria

**AC1 — Admin uploads a document on behalf of an employee**
**Given** an authenticated Admin with `CanUploadEmployeeDocument` viewing an employee's Documents tab,
**When** they pick a file (PDF/JPG/PNG/DOC/DOCX, ≤ 10 MB) and a `DocumentType`, optionally a description, and confirm,
**Then** the file is uploaded to the `EmployeeDocuments` blob container under that employee's virtual directory, a new `EmployeeDocument` row (Version 1, status Pending) is persisted for that employee with the stored blob path, and the document appears in the tab without a page reload.

**AC2 — Admin upload reuses the same blob-backed contract as the partner path**
**Given** the upload feature is implemented,
**When** the admin upload command is sent,
**Then** it carries the file content as a `BlobFileDto` (base64) consistent with `SaveMyDocuments`, and the handler performs the blob upload itself — i.e. the previously dangling `FilePath`-only contract is not exposed to any caller, so no document row is ever created pointing at a blob that was never written.

**AC3 — Admin replaces a document with a new version**
**Given** an Admin viewing an existing document for an employee,
**When** they upload a replacement file against that document,
**Then** a new `EmployeeDocument` version is created via `CreateNewVersion` (version incremented, document type inherited from the previous version), the previous version is retained in version history, and the latest-version view shows the new file.

**AC4 — Admin deletes a document**
**Given** an Admin with `CanDeleteEmployeeDocument` viewing a document,
**When** they confirm deletion,
**Then** the document is soft-deleted (`SoftDelete` with the acting admin's user id) and disappears from the active/latest-version list; the admin delete path applies **no** owner/ownership or not-approved guard (unlike `DeleteMyDocument`), so an admin can remove an approved or any-owner document.

**AC5 — Invalid uploads are rejected with localized errors**
**Given** an Admin attempts to upload a file that is empty, over 10 MB, or of a disallowed content type, or targets a non-existent employee/document,
**When** they submit,
**Then** the request fails validation and the operator sees the corresponding localized error (`errors.*` for `FileSizeExceeded10MB`, `FileTypeNotAllowed`, `Required`, `NotFound`) in all 5 locales, and no blob is written and no document row is created.

**AC6 — Only authorized roles can perform writes**
**Given** a caller without the relevant document-write permission,
**When** they call the admin upload / new-version / delete endpoint,
**Then** the API returns 403 Forbidden, matching the existing `[Permission(...)]` gating pattern on the controller.

## Out of scope
- Approve / reject workflow and the approval state machine (already implemented — view/approve/reject/download stay as-is).
- Partner-self document flows (`SaveMyDocuments` / `DeleteMyDocument`) on partner web and mobile-partner APIs — unchanged.
- Introducing any new permission/policy (existing `CanUploadEmployeeDocument` / `CanDeleteEmployeeDocument` already cover admin); no role-matrix changes.
- Bulk/multi-file admin upload in a single action (single-document upload is sufficient for this story).
- Hard delete or physical blob deletion/cleanup (deletion is soft-delete only, consistent with existing behaviour; blob lifecycle cleanup is a separate concern handled by Functions).
- Document expiry, reminders, e-signature, or notifying the employee that an admin changed their documents.
- Customer-facing or partner-facing UI changes.
- EF Core migration authoring and NSwag client regeneration (owner-only manual steps — see below).

## Layers touched
- **Backend — API (`Cleansia.Web.Admin`)**: add upload, upload-new-version, and delete actions to `AdminEmployeeDocumentController`, gated by `CanUploadEmployeeDocument` / `CanDeleteEmployeeDocument`.
- **Backend — AppServices (`Cleansia.Core.AppServices/Features/EmployeeDocuments`)**: reconcile `UploadEmployeeDocument` / `UploadNewDocumentVersion` commands to a `BlobFileDto`-based, blob-uploading contract (mirroring `SaveMyDocuments`); reuse `DeleteDocument`. Validators/handlers already largely present.
- **Backend — Infra (`Cleansia.Infra.Services` / blob)**: reuse the existing `IBlobContainerClientFactory` + `EmployeeDocuments` container/virtual-directory plumbing used by `SaveMyDocuments`.
- **Frontend — Admin (`libs/cleansia-admin-features/employee-management/.../employee-detail`)**: add upload / replace-version / delete methods to `EmployeeDocumentsFacade`, an upload dialog/UI on the Documents tab using `<cleansia-*>` / PrimeNG controls, and a delete confirmation; refresh the signal list after each action.
- **Frontend — i18n**: new `errors.*` keys (if any new ones) and `pages.employee_detail.*` labels/messages across `en/cs/sk/uk/ru`.
- **No new domain entity or enum** — `EmployeeDocument`, `DocumentType`, `DocumentStatus`, `Version`, `SoftDelete`, `CreateNewVersion` already exist.

## Manual steps (owner-only — do NOT perform in implementation)
- `manual_step: nswag-regen` — admin client must be regenerated after the new admin endpoints/DTOs land (`adminEmployeeDocumentClient` gains upload/new-version/delete).
- `manual_step: ef-migration` — **likely none**; reconciling the upload command to `BlobFileDto` is an app-layer DTO change, not a schema change (the `EmployeeDocument` entity is unchanged). Confirm during implementation; if any column is added, flag a migration.

## Open question for the architect (flagged, not resolved here)
Confirm the chosen reconciliation for AC2: either (a) change `UploadEmployeeDocument` / `UploadNewDocumentVersion` to accept a `BlobFileDto` and upload in-handler like `SaveMyDocuments`, or (b) have the admin endpoints route through a `SaveMyDocuments`-style handler parameterized by a target `EmployeeId`. Option (a) keeps the three existing handlers; option (b) reduces duplicated blob-upload logic. Either way, the dangling `FilePath`-only contract must not reach a caller.


---

I have everything grounded. The "No dead code" convention (conventions.md:60) directly supports deletion, and security-rules.md is the relevant audit harness. I'll write the story as an admin-persona story (the actor who configures email templates / who is the stakeholder in trustworthy email rendering and a clean codebase — and the SendGrid template audit is an admin/owner concern).

---

```markdown
---
id: US-admin-0001
title: Remove the unused HTML template engine and close the email XSS surface
persona: admin
created: 2026-06-01
status: draft
related_adrs: []
related_tickets: []
---

## Narrative
As an **admin** (platform owner responsible for the codebase and for the emails Cleansia sends to
customers and partners), I want **the orphaned `HandlebarsTemplateEngine` removed and our SendGrid
email templates verified to HTML-escape customer-controlled data**, so that **a future developer
cannot inherit an unescaped-output rendering helper and so that a customer's name or address can
never inject markup/script into an email body (stored/reflected XSS)**.

## Context (grounded in code)
- `src/Cleansia.Infra.Services/Templates/HandlebarsTemplateEngine.cs` is registered as the
  `ITemplateEngine` implementation at `src/Cleansia.Infra.Services/ServiceCollectionExtensions.cs:13`
  (`services.AddScoped<ITemplateEngine, HandlebarsTemplateEngine>();`).
- It has **zero consumers**: a repo-wide search for `ITemplateEngine` / `CompileAsync` /
  `HandlebarsTemplateEngine` returns only the interface (`ITemplateEngine.cs:3`), the implementation,
  and that one DI line. Nothing injects it.
- The **real** email path is `Cleansia.Core.AppServices/Services/EmailService.cs`, which calls
  `MailHelper.CreateSingleTemplateEmail(..., templateId, model)` against **SendGrid hosted dynamic
  templates** (`SendTemplatedAsync`, line 349). Rendering happens in SendGrid's cloud, not in this
  repo — confirming the engine is dead code.
- Every helper in `HandlebarsTemplateEngine` (`formatCurrency`, `formatDate`, `formatDateTime`,
  `formatNumber`, `eq`, `add`, `multiply`) emits via `writer.WriteSafeString(...)`, which **bypasses
  Handlebars HTML-encoding**. `formatNumber`/`eq`/`add`/`multiply` write results with no encoding at
  all (lines 86, 93, 106, 119), and the engine uses `Handlebars.Create()` defaults (line 12). If a
  future template author routes a user-controlled string through these helpers (or uses `{{{triple}}}`),
  it is an HTML-email XSS sink.
- The actual production XSS surface is the **SendGrid hosted templates** (out of repo): `EmailService`
  passes raw `CustomerName`, `Address`, `EmployeeName` as merge data (e.g. lines 91, 127, 189, 314).
  If any hosted template wraps those in `{{{...}}}`, the user-supplied value is injected unescaped.
  This needs an out-of-band check of the SendGrid templates.
- Cited rules: `agents/knowledge/conventions.md:60` ("**No dead code.** Delete unreferenced
  methods/classes…") directly supports removal; `agents/knowledge/security-rules.md` is the audit
  harness this gap is graded against (no existing S-rule covers email-template escaping, so this story
  also asks to record the "no `{{{ }}}` on user data" rule).

## Acceptance criteria
- **AC1** — Given a repo-wide search for `ITemplateEngine`, `HandlebarsTemplateEngine`, and
  `CompileAsync`, When the change is complete, Then the interface, the implementation, the
  `HandlebarsDotNet` package reference, and the `AddScoped<ITemplateEngine, HandlebarsTemplateEngine>()`
  line at `ServiceCollectionExtensions.cs:13` are all gone, and the solution still builds and all
  existing tests pass.
- **AC2** — Given the registration is removed, When the four APIs (Partner :5000, Admin :5001,
  Mobile :5002, Customer :5003) and the Azure Functions host start, Then there is no DI resolution
  error and all real email flows in `EmailService` (reset-password, order-receipt, confirmation,
  status-update, period-closed/reminder) continue to send via SendGrid unchanged.
- **AC3** — Given the SendGrid hosted dynamic templates referenced by `EmailService` (OrderReceipt,
  OrderStatusUpdate, ResetPassword, ConfirmationEmail, PeriodClosed, PeriodEndReminder), When each is
  reviewed out-of-band, Then no customer-controlled merge field — at minimum `CustomerName`,
  `Address`, `EmployeeName`, `OrderNumber`, `UserName` — is rendered with triple-stache `{{{ }}}`,
  and any that is gets switched to double-stache `{{ }}`; the audit result is recorded in
  `agents/backlog/audits/`.
- **AC4** — Given a customer whose saved name or address contains HTML/script characters (e.g.
  `<img src=x onerror=alert(1)>`), When an order-receipt or order-status-update email is generated for
  them, Then the rendered email body shows that value as inert literal text (HTML-escaped), with no
  executable markup.
- **AC5** — Given the security knowledge base, When this story is delivered, Then a rule is recorded
  (in `agents/knowledge/security-rules.md`, e.g. as S11 or an addendum to S4/S6) stating "email/HTML
  template output must be HTML-encoded; never place user-controlled data inside `{{{ }}}` or behind a
  `WriteSafeString` helper", so future template work is graded against it.

## Out of scope
- Building any new in-repo HTML email/document rendering capability. (If the engine were *kept* instead
  of deleted — non-preferred — its hardening, `Configuration { NoEscape = false }`, and
  encode-only-in-every-helper work would be a **separate** story; this story takes the delete path.)
- Changing the *content, layout, copy, or styling* of any SendGrid template — only the escaping of
  customer-controlled merge fields is in scope.
- Migrating email rendering off SendGrid hosted templates into the repo.
- The QuestPDF receipt/invoice rendering path (`Cleansia.Infra.Services/Pdf/*`) — it does not use
  `ITemplateEngine` and is unaffected.
- Input-side sanitization/validation of `CustomerName` / `Address` at order-creation time (defense in
  depth, but a distinct change).
- Frontend, Android, and the email-template-management admin UI
  (`libs/cleansia-admin-features/template-management/*`) — that feature edits the **translation
  strings** stored in DB, not the engine being removed; no client contract changes.

## Layers touched
- **Backend / Infrastructure** — delete `Cleansia.Infra.Services/Templates/{HandlebarsTemplateEngine,
  ITemplateEngine}.cs`; remove the DI line in `ServiceCollectionExtensions.cs`; drop the now-unused
  `HandlebarsDotNet` package reference (`src/Directory.Packages.props` + the `Cleansia.Infra.Services`
  csproj).
- **External config (out of repo, owner/admin task)** — review and, where needed, edit the SendGrid
  hosted dynamic templates to use double-stache for customer-controlled fields.
- **Knowledge base / process** — record the audit result under `agents/backlog/audits/` and add the
  template-escaping rule to `agents/knowledge/security-rules.md`.
- **No** changes to: frontend apps, Android apps, NSwag clients, DB schema/migrations, or API
  contracts (so no `ef-migration` / `nswag-regen` manual step is required).

## Open questions
- **Q (default assumption taken):** Delete vs. harden-and-keep. Default taken here = **delete**, per
  the "No dead code" convention (`conventions.md:60`) and the confirmed zero consumers. If the owner
  has a near-term plan to render HTML emails/documents in-repo, flip to the hardening path and split
  AC1/AC2 into a separate "harden `HandlebarsTemplateEngine`" story.
- **Q:** Do the SendGrid hosted templates currently use `{{{ }}}` anywhere? Cannot be determined from
  this repo (templates live in the SendGrid account); AC3 requires owner access to confirm.
```


---

Confirmed: no test exists for `PayCalculator` at all, so the stub was never flagged. I now have everything grounded. Here is the user story.

---

# US-admin-0007 — Country-aware holiday calendar so peak-time holiday bonuses actually pay

## Context / grounding (audit evidence — do not change code)

- `PayCalculator.IsHoliday(DateTime)` is a stub that unconditionally `return false` with a `// TODO: Implement actual holiday check based on country` — `src/Cleansia.Core.Domain/EmployeePayroll/Services/PayCalculator.cs:262-266`. The TODO itself violates `agents/knowledge/conventions.md:62` ("No `// TODO(JIRA-x)` — use a ticket").
- The only consumer is `CalculatePeakTimeBonus(Order order, decimal peakRate)` — `PayCalculator.cs:154-165` — which computes `isPeakTime = IsWeekend(...) || IsHoliday(...) || IsEveningTime(...)`. Because `IsHoliday` is hardwired `false`, the holiday branch of the peak bonus can never fire: a holiday-only cleaning (a weekday public holiday, daytime hours) silently pays **zero** peak bonus.
- Wider finding (in scope to note, not necessarily to fix here): `CalculatePeakTimeBonus` is **not referenced anywhere** in the codebase (grep across the repo finds only its definition and the `peakRate`/`isPeakTime` usage inside it). `OrderEmployeePay.BonusPay` (`OrderEmployeePay.cs:29`) is currently set only via manual `AddBonus(...)` / `UpdatePay(...)` paths, never from peak-time calculation. So today the holiday bonus is doubly dead: the calendar is stubbed **and** the calculator is unwired. A correct fix must connect the calculator to the pay flow, not just fill the calendar.
- The codebase already models holidays as an injected set elsewhere: `PayPeriodService.CalculateWorkingDays(PayPeriod period, IEnumerable<DateOnly>? holidays = null)` — `PayPeriodService.cs:84-99` — so a country-keyed holiday source is the consistent design (`conventions.md:16-36`, "reuse the real types / one way to do each thing").
- Country context is available: `Order.CleaningDateTime` (`Order.cs:39`) and `Order.CustomerAddress` → `Address` (with country) (`Order.cs:28`); the platform is explicitly multi-country (CZ/SK/UA/RU/DE/PL per root `CLAUDE.md`).
- No test covers `PayCalculator` at all (no match in `src/Cleansia.Tests`), so the stub was never caught — directly contradicting the TDD bar for pure pricing/pay logic (`conventions.md:123-125`).

Persona is **admin** because the holiday calendar is platform/tenant configuration that an operations administrator owns and the resulting bonus is an admin-governed payroll outcome.

## Actor narrative

> **As an** operations administrator (payroll owner)
> **I want** the pay engine to recognise public holidays for the order's country when it computes the peak-time bonus, driven by a maintainable holiday calendar rather than a hardcoded `false`,
> **so that** partners who clean on a public holiday are correctly paid the holiday peak-time bonus, and payroll figures are accurate and auditable instead of silently underpaying.

## Acceptance criteria (Given / When / Then — observable outcomes)

1. **Holiday on a non-weekend, daytime order pays the bonus**
   **Given** a configured public holiday for the order's country (e.g. CZ 2026-07-05, a Sunday-avoiding example — use a weekday holiday such as CZ 2026-05-01) that falls on a weekday at a daytime hour
   **When** the peak-time bonus is calculated for an order with `CleaningDateTime` on that holiday
   **Then** the returned bonus equals the configured `peakRate` (not 0), and `IsHoliday` no longer returns a hardcoded `false`.

2. **Country isolation**
   **Given** the same calendar date is a public holiday in country A but a normal working weekday in country B
   **When** the bonus is calculated for two otherwise-identical orders whose addresses resolve to A and B respectively
   **Then** the order in country A receives the holiday peak bonus and the order in country B receives 0 (assuming it is not also weekend/evening).

3. **Non-holiday weekday daytime pays nothing (no false positives)**
   **Given** an order whose `CleaningDateTime` is an ordinary weekday, daytime, and not a configured holiday
   **When** the peak-time bonus is calculated
   **Then** the holiday contribution is 0 and the existing weekend/evening behaviour (`IsWeekend`, `IsEveningTime` at `PayCalculator.cs:252-260`) is unchanged.

4. **Holiday bonus reaches the partner's actual pay**
   **Given** an order cleaned on a configured public holiday in its country
   **When** that order's `OrderEmployeePay` is produced/recalculated through the real pay flow
   **Then** the holiday peak amount is included in `BonusPay` and therefore in `TotalPay` (`OrderEmployeePay.cs:118`) and in the period/invoice aggregation (`AggregatePeriodBreakdown` → `GenerateInvoice` `bonusAmount`, `GenerateInvoice.cs:79`) — i.e. the calculator is wired into the pay pipeline, not left orphaned.

5. **Calendar is configuration, not hardcoded literals**
   **Given** the set of public holidays per country
   **When** a holiday is added, removed, or a new year is configured
   **Then** it is changed through the holiday-calendar data source (no recompile, no magic-date literals inline — per `conventions.md:56`), and the change takes effect for subsequent pay calculations.

6. **Regression test exists for the holiday path**
   **Given** the TDD bar for pure pay logic (`conventions.md:123-125`)
   **When** the suite runs
   **Then** there is a `PayCalculator` test that fails against the old stub and passes with the real calendar, asserting the holiday, country-isolation, and no-false-positive cases above.

## Out of scope (explicit)

- Configuring the *value* of `peakRate` / the holiday surcharge percentage, and any per-employee `EmployeePayConfig` override of it (that is the existing IMP-3 / pay-config work).
- A full admin UI screen for CRUD-managing the holiday calendar, unless a minimal seed/config source is insufficient to satisfy AC-5 — a seeded per-country holiday dataset is acceptable for this story; a rich management screen is a follow-up.
- Region/state-level or company-observed/floating holidays (e.g. US state holidays, Easter-relative computations beyond the supported launch countries). Launch countries only (CZ/SK/UA/RU/DE/PL).
- Time-zone normalisation of `CleaningDateTime` beyond what is already used by the calculator; if a TZ ambiguity is discovered it is raised as a separate finding, not fixed here.
- Retroactive recalculation/back-pay of already-finalised invoices for past holidays.
- Weekend and evening peak logic (already implemented and explicitly preserved unchanged).
- Removing/cleaning up other unrelated dead methods in `PayCalculator` surfaced during the audit (separate audit items).

## Layers touched

- **Backend — Domain** (primary): `Cleansia.Core.Domain/EmployeePayroll/Services/PayCalculator.cs` — implement `IsHoliday` against a country-keyed holiday source; keep `CalculatePeakTimeBonus` as the single entry point.
- **Backend — Domain/Infra**: a holiday-calendar abstraction + per-country holiday data (consistent with the existing `IEnumerable<DateOnly> holidays` shape in `PayPeriodService.CalculateWorkingDays`). New data → likely a seed/config and possibly an EF table.
- **Backend — AppServices** (wiring): connect `CalculatePeakTimeBonus` into the pay-build path that sets `OrderEmployeePay.BonusPay` (today unwired) so the bonus flows to `GenerateInvoice` aggregation.
- **Tests**: `Cleansia.Tests` — new `PayCalculator` holiday/peak-time unit tests (none exist today).
- **Manual steps to flag (owner-only):** `manual_step: ef-migration` if the holiday calendar is persisted as a new table; `manual_step: db-seed` if holidays are seeded via `sql-scripts`. **No** NSwag/frontend/mobile change is required unless an admin management endpoint/DTO is added (out of scope above), in which case flag `manual_step: nswag-regen`.


---

All findings are confirmed and grounded in real code and real rules:

- `GetPagedPackages.cs:36` calls `.Select(package => package.MapToDto())` — the instance mapper, not an Expression.
- `PackageMappers.cs:15-17` reads `package.IncludedServices` (computed property `Package.cs:24` returning `_includedServices.ToList().AsReadOnly()`) and `ps.Service.Translations` (JSON value-converted).
- `BaseRepository.GetPagedSort` → `FilterData` (BaseRepository.cs:160) does **no** `Include`; only `PackageRepository.GetByIdAsync` (lines 9-15) includes `IncludedServices.ThenInclude(Service)`.
- No `UseLazyLoadingProxies` anywhere → `IncludedServices` materializes empty on the paged path.
- Rule **A6** (consistency.md:33) mandates `.Include(...) → .AsNoTracking() → .Select(x => x.MapToDto())`; this handler omits the `Include`.
- Admin list (`package-management.models.ts:14-44`) renders only name / description / price — `IncludedServices` is never consumed in the list.

Here is the user story.

---

# US-admin-0042 — Fix paged package list returning empty included-services and over-fetching

## Actor narrative

**As an** admin managing the service catalog,
**I want** the paged package list to return correct, lean data efficiently,
**so that** the package-management table loads quickly and any field I rely on (now or later) reflects the real package contents instead of a silently-empty array.

## Context / grounding (why this story exists)

The paged-packages read path is functionally broken and inefficient:

- `Features/Packages/GetPagedPackages.cs:33-37` runs `.GetPagedSort<PackageSort>(...).AsNoTracking().Select(package => package.MapToDto()).ToListAsync(...)`.
- `Mappers/PackageMappers.cs:7-18` `MapToDto` is a **compiled instance method** (not an `Expression`) that reads `package.IncludedServices` — a computed property at `Packages/Package.cs:24` (`_includedServices.ToList().AsReadOnly()`) — plus `ps.Service.Translations` (a JSON value-converted dictionary). EF Core 10 cannot translate this projection, so it **materializes full `Package` rows and runs the mapper in memory**.
- The paged query comes from `BaseRepository.GetPagedSort` → `FilterData` (`BaseRepository.cs:60-71, 160-169`), which performs **no `Include`**. The only place that includes `IncludedServices.ThenInclude(Service)` is `PackageRepository.GetByIdAsync` (`Repositories/PackageRepository.cs:9-15`).
- Lazy-loading proxies are **not registered** anywhere in the solution (no `UseLazyLoadingProxies`).
- **Net effect:** for every paged-packages call, `IncludedServices` comes back **empty** (silent wrong data — `PackageListItem.IncludedServices` always serializes `[]`), while EF over-fetches **all** `Package` columns instead of the ~4 the list needs.
- This violates rule **A6** (`agents/knowledge/consistency.md:33`, mirrored at `patterns-backend.md:246`): the read path must be `.Include(...) → .AsNoTracking() → .Select(x => x.MapToDto())`; this handler omits the `Include`.
- The admin list UI (`package-management.models.ts:13-44`) renders **only** `name`, `description`, `price` — it never consumes `IncludedServices`, confirming the field is dead weight in this list DTO.

## Acceptance criteria

1. **Given** an admin requests the paged package list and packages have included services,
   **When** the response is returned,
   **Then** no package row contains a silently-empty `IncludedServices` that contradicts the package's actual included services (the previously-observed always-`[]` behavior no longer occurs).

2. **Given** the paged-packages endpoint executes,
   **When** the underlying database query runs,
   **Then** the projection is translated to a single SQL query that selects only the columns the list DTO needs (no full-entity over-fetch and no client-side evaluation of the mapper), consistent with rule A6.

3. **Given** the trimmed list DTO,
   **When** the admin package-management table renders,
   **Then** the name, description, and price columns (`package-management.models.ts`) display exactly as before, with no regression in sorting (`name`, `price`) or the description truncation behavior.

4. **Given** the paged list no longer needs per-service data,
   **When** `PackageListItem` is reviewed,
   **Then** it no longer carries an unused `IncludedServices` collection (the list contract is reduced to what the list actually consumes), and `PackageServiceSummary` is removed if it has no other consumer.

5. **Given** an admin opens a single package's detail or overview,
   **When** that detail/overview is loaded,
   **Then** its included services still load correctly via the existing `GetByIdAsync`/detail path (this story must not regress `GetPackageById`, `GetPackageOverview`, or `AdminPackageDetailDto`).

6. **Given** the package list is requested at any page/offset,
   **When** results are sorted via `PackageSort` and filtered via `PackageSpecification`,
   **Then** ordering, paging totals (`GetCountAsync`), and search-term filtering behave identically to before.

## Out of scope

- Changing the **package detail / overview** read paths (`GetPackageById.cs`, `GetPackageOverview.cs`) or their DTOs (`PackageDetails`, `AdminPackageDetailDto`) — those legitimately include services and stay as-is.
- Introducing lazy-loading proxies (`UseLazyLoadingProxies`) — explicitly **not** the chosen remedy.
- Refactoring the generic `BaseRepository.GetPagedSort` / `FilterData` signature or behavior for other entities.
- Auditing or "fixing" the same instance-mapper-in-`Select` pattern in **other** features (e.g. Services) — track separately if found.
- Any frontend feature change beyond the contract trim: no new columns, no display of included services in the admin package **list**.
- NSwag client regeneration is **owner-only** — this story must flag `manual_step: nswag-regen` (the `PackageListItem` shape changes) but must not perform it.
- EF Core migrations — none expected (no schema change); if one is somehow needed, flag `MANUAL_STEP` and do not run it.

## Layers touched

- **Backend — AppServices:** `GetPagedPackages.cs` handler (translatable projection), `DTOs/PackageListItem.cs` (trim `IncludedServices`, possibly remove `PackageServiceSummary`), `Mappers/PackageMappers.cs` (stop using the instance `MapToDto` inside `IQueryable.Select`, or split a list-specific projection). *Choose remedy (b): a dedicated EF-translatable DTO projection over remedy (a) over-fetch, since the list does not need services.*
- **Backend — Infra.Database:** none expected (no new repo `Include` if the DTO drops services); only revisit `PackageRepository` if remedy (a) were chosen.
- **Frontend — admin:** consumes regenerated `PackageListItem` from `@cleansia/admin-services`; verify `package-management.models.ts` / facade still compile against the trimmed type. No UI behavior change.
- **Contracts:** OpenAPI/NSwag for the admin API changes → `manual_step: nswag-regen` (owner).
- **Tests:** xUnit coverage for `GetPagedPackages` asserting a translatable, non-over-fetching projection and correct list fields.


---

I now have everything I need, grounded in the code. Key confirmed facts:

- `CleansiaStartupBase.cs:76-92` — two `AddFixedWindowLimiter("auth"/"interactive", ...)` named limiters, no partitioning. The "auth" bucket is `PermitLimit = 10` global.
- `[EnableRateLimiting("auth")]` is class-level on `AuthController` in all 5 hosts (Customer, Admin, Partner, Mobile.Partner, Mobile.Customer) plus side-effecting methods (Payment, Gdpr, Promo, Referral, Order).
- No `ForwardedHeaders` middleware anywhere — `RemoteIpAddress` is used raw in `RequestLoggingMiddleware` and `RequestMetadataProvider`, so behind the proxy it resolves to the proxy IP. A per-IP fix must include `UseForwardedHeaders` or the partition collapses to one (proxy) key.
- `RequestMetadataProvider.cs:12` already reads `RemoteIpAddress` for audit — same forwarded-headers caveat applies and is a reuse anchor.
- Maps to security law **S5** (rate limiting on auth/side-effecting endpoints), which the GAP currently violates in spirit.

Here is the user story.

---

# US-partner-0042 — Per-IP / per-account auth rate limiting (replace the single global bucket)

**Type:** Security / Hardening
**Security-touching:** yes (S5 — rate limiting on auth + side-effecting endpoints; relates to S6 logging hygiene for the partition key)
**Persona:** partner (the partner-host login is the highest-value brute-force target; the fix is platform-wide across all five hosts)
**Priority:** High
**Layer(s) touched:** Backend only — `Cleansia.Config` (shared startup), reverse-proxy/forwarded-headers config, `Cleansia.Tests`/`Cleansia.IntegrationTests`. No frontend, no mobile, no DB, no DTO/NSwag changes.

## Context (grounded in code)

`src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs:76-92` registers the rate limiter as two **named fixed-window limiters** with no partition key:

```csharp
options.AddFixedWindowLimiter("auth", o => { o.PermitLimit = 10; o.Window = TimeSpan.FromMinutes(1); o.QueueLimit = 0; });
options.AddFixedWindowLimiter("interactive", o => { o.PermitLimit = 60; o.Window = TimeSpan.FromMinutes(1); o.QueueLimit = 0; });
```

A named limiter with no partition is **one global bucket** for every caller. The inline comment "10/min — brute-force defense" is misleading: there is no per-IP or per-account isolation. Consequences, both real:

1. **Brute-force / credential-stuffing under-throttled.** Distributed guessing of passwords or 6-digit email-confirmation / password-reset codes is only limited against the shared global pool, not per attacker and not per targeted account.
2. **Trivial global DoS / login lockout.** A single client can spend all 10 permits/min on `/Login`, locking out *every* legitimate login, register, password-reset, refresh, confirm-email request across the whole API for the rest of each window.

The `auth` policy is the platform-wide auth gate: `[EnableRateLimiting("auth")]` is applied **class-level on `AuthController` in all five hosts** (`Cleansia.Web.Customer`, `Cleansia.Web.Admin`, `Cleansia.Web.Partner`, `Cleansia.Web.Mobile.Partner`, `Cleansia.Web.Mobile.Customer`) and method-level on side-effecting endpoints (`PaymentController`, `GdprController`, `PromoCodeController`, `ReferralController`, customer `OrderController`). One global bucket therefore couples five independent hosts and dozens of endpoints.

**Proxy caveat (must be handled or the fix is a no-op):** there is **no `UseForwardedHeaders` / `ForwardedHeaders` configuration anywhere** in the solution. Behind the reverse proxy, `HttpContext.Connection.RemoteIpAddress` resolves to the *proxy* IP, so partitioning on it without forwarded-headers handling collapses all clients back into a single partition. The same raw `RemoteIpAddress` read already exists at `RequestMetadataProvider.cs:12` and `*/Middleware/RequestLoggingMiddleware.cs` — those are the reuse anchors and share the caveat.

This is the gap S5 was written to close.

## Story

**As a** partner whose login endpoint is a public credential gate,
**I want** the auth rate limiter to throttle each client/account independently (per source IP for anonymous auth routes, per user id for authenticated mutations) instead of one shared global bucket,
**so that** an attacker can't grind down my password or reset codes within the global pool, and no single client can exhaust the global limit and lock every legitimate user out of login/register/reset across the whole platform.

## Acceptance Criteria

1. **Per-IP isolation on anonymous auth routes**
   **Given** the `auth` policy is partitioned by client IP
   **When** client A (IP 10.0.0.1) sends 10 `POST /Auth/Login` requests in one window and is throttled (429), **and** client B (IP 10.0.0.2) then sends its first `POST /Auth/Login` in the same window
   **Then** client A receives `429 TooManyRequests` and client B receives a normal (non-429) response — A's exhaustion does not consume B's allowance.

2. **No cross-host / cross-client global lockout (DoS regression closed)**
   **Given** one client floods `auth`-tagged endpoints on one host
   **When** an unrelated legitimate user on the same or another host hits a different `auth`-tagged endpoint within the same window
   **Then** the legitimate user is **not** rejected solely because of the flooder — there is no single bucket whose exhaustion blocks all callers platform-wide.

3. **Per-account partitioning on authenticated side-effecting mutations**
   **Given** an authenticated user calling an `auth`-tagged authenticated endpoint (e.g. GDPR export, payment, promo)
   **When** that user exhausts their per-user window
   **Then** only that user (their user id partition) is throttled; a different authenticated user is unaffected.

4. **Forwarded-headers correctness behind the proxy**
   **Given** the API runs behind the reverse proxy with no direct client connections
   **When** two real clients arrive via the proxy with distinct `X-Forwarded-For` client IPs (from the trusted proxy only)
   **Then** they are placed in **distinct** partitions (the partition key is the real client IP, not the shared proxy IP), and a spoofed `X-Forwarded-For` from an untrusted hop is **not** honored.

5. **Limits and reject behavior preserved**
   **Given** the migration from named limiters to a partitioned policy
   **When** any previously `[EnableRateLimiting("auth")]` or `[EnableRateLimiting("interactive")]` endpoint is called
   **Then** the per-partition limit stays 10/min (auth) and 60/min (interactive), `QueueLimit` stays 0, and the rejection status stays `429`, with no controller attribute changes required.

6. **Regression test exists and is meaningful**
   **Given** an integration test asserting partition isolation
   **When** the test simulates client A's 429s and then client B's first request
   **Then** B is not affected by A (AC-1 codified); the test fails against the current single-bucket implementation and passes after the fix.

## Out of scope

- Changing the *numeric* limits (10/min, 60/min) or window length — values are preserved; only the partitioning changes.
- Account-lockout-after-N-failures, CAPTCHA, exponential backoff, or any auth-attempt tracking persisted to the DB (those are separate hardening stories).
- Distributed / cross-instance rate-limit state (Redis-backed limiter). In-process per-partition limiter is acceptable for this story; flag a follow-up if multi-instance scale-out invalidates it.
- Auditing or re-tiering *which* endpoints carry `auth` vs `interactive` (e.g. whether order-create deserves its own per-user limit) — keep the current attribute placements.
- Any frontend/mobile change; any NSwag regeneration; any DB migration. (None required — confirm none introduced.)
- Re-securing the raw `RemoteIpAddress` reads in `RequestLoggingMiddleware` / `RequestMetadataProvider` for *logging* purposes beyond what AC-4's forwarded-headers setup naturally fixes.

## Layers touched

- **Backend (shared config):** `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs` — replace the two `AddFixedWindowLimiter(name, …)` calls with `options.AddPolicy(name, httpContext => RateLimitPartition.GetFixedWindowLimiter(partitionKey, …))`; key anonymous auth routes on real client IP (and optionally IP+email for `/Login`), authenticated mutations on user id. Add `app.UseForwardedHeaders(...)` with trusted-proxy/known-network config ahead of `UseRateLimiter()` in `Configure(...)`. Applies to all five hosts via the shared base — no per-host edits expected.
- **Infra config:** forwarded-headers `KnownProxies`/`KnownNetworks` (env-driven; no secrets in `appsettings`).
- **Tests:** `Cleansia.IntegrationTests` (per-partition isolation, AC-1/AC-2/AC-6); optionally a unit test on the partition-key selection logic.
- **Not touched:** Angular apps, Android apps, EF/DB schema, DTOs / NSwag clients, controller `[EnableRateLimiting]` attributes (policy names unchanged).