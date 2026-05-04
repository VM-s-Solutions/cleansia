# Guest Checkout — web only (mobile is sign-in-required by product decision)

**Status:** Scope reduced — mobile portions dropped. Web portions still applicable if/when we want to polish the existing guest flow.
**Depends on:** Mobile Booking Submission (Phase 6) complete — `BookingViewModel`, `BookingApi`, real catalog, address linkage all wired.

## Scope change — 2026-04-24

Mobile is intentionally sign-in-required for booking. All mobile tasks in this spec (GC1–GC6 as originally written) are **dead**. The web customer app already supports guest checkout today: no route guard, `[AllowAnonymous]` backend, inline contact fields, `GuestOrderService` persists ids in localStorage. So there's no active work here either.

What this spec could still drive if ever revived:

- **Polish existing web guest UX** — e.g., "Save this confirmation code" hint post-submit, a guest-order-lookup page (separate spec already references `/api/Order/GetByConfirmationCode`).
- **Mobile reversal** — if product ever allows anonymous mobile bookings, scope-restore this file.

Until either happens, treat this as archived reference.

## Cross-references

- **Profile completion** ([profile-completion-onboarding.md](./profile-completion-onboarding.md)) — handles the mobile equivalent: every mobile user is signed-in, so the concern is keeping their profile complete (phone required for booking) rather than letting them skip sign-in.

---

## Historical content below (mobile-focused, not to execute)

## Decisions in scope for this spec

1. **Remove the sign-in wall.** `BookingViewModel.submit()` currently hard-aborts with `error_booking_sign_in_required` when `currentUser == null`. Backend already accepts anonymous `POST /api/Order/Create` — it only validates `CustomerName`, `CustomerEmail`, `CustomerPhone`, and XOR between `CustomerAddress` and `SavedAddressId`. We let guests through by collecting contact info in-sheet.
2. **Contact info lives inline in `ConfirmStep`** — three fields (name, email, phone). Pre-filled and read-only for signed-in users (authoritative values come from `currentUser`). Pre-filled from blank and editable for guests. Rejected UX alternatives: a dedicated 4th step (adds friction for the common signed-in case) and a post-swipe modal (splits submit into two interactions).
3. **Saved-address path stays sign-in only.** Guests keep using one-off inline `customerAddress`. They can still open `AddressManagerScreen` and pick a locally-saved DataStore entry — those have `serverId == null`, so `BookingState.savedAddressId` stays null and the submit payload correctly sends `customerAddress`. No repository changes needed; Phase B already covers this.
4. **Card payment requires sign-in.** Mobile Stripe integration doesn't exist yet — card flow would break on the redirect. Guests get the cash chip only; the card chip renders disabled with a hint. Enforced at UI + viewmodel (viewmodel forces `paymentMethod = "cash"` when `currentUser == null`).
5. **Post-submit UX is the same `BookingSuccessScreen`.** Guests see their confirmation code. Add a "Save this code" hint copy — without an account, this code is their only record.
6. **Validation for guest contact fields** mirrors the registration screen patterns. Name 2-100 chars, email valid format, phone non-empty 5-20 chars. No country-specific phone format for MVP.

## What this spec does NOT do

- Add a `POST /api/Order/GetByConfirmationCode` anonymous lookup endpoint or the mobile/web UI to use it. Flagged as follow-up.
- Post-booking "create an account to save this booking" upsell. Separate spec — requires backend attribution logic to retroactively bind an order to a newly-created user by matching email.
- Mobile Stripe integration for guest card payments. Separate workstream.
- Phone format validation / country-aware rules.
- Any change to the Orders tab — that stays sign-in gated. Guests have no order history surface inside the app.

---

## Phase 1 — State + submit wiring

### TASK-GC1: Add guest contact fields to `BookingState`

```yaml
task: Add customerName/customerEmail/customerPhone to BookingState; prefill from currentUser in BookingViewModel.init
id: TASK-GC1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Today BookingState has no contact-info fields — the submit path pulls
  directly from UserRepository.currentUser. That breaks for guests. Move
  the three fields (name, email, phone) onto BookingState so they're
  editable by guests and pre-filled for signed-in users.

  Pre-fill rule: BookingViewModel.init observes userRepository.currentUser
  once and seeds the state fields if non-null. Signed-in users still see
  the fields populated in ConfirmStep — they're just read-only there
  (TASK-GC2 enforces the readonly UI).

  Authoritative source for signed-in submit: we DO NOT trust state.
  customerName/email/phone for signed-in users on submit — we re-read
  currentUser.value at submit time (see TASK-GC3). The state fields are
  purely a UI convenience for the read-only display.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Add after the address fields (near savedAddressId):
        customerName: String = "",
        customerEmail: String = "",
        customerPhone: String = "",

      No other behavioral changes to the data class. These are populated
      either by BookingViewModel.init (signed-in) or user edits in
      ConfirmStep (guest).

  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      In the init block (or add one if none exists), collect from
      userRepository.currentUser and seed the three fields when the user
      is non-null. Use viewModelScope:

        init {
            viewModelScope.launch {
                userRepository.currentUser.collect { user ->
                    if (user != null) {
                        _state.update { s ->
                            // Only prefill blanks — do not clobber a guest's
                            // edits if they sign in mid-flow (edge case).
                            s.copy(
                                customerName = if (s.customerName.isBlank())
                                    listOfNotNull(user.firstName, user.lastName).joinToString(" ").trim()
                                    else s.customerName,
                                customerEmail = if (s.customerEmail.isBlank())
                                    user.email else s.customerEmail,
                                customerPhone = if (s.customerPhone.isBlank())
                                    user.phoneNumber.orEmpty() else s.customerPhone,
                            )
                        }
                    }
                }
            }
        }

      Use _state.update { ... } (MutableStateFlow.update extension) to
      avoid racing with user edits. If update{} isn't already imported,
      add `import kotlinx.coroutines.flow.update`.

dependencies: []
verification:
  - Syntactic build in Android Studio (no gradle wrapper CLI)
  - Open booking sheet while signed in → ConfirmStep inputs show real
    name/email/phone.
  - Open booking sheet while signed out → ConfirmStep inputs are blank
    and editable.
```

### TASK-GC2: `ConfirmStep` contact fields — inline, guest-editable

```yaml
task: Add three contact-info text fields to ConfirmStep with signed-in read-only / guest-editable modes
id: TASK-GC2
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Render three stacked cleansia-text-field inputs (or the Material3
  equivalent) above the payment-method chips in ConfirmStep — grep
  existing ConfirmStep for section layout to match spacing. Order:
  Name, Email, Phone.

  Signed-in branch (userRepository.currentUser.collectAsState().value != null):
    - Fields render with the pre-filled values from BookingState.
    - readOnly = true, enabled = false (so they look filled but muted).
    - Below the group: a single small caption
      stringResource(R.string.booking_contact_from_profile).
    - Do NOT show per-field validation errors here — the values come
      from the backend profile and are authoritative.

  Guest branch (currentUser == null):
    - Fields editable. onValueChange → bookingVm.update { it.copy(...) }
    - Inline validation fires on blur OR on submit-tap:
      * Name: trimmed length in 2..100 → else error booking_name_required
      * Email: Patterns.EMAIL_ADDRESS.matcher(it.trim()).matches()
        → else error booking_email_invalid
      * Phone: trimmed length in 5..20 → else error booking_phone_required
    - Expose an `isGuestContactValid: Boolean` derivedStateOf in the
      composable, wired to the slide-to-confirm enabled state. The
      existing slide-to-confirm gate already checks other step-completion
      predicates — add this guest-only check alongside.

  Use cleansia-text-field if a shared primitive exists in the codebase;
  otherwise use Material3 OutlinedTextField with consistent styling. Grep
  for "cleansia-text-field" or "CleansiaTextField" first. If nothing, fall
  back to OutlinedTextField and leave a // TODO to extract later.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/ConfirmStep.kt
    change: |
      1. Import stringResource, collectAsState, UserRepository entry point
         (mirror the catalog entry-point pattern from MainShell / Phase-6
         TASK-BS5 if there's no ready-made access path).

      2. Read currentUser inside the composable:
           val currentUser by userRepo.currentUser.collectAsState()
           val isGuest = currentUser == null

      3. Insert a new section above the payment-method row:
           Column(Modifier.fillMaxWidth()) {
               SectionHeader(text = stringResource(R.string.booking_contact_title))

               val nameError = if (isGuest && attemptedSubmit &&
                       state.customerName.trim().length !in 2..100)
                   stringResource(R.string.booking_name_required) else null

               OutlinedTextField(
                   value = state.customerName,
                   onValueChange = { v -> bookingVm.update { it.copy(customerName = v) } },
                   label = { Text(stringResource(R.string.booking_name_label)) },
                   readOnly = !isGuest,
                   enabled = isGuest,
                   isError = nameError != null,
                   supportingText = nameError?.let { { Text(it) } },
                   singleLine = true,
                   modifier = Modifier.fillMaxWidth(),
               )
               // ... email + phone follow same pattern
               if (!isGuest) {
                   Text(
                       stringResource(R.string.booking_contact_from_profile),
                       style = MaterialTheme.typography.bodySmall,
                       color = MaterialTheme.colorScheme.onSurfaceVariant,
                   )
               }
           }

      4. Add local `var attemptedSubmit by remember { mutableStateOf(false) }`.
         Toggle to true on the first slide-to-confirm activation. Validation
         errors render only after attemptedSubmit == true (don't flash errors
         on an empty form the user just opened).

      5. Compute guest validity:
           val isGuestContactValid = remember(state, isGuest) {
               derivedStateOf {
                   !isGuest || (
                       state.customerName.trim().length in 2..100 &&
                       android.util.Patterns.EMAIL_ADDRESS.matcher(state.customerEmail.trim()).matches() &&
                       state.customerPhone.trim().length in 5..20
                   )
               }
           }.value

         Thread into the slide-to-confirm enabled prop — AND it with the
         existing gates.

      6. The slide-to-confirm onActivate lambda: first set
         attemptedSubmit = true, then early-return if !isGuestContactValid
         (validation errors will now render). Only call vm.submit() if all
         checks pass.

files_to_create: []

dependencies:
  - TASK-GC1
  - TASK-GC5 # strings must exist before this compiles
verification:
  - Signed-in: ConfirmStep shows three fields with profile values, all
    disabled, caption "Change in your profile" below.
  - Guest: Fields editable, blank. Swipe-to-confirm disabled until all
    three are valid.
  - Guest with empty email: tap swipe-to-confirm → email field shows
    "Enter a valid email" error.
  - Guest with all valid → swipe-to-confirm activates.
```

### TASK-GC3: Remove sign-in gate from `BookingViewModel.submit()`

```yaml
task: Replace the signed-in-only gate with a guest/signed-in branching path
id: TASK-GC3
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Today submit() early-returns when currentUser == null. Replace with:
  if signed-in, use currentUser values (authoritative — never trust
  state.customerName/etc for a signed-in user); if guest, use
  state.customerName/email/phone.

  The UI-level validation (TASK-GC2) already prevents guests from
  reaching submit() with invalid data — but defense-in-depth: submit()
  re-validates the guest fields and snackbars on failure. Prevents a bug
  where a future refactor accidentally drops UI validation.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    line_range: '~743-800' # the submit() body from Phase-6 spec
    change: |
      Replace the block:
        val user = userRepository.currentUser.value
        if (user == null) {
            snackbar.showErrorKey(R.string.error_booking_sign_in_required)
            return null
        }
        ...
        val createCmd = CreateOrderCommand(
            customerName = listOfNotNull(user.firstName, user.lastName).joinToString(" "),
            customerEmail = user.email,
            customerPhone = user.phoneNumber.orEmpty(),
            ...
        )

      With:
        val user = userRepository.currentUser.value
        val s = _state.value

        // Resolve contact info: currentUser is authoritative for signed-in,
        // state is authoritative for guests.
        val resolvedName: String
        val resolvedEmail: String
        val resolvedPhone: String
        if (user != null) {
            resolvedName = listOfNotNull(user.firstName, user.lastName)
                .joinToString(" ").trim()
            resolvedEmail = user.email
            resolvedPhone = user.phoneNumber.orEmpty()
        } else {
            // Guest — pull from state and re-validate as defense-in-depth.
            val name = s.customerName.trim()
            val email = s.customerEmail.trim()
            val phone = s.customerPhone.trim()
            if (name.length !in 2..100) {
                snackbar.showErrorKey(R.string.booking_name_required); return null
            }
            if (!android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()) {
                snackbar.showErrorKey(R.string.booking_email_invalid); return null
            }
            if (phone.length !in 5..20) {
                snackbar.showErrorKey(R.string.booking_phone_required); return null
            }
            resolvedName = name
            resolvedEmail = email
            resolvedPhone = phone
        }

        // Quote + Create as before — use resolved* in the CreateOrderCommand.

      In the CreateOrderCommand construction below:
        customerName = resolvedName,
        customerEmail = resolvedEmail,
        customerPhone = resolvedPhone,

      Also enforce the payment-method override here (TASK-GC4 adds the UI
      disabled state, but submit() is the last line of defense):
        paymentType = if (user != null && s.paymentMethod == "card") 2 else 1,

      That way even if the UI bug regresses, a guest cannot accidentally
      submit paymentType = 2 (card).

dependencies:
  - TASK-GC1
  - TASK-GC5 # snackbar keys
verification:
  - Signed-in flow still works end-to-end (no regression).
  - Guest flow: valid contact info → order creates, confirmation code
    returned, backend order has the guest's typed name/email/phone.
  - Guest flow: paymentMethod toggled to "card" in state (simulated) →
    submit still sends paymentType = 1 (cash).
```

---

## Phase 2 — Payment + strings

### TASK-GC4: Disable card payment for guests in UI

```yaml
task: Grey-out the card chip for guests and force cash on state changes
id: TASK-GC4
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  The payment chips in ConfirmStep today let users toggle cash/card.
  For guests we want card visibly disabled with a hint. On isGuest == true,
  force state.paymentMethod = "cash" if it's not already.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/ConfirmStep.kt
    change: |
      In the payment chip row:

        val currentUser by userRepo.currentUser.collectAsState()
        val isGuest = currentUser == null

        // Force cash for guests whenever isGuest flips on or on first composition.
        LaunchedEffect(isGuest) {
            if (isGuest && state.paymentMethod != "cash") {
                bookingVm.update { it.copy(paymentMethod = "cash") }
            }
        }

        Row(...) {
            PaymentChip(
                label = stringResource(R.string.booking_payment_cash),
                selected = state.paymentMethod == "cash",
                enabled = true,
                onClick = { bookingVm.update { it.copy(paymentMethod = "cash") } },
            )
            PaymentChip(
                label = stringResource(R.string.booking_payment_card),
                selected = state.paymentMethod == "card",
                enabled = !isGuest,
                onClick = {
                    if (isGuest) return@PaymentChip // no-op, chip is disabled
                    bookingVm.update { it.copy(paymentMethod = "card") }
                },
            )
        }
        if (isGuest) {
            Text(
                stringResource(R.string.booking_card_requires_signin),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }

      If `PaymentChip` (or whatever the actual chip composable is called)
      doesn't support an `enabled` flag yet, add one:
        @Composable fun PaymentChip(
            label: String,
            selected: Boolean,
            enabled: Boolean = true,
            onClick: () -> Unit,
        ) {
            FilterChip(
                selected = selected,
                onClick = { if (enabled) onClick() },
                enabled = enabled,
                label = { Text(label) },
            )
        }

dependencies:
  - TASK-GC5 # new string key
verification:
  - Guest view: card chip rendered greyed/disabled, hint text below.
  - Guest view: tapping disabled card chip is a no-op.
  - Signed-in view: both chips work as today.
  - Flip sign-in during an open sheet: paymentMethod auto-resets to "cash".
```

### TASK-GC5: i18n strings for guest checkout (5 locales)

```yaml
task: Add guest-checkout strings across values, values-cs, values-sk, values-uk, values-ru
id: TASK-GC5
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Add eight new string resources covering the guest contact fields,
  validation errors, payment-method hint, and success screen save-code
  hint. Mirror across all five locale folders.

  Do NOT use machine translation verbatim — use the translation patterns
  already established elsewhere in the mobile string files as a style
  reference (grep for a handful of existing keys to match tone).

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Add (English):
        <string name="booking_contact_title">Contact info</string>
        <string name="booking_name_label">Full name</string>
        <string name="booking_email_label">Email</string>
        <string name="booking_phone_label">Phone</string>
        <string name="booking_name_required">Enter your full name (2–100 characters).</string>
        <string name="booking_email_invalid">Enter a valid email address.</string>
        <string name="booking_phone_required">Enter a valid phone number.</string>
        <string name="booking_contact_from_profile">Change in your profile</string>
        <string name="booking_card_requires_signin">Sign in to pay by card.</string>
        <string name="booking_success_save_code">Save this code — it\'s how you\'ll look up your booking.</string>

  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: |
      Add the same keys with Czech copy. Suggested:
        booking_contact_title → "Kontaktní údaje"
        booking_name_label → "Celé jméno"
        booking_email_label → "E-mail"
        booking_phone_label → "Telefon"
        booking_name_required → "Zadejte celé jméno (2–100 znaků)."
        booking_email_invalid → "Zadejte platnou e-mailovou adresu."
        booking_phone_required → "Zadejte platné telefonní číslo."
        booking_contact_from_profile → "Změnit v profilu"
        booking_card_requires_signin → "Pro platbu kartou se prosím přihlaste."
        booking_success_save_code → "Uložte si tento kód — budete ho potřebovat k vyhledání rezervace."

  - path: src/cleansia_customer_android/app/src/main/res/values-sk/strings.xml
    change: |
      Slovak copy:
        booking_contact_title → "Kontaktné údaje"
        booking_name_label → "Celé meno"
        booking_email_label → "E-mail"
        booking_phone_label → "Telefón"
        booking_name_required → "Zadajte celé meno (2–100 znakov)."
        booking_email_invalid → "Zadajte platnú e-mailovú adresu."
        booking_phone_required → "Zadajte platné telefónne číslo."
        booking_contact_from_profile → "Zmeniť v profile"
        booking_card_requires_signin → "Pre platbu kartou sa prosím prihláste."
        booking_success_save_code → "Uložte si tento kód — budete ho potrebovať na vyhľadanie rezervácie."

  - path: src/cleansia_customer_android/app/src/main/res/values-uk/strings.xml
    change: |
      Ukrainian copy:
        booking_contact_title → "Контактні дані"
        booking_name_label → "Повне ім\'я"
        booking_email_label → "Електронна пошта"
        booking_phone_label → "Телефон"
        booking_name_required → "Введіть повне ім\'я (2–100 символів)."
        booking_email_invalid → "Введіть дійсну адресу електронної пошти."
        booking_phone_required → "Введіть дійсний номер телефону."
        booking_contact_from_profile → "Змінити в профілі"
        booking_card_requires_signin → "Щоб оплатити карткою, увійдіть в акаунт."
        booking_success_save_code → "Збережіть цей код — він знадобиться, щоб знайти замовлення."

  - path: src/cleansia_customer_android/app/src/main/res/values-ru/strings.xml
    change: |
      Russian copy:
        booking_contact_title → "Контактные данные"
        booking_name_label → "Полное имя"
        booking_email_label → "Электронная почта"
        booking_phone_label → "Телефон"
        booking_name_required → "Введите полное имя (2–100 символов)."
        booking_email_invalid → "Введите действительный адрес электронной почты."
        booking_phone_required → "Введите действительный номер телефона."
        booking_contact_from_profile → "Изменить в профиле"
        booking_card_requires_signin → "Для оплаты картой войдите в аккаунт."
        booking_success_save_code → "Сохраните этот код — он понадобится, чтобы найти заказ."

dependencies: []
verification:
  - Build → no unresolved R.string references.
  - Switch device language between en/cs/sk/uk/ru → ConfirmStep labels
    and validation errors localize correctly.
```

### TASK-GC6: Delete the now-obsolete `error_booking_sign_in_required`

```yaml
task: Remove the retired string from all five locales
id: TASK-GC6
type: refactor
priority: low
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Phase 6 introduced `error_booking_sign_in_required`. GC3 removes the
  code path that used it. Delete the string to prevent dead-code drift.
  Sanity-grep the mobile tree for any other R.string.error_booking_sign_in_required
  references before deleting — fail loud if one exists.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/res/values/strings.xml
    change: |
      Delete the <string name="error_booking_sign_in_required">...</string> line.

  - path: src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml
    change: Delete the same key.

  - path: src/cleansia_customer_android/app/src/main/res/values-sk/strings.xml
    change: Delete the same key.

  - path: src/cleansia_customer_android/app/src/main/res/values-uk/strings.xml
    change: Delete the same key.

  - path: src/cleansia_customer_android/app/src/main/res/values-ru/strings.xml
    change: Delete the same key.

dependencies:
  - TASK-GC3 # must have removed the code reference first
verification:
  - grep -rn "error_booking_sign_in_required" src/cleansia_customer_android/
    returns zero hits.
  - Build clean.
```

---

## Phase 3 — Success screen copy tweak

### TASK-GC7: "Save this code" hint on `BookingSuccessScreen`

```yaml
task: Add the save-code hint below the confirmation code, visible to all users (guest + signed-in)
id: TASK-GC7
type: feature
priority: low
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Phase 6's BookingSuccessScreen renders `confirmationCode` prominently.
  Add a small caption below it using booking_success_save_code. Shown to
  everyone — guests genuinely need to save it (no Orders tab access),
  signed-in users get the same reminder harmlessly.

  A cleaner split would be to show the hint only when guest, but that
  requires the success screen to know the auth state, which it currently
  doesn't. Keep it simple for MVP.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingSuccessScreen.kt
    change: |
      Below the confirmation code Text composable, add:
        Spacer(Modifier.height(8.dp))
        Text(
            stringResource(R.string.booking_success_save_code),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.fillMaxWidth().padding(horizontal = 24.dp),
        )

dependencies:
  - TASK-GC5
verification:
  - Complete a booking (guest or signed-in) → success screen shows
    confirmation code + hint text below.
```

---

## Execution order

1. **TASK-GC5** (strings) — no deps; must land before any composable that references the keys compiles. Land first.
2. **TASK-GC1** (state fields + prefill) — no code deps on other GC tasks; needed by GC2 and GC3.
3. **TASK-GC2** (ConfirmStep contact UI), **TASK-GC3** (submit guest path), **TASK-GC4** (payment chip) — parallelizable after GC1 + GC5. All three touch ConfirmStep + BookingViewModel; coordinate diffs if landing concurrently.
4. **TASK-GC7** (success screen hint) — trivial; after GC5.
5. **TASK-GC6** (remove retired string) — after GC3 removes the only code reference.

Parallelizable: GC1 + GC5 (kickoff); GC2 + GC3 + GC4 (once GC1/GC5 land); GC6 + GC7 (cleanup).

Estimated tokens: ~35k total.

---

## Out of scope (followup specs)

- **`POST /api/Order/GetByConfirmationCode` anonymous lookup** — guests need a way to check the status of a booking they made without an account. Endpoint takes the code + maybe a matching email, returns the order summary. Web + mobile UI to enter the code and see the result.
- **"Save this booking to an account" post-submit upsell** — show a CTA on the success screen for guests inviting them to create an account. Backend logic needs to retroactively bind existing orders (by `CustomerEmail`) to the newly-created user at registration time.
- **Mobile Stripe integration for guest card payments** — current blocker is mobile doesn't have Stripe wired at all. Unblocks the `booking_card_requires_signin` restriction once done.
- **Phone format validation + country rules** — today we only check 5-20 chars. A real libphonenumber-backed validator with the user's locale country would reduce garbage submissions.
- **Consent / terms checkbox for guests** — depending on legal review, guests may need to explicitly accept ToS at submit time. Signed-in users accepted at registration.
