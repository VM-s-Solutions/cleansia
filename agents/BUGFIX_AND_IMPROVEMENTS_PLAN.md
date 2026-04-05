# Cleansia — Bug Fixes & Improvements Plan (v3)

> Updated: 2026-04-04 | Status: Active

---

## Current Bugs

### BUG-1: submitReview not a function (Customer App)

**Priority:** Critical | **Status: OPEN**

**Error:** `TypeError: this.customerClient.orderClient.submitReview is not a function`

**Root cause:** `CustomerClient` in `customer-base-client.ts` wraps `OrderClient` from `@cleansia/partner-services`. The partner's `OrderClient` does NOT have `submitReview` — it exists in the customer-generated `customer-client.ts`. The customer-base-client needs to use the customer-generated OrderClient for this method.

**Fix:**
- [ ] In `customer-base-client.ts`, the `orderClient` property uses `OrderClient` from `@cleansia/partner-services`
- [ ] The customer NSwag-generated client has its own `OrderClient` with `submitReview`
- [ ] Either swap the OrderClient to the customer-generated one, or add a dedicated `reviewClient` property using the customer-generated client
- [ ] The NSwag clients were regenerated — check if `submitReview` now exists on the customer OrderClient interface

**Files:** `libs/core/customer-services/src/lib/client/customer-base-client.ts`

---

### BUG-2: No default sort on My Orders page (Partner App)

**Priority:** High | **Status: OPEN**

**Root cause:** `orders.facade.ts` loads My Orders with `this.currentSort()` which initializes as empty `signal<SortDefinition[]>([])`. No default sort applied.

**Fix:**
- [ ] Set default sort in `loadMyOrders()`: `sort: [{ field: 'cleaningDateTime', direction: 'desc' }]`
- [ ] This will show newest orders first (by cleaning date descending)

**Files:** `libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts`

---

### BUG-3: Registration lock state is stale + conflates "missing data" with "awaiting approval"

**Priority:** High | **Status: OPEN**

**Two distinct problems, same component:**

#### Problem 3a — Store is not refreshed after profile save

`checkEmployeeCurrent()` is dispatched once in the registration lock's `ngOnInit()`. After the user saves profile fields or uploads documents, the store still holds the pre-save snapshot, so the lock continues to display obsolete missing fields / missing documents.

#### Problem 3b — "Awaiting admin approval" is not modeled as a distinct state

[cleansia-registration-lock.component.ts:101-108](src/Cleansia.App/libs/shared/components/src/lib/cleansia-registration-lock/cleansia-registration-lock.component.ts#L101-L108) currently encodes the approval step as a pseudo-state on the `availability` category:

```ts
const availabilityDone = result.isComplete;
const availabilityPending = result.hasCompletedProfile && result.hasUploadedDocuments && !result.isComplete;
```

This is wrong in two ways:
1. **The category is labelled "Availability"** — but it's actually showing approval status. A user who has filled out their schedule still sees "availability pending" because what's really pending is admin approval.
2. **The message is identical** whether the user has uploaded documents (waiting for admin) or hasn't uploaded anything yet (waiting for the user to act). These are two completely different situations and need different CTAs:
   - **Docs not uploaded** → "You need to upload your documents" + button to docs page
   - **Docs uploaded, awaiting review** → "Your documents are being reviewed by our team. We'll notify you once approved." + no CTA, just status
   - **Docs rejected** → "Your documents were rejected: {reason}. Please re-upload." + button to docs page + reason text

The backend already has the data — [Employee.cs:21](src/Cleansia.Core.Domain/Users/Employee.cs#L21) `ContractStatus` enum (`Pending`, `Approved`, `Rejected`) + `RejectionReason` field. The frontend just doesn't surface them.

### Combined fix

**Backend — extend the registration completion DTO:**
- Add to `RegistrationCompletionResult` (or equivalent DTO returned by `checkEmployeeCurrent`):
  ```csharp
  public ContractStatus ContractStatus { get; init; }
  public string? RejectionReason { get; init; }
  public bool AwaitingApproval => HasCompletedProfile && HasUploadedDocuments && ContractStatus == ContractStatus.Pending;
  ```
- This gives the frontend enough to distinguish all four states without adding new API calls.

**Frontend — rework category logic:**
- Replace the "availability" approval hack with a dedicated `approval` category in `buildEnhancedStatus()`. Availability stays as its own honest category (or is removed entirely per BUG-4).
- New state machine for the `approval` category:
  | Profile done? | Docs uploaded? | ContractStatus | Category status | Message |
  |---|---|---|---|---|
  | ✗ | — | — | `missing` | "Complete your profile first" |
  | ✓ | ✗ | — | `missing` | "Upload your documents" (CTA: docs page) |
  | ✓ | ✓ | `Pending` | `pending` | "Documents under review — we'll notify you when approved" |
  | ✓ | ✓ | `Rejected` | `missing` | "Documents rejected: {reason}" (CTA: re-upload) |
  | ✓ | ✓ | `Approved` | `done` | — |
- New i18n keys: `registrationLock.categories.approval.title`, `.awaitingReview`, `.rejected`, `.rejectedReason`, `.uploadDocuments`, `.completeProfileFirst`
- Translations in all 5 partner languages (en, cs, sk, uk, ru)

**Frontend — store refresh after writes:**
- In `profile.facade.ts`, after `updateEmployeeSuccess` → dispatch `checkEmployeeCurrent()` to re-fetch
- In the documents facade/effects, after `uploadDocumentSuccess` / `deleteDocumentSuccess` → same re-dispatch
- Cleaner alternative: add a NgRx effect that listens for `updateEmployeeSuccess | uploadDocumentSuccess | deleteDocumentSuccess | submitForApprovalSuccess` actions and dispatches `checkEmployeeCurrent()` — single source of truth, no scattered calls in facades
- Push approach (future): SignalR notification from backend when admin approves/rejects → automatic UI update without polling. Out of scope for this fix.

**Files:**
- `src/Cleansia.Core.AppServices/Features/Employees/CheckEmployeeCurrent*.cs` (or equivalent) — extend DTO
- `src/Cleansia.Core.Domain/Users/Employee.cs` — may need a helper method `GetRegistrationStatus()` returning a richer view model
- NSwag client regeneration (partner)
- `libs/cleansia-partner-features/profile/src/lib/profile/profile.facade.ts` — re-fetch on success
- `libs/cleansia-partner-features/documents/.../documents.effects.ts` (or similar) — re-fetch on success
- `libs/shared/components/src/lib/cleansia-registration-lock/cleansia-registration-lock.component.ts` — add `approval` category, remove approval pseudo-state from `availability`
- `libs/shared/components/src/lib/cleansia-registration-lock/cleansia-registration-lock.component.html` — render rejection reason when present
- `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — new approval keys

**Dependency on BUG-4:** This fix cleans up the availability category's semantics, which directly enables BUG-4's fix (availability becomes a real category or is removed entirely — no more dual meaning).

---

### BUG-4: Availability listed twice in registration lock + missing field translations

**Priority:** Medium | **Status: OPEN**

**Root cause:** [Employee.cs:205-226](src/Cleansia.Core.Domain/Users/Employee.cs#L205-L226) `GetMissingProfileFields()` returns a flat `List<string>` of **hardcoded English labels** (`"First Name"`, `"Street"`, `"Availability"`, etc.). The registration lock component:
1. Shows these strings verbatim under the profile category — they are **not translated** (a Czech user sees "First Name" in English)
2. Also renders Availability as its own top-level category — **so Availability appears twice** (once in the profile category's missing-fields list, once as its own category)

**Two fixes needed:**

#### Fix 4a — Eliminate the duplication

Remove `"Availability"` from `GetMissingProfileFields()` in [Employee.cs:223](src/Cleansia.Core.Domain/Users/Employee.cs#L223). The frontend already renders availability as a dedicated category (and after BUG-3's fix will render approval as yet another dedicated category). Availability belongs to one of those two buckets, not inside the "profile fields" list.

#### Fix 4b — Translate the missing field labels

The current design leaks English labels from C# to the frontend. There are two approaches:

**Approach A (recommended): Return translation keys from the backend.**

Change `GetMissingProfileFields()` to return keys instead of text:
```csharp
public List<string> GetMissingProfileFields()
{
    var missingFields = new List<string>();
    if (string.IsNullOrEmpty(User?.FirstName)) missingFields.Add("profile.fields.firstName");
    if (string.IsNullOrEmpty(User?.LastName)) missingFields.Add("profile.fields.lastName");
    if (string.IsNullOrEmpty(User?.Email)) missingFields.Add("profile.fields.email");
    if (string.IsNullOrEmpty(User?.PhoneNumber)) missingFields.Add("profile.fields.phoneNumber");
    if (User?.BirthDate == null) missingFields.Add("profile.fields.birthDate");
    if (string.IsNullOrEmpty(Address?.Street)) missingFields.Add("profile.fields.street");
    if (string.IsNullOrEmpty(Address?.City)) missingFields.Add("profile.fields.city");
    if (string.IsNullOrEmpty(Address?.ZipCode)) missingFields.Add("profile.fields.zipCode");
    if (string.IsNullOrEmpty(Address?.CountryId)) missingFields.Add("profile.fields.country");
    if (string.IsNullOrEmpty(TaxId)) missingFields.Add("profile.fields.taxId");
    if (string.IsNullOrEmpty(IBAN)) missingFields.Add("profile.fields.iban");
    if (string.IsNullOrEmpty(PassportId)) missingFields.Add("profile.fields.passportId");
    if (string.IsNullOrEmpty(NationalityId)) missingFields.Add("profile.fields.nationality");
    if (!Documents.Any(d => d.IsActive)) missingFields.Add("profile.fields.documents");
    // Availability REMOVED (handled by its own category in the frontend)
    return missingFields;
}
```

Frontend pipes each key through `translate` before rendering. Zero hardcoded user-facing English in the domain layer — which is correct for a multi-tenant multi-language system.

**Rename caveat:** After BUG-12 (IČO/DIČ redesign), `"profile.fields.taxId"` becomes `"profile.fields.registrationNumber"` + `"profile.fields.vatNumber"`. Coordinate the two changes — rename the key only once to avoid churn.

**Approach B: Map labels on the frontend.**

Keep the English strings in the backend, build a `Record<string, string>` lookup in the component:
```ts
const FIELD_LABEL_KEYS: Record<string, string> = {
  'First Name': 'profile.fields.firstName',
  // ...
};
```
**Rejected** — brittle (any backend wording change silently breaks translation), duplicates the field list in two places.

#### Required i18n additions (all 5 partner languages)

Add to `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`:

```json
"profile": {
  "fields": {
    "firstName": "First Name",
    "lastName": "Last Name",
    "email": "Email",
    "phoneNumber": "Phone Number",
    "birthDate": "Birth Date",
    "street": "Street",
    "city": "City",
    "zipCode": "Zip Code",
    "country": "Country",
    "taxId": "Tax ID",
    "iban": "IBAN",
    "passportId": "Passport ID",
    "nationality": "Nationality",
    "documents": "Documents"
  }
}
```

**Same keys needed in the admin app** (`apps/cleansia-admin.app/src/assets/i18n/*.json`) — the registration lock component is shared across both apps via `libs/shared/components`, and any admin-facing display of "missing fields" (e.g., in the employee detail page) should use the same keys.

**Translation task:** English provided; need cs / sk / uk / ru translations for all 14 field labels × 2 apps = 70 strings (though the same keys can live in a shared i18n bundle if one exists — check `libs/shared/i18n` or similar).

**Files:**
- `src/Cleansia.Core.Domain/Users/Employee.cs` — switch to translation keys, remove Availability entry
- `libs/shared/components/src/lib/cleansia-registration-lock/cleansia-registration-lock.component.html` — pipe missing fields through `| translate`
- `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — add `profile.fields.*` section
- `apps/cleansia-admin.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — same keys

**Coordination note:** Ship BUG-3 and BUG-4 in a single PR. They touch the same component, share i18n work, and the "remove Availability from GetMissingProfileFields" step in BUG-4 is a hard prerequisite for BUG-3's clean category model (approval + availability as separate, honest categories).

---

### BUG-5: Admin employee edit uses native HTML inputs

**Priority:** Medium | **Status: OPEN**

**Root cause:** The edit mode in `employee-detail.component.html` uses `<input type="text" class="edit-input">` instead of Cleansia components (`cleansia-text-input`, `cleansia-calendar`, `cleansia-select`, `cleansia-telephone`).

**Fix:**
- [ ] Replace all native `<input>` elements in edit sections with:
  - `<cleansia-text-input>` for text fields (name, email, tax ID, IBAN, passport, emergency)
  - `<cleansia-calendar>` for date of birth
  - `<cleansia-telephone>` for phone numbers
  - `<cleansia-select>` for nationality/country dropdowns
- [ ] Use reactive FormGroup instead of `[(ngModel)]` with `editFormData` object
- [ ] Match the styling of partner app's profile page

**Files:** `libs/cleansia-admin-features/employee-management/src/lib/employee-detail/employee-detail.component.html` and `.ts`

---

### BUG-6: Missing common translations in admin app

**Priority:** High | **Status: OPEN**

**Root cause:** The employee edit UI uses keys like `common.save`, `common.cancel`, `common.edit` but the admin i18n files don't have a top-level `common` section. These keys exist under `global.actions` instead.

**Fix:**
- [ ] Add `common` section to all admin i18n files (en, cs, sk, ru, uk):
  ```json
  "common": {
    "save": "Save",
    "cancel": "Cancel",
    "edit": "Edit",
    "delete": "Delete",
    "loading": "Loading...",
    "confirm": "Confirm"
  }
  ```
- [ ] OR change the component to use `global.actions.save` etc. (match existing admin convention)
- [ ] Recommended: Add `common` section to match partner app convention

**Files:** `apps/cleansia-admin.app/src/assets/i18n/*.json`

---

### BUG-7: Availability day name translations in admin (short form)

**Priority:** Medium | **Status: OPEN**

**Root cause:** The availability component uses `COMPONENTS.AVAILABILITY.DAYS.MONDAY_SHORT` translation keys but only full names exist (`monday`, `tuesday`, etc.). Short abbreviations are missing.

**Fix:**
- [ ] Add short day name translations to all admin i18n files:
  ```json
  "components": {
    "availability": {
      "days": {
        "monday": "Monday",
        "monday_short": "Mon",
        "tuesday": "Tuesday",
        "tuesday_short": "Tue",
        ...
      }
    }
  }
  ```
- [ ] Also check if the availability day toggle buttons (the blue "comp..." buttons in the screenshot) use the wrong translation key

**Files:** `apps/cleansia-admin.app/src/assets/i18n/*.json`

---

### BUG-8: Pay config has no grade/level system (Admin App)

**Priority:** Medium | **Status: OPEN**

**Root cause:** The `EmployeePayConfig` entity has flat pay rates per service/package but NO grade/level/skill field. The frontend pay config form was created but doesn't have a grade template system.

**Fix:**

**Backend:**
- [ ] The pay config CRUD already exists — no backend changes needed for basic grade templates
- [ ] Grade templates are a frontend-only concept (predefined multiplier sets)

**Frontend:**
- [ ] Add "Grade Template" dropdown to pay config form with options:
  - Junior (0.5x base rate)
  - Medior (0.75x base rate)
  - Senior (1.0x base rate)
- [ ] When selected, auto-fill the pay rate fields as multipliers of the service's base price
- [ ] Admin can then override individual values after applying template
- [ ] Add "Apply to all services" bulk action

**Files:**
- `libs/cleansia-admin-features/pay-config-management/src/lib/pay-config-form/`

---

### BUG-9: Password requirements mismatch (Profile vs Register)

**Priority:** High | **Status: OPEN**

**Root cause:** 
- Profile page requires: lowercase + uppercase + digit + special char + 12+ chars
- Register page requires: any letter + digit + 8+ chars

**Fix:**
- [ ] Unify password validation to match register page (the more lenient one, since users already registered with it):
  ```typescript
  const passwordPattern = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;
  ```
- [ ] Update profile page password validator to use the same pattern
- [ ] Show the same password requirements UI on both pages
- [ ] OR strengthen register validation to match profile (breaking change for existing users)
- [ ] Recommended: Use register pattern everywhere (8+ chars, letter + digit)

**Files:**
- `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts` (line ~131)
- `libs/cleansia-partner-features/profile/src/lib/profile/profile.facade.ts`

---

### BUG-10: Address validation missing on profile page

**Priority:** Medium | **Status: OPEN**

**Root cause:** Address form uses plain object without FormGroup validation. No country dropdown, no required field validation, accepts any input.

**Fix:**
- [ ] Convert address form to reactive FormGroup with validators:
  - Street: required, 3-255 chars
  - City: required, 2-100 chars
  - Zip code: required, 3-20 chars
  - Country: required, use `cleansia-select` dropdown (pre-select Czech Republic)
- [ ] Load countries from API (same as partner profile)
- [ ] Default country to Czech Republic (disabled or pre-selected)
- [ ] Add form validation error messages

**Files:** 
- `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.ts`
- `libs/cleansia-customer-features/profile/src/lib/profile/profile.component.html`

---

## Customer App Content Updates

### CONTENT-1: "Why Choose Us?" — Replace "Fast Drying" with "Precision Care"

**Priority:** Low | **Status: OPEN** | **Type: Content/i18n only**

**Current:** `item3_title: "Fast Drying"` / `item3_desc: "Ready in 2-4 hours — start using your furniture right away."`

**New:**
- Title: `Precision Care`
- Subtitle: `We give each material the right procedure and the right products.`

**Analysis:** Pure i18n change. No component or structural code changes needed. The `features.component.html` already reads `item3_title` / `item3_desc` via translation keys.

**Files (update `pages.home.why_choose_us.item3_title` + `item3_desc` in all 5):**
- `apps/cleansia.app/src/assets/i18n/en.json` (line ~388)
- `apps/cleansia.app/src/assets/i18n/cs.json`
- `apps/cleansia.app/src/assets/i18n/sk.json`
- `apps/cleansia.app/src/assets/i18n/uk.json`
- `apps/cleansia.app/src/assets/i18n/ru.json`

**Translation task:** English provided; need cs/sk/uk/ru translations for "Precision Care" + subtitle.

---

### CONTENT-2: "How it works" — Rewrite process section with 6 steps

**Priority:** Medium | **Status: OPEN** | **Type: Component + i18n**

**Current:** 5-step process ("Inspection → Stain Removal → Deep Cleaning → Steam Treatment → Drying") in `process.component.html` with hardcoded steps 1-5 and SVG path with 5 dots.

**New (6 steps) — booking-flow oriented, not cleaning-technique oriented:**

| # | Title | Description |
|---|-------|-------------|
| Section title | How it works | A few clicks, a clear price and professional cleaning right at your home. |
| 01 | Choose a service | Household cleaning, upholstery cleaning or regular care. Exactly according to your needs. |
| 02 | Enter details | Address, date, scope. No phone calls, no waiting for a response. |
| 03 | Pay online | The price is fixed and visible in advance. You will confirm the booking within seconds. |
| 04 | We will assign you a professional | As soon as a verified cleaner accepts the order, you will receive a confirmation with her name and rating. |
| 05 | We will arrive at your place | At the agreed time, fully equipped and ready to get to work. |
| 06 | A result that is visible | Carefully, reliably and without compromise. |

**Analysis — structural changes required:**

1. **`process.component.html`** — Currently has exactly 5 hardcoded `<div class="cl-roadmap__step">` blocks (steps 1-5). Need to add a 6th step block (alternating side — step 6 should go on the right side matching the pattern: 1-right, 2-left, 3-right, 4-left, 5-right, 6-left).
2. **SVG roadmap path (line 10-19)** — The `viewBox="0 0 800 1100"` path has 5 circles/dots at hardcoded coordinates. Needs extending:
   - Increase viewBox height (e.g., `0 0 800 1320`)
   - Extend the `d=` path with one more curve segment down to ~y=1150
   - Add a 6th `<circle>` dot
   - Move the final `M 400,1060` end-point further down
3. **SCSS** — Check `process.component.scss` for `.cl-roadmap__step--5` styles; add `--6` variant with matching positioning.
4. **i18n** — Replace all `process.step1..step5` + `_desc` keys with `step1..step6` new content; update `title` + `subtitle` + `badge`.

**Files:**
- `libs/cleansia-customer-features/home/src/lib/home/components/process/process.component.html` (add step6 block, update SVG)
- `libs/cleansia-customer-features/home/src/lib/home/components/process/process.component.scss` (add `.cl-roadmap__step--6`)
- `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — update `pages.home.process.*`

**Translation task:** English provided; translate all 6 step titles + descriptions + section title/subtitle into cs/sk/uk/ru.

**Alternative (recommended for maintainability):** Refactor `process.component.ts` to hold a `steps` array and `@for` loop over it in the template (like the FAQ component does). This would make future step additions trivial. Same SVG work still required.

---

### CONTENT-3: FAQ — Add 6 new questions

**Priority:** Low | **Status: OPEN** | **Type: Component + i18n**

**Current:** 4 FAQs (q1-q4) defined in `faq.component.ts` `faqs` array, translation keys `pages.home.faq.q1..q4`. Note: q1, q2, q3, q4 already cover suburbs, delicate upholstery, stain removal, and evening/weekends — the new content overlaps significantly but adds 2 more questions (home presence, equipment) and rewords existing ones.

**New FAQ list (6 items):**

1. **Do you also travel outside Prague?** — Yes, we cover surrounding areas up to ~30 km from Prague. For more distant locations, contact us — we will find a solution.
2. **Do I have to be at home during the cleaning?** — It is not necessary. Many clients hand over the keys or let us in and leave. We work independently and reliably.
3. **Do you bring your own equipment and products?** — Yes, we always arrive fully equipped. We use professional machines and gentle, certified products.
4. **Can you clean furniture with delicate upholstery?** — Yes, we select gentle cleaning agents and methods appropriate for each fabric type: silk, wool, velvet, microfibre and more. Before cleaning, we always test on a small area to make sure the fabric won't be damaged.
5. **Do you remove all stains completely?** — We remove up to 95% of stains. Some very old or chemically set stains may lighten significantly but not disappear fully — we will always inform you honestly about the expected result.
6. **Can I book cleaning in the evening or on weekends?** — Yes. We offer flexible scheduling including evenings and weekends to fit your schedule.

**Analysis:**

1. **`faq.component.ts`** — Extend `faqs` array from 4 to 6 entries (add q5, q6 keys). Simple change.
2. **i18n** — Replace q1-q4 text content with new wording, add q5 + q6 keys across all 5 language files.

**Files:**
- `libs/cleansia-customer-features/home/src/lib/home/components/faq/faq.component.ts` (line 15 — add q5, q6)
- `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — rewrite `pages.home.faq.q1..q4`, add `q5`, `q6`

**Translation task:** English provided; translate all 6 Q&A pairs into cs/sk/uk/ru. Note: the Czech translation is especially important since the source wording reads like it was drafted in Czech originally ("Do you also travel outside Prague" implies a CZ-centric audience).

---

## Partner App Improvements

### BUG-11: Past-due orders shown in "Available Orders" list (Partner App)

**Priority:** High | **Status: OPEN**

**Symptom:** On 2026-04-04, the Available Orders list shows orders with `cleaningDateTime` as far back as 2026-03-16 — orders that are physically impossible to fulfil.

**Root cause:** [orders.facade.ts:119-142](src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts#L119-L142) builds an `OrderFilter` with:
```ts
orderStatuses: [OrderStatus.Pending, OrderStatus.Confirmed],
hasAvailableSpots: true,
excludeEmployeeId: employeeId
```
There is **no floor on `cleaningDateTimeFrom`**. The backend's `GetOrdersPaged` handler honours whatever is passed, so if the frontend doesn't send a date filter, stale Pending/Confirmed orders that were never matched leak into the list indefinitely.

Secondary cause: when an order passes its `cleaningDateTime` with no assignment, there is no background job that transitions its status to `Expired`/`Cancelled`. The data layer considers them still "Pending", which is technically accurate but operationally wrong.

**Two-layer fix (defense in depth):**

**Layer 1 — Frontend filter (quick win, ship immediately):**
- In `loadAvailableOrders()`, inject `cleaningDateTimeFrom: new Date()` (or "start of today" — see question below) into the `OrderFilter`.
- This guarantees partners never see past-due orders even if backend data is dirty.

**Layer 2 — Backend enforcement (authoritative fix):**
- Option A (recommended): In [orders.facade.ts:119](src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts#L119) `GetOrdersPagedQueryHandler`, when `hasAvailableSpots == true` is passed (the "available for pickup" semantic), automatically apply `cleaningDateTime >= DateTimeOffset.UtcNow` at the query level. The `hasAvailableSpots` flag is specifically used for the partner "orders I could take" view — past-due orders must never be in this scope.
- Option B: New `OrderStatus.Expired` enum value + a background job (fits into the planned `Cleansia.Functions` timer triggers — see `fancy-painting-cake.md`) that runs hourly and transitions unassigned Pending/Confirmed orders past `cleaningDateTime + grace period` to `Expired`. Cleaner semantically, requires EF migration + state machine updates.

**Recommended:** Ship Layer 1 today (5-minute change), plan Layer 2 Option B alongside the Azure Functions migration.

**Open question:** What's the correct cutoff — "now" (UTC) or "start of today in customer's timezone"? An order scheduled for 2 hours from now is still takeable. An order scheduled for 1 hour ago might still be takeable if the cleaner can arrive quickly. Recommendation: use `DateTimeOffset.UtcNow` (strict "future only") for the frontend filter, and add a 2-hour grace buffer on the backend (`cleaningDateTime >= UtcNow - 2h`) to handle the "late but still viable" case.

**Files:**
- `libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts` (Layer 1)
- `src/Cleansia.Core.AppServices/Features/Orders/GetOrdersPaged*.cs` (Layer 2 Option A)
- `src/Cleansia.Core.Domain/Enums/OrderStatus.cs` + EF migration (Layer 2 Option B)

---

### BUG-12: Employee tax ID is a single flat field — needs IČO (mandatory) + DIČ (optional) + entity type

**Priority:** High | **Status: OPEN** | **Type: Domain model redesign**

**Problem:** [Employee.cs:13](src/Cleansia.Core.Domain/Users/Employee.cs#L13) has a single `TaxId` field:
```csharp
[MaxLength(50)]
public string? TaxId { get; private set; }
```

This conflates two distinct concepts that Czech (and most EU) law keeps separate:

| Czech term | English | Who has it | Mandatory? |
|---|---|---|---|
| **IČO** | Registration / Company ID | Every self-employed person (FO) + every legal entity (PO) | **Yes** |
| **DIČ** | VAT / Tax Identification Number | Only VAT-registered entities | **No** (optional — below turnover threshold, self-employed don't need it) |

Additionally, the **entity type** (natural person vs. legal entity) affects:
- Which ID fields are required
- How invoices are addressed (FO: personal name; PO: company name)
- How payments are reported for tax purposes
- Which documents the partner must upload during onboarding

The current model has no way to express "I am a self-employed cleaner with IČO 12345678 but no DIČ" — which is the **most common case in CZ**.

**Design goal: country-agnostic, easy to expand**

The terminology is Czech but every country has the same structural problem: a **primary registration identifier** (always mandatory) + an **optional tax/VAT identifier** + an **entity type** (individual vs. organization). Examples:

| Country | Primary (mandatory) | VAT (optional) | Example label override |
|---|---|---|---|
| CZ | IČO (8 digits) | DIČ (CZ + 8-10 digits) | "IČO" / "DIČ" |
| SK | IČO (8 digits) | DIČ / IČ DPH | "IČO" / "IČ DPH" |
| PL | NIP (10 digits) | EU VAT | "NIP" / "VAT UE" |
| DE | Steuernummer | USt-IdNr | "Steuernummer" / "USt-IdNr" |
| AT | UID | UID | "UID" / "UID" |
| default / generic | Tax ID | VAT Number | "Tax ID" / "VAT Number" |

Each country also has its own **format/regex** (e.g., CZ IČO is exactly 8 digits; SK DIČ is 10 digits starting with 1-9, etc.).

**Existing precedent in the codebase:** Good news — both halves of the solution already exist partially:

1. [CountryConfiguration.cs:37-40](src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs#L37-L40) **already has `TaxIdLabel` + `TaxIdFormat`** — but only for a single field. Needs extension to support a second (VAT) field.
2. [CompanyInfo.cs:22-25](src/Cleansia.Core.Domain/Company/CompanyInfo.cs#L22-L25) **already separates `RegistrationNumber` + `VatNumber`** on the company side — this is exactly the model we need to mirror on `Employee`.

The fix is therefore not a new pattern — it's **making Employee consistent with CompanyInfo**, plus extending `CountryConfiguration` to drive the labels/validation.

### Proposed Domain Model

**New enum: `src/Cleansia.Core.Domain/Enums/EmployeeEntityType.cs`**
```csharp
public enum EmployeeEntityType
{
    NaturalPerson = 0,  // FO — self-employed individual ("OSVČ" in CZ)
    LegalEntity = 1     // PO — company/organization (s.r.o., a.s., etc.)
}
```

**Modified: `Employee.cs`**
```csharp
public EmployeeEntityType EntityType { get; private set; } = EmployeeEntityType.NaturalPerson;

[MaxLength(50)]
public string? RegistrationNumber { get; private set; }  // IČO in CZ — MANDATORY

[MaxLength(50)]
public string? VatNumber { get; private set; }           // DIČ in CZ — OPTIONAL

// For LegalEntity only — optional display name of the company
[MaxLength(200)]
public string? LegalEntityName { get; private set; }

// DEPRECATED — migrate existing TaxId values into RegistrationNumber during EF migration
// Keep the property for one release cycle as [Obsolete], then remove
```

**Migration strategy for existing data:**
- Existing `Employee.TaxId` values → copy into `RegistrationNumber`
- Default `EntityType = NaturalPerson` (safe default — the vast majority of existing cleaners are FO)
- `VatNumber = null` for all existing rows
- Drop `TaxId` column in a subsequent migration (after verifying no code references remain)

**Validation — where it belongs:**

Validation is **per-country** and **per-entity-type**. Put the rules in a new domain service:

```csharp
// src/Cleansia.Core.Domain/Services/ITaxIdValidator.cs
public interface ITaxIdValidator
{
    TaxIdValidationResult ValidateRegistrationNumber(string countryCode, EmployeeEntityType type, string? value);
    TaxIdValidationResult ValidateVatNumber(string countryCode, string? value);
}

public record TaxIdValidationResult(bool IsValid, string? ErrorKey);
```

Implementation pulls rules from `CountryConfiguration` — don't hardcode country logic in C#. Store regex patterns in the config table so ops can add a country without a deploy:

```csharp
// CountryConfiguration.cs — extend
public string? RegistrationNumberLabel { get; private set; }    // "IČO"
public string? RegistrationNumberFormat { get; private set; }   // ^\d{8}$
public bool RegistrationNumberRequired { get; private set; } = true;

public string? VatNumberLabel { get; private set; }             // "DIČ"
public string? VatNumberFormat { get; private set; }            // ^CZ\d{8,10}$
public bool VatNumberRequired { get; private set; } = false;
```

Then each country row in `CountryConfigurations` table carries its own regex + labels. Adding Poland = `INSERT` row with NIP format, no code change.

**Optional external validation (v2):** CZ + SK + most EU countries expose public registries (ARES for CZ, OR for SK, VIES for EU VAT). Add an `ITaxIdLookupService` with country-specific implementations that verify the number actually exists and optionally auto-fill the legal entity name. Out of scope for v1 — format validation first, registry lookup later.

### Frontend Changes

**Partner App profile page:**
- New "Business Information" section with:
  - Entity type radio/segmented control: `Natural Person (OSVČ)` / `Legal Entity (s.r.o., a.s.)`
  - Company name field (only visible when `EntityType == LegalEntity`, required in that case)
  - Registration number field — label dynamic (`IČO` for CZ, `NIP` for PL, etc.) pulled from `CountryConfiguration` via API, always required
  - VAT number field — label dynamic (`DIČ`, `USt-IdNr`, etc.), optional, with helper text "Only if VAT-registered"
- Client-side format validation driven by the same country config (`RegistrationNumberFormat` regex)
- Server-side validation via the new `ITaxIdValidator` — always the source of truth

**Country resolution for labels:** Which country's labels to show? Options:
- **Option A (recommended):** Use the partner's address country (`Employee.Address.CountryId`) — already set during onboarding
- Option B: Use tenant's country (if multi-tenant by country)
- Option C: Let the partner pick — unnecessary complexity

Option A matches real-world usage: a Czech cleaner sees IČO/DIČ, a Polish cleaner sees NIP. When address changes country, re-fetch labels.

**Admin App employee edit:** Same treatment — replace the current single `taxId` input with the new entity-type-aware form.

**Registration/onboarding wizard:** Add entity type selection as an early step; branches the required-docs list (e.g., `Živnostenský list` for FO, `Výpis z OR` for PO in CZ).

### Backend Changes

- `Employee` domain update methods: split `UpdateIdentification` into `UpdateBusinessIdentity(entityType, registrationNumber, vatNumber, legalEntityName)` + `UpdatePersonalIdentity(nationalityId, passportId)` — different concerns, different validation
- `GetMissingProfileFields()` — update to check `RegistrationNumber` (always) + `VatNumber` only if country requires it (driven by `CountryConfiguration.VatNumberRequired`)
- `IsProfileComplete()` — same
- DTO updates: `EmployeeDto`, `UpdateEmployeeCommand`, etc.
- NSwag client regeneration (partner + admin)
- PDF receipt/invoice generation (see Phase 8 plan in `fancy-painting-cake.md`) — layouts must print `RegistrationNumber` + `VatNumber` + entity-appropriate name (personal name for FO, legal entity name for PO) with correct labels per country

### Files Summary

| File | Action |
|---|---|
| `src/Cleansia.Core.Domain/Enums/EmployeeEntityType.cs` | CREATE |
| `src/Cleansia.Core.Domain/Users/Employee.cs` | MODIFY — add fields, migration-aware setters, deprecate `TaxId` |
| `src/Cleansia.Core.Domain/Configuration/CountryConfiguration.cs` | MODIFY — add `RegistrationNumber*` + `VatNumber*` label/format/required fields |
| `src/Cleansia.Core.Domain/Services/ITaxIdValidator.cs` | CREATE |
| `src/Cleansia.Core.Domain/Services/TaxIdValidator.cs` | CREATE — config-driven, not country-hardcoded |
| `src/Cleansia.Infra.Persistence/Migrations/XXXX_EmployeeBusinessIdentity.cs` | CREATE — EF migration with data backfill |
| `src/Cleansia.Infra.Persistence/Seed/CountryConfigurationSeed.cs` | MODIFY — seed CZ, SK, PL, DE, AT label/format rules |
| `src/Cleansia.Core.AppServices/Features/Employees/UpdateEmployee*.cs` | MODIFY — new validation + command fields |
| `src/Cleansia.Api.Partner/...` NSwag contracts | MODIFY — regenerate |
| `libs/cleansia-partner-features/profile/src/lib/profile/profile.component.{ts,html}` | MODIFY — entity-type-aware form |
| `libs/cleansia-admin-features/employee-management/.../employee-detail.component.{ts,html}` | MODIFY — same |
| `libs/cleansia-partner-features/onboarding/.../business-info-step.component.ts` | CREATE or MODIFY |
| i18n files — 5 partner langs + 5 admin langs | MODIFY — add entity type labels, fallback labels ("Tax ID", "VAT Number") |
| PDF invoice/receipt layouts (Phase 8) | MODIFY — print new fields with country-aware labels |

### Why this is the right abstraction

1. **Not hardcoding CZ in C# code.** All country-specific rules live in `CountryConfiguration` rows. Adding Poland = DB insert. Adding a new regex for SK = DB update. No deploy required for ops changes.
2. **Mirrors existing `CompanyInfo` pattern.** Less cognitive load — partners and company share the same conceptual model (RegistrationNumber + VatNumber + entity identity).
3. **Forward-compatible with registry lookups.** `ITaxIdLookupService` can be added later without touching the storage model.
4. **Backwards-compatible migration.** Existing data flows into `RegistrationNumber`; app keeps working through the transition; no data loss.
5. **Entity type drives downstream behavior.** Required docs, invoice addressing, tax reporting — all switch on one enum rather than scattered nullable-field heuristics.

---

## Implementation Priority

| Phase | Items | Effort |
|-------|-------|--------|
| **Phase 1 (Critical)** | BUG-1 (submitReview), BUG-6 (translations), BUG-9 (password) | Low |
| **Phase 2 (High)** | BUG-2 (sort), BUG-3 (registration refresh), BUG-7 (day names) | Low-Medium |
| **Phase 3 (Medium)** | BUG-4 (availability dupe), BUG-5 (admin inputs), BUG-10 (address) | Medium |
| **Phase 4 (Feature)** | BUG-8 (pay config grades) | Medium |
| **Phase 5 (Content)** | CONTENT-1 (Precision Care), CONTENT-2 (6-step process), CONTENT-3 (FAQ expansion) | Low-Medium |
| **Phase 6 (Partner fixes)** | BUG-11 (past-due filter, L1 only), BUG-12 (employee business identity model) | Medium-High |

---

## Summary

| # | Bug | Priority | Status |
|---|-----|----------|--------|
| 1 | submitReview not a function | Critical | OPEN |
| 2 | No default sort on My Orders | High | OPEN |
| 3 | Registration lock not refreshing | High | OPEN |
| 4 | Availability listed twice | Medium | OPEN |
| 5 | Admin edit uses native inputs | Medium | OPEN |
| 6 | Missing common translations | High | OPEN |
| 7 | Availability short day names | Medium | OPEN |
| 8 | Pay config no grade system | Medium | OPEN |
| 9 | Password requirements mismatch | High | OPEN |
| 10 | Address validation missing | Medium | OPEN |
| C1 | "Fast Drying" → "Precision Care" | Low | OPEN |
| C2 | Process section: 5 → 6 booking-flow steps | Medium | OPEN |
| C3 | FAQ: expand 4 → 6 items, reword existing | Low | OPEN |
| 11 | Past-due orders shown in Available list | High | OPEN |
| 12 | Employee IČO/DIČ + entity type model | High | OPEN |
