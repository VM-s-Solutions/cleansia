# Booking Date/Time Picker — real calendar + lead-time enforcement

**Status:** Ready for execution
**Depends on:** TASK-BS7 (mobile booking submission) — `selectedInstant: Instant?` field already on BookingState and `combineDateAndTime(date, timeLabel)` helper in WhenWhereStep.

## Decisions in scope for this spec

1. Replace the mock 8-day horizontal chip strip with a scrollable 14-day range + an optional "More dates" DatePickerDialog capped at +60 days.
2. Replace the hardcoded 8 time slots (2 Unavailable / 2 Express / rest Available) with a pure function `generateSlots(date, now)` that derives state from lead-time policy.
3. Introduce `BookingPolicy.kt` on mobile as the single source of truth for lead-time math — `standardLeadHours = 4`, `expressLeadHours = 2`, `expressSurchargeRate = 0.20`. Hardcoded for MVP; syncs from backend later via `GET /api/Policy/GetBookingPolicy` (not yet committed — from the Extras+Surcharge spec).
4. Add `isExpressBooking: Boolean = false` to `BookingState` so the live-quote price footer (coupled spec) can reflect the surcharge visually even before the server response returns.
5. Backend remains the authority. `BookingPolicy.cs` (src/Cleansia.Core.AppServices/Authentication/) defines the same constants on the server, and `CreateOrder.Validator` enforces them via `BookingPolicy.RequiresExpressSurcharge`. Mobile-side enforcement is UX-only — if an invalid slot ever leaks through, backend rejects it.

## What this spec does NOT do

- Per-cleaner availability lookup (would need a new `GET /api/Order/AvailableSlots` endpoint that queries the scheduling engine).
- Bank holiday disabling (Czech / Slovak calendars — flagged as follow-up).
- Time-zone handling beyond `currentSystemDefault()` — assumes the user is in their local time.
- Backend-driven policy (hardcoded on mobile for now; switch to remote when `/api/Policy/GetBookingPolicy` lands).
- Per-cleaner busy-slot UX (all standard slots are assumed available today).
- Web/admin: this spec is mobile-only. The customer web order-wizard has its own date picker already — aligning it is a separate spec.

## Ground truth (what exists today)

- **Mobile `WhenWhereStep.kt`**
  - `buildDays()` at line 67 generates `today + 0..7` — 8 days forward.
  - Day chips rendered in `LazyRow` at lines 161-182.
  - Time slots rendered full-width below at lines 187-255.
  - First 2 slots hardcoded `Unavailable`, next 2 `Express`, rest `Available`. All mock.
  - `combineDateAndTime(date, timeLabel)` (added in TASK-BS7) builds a real `Instant` from the picked `LocalDate` + `HH:mm` string.
- **Backend `BookingPolicy.cs:18-30`**
  - `ExpressLeadTimeHours = 2`, `StandardLeadTimeHours = 4`, `ExpressSurchargeRate = 0.20`.
  - `RequiresExpressSurcharge(cleaningDateUtc, now)` returns true when `cleaningDateUtc - now < StandardLeadTime`.
  - Not referenced anywhere under `src/cleansia_android/` — mobile has no idea about lead times today.
- **Extras+Surcharge spec (separate, in progress)** proposes `GET /api/Policy/GetBookingPolicy` returning `{ standardLeadHours, expressLeadHours, expressSurchargeRate }`. Not yet committed. This spec assumes it will land; we reference it via a TODO only.

## Product decisions flagged (need sign-off before execution)

These are defaulted to the recommendation but surfaced explicitly so the owner can override without re-opening the spec.

- **Slot granularity: 1h vs 2h.** Recommend **2h blocks** for MVP — matches real cleaner scheduling reality (most bookings are 2-4h) and keeps the chip grid readable on small screens. Flag for product sign-off.
- **Slot density: 7 slots 07:00-19:00.** Recommend this as the working-hours window. Flag for product sign-off — some markets may want 08:00 start / 20:00 end / skip 13:00.
- **Express surcharge UX: blocking confirmation dialog vs inline warning.** Recommend **blocking dialog, first-time-per-session only**. Reduces complaint/dispute volume later ("I didn't know about the surcharge"). After the user confirms once in a session, subsequent Express taps are silent.
- **Bank holiday disabling.** Defer to post-MVP — needs tenant-configurable calendar (Czech + Slovak). Today Sundays are disabled via the existing `available = false` pattern; keep that.

---

## Phase A — Day selection (14 days + optional far-future picker)

### TASK-DT1: Extend day row to 14 days + "More dates" DatePickerDialog

```yaml
task: Extend day chip range to 14 days and add Material3 DatePickerDialog for 14-60 day window
id: TASK-DT1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Today buildDays() at WhenWhereStep.kt line 67 generates exactly 8 days
  (today + 0..7). We want to widen that to 14 days always visible via
  horizontal scroll, and add a "More dates" pill at the end of the
  LazyRow that opens a Material3 DatePickerDialog for dates 14-60 days
  out. Cap at +60 days so users can't book 2 years in advance.

  Keep the existing Sunday-disabled logic (available = false). Do NOT
  add bank holiday disabling in this task — flagged as post-MVP.

  When the user picks a date via the DatePickerDialog, we do NOT add a
  new chip to the LazyRow. Instead:
    - Write the picked LocalDate into a separate state slot
      (e.g. selectedFarFutureDate: LocalDate?).
    - The "More dates" chip renders in "selected" state with the picked
      date's short label ("May 12") when selectedFarFutureDate != null.
    - Tapping any of the 14 inline chips clears selectedFarFutureDate.
    - Tapping "More dates" again re-opens the dialog with the current
      selection pre-populated.

  This keeps the LazyRow bounded to 14 chips + 1 "More dates" chip —
  predictable scroll distance.

files_to_modify:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    line_range: '65-75'  # buildDays() and the day-count constant
    change: |
      Replace buildDays() body to generate 14 days (today + 0..13).
      The existing `available = false` rule for Sundays stays.

      Extract the number 14 into a `private const val INLINE_DAYS = 14`
      top-of-file so TASK-DT2's tests can read the same constant.

  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    line_range: '161-182'  # the day-chip LazyRow
    change: |
      After the items(...) loop for the 14 inline days, append one more
      item that renders a "More dates" chip. Use an OutlinedCard or
      AssistChip (match the visual style of existing day chips).

      When tapped, set `showDatePickerDialog = true`. Render the dialog
      as a sibling composable after the LazyRow:

        if (showDatePickerDialog) {
            val minMillis = Clock.System.now()
                .plus(14.days)
                .toEpochMilliseconds()
            val maxMillis = Clock.System.now()
                .plus(60.days)
                .toEpochMilliseconds()
            val datePickerState = rememberDatePickerState(
                initialSelectedDateMillis = selectedFarFutureDate?.let {
                    it.atStartOfDayIn(TimeZone.currentSystemDefault())
                      .toEpochMilliseconds()
                },
                selectableDates = object : SelectableDates {
                    override fun isSelectableDate(utcTimeMillis: Long): Boolean =
                        utcTimeMillis in minMillis..maxMillis
                },
            )
            DatePickerDialog(
                onDismissRequest = { showDatePickerDialog = false },
                confirmButton = {
                    TextButton(onClick = {
                        val picked = datePickerState.selectedDateMillis?.let {
                            Instant.fromEpochMilliseconds(it)
                                .toLocalDateTime(TimeZone.currentSystemDefault()).date
                        }
                        if (picked != null) {
                            selectedFarFutureDate = picked
                            // Clear any inline-day selection so state is consistent
                            onDaySelected(picked)
                        }
                        showDatePickerDialog = false
                    }) { Text(stringResource(R.string.booking_date_more_confirm)) }
                },
                dismissButton = {
                    TextButton(onClick = { showDatePickerDialog = false }) {
                        Text(stringResource(android.R.string.cancel))
                    }
                },
            ) {
                DatePicker(state = datePickerState)
            }
        }

      Add `var showDatePickerDialog by remember { mutableStateOf(false) }`
      and `var selectedFarFutureDate by remember { mutableStateOf<LocalDate?>(null) }`
      near the top of the composable. If the ViewModel owns this state
      (BookingViewModel from TASK-BS7), hoist instead — mirror whatever
      pattern the surrounding step uses for selectedDate.

      When an inline-day chip is tapped, clear selectedFarFutureDate.
      When "More dates" renders, it should show "More dates" label if
      selectedFarFutureDate == null, else the short formatted label
      (e.g. "May 12") plus a checkmark / selected-state styling.

dependencies: []
verification:
  - Syntactic check — no gradle wrapper in this project
  - Manual: LazyRow scrolls horizontally through 14 days + 1 "More dates" pill
  - Manual: tap "More dates" → dialog opens, dates 0-13 days out are greyed,
    dates 14-60 out are selectable, dates >60 out are greyed
  - Manual: pick a date 20 days out → "More dates" chip now shows "May 12"
    and is styled as selected; inline chips are all unselected
  - Manual: tap an inline day → "More dates" reverts to default label
```

---

## Phase B — Time slot generation

### TASK-DT2: Replace hardcoded slot list with `generateSlots(date, now)` pure function

```yaml
task: Introduce generateSlots pure function that derives slot state from lead time
id: TASK-DT2
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Today's WhenWhereStep hardcodes 8 slots with fake state (first 2
  Unavailable, next 2 Express, rest Available). Replace with a pure
  function that produces 7 slots (07:00, 09:00, 11:00, 13:00, 15:00,
  17:00, 19:00) and derives each slot's state from BookingPolicy.

  Recommended slot granularity: 2h blocks (matches real cleaner
  scheduling). Recommended window: 07:00-19:00 (12 hours, 7 slots).
  Both flagged for product sign-off — if the owner wants 1h blocks or
  a different window, change the SLOT_HOURS constant before executing.

  Rules:
    - For TODAY:
        * slotInstant < now + expressLeadHours → Unavailable
        * now + expressLeadHours <= slotInstant < now + standardLeadHours → Express
        * slotInstant >= now + standardLeadHours → Available
    - For FUTURE days: all slots → Available
    - Past days should never reach this function; if they do, everything
      returns Unavailable (defensive).

  The function must be pure — no side effects, takes `date: LocalDate`
  and `now: Instant`, returns `List<TimeSlot>`. This makes it a perfect
  unit-test target (see TASK-DT6).

files_to_create:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/TimeSlot.kt
    change: |
      package cz.cleansia.customer.features.booking

      import kotlinx.datetime.Instant

      enum class SlotState { Unavailable, Express, Available }

      data class TimeSlot(
          val label: String,        // "07:00"
          val instant: Instant,     // full wall-clock → instant in currentSystemDefault()
          val state: SlotState,
      )

  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/SlotGenerator.kt
    change: |
      package cz.cleansia.customer.features.booking

      import kotlinx.datetime.Clock
      import kotlinx.datetime.Instant
      import kotlinx.datetime.LocalDate
      import kotlinx.datetime.LocalDateTime
      import kotlinx.datetime.LocalTime
      import kotlinx.datetime.TimeZone
      import kotlinx.datetime.toInstant
      import kotlin.time.Duration.Companion.hours

      /**
       * Working-hours window. Recommend 2h blocks 07:00-19:00 for MVP
       * (7 slots). Flagged for product sign-off — if product chooses 1h
       * blocks, replace with (7..19).toList() and adjust TASK-DT6 cases.
       */
      private val SLOT_HOURS = listOf(7, 9, 11, 13, 15, 17, 19)

      fun generateSlots(
          date: LocalDate,
          now: Instant,
          timeZone: TimeZone = TimeZone.currentSystemDefault(),
          policy: BookingPolicy = BookingPolicy.Default,
      ): List<TimeSlot> {
          val today = now.toLocalDateTime(timeZone).date
          val isPast = date < today
          val isToday = date == today

          val expressThreshold = now.plus(policy.expressLeadHours.hours)
          val standardThreshold = now.plus(policy.standardLeadHours.hours)

          return SLOT_HOURS.map { hour ->
              val slotLdt = LocalDateTime(date, LocalTime(hour, 0))
              val slotInstant = slotLdt.toInstant(timeZone)
              val state = when {
                  isPast -> SlotState.Unavailable
                  !isToday -> SlotState.Available
                  slotInstant < expressThreshold -> SlotState.Unavailable
                  slotInstant < standardThreshold -> SlotState.Express
                  else -> SlotState.Available
              }
              TimeSlot(
                  label = "%02d:00".format(hour),
                  instant = slotInstant,
                  state = state,
              )
          }
      }

files_to_modify:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    line_range: '187-255'  # the hardcoded slot list + rendering
    change: |
      1. DELETE the hardcoded list of 8 slot literals.

      2. Replace with:
           val now = remember { Clock.System.now() }
           val slots = remember(selectedDate) {
               generateSlots(selectedDate.toLocalDate(), now)
           }

         `selectedDate` is whatever BookingState / BookingViewModel
         exposes as the picked LocalDate. If it's a String today, adjust
         TASK-DT1 to carry a real LocalDate and use that here.

      3. Render one Row/LazyVerticalGrid per slot with the new state.
         TASK-DT4 handles the express / unavailable visual polish —
         for this task, accept plain styling (e.g. greyed text for
         Unavailable, tinted background for Express).

      4. Slot click handler:
           if (slot.state == SlotState.Unavailable) return@clickable
           onSlotSelected(slot)    // viewmodel writes selectedInstant

         The ViewModel layer then sets:
           state.copy(
               selectedInstant = slot.instant,
               isExpressBooking = slot.state == SlotState.Express,
           )

         isExpressBooking is new — TASK-DT5 adds it to BookingState.

      5. The existing combineDateAndTime(date, timeLabel) helper (added
         in TASK-BS7) is no longer needed — slots now carry their own
         precomputed Instant. Remove the helper OR keep it as a no-op
         wrapper that delegates to slot.instant. Prefer remove if no
         other call site references it — grep first.

dependencies:
  - TASK-DT3  # BookingPolicy must exist
verification:
  - Syntactic check
  - Manual: pick today at 8am local → first slot (07:00) Unavailable,
    09:00 Express, 11:00 Express (still inside 4h window? depends on
    run time — verify against the formula, not a fixed expectation),
    all later slots Available
  - Manual: pick tomorrow → all 7 slots Available
  - Manual: pick 5 days out → all 7 slots Available
```

### TASK-DT3: Introduce `BookingPolicy.kt` on mobile

```yaml
task: Mobile-side BookingPolicy data class mirroring backend constants
id: TASK-DT3
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Single source of truth for lead-time math on mobile. Mirror the
  backend constants from src/Cleansia.Core.AppServices/Authentication/BookingPolicy.cs
  (StandardLeadTimeHours = 4, ExpressLeadTimeHours = 2,
  ExpressSurchargeRate = 0.20).

  Hardcoded for MVP. When the Extras+Surcharge spec lands its
  GET /api/Policy/GetBookingPolicy endpoint, a follow-up task will
  fetch this from the server at app start and override BookingPolicy.Default.
  For now, the TODO comment makes the follow-up obvious.

files_to_create:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingPolicy.kt
    change: |
      package cz.cleansia.customer.features.booking

      /**
       * Lead-time + surcharge policy for booking. Mirrors backend
       * BookingPolicy constants (src/Cleansia.Core.AppServices/Authentication/BookingPolicy.cs).
       *
       * TODO: sync from backend via GET /api/Policy/GetBookingPolicy
       * when that endpoint exists (see Extras+Surcharge spec). For MVP
       * we hardcode and trust the server to reject invalid slots at
       * CreateOrder time — the backend is always the authority.
       */
      data class BookingPolicy(
          val standardLeadHours: Int = 4,
          val expressLeadHours: Int = 2,
          val expressSurchargeRate: Double = 0.20,
      ) {
          companion object {
              val Default = BookingPolicy()
          }
      }

dependencies: []
verification:
  - Syntactic check
  - Constants match backend BookingPolicy.cs exactly (manual diff)
```

---

## Phase C — State + UI polish

### TASK-DT4: Slot UI — Express badge + Unavailable disabled + first-time dialog

```yaml
task: Visual polish for Express (+20% pill, confirm dialog) and Unavailable (greyed, disabled) slots
id: TASK-DT4
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  TASK-DT2 renders slots with plain styling. This task adds the
  production-quality visual treatment:

  - Express slot chip:
      * Label "09:00" + small pill/badge next to it showing
        stringResource(R.string.booking_slot_express_hint) → "+20%"
      * Tint the pill orange-ish (Color(0xFFEA580C) or the Material
        warning container) to draw attention.
      * Optional small "Express" sublabel under the time — use
        R.string.booking_slot_express_badge.

  - Unavailable slot chip:
      * Greyed background (e.g. MaterialTheme.colorScheme.surfaceVariant
        at alpha 0.4f).
      * Non-tappable — clickable { } either absent or early-returns.
      * Text alpha 0.4f. Optional small label "Not available" from
        R.string.booking_slot_unavailable — decide based on visual
        density.

  - First-time Express confirmation dialog:
      * State: `var expressConfirmedThisSession by remember { mutableStateOf(false) }`
      * When user taps an Express slot AND !expressConfirmedThisSession:
          - Open a Material3 AlertDialog with:
              title:  R.string.booking_slot_express_dialog_title
              text:   R.string.booking_slot_express_dialog_message
              confirm: R.string.booking_slot_express_dialog_confirm → sets
                       expressConfirmedThisSession = true and proceeds
                       with slot selection
              dismiss: standard "Cancel" (android.R.string.cancel) →
                       do nothing, do NOT mark confirmed
      * When user taps an Express slot AND expressConfirmedThisSession:
          - Skip the dialog, proceed directly with selection.
      * Scope: "session" = lifetime of the booking flow composition
        (the remember survives until the bottom sheet is dismissed).
        Re-opening the booking sheet starts a fresh session.

files_to_modify:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/WhenWhereStep.kt
    change: |
      1. Extract the slot chip into a small @Composable SlotChip(slot, onClick).
         Internally switches on slot.state for background, alpha, badge
         visibility. Keep the file tidy — can be a top-level private
         composable.

      2. Add the expressConfirmedThisSession state variable and the
         AlertDialog as described above.

      3. The onSlotSelected(slot) callback becomes:
           when (slot.state) {
               SlotState.Unavailable -> { /* should never fire, chip is disabled */ }
               SlotState.Available -> selectSlot(slot)
               SlotState.Express -> {
                   if (expressConfirmedThisSession) selectSlot(slot)
                   else pendingExpressSlot = slot   // triggers dialog
               }
           }

         `private fun selectSlot(slot: TimeSlot)` writes to
         BookingState.selectedInstant + isExpressBooking (TASK-DT5).

      4. When the dialog confirms, set expressConfirmedThisSession = true,
         call selectSlot(pendingExpressSlot!!), clear pendingExpressSlot.

      5. Dialog state vars:
           var pendingExpressSlot by remember { mutableStateOf<TimeSlot?>(null) }
           var expressConfirmedThisSession by remember { mutableStateOf(false) }

dependencies:
  - TASK-DT2
  - TASK-DT7  # strings
verification:
  - Manual: Express slot shows "09:00 +20%" with orange-ish pill
  - Manual: first tap on any Express slot shows the dialog
  - Manual: after confirming the dialog once, subsequent Express taps
    in the same sheet don't show it again
  - Manual: dismiss the dialog → slot is NOT selected, state unchanged
  - Manual: Unavailable slot is visibly greyed and ignores taps
```

### TASK-DT5: Pipe `isExpressBooking` into `BookingState` + BookingViewModel

```yaml
task: Add isExpressBooking to BookingState so price footer can reflect surcharge
id: TASK-DT5
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Today BookingState carries `selectedInstant: Instant?` (added in
  TASK-BS7) but nothing indicates whether the chosen slot is Express.
  The live-quote spec (coupled) and the confirm-step summary need to
  reflect the surcharge visually.

  Backend still does the authoritative math — this flag is UX-only.
  When isExpressBooking = true, the confirm screen can show a line
  item "Express surcharge +20%" alongside the base total.

files_to_modify:
  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingState.kt
    change: |
      Add next to selectedInstant:
        val isExpressBooking: Boolean = false

      Invariant: when selectedInstant is null, isExpressBooking must be
      false. Enforce at the only write site (WhenWhereStep slot-tap
      handler) — no need for a setter guard.

  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    change: |
      Add convenience setter that both fields change atomically:

        fun onSlotSelected(slot: TimeSlot) {
            _state.update { s ->
                s.copy(
                    selectedInstant = slot.instant,
                    isExpressBooking = slot.state == SlotState.Express,
                )
            }
        }

      WhenWhereStep's selectSlot(slot) helper (from TASK-DT4) calls
      viewModel.onSlotSelected(slot).

  - path: src/cleansia_android/app/src/main/java/cz/cleansia/customer/features/booking/SummaryStep.kt
    # Or wherever the confirm/summary screen lives — grep for
    # "confirm" / "summary" under features/booking/. If the summary
    # is still the live-quote-display placeholder, that's fine —
    # this task just exposes the flag; rendering the surcharge line
    # is the live-quote spec's responsibility.
    change: |
      OPTIONAL (coordinated with the live-quote spec): if the confirm
      step already displays the price footer, read isExpressBooking
      from state and when true render a small "Express surcharge (+20%)"
      row under the services subtotal.

      If the live-quote spec owns the price footer entirely, skip
      this modify block — just exposing the flag is enough for that
      spec to pick up.

dependencies:
  - TASK-DT2
  - TASK-DT4
verification:
  - Pick an Available slot → state.isExpressBooking == false
  - Pick an Express slot (confirm the dialog) → state.isExpressBooking == true
  - Switch back to an Available slot → isExpressBooking flips back to false
```

---

## Phase D — Tests + strings

### TASK-DT6: Unit tests for `generateSlots`

```yaml
task: Unit tests for the pure slot-generation function
id: TASK-DT6
type: test
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  generateSlots(date, now) is a pure function — perfect unit-test
  target. Covers the core lead-time behavior without needing the full
  Compose / Hilt harness.

  PRE-CHECK: grep under src/cleansia_android/app/src/test/ for any
  existing Kotlin unit tests. If the app has no test harness set up
  (no src/test directory, no junit dependency in build.gradle.kts),
  FLAG this task as no-test and move on. Do not add a test harness in
  this task — that's a separate infra task.

  If a harness exists, add the test file below.

files_to_create:
  - path: src/cleansia_android/app/src/test/java/cz/cleansia/customer/features/booking/SlotGeneratorTest.kt
    change: |
      package cz.cleansia.customer.features.booking

      import kotlinx.datetime.LocalDate
      import kotlinx.datetime.LocalDateTime
      import kotlinx.datetime.TimeZone
      import kotlinx.datetime.toInstant
      import org.junit.Assert.assertEquals
      import org.junit.Test

      class SlotGeneratorTest {
          private val utc = TimeZone.UTC

          private fun instantAt(year: Int, month: Int, day: Int, hour: Int, minute: Int = 0) =
              LocalDateTime(year, month, day, hour, minute).toInstant(utc)

          @Test
          fun `all future day slots are Available`() {
              val now = instantAt(2026, 5, 1, 12)
              val future = LocalDate(2026, 5, 5)
              val slots = generateSlots(future, now, utc)
              assertEquals(7, slots.size)
              assertEquals(List(7) { SlotState.Available }, slots.map { it.state })
          }

          @Test
          fun `past day slots are all Unavailable`() {
              val now = instantAt(2026, 5, 10, 12)
              val past = LocalDate(2026, 5, 5)
              val slots = generateSlots(past, now, utc)
              assertEquals(List(7) { SlotState.Unavailable }, slots.map { it.state })
          }

          @Test
          fun `today slots before express threshold are Unavailable`() {
              // Now = 08:00, expressLead = 2h → 10:00, standardLead = 4h → 12:00
              // Slots: 07 < 10 Unavailable, 09 < 10 Unavailable,
              //        11 in [10,12) Express, 13 >= 12 Available, ...
              val now = instantAt(2026, 5, 1, 8)
              val today = LocalDate(2026, 5, 1)
              val slots = generateSlots(today, now, utc)
              assertEquals(SlotState.Unavailable, slots[0].state)  // 07:00
              assertEquals(SlotState.Unavailable, slots[1].state)  // 09:00
              assertEquals(SlotState.Express,      slots[2].state) // 11:00
              assertEquals(SlotState.Available,    slots[3].state) // 13:00
              assertEquals(SlotState.Available,    slots[4].state) // 15:00
              assertEquals(SlotState.Available,    slots[5].state) // 17:00
              assertEquals(SlotState.Available,    slots[6].state) // 19:00
          }

          @Test
          fun `slot exactly at express threshold is Express`() {
              // Now = 07:00, express threshold = 09:00, standard = 11:00
              // 09:00 slot lies exactly at expressThreshold — NOT <, so Express.
              val now = instantAt(2026, 5, 1, 7)
              val today = LocalDate(2026, 5, 1)
              val slots = generateSlots(today, now, utc)
              assertEquals(SlotState.Express, slots[1].state) // 09:00
          }

          @Test
          fun `slot exactly at standard threshold is Available`() {
              // Now = 07:00, standard threshold = 11:00
              // 11:00 slot is NOT < 11:00, so Available.
              val now = instantAt(2026, 5, 1, 7)
              val today = LocalDate(2026, 5, 1)
              val slots = generateSlots(today, now, utc)
              assertEquals(SlotState.Available, slots[2].state) // 11:00
          }

          @Test
          fun `slot label formatting is zero-padded HH colon 00`() {
              val now = instantAt(2026, 5, 1, 0)
              val today = LocalDate(2026, 5, 2)
              val labels = generateSlots(today, now, utc).map { it.label }
              assertEquals(
                  listOf("07:00", "09:00", "11:00", "13:00", "15:00", "17:00", "19:00"),
                  labels,
              )
          }
      }

dependencies:
  - TASK-DT2
  - TASK-DT3
verification:
  - If harness exists: run the test task (gradlew :app:testDebugUnitTest
    from Android Studio — no gradle wrapper exists in this repo today
    per the companion specs)
  - If no harness: flag with a PR comment and skip — do not add junit
    infra in this task
```

### TASK-DT7: Mobile strings — 5 locales

```yaml
task: Add booking date/slot strings to values/ and all 4 locale folders
id: TASK-DT7
type: feature
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  All 5 locales supported by the app: en (values/), cs (values-cs/),
  sk (values-sk/), uk (values-uk/), ru (values-ru/).

  Keys added:
    - booking_date_more_dates       ("More dates")
    - booking_date_more_confirm     ("Select")
    - booking_slot_express_badge    ("Express")
    - booking_slot_express_hint     ("+20%")
    - booking_slot_express_dialog_title   ("Fast booking surcharge")
    - booking_slot_express_dialog_message ("This time slot is within 4 hours of now. A 20% express surcharge will be added to your total.")
    - booking_slot_express_dialog_confirm ("Got it")
    - booking_slot_unavailable      ("Not available")

  Note on the message string: it hardcodes "4 hours". If product later
  tunes standardLeadHours to a different value, this copy goes stale.
  For MVP, keep it hardcoded (matches backend constant). Add a code
  comment in strings.xml:
    <!-- When BookingPolicy.standardLeadHours changes, update this copy -->

files_to_modify:
  - path: src/cleansia_android/app/src/main/res/values/strings.xml
    change: |
      Add all 8 keys above in English.

  - path: src/cleansia_android/app/src/main/res/values-cs/strings.xml
    change: |
      Add Czech translations:
        booking_date_more_dates          → "Další termíny"
        booking_date_more_confirm        → "Vybrat"
        booking_slot_express_badge       → "Expres"
        booking_slot_express_hint        → "+20 %"
        booking_slot_express_dialog_title   → "Příplatek za rychlou rezervaci"
        booking_slot_express_dialog_message → "Tento termín je do 4 hodin od nynějška. K celkové částce bude připočten 20% expresní příplatek."
        booking_slot_express_dialog_confirm → "Rozumím"
        booking_slot_unavailable         → "Nedostupné"

  - path: src/cleansia_android/app/src/main/res/values-sk/strings.xml
    change: |
      Add Slovak translations:
        booking_date_more_dates          → "Ďalšie termíny"
        booking_date_more_confirm        → "Vybrať"
        booking_slot_express_badge       → "Expres"
        booking_slot_express_hint        → "+20 %"
        booking_slot_express_dialog_title   → "Príplatok za rýchlu rezerváciu"
        booking_slot_express_dialog_message → "Tento termín je do 4 hodín od teraz. K celkovej sume bude pripočítaný 20% expresný príplatok."
        booking_slot_express_dialog_confirm → "Rozumiem"
        booking_slot_unavailable         → "Nedostupné"

  - path: src/cleansia_android/app/src/main/res/values-uk/strings.xml
    change: |
      Add Ukrainian translations:
        booking_date_more_dates          → "Інші дати"
        booking_date_more_confirm        → "Вибрати"
        booking_slot_express_badge       → "Експрес"
        booking_slot_express_hint        → "+20%"
        booking_slot_express_dialog_title   → "Доплата за термінове замовлення"
        booking_slot_express_dialog_message → "Цей час наступить менш ніж за 4 години. До загальної суми буде додано 20% експрес-доплати."
        booking_slot_express_dialog_confirm → "Зрозуміло"
        booking_slot_unavailable         → "Недоступно"

  - path: src/cleansia_android/app/src/main/res/values-ru/strings.xml
    change: |
      Add Russian translations:
        booking_date_more_dates          → "Другие даты"
        booking_date_more_confirm        → "Выбрать"
        booking_slot_express_badge       → "Экспресс"
        booking_slot_express_hint        → "+20%"
        booking_slot_express_dialog_title   → "Доплата за срочное бронирование"
        booking_slot_express_dialog_message → "Это время наступит менее чем через 4 часа. К общей сумме будет добавлена 20% экспресс-доплата."
        booking_slot_express_dialog_confirm → "Понятно"
        booking_slot_unavailable         → "Недоступно"

dependencies: []
verification:
  - All 5 strings.xml files parse (Android Studio resource check)
  - Runtime: change device language to each of cs/sk/uk/ru → booking
    step renders the translated chip labels and dialog copy
```

---

## Execution order

1. **TASK-DT1** (14-day row + "More dates" dialog) — mobile, no deps
2. **TASK-DT3** (BookingPolicy.kt) — mobile, no deps; parallelizable with DT1
3. **TASK-DT2** (generateSlots + replace hardcoded slots) — mobile, depends on DT3

   → DT1 + DT3 in parallel first, then DT2 consumes DT3.

4. **TASK-DT4** (slot UI polish + Express dialog) — mobile, depends on DT2 + DT7
5. **TASK-DT5** (isExpressBooking on BookingState + VM) — mobile, depends on DT2 + DT4
6. **TASK-DT7** (strings in 5 locales) — mobile, no deps; parallelizable with any task but DT4 needs it wired

   → DT4 + DT5 after DT2 is merged; DT7 lands at any time but ideally before DT4 so `stringResource` lookups resolve during DT4 manual testing.

7. **TASK-DT6** (unit tests) — mobile, depends on DT2 + DT3; standalone, can land last

Parallelizable: DT1 + DT3 together; DT7 any time; DT4 + DT5 after DT2; DT6 at the end.

Estimated tokens: ~45k total.

---

## MANUAL_STEPs for the owner

- **No backend migration required** — this is a mobile-only spec; `BookingPolicy.cs` already exists on the server.
- **No NSwag regen required** — no backend DTOs or endpoints change in this spec. The future `GET /api/Policy/GetBookingPolicy` endpoint is owned by the Extras+Surcharge spec and will trigger its own NSwag regen when it lands.
- **Manual test on device**: once DT1-DT7 land, do a full pass on a physical device or emulator for each of the 5 locales to verify chip labels and the Express confirmation dialog render correctly.

---

## Out of scope (followup specs)

- **Per-cleaner availability lookup** — needs a new `GET /api/Order/AvailableSlots` endpoint that queries the scheduling engine and returns per-day busy-slot arrays. Mobile would then grey out slots that overlap with existing assignments. Requires backend scheduling-engine work; significant.
- **Bank holiday disabling** — Czech + Slovak calendars. Tenant-configurable (some tenants may want to operate on holidays). Needs a new `HolidayCalendar` entity or a simple `IsWorkingDay` service. Mobile then calls `GET /api/Calendar/WorkingDays?from=...&to=...` and greys disabled days.
- **Time-zone handling** — today we use `TimeZone.currentSystemDefault()` everywhere. For multi-country tenants where the cleaner is in a different tz than the customer, we'll need to decide whose tz is authoritative. MVP punts.
- **Backend-driven policy** — when the Extras+Surcharge spec lands `GET /api/Policy/GetBookingPolicy`, add a small `PolicyRepository` on mobile that fetches at app start and overrides `BookingPolicy.Default`. Keeps local + remote in sync without a release.
- **Web order-wizard alignment** — the customer web wizard has its own date picker. Aligning it to the same lead-time rules (and the same surcharge UX) is a separate frontend spec.
- **Express surcharge in price math** — `BookingPolicy.ExpressSurchargeRate` (20%) is referenced in `CreateOrder.Validator` for lead-time gating but may not yet apply the +20% to the priced total. Audit during the Extras+Surcharge spec's execution; if the server doesn't multiply, fix there.
