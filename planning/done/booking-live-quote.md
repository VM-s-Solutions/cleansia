# Booking Live Quote — show server-authoritative total while editing

**Status:** Ready for execution
**Depends on:** `mobile-booking-submission.md` complete (TASK-BS2 backend `/api/Order/Quote` endpoint live, TASK-BS7 `BookingViewModel` + `BookingApi.quote(...)` wired for submit-time quote-then-create flow)

## Decisions in scope for this spec

1. **Mobile only.** Web order wizard still submits via `CreateOrder` directly today; giving it the same live-quote treatment is a separate follow-up. Scope here is strictly the native Android customer app.
2. **Debounced live refresh** — as the user toggles services/packages or adjusts rooms/bathrooms, a 400 ms debounce fires a `POST /api/Order/Quote` and updates the displayed total.
3. **Silent failure during editing** — transient network or validator errors while the user is still editing do NOT surface a snackbar. The previous known-good quote stays on screen. A mid-edit error should never interrupt the tap flurry; real errors surface at submit time (that path is already handled by TASK-BS7).
4. **Cache reuse at submit** — if the cached quote's inputs match current state when slide-to-confirm fires, skip the redundant `/Quote` call. This saves a round trip and guarantees the number the user saw is the number submitted. Fall back to an ad-hoc quote call if the cache is stale or empty.
5. **Guest-friendly** — the backend `/Quote` endpoint is `[AllowAnonymous]`, so live quotes work before sign-in. The sign-in gate stays on submit only.

## What this spec does NOT do

- Touch the backend. `/api/Order/Quote` is already live.
- Touch the web order wizard (follow-up spec).
- Cancel in-flight quote requests when a new one supersedes them. `collectLatest` on the input flow is enough for MVP — Retrofit will complete the stale call but its result is ignored. Adding explicit cancellation is a micro-optimization.
- Retry on reconnect. If the device is offline and the user keeps editing, the displayed quote simply goes stale. Reconnect + next edit triggers a fresh quote.
- Surface subtotals (`servicesSubtotal` / `packagesSubtotal`) in the footer UI. Total only for this pass; breakdown can land in a visual-polish spec.
- Change currency handling. Quote is sent with `currencyId = null` (server default = CZK for MVP). If a future UI lets users pick currency, the flow already reacts because `currencyId` is part of the derived input.

---

## Phase 1 — Mobile live quote

### TASK-LQ1: Add `quote` + `quoting` state on BookingViewModel + debounced input flow

```yaml
task: Expose live quote state; wire a debounced input flow to /api/Order/Quote
id: TASK-LQ1
type: feature
priority: high
specialist: mobile
app: customer-android
estimated_complexity: medium
recommended_model: sonnet

context: |
  Today BookingViewModel only tracks _submitting and calls /Quote at
  submit time. We want the bottom-sheet footer to show a live,
  server-authoritative total while the user is still building their
  order — no client-side drift, no stale estimates.

  The input that matters is a tuple of (selectedServiceIds,
  selectedPackageIds, rooms, bathrooms). Derive a flow over
  `_state` that emits only when one of those four fields changes
  (distinctUntilChanged on a small data class), debounce ~400 ms so
  chip-tap flurries collapse into a single network call, then
  collectLatest into refreshQuote().

  State shape:
    - `quote: StateFlow<QuoteOrderResponse?>` — null when no services
      AND no packages are selected, or before the first quote lands.
      Otherwise holds the latest server response.
    - `quoting: StateFlow<Boolean>` — true while a /Quote call is in
      flight. Used by the footer to show a small spinner.

  Behavior:
    - On empty input (no services AND no packages), set `_quote = null`
      and do NOT issue a network call. This is the placeholder-dash
      state in the footer.
    - Keep the previous `_quote` value across in-flight requests.
      Only clear on empty input. Transient failures must not wipe the
      last known-good total from the UI.
    - Failures are silent during editing:
        * Network / 5xx / timeout → log at debug, leave `_quote` alone.
        * 4xx ProblemDetails (e.g. InvalidSelectedServices if a service
          id was deleted server-side) → also silent. The submit path
          in TASK-BS7 surfaces this properly; mid-edit snackbars would
          spam the user during toggling.
    - `quoting` flips true the moment a call starts and false when it
      completes (success OR failure), inside a try/finally.

  Currency: pass `null` (server-default) for MVP. When the UI gains a
  currency picker, add it to the derived input tuple and the flow
  reacts automatically.

  Keep TASK-BS7's existing `refreshQuote()` method — but rework its
  failure semantics to match the "silent during editing" rule above
  (today it already swallows errors silently per TASK-BS7's spec, but
  also does not manage a `quoting` flag — add that here).

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    line_range: 'around the @HiltViewModel class body — state declarations + init block'
    change: |
      1. Add a `quoting` StateFlow next to `quote`:
           private val _quoting = MutableStateFlow(false)
           val quoting: StateFlow<Boolean> = _quoting.asStateFlow()

      2. Replace refreshQuote() body with a version that manages the
         `_quoting` flag and preserves the previous quote on failure:

           private suspend fun refreshQuote() {
               val s = _state.value
               if (s.selectedServiceIds.isEmpty() && s.selectedPackageIds.isEmpty()) {
                   _quote.value = null
                   return
               }
               _quoting.value = true
               try {
                   val resp = runCatching {
                       bookingApi.quote(QuoteOrderCommand(
                           selectedServiceIds = s.selectedServiceIds.toList(),
                           selectedPackageIds = s.selectedPackageIds.toList(),
                           rooms = s.rooms,
                           bathrooms = s.bathrooms,
                           currencyId = null,
                       ))
                   }.getOrNull()
                   if (resp?.isSuccessful == true) {
                       _quote.value = resp.body()
                   }
                   // Failures are silent during editing — leave _quote as is.
               } finally {
                   _quoting.value = false
               }
           }

      3. Add a small private data class to key the derived input flow:

           private data class QuoteInputs(
               val serviceIds: Set<String>,
               val packageIds: Set<String>,
               val rooms: Int,
               val bathrooms: Int,
           )

           private fun BookingState.toQuoteInputs() = QuoteInputs(
               serviceIds = selectedServiceIds,
               packageIds = selectedPackageIds,
               rooms = rooms,
               bathrooms = bathrooms,
           )

      4. Add an init block that wires the flow. MUST come after the
         state flows are constructed:

           init {
               viewModelScope.launch {
                   _state
                       .map { it.toQuoteInputs() }
                       .distinctUntilChanged()
                       .debounce(400L)
                       .collectLatest { refreshQuote() }
               }
           }

         Imports:
           import kotlinx.coroutines.flow.debounce
           import kotlinx.coroutines.flow.distinctUntilChanged
           import kotlinx.coroutines.flow.map
           import kotlinx.coroutines.flow.collectLatest
           import kotlinx.coroutines.launch

         `debounce` is a FlowPreview API in older coroutines releases;
         if the build raises an opt-in warning, annotate the init
         lambda (or the class) with @OptIn(FlowPreview::class). Do not
         silence at module level.

         NOTE: collectLatest means when inputs change mid-flight, the
         in-progress refreshQuote() coroutine is cancelled. Because the
         only suspension point is the Retrofit call, Retrofit will
         attempt to cancel the underlying request. If the request
         already completed on the wire, its result is simply dropped.
         Either outcome is correct — the newer input's refresh will
         overwrite `_quote` shortly.

      5. Do NOT change the submit() cache-reuse logic here — that's
         TASK-LQ3. This task stops at "the state flows exist and
         refresh on edit."

dependencies: []
verification:
  - Syntactic check — no gradle wrapper in this project
  - Manual (once running): toggle a service chip; within ~400ms the
    footer's quote updates. Toggle several chips quickly; only one
    network request fires after the burst settles.
  - Manual: unplug WiFi mid-edit; previous total stays displayed; no
    snackbar fires.
  - Manual: deselect all services + packages; footer shows the empty
    placeholder; no network call.
```

### TASK-LQ2: Rewire BookingBottomSheet footer to read `vm.quote`

```yaml
task: Replace BS7 local-sum fallback at BookingBottomSheet.kt:331 with live quote display
id: TASK-LQ2
type: refactor
priority: high
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  BookingBottomSheet.kt:331 currently computes a local fallback total
  via something like `state.rooms * perRoom * services + packages`,
  tagged with a `// TODO(BS7)` comment left over from when the quote
  endpoint didn't exist. Now it does. Kill the fallback, show the
  server total.

  Three display states for the footer total:
    1. `quoting == true && quote == null`   → small CircularProgressIndicator
       (first-time load, before any quote has landed)
    2. `quote != null`                      → formatted `quote.totalPrice` with currency symbol
    3. else                                 → `—` placeholder (empty selection)

  Note: when `quoting == true && quote != null`, show the previous
  quote's amount (no spinner overlay). Ephemeral loading state during
  refresh is not worth a visual flicker; the debounce already smooths
  this. If a visual cue is wanted later, a tiny dot or faded style
  can be added without changing the logic here.

  Currency formatting: for MVP everything is CZK. Use a simple helper
  that takes the `currencyId` string and the numeric total and returns
  a display string. If `currencyId` is unknown, fall back to showing
  the raw number with no symbol. A proper currency formatter using
  NumberFormat.getCurrencyInstance(Locale) can be added when the
  picker lands; not needed now.

  Example helper (file-private):

      private fun formatQuotedTotal(total: Double, currencyId: String): String {
          val symbol = when (currencyId.uppercase()) {
              "CZK" -> "Kč"
              "EUR" -> "€"
              "USD" -> "$"
              else -> currencyId
          }
          // Czech convention: symbol follows the number, e.g. "1 234 Kč".
          // Keep it simple — no thousands separator for MVP unless the
          // rest of the app already uses one (grep before adding).
          return "%.0f %s".format(total, symbol)
      }

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingBottomSheet.kt
    line_range: '~320-345 — the footer total composable, around line 331'
    change: |
      1. At the top of the composable that renders the footer, add:

           val quote by bookingVm.quote.collectAsState()
           val quoting by bookingVm.quoting.collectAsState()

         (bookingVm is the hiltViewModel<BookingViewModel>() introduced
         in TASK-BS7.)

      2. DELETE the `// TODO(BS7)` local-sum block (roughly the lines
         computing `total = rows * ... + packages...` or equivalent).
         Grep for `TODO(BS7)` to locate the exact span.

      3. Replace the Text(total) invocation with a small branching
         composable:

           when {
               quote != null -> {
                   Text(
                       text = formatQuotedTotal(quote!!.totalPrice, quote!!.currencyId),
                       style = MaterialTheme.typography.titleLarge,
                       fontWeight = FontWeight.SemiBold,
                   )
               }
               quoting -> {
                   CircularProgressIndicator(
                       modifier = Modifier.size(20.dp),
                       strokeWidth = 2.dp,
                   )
               }
               else -> {
                   Text(
                       text = "—",
                       style = MaterialTheme.typography.titleLarge,
                       color = MaterialTheme.colorScheme.onSurfaceVariant,
                   )
               }
           }

      4. Add the formatQuotedTotal() helper near the bottom of the
         file (private, file-scoped).

      5. If the footer previously showed a "Total" label string from
         strings.xml (e.g. `booking_total_label`), keep it — just
         pair it with the new value composable.

      6. Imports to add if missing:
           import androidx.compose.runtime.collectAsState
           import androidx.compose.runtime.getValue
           import androidx.compose.material3.CircularProgressIndicator
           import androidx.compose.ui.unit.dp
           import androidx.compose.foundation.layout.size

dependencies:
  - TASK-LQ1
verification:
  - Syntactic check
  - Manual: open the sheet with no selections — footer shows "—".
  - Manual: pick one service — spinner briefly visible (~400 ms debounce
    + network latency), then real price appears.
  - Manual: add a second service — price updates; during the call
    the previous price stays visible (no spinner flash, no layout jump).
  - Manual: clear everything — footer reverts to "—".
```

### TASK-LQ3: Reuse cached quote in submit() when inputs match

```yaml
task: Skip redundant /Quote on submit if cached quote matches current state
id: TASK-LQ3
type: refactor
priority: medium
specialist: mobile
app: customer-android
estimated_complexity: small
recommended_model: sonnet

context: |
  Today's submit() (from TASK-BS7) unconditionally issues a /Quote
  call right before /Create. With live quoting from TASK-LQ1, the
  most recently displayed quote is usually still valid at
  slide-to-confirm time. Save the round trip AND guarantee the user
  submits exactly the number they saw.

  Logic:
    1. Snapshot the current state's (serviceIds, packageIds, rooms,
       bathrooms) into a QuoteInputs just like TASK-LQ1's helper.
    2. If `_quote.value != null` AND its originating inputs match the
       current snapshot, use the cached quote. Skip the network call.
    3. Otherwise, fall back to the existing behavior — issue a fresh
       /Quote call inline. On failure here (not during editing),
       surface the snackbar as before.

  To compare "originating inputs" against the current snapshot we
  need to remember what inputs produced `_quote.value`. Add a second
  MutableStateFlow (or a private `var` on the ViewModel, since it's
  an implementation detail, not observed by UI) that captures the
  inputs used for the most recent successful response:

      private var lastQuoteInputs: QuoteInputs? = null

  Update it inside refreshQuote() on a successful response, right
  after `_quote.value = resp.body()`:

      lastQuoteInputs = s.toQuoteInputs()

  Clear it when `_quote` is cleared on empty input:

      _quote.value = null
      lastQuoteInputs = null

  In submit(), before the existing quote call:

      val currentInputs = s.toQuoteInputs()
      val cached = _quote.value
      val quoted: QuoteOrderResponse = if (cached != null && lastQuoteInputs == currentInputs) {
          cached
      } else {
          val resp = runCatching { bookingApi.quote(quoteCmd) }.getOrNull()
          if (resp == null || !resp.isSuccessful) {
              val msg = if (resp != null) {
                  ApiErrorParser.parseToUserMessage(context, resp.errorBody(), resp.code())
              } else context.getString(R.string.error_generic_network)
              snackbar.showError(msg)
              return null
          }
          resp.body()!!
      }

  Everything downstream of `quoted` in submit() stays exactly the same.
  This is a purely additive change + one extracted branch.

  Edge case: if a live-quote call is in flight right at the moment
  the user hits slide-to-confirm (so lastQuoteInputs may or may not
  match yet), submit()'s fallback path just issues its own call. No
  coordination needed — worst case we pay for one extra quote, which
  is what we do today anyway.

files_to_modify:
  - path: src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/booking/BookingViewModel.kt
    line_range: 'inside submit(); also inside refreshQuote()'
    change: |
      1. Add the private field near the other state declarations:

           private var lastQuoteInputs: QuoteInputs? = null

      2. In refreshQuote():
         - On empty-input branch (where `_quote.value = null`), also
           set `lastQuoteInputs = null`.
         - On successful response, after `_quote.value = resp.body()`,
           add `lastQuoteInputs = s.toQuoteInputs()`.

      3. In submit(), replace the existing quote-call block with the
         cache-then-fallback version shown above. Preserve all error
         handling for the fallback path — submit-time errors are
         still loud (snackbar + return null).

dependencies:
  - TASK-LQ1
verification:
  - Syntactic check
  - Manual: pick services, wait for live quote to land, slide to
    confirm. Network tab shows ONLY /api/Order/Create, no /Quote
    (cache hit).
  - Manual: pick services, wait for quote, change rooms from 1 to 2,
    IMMEDIATELY slide to confirm before debounce fires. Network tab
    shows /Quote then /Create (cache miss — rooms changed; lastQuoteInputs
    stale).
  - Manual: airplane mode, slide to confirm. Fallback fires; snackbar
    surfaces "network error". submit() returns null cleanly.
  - Manual: select nothing, slide to confirm. The existing validator
    in submit() rejects before the cache/fallback branch reaches
    anything (state is empty). No regression.
```

---

## Execution order

1. **TASK-LQ1** — ViewModel state + debounced input flow. Foundation; everything depends on this.
2. **TASK-LQ2** — Footer UI rewire. Depends on LQ1's `quote`/`quoting` flows existing.
3. **TASK-LQ3** — submit() cache reuse. Depends on LQ1's `lastQuoteInputs` tracking.

LQ2 and LQ3 are independent of each other and can run in parallel once LQ1 lands.

Estimated tokens: ~15k total.

---

## Out of scope (followup specs)

- **Web order wizard live quote** — `libs/cleansia-customer-features/order-wizard` currently submits via `CreateOrder` directly with a client-computed total. Mirror this spec on web: add `/Quote` to the customer TypeScript client (already regenerated after TASK-BS2 per mobile spec's manual step), expose `quote$`/`quoting$` signals on the wizard facade, debounce via `rxjs.debounceTime(400)`, and replace the wizard-summary-step.component's local total with the server value.
- **Cancel in-flight quotes explicitly** — `collectLatest` handles this implicitly, but an explicit `Job.cancel()` + a cancellable OkHttp call would drop bytes on the wire when a newer edit supersedes an older one. Worth it only if analytics shows significant wasted traffic.
- **Subtotals in the footer** — display `servicesSubtotal` / `packagesSubtotal` as a small breakdown line under the total. Product-driven — not strictly needed yet.
- **Currency picker integration** — when the UI gains a currency selector, feed it into the QuoteInputs tuple and into the QuoteOrderCommand. The plumbing is already currency-aware; the selector itself is the only missing piece.
- **Retry on reconnect** — observe ConnectivityManager; on reconnect, if `_quote` is null and state is non-empty, re-fire refreshQuote(). Nice-to-have; easy to add later.
- **Stale-quote banner** — if a quote is older than, say, 5 minutes (pay-config drift risk), gray it out and warn. Not needed until live pay-config edits become common.
