# Web Order Wizard — adopt the live-quote pattern from mobile

**Status:** Ready for execution
**Depends on:** TASK-BS2 (backend `POST /api/Order/Quote` endpoint landed) + NSwag customer client regenerated

## Decisions in scope for this spec

1. **Server-authoritative pricing.** The customer-facing web order wizard stops computing totals client-side and instead mirrors the mobile live-quote pattern: debounced `/api/Order/Quote` call whenever selections change, display the server total, and reuse the cached quote at submit time.
2. **Signals-first.** Use Angular 19 signals (`signal`, `computed`, `effect`) for the live-quote state. Debouncing sits on the RxJS/signal bridge (`toObservable` + `debounceTime` + `distinctUntilChanged` + `switchMap`) because pure-signal debouncing is awkward. Final value lands back into a signal.
3. **Silent during editing.** Quote failures while the user is still picking services are swallowed (log-only). Errors only surface when the user taps submit and we cannot confirm a total.
4. **Quote reuse at submit.** If the cached quote's inputs match the current wizard state, the `CreateOrderCommand` reuses `quote.totalPrice` + `quote.currencyId` without a second roundtrip. If the cache is stale/empty, the facade awaits a fresh `/Quote` call synchronously before calling `/Create`.
5. **Guests included.** `/Quote` is `[AllowAnonymous]` backend-side and the customer app already supports anonymous submission — guests see live quotes too.

## What this spec does NOT do

- Touch the partner or admin order wizards (they do not use `/Quote`).
- Add extras pricing / extras UI (covered by the extras spec).
- Introduce a currency switcher. The currency resolution is backend-default until a separate UX spec defines the picker.
- Change the mobile app. Mobile's live-quote work is tracked by its own spec.
- Modify the backend. TASK-BS2 already delivered the endpoint.
- Redesign the cart/footer layout. We reuse existing totals region, only swap the data source.

---

## Phase 1 — Facade wiring

### TASK-WLQ1: Live-quote signals + debounced fetch in `OrderWizardFacade`

```yaml
task: Extend order-wizard.facade.ts with quote/quoting signals and debounced /Quote fetch
id: TASK-WLQ1
type: feature
priority: high
specialist: frontend
app: customer-web
estimated_complexity: medium
recommended_model: sonnet

context: |
  The customer order wizard today calculates the total client-side
  (same formula mobile used to use). We are replacing that with a
  server-authoritative live quote:

    - signal `quote` holds the latest successful QuoteOrderResponse
      (or null when inputs are empty / no quote yet)
    - signal `quoting` reflects an in-flight request
    - a `computed` derives the "quote input snapshot" from the wizard
      selections (service ids, package ids, rooms, bathrooms, currencyId)
    - a bridge via `toObservable(snapshot)` debounces and fires the
      HTTP call through `customerClient.orderClient.quote(...)`

  Facade is the only place that touches these signals. The template
  and submit logic read them through the existing facade getters.

  SSR note: cleansia-app is SSR-enabled. The effect / observable must
  NOT fire during server render — guard with `isPlatformBrowser(PLATFORM_ID)`
  before subscribing. On the server we just expose null quote and let
  the client hydrate and then start the debounce loop.

files_to_modify:
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    change: |
      1. Import the generated client response:
           import { QuoteOrderCommand, QuoteOrderResponse } from '<customer-client path>';
         Double-check the import path after NSwag regen — likely
         `@cleansia/core-customer-services` or similar. Grep the file
         for existing imports from that barrel and reuse.

      2. Import Angular + RxJS interop:
           import { signal, computed, effect, inject, PLATFORM_ID, DestroyRef } from '@angular/core';
           import { toObservable } from '@angular/core/rxjs-interop';
           import { debounceTime, distinctUntilChanged, switchMap, catchError, of, tap, filter } from 'rxjs';
           import { isPlatformBrowser } from '@angular/common';

      3. Add the state signals inside the facade class (public readonly
         API, mutable inside the facade):
           readonly quote = signal<QuoteOrderResponse | null>(null);
           readonly quoting = signal(false);

      4. Add the input snapshot computed. Use whatever wizard-state
         signals already exist — grep the facade for `selectedServiceIds`,
         `selectedPackageIds`, `rooms`, `bathrooms`, `currencyId`. If any
         live as RxJS/BehaviorSubject today, either convert to signals or
         wrap via `toSignal` before the snapshot step.

           private readonly quoteInputs = computed(() => ({
             selectedServiceIds: [...this.selectedServiceIds()].sort(),
             selectedPackageIds: [...this.selectedPackageIds()].sort(),
             rooms: this.rooms(),
             bathrooms: this.bathrooms(),
             currencyId: this.currencyId?.() ?? null,
           }));

         Sorting the id arrays ensures `distinctUntilChanged` with JSON
         compare catches "same set, different insertion order" as equal.

      5. In the constructor (or a dedicated init method called from
         constructor), set up the bridge — but ONLY on the browser:
           const platformId = inject(PLATFORM_ID);
           const destroyRef = inject(DestroyRef);
           if (isPlatformBrowser(platformId)) {
             toObservable(this.quoteInputs)
               .pipe(
                 debounceTime(400),
                 distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
                 tap(inputs => {
                   if (this.isEmptyInputs(inputs)) {
                     this.quote.set(null);
                     this.quoting.set(false);
                   }
                 }),
                 filter(inputs => !this.isEmptyInputs(inputs)),
                 tap(() => this.quoting.set(true)),
                 switchMap(inputs => this.customerClient.orderClient
                   .quote(new QuoteOrderCommand({ ...inputs }))
                   .pipe(
                     catchError(() => of(null)), // silent during editing
                   ),
                 ),
                 takeUntilDestroyed(destroyRef),
               )
               .subscribe(resp => {
                 this.quoting.set(false);
                 if (resp) {
                   this.quote.set(resp);
                 }
                 // on null (error), keep the prior quote — user still
                 // sees the last good total while they continue editing
               });
           }

      6. Helper:
           private isEmptyInputs(i: { selectedServiceIds: string[]; selectedPackageIds: string[] }): boolean {
             return i.selectedServiceIds.length === 0 && i.selectedPackageIds.length === 0;
           }

      7. Add an explicit `refreshQuoteNow()` method that fires the
         quote call imperatively (bypassing the debounce). Used by
         submit when the cache is stale:
           async refreshQuoteNow(): Promise<QuoteOrderResponse | null> {
             const inputs = this.quoteInputs();
             if (this.isEmptyInputs(inputs)) {
               this.quote.set(null);
               return null;
             }
             this.quoting.set(true);
             try {
               const resp = await firstValueFrom(
                 this.customerClient.orderClient.quote(new QuoteOrderCommand({ ...inputs }))
               );
               this.quote.set(resp);
               return resp;
             } catch {
               return null;
             } finally {
               this.quoting.set(false);
             }
           }

dependencies: []
verification:
  - npx nx build cleansia-app — compiles
  - Open wizard in browser, DevTools network — toggling a service fires
    exactly one /api/Order/Quote call ~400ms after the last click
  - Rapidly toggling services only fires the last one (debounce works)
  - Selecting then deselecting until empty → quote signal becomes null
  - SSR dev render (check Terminal for node render) does NOT log the
    quote call — only client-side after hydrate
```

---

## Phase 2 — Template & submit

### TASK-WLQ2: Render server total in the footer/cart

```yaml
task: Replace client-calc total in order-wizard template with facade.quote() total
id: TASK-WLQ2
type: refactor
priority: high
specialist: frontend
app: customer-web
estimated_complexity: small
recommended_model: sonnet

context: |
  Today the cart/footer shows a total computed by some facade
  helper (likely `computeTotal()` or similar). Replace with the
  server quote. Add a small spinner when quoting && !quote
  (only a spinner on FIRST quote fetch; subsequent refreshes keep
  showing the prior total to avoid flicker).

files_to_modify:
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.html
    change: |
      1. Grep for the current total-display block. Candidates:
         - {{ facade.computeTotal() | currency }}
         - {{ total() }}
         - *ngIf binding on a "cart-total" / "summary-total" class
         Replace with:
           <ng-container *ngIf="facade.quote(); else quoteFallback">
             {{ facade.quote()?.totalPrice | currency: (facade.quote()?.currencyId ?? 'CZK') }}
           </ng-container>
           <ng-template #quoteFallback>
             <ng-container *ngIf="facade.quoting(); else dashTotal">
               <cleansia-spinner size="sm"></cleansia-spinner>
             </ng-container>
             <ng-template #dashTotal>—</ng-template>
           </ng-template>

         Use whatever currency pipe is configured in the app. If
         `CleansiaTranslateCurrencyPipe` exists, prefer that. Grep first.

      2. If the wizard-summary-step also shows a subtotal breakdown,
         update it too:
           Services subtotal: {{ facade.quote()?.servicesSubtotal | currency }}
           Packages subtotal: {{ facade.quote()?.packagesSubtotal | currency }}

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts
    change: |
      If the summary step reads totals from its own signal/input, pipe
      `facade.quote()` values into it. Prefer passing via component
      input rather than injecting the whole facade (keeps the step
      presentational).

dependencies:
  - TASK-WLQ1
verification:
  - Pick a service → spinner flashes ~400ms → real total appears
  - Deselect everything → "—" displayed
  - Flaky network (DevTools throttle) → total stays on prior value
    during silent failures
```

### TASK-WLQ3: Submit reuses cached quote (with fallback refresh)

```yaml
task: OrderWizardFacade.submit reuses quote() when inputs match, refreshes otherwise
id: TASK-WLQ3
type: refactor
priority: high
specialist: frontend
app: customer-web
estimated_complexity: small
recommended_model: sonnet

context: |
  Today's `placeOrder()` / `submit()` builds a CreateOrderCommand
  with a locally computed `totalPrice`. Switch to:

    1. If `quote() !== null` AND `cachedQuoteMatchesCurrentState()`:
       use `quote.totalPrice` + `quote.currencyId`.
    2. Else: `await refreshQuoteNow()`. If that returns null → error
       snackbar "We couldn't calculate the price. Please try again."
       and abort submit.
    3. Build CreateOrderCommand with the authoritative values.
    4. Call `orderClient.create(...)` as before.

  The match check compares the SAME snapshot shape the live-quote
  effect uses. Factor out a private helper so there's one source
  of truth for "these inputs are equivalent".

files_to_modify:
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    change: |
      1. Add the comparison helper:
           private cachedQuoteMatchesCurrentState(): boolean {
             const q = this.quote();
             if (!q) return false;
             const inputs = this.quoteInputs();
             // We cached the inputs that PRODUCED the current quote in a
             // separate signal — update the quote effect's subscribe
             // block to also call `this.lastQuotedInputs.set(inputs)`
             // before `this.quote.set(resp)`. That way we can compare
             // the current snapshot against the snapshot used last time.
             const last = this.lastQuotedInputs();
             if (!last) return false;
             return JSON.stringify(inputs) === JSON.stringify(last);
           }

         Add the signal above the constructor:
           private readonly lastQuotedInputs = signal<ReturnType<typeof this.quoteInputs> | null>(null);

         Update the effect's `.subscribe(resp => {...})` to set
         `lastQuotedInputs` when resp is truthy.

         Update `refreshQuoteNow()` to also set `lastQuotedInputs` on
         success.

      2. Update submit/placeOrder:
           async submit(): Promise<void> {
             let quoted = this.quote();
             if (!quoted || !this.cachedQuoteMatchesCurrentState()) {
               quoted = await this.refreshQuoteNow();
             }
             if (!quoted) {
               this.snackbar.error('errors.order.quote_failed');
               return;
             }
             const command = new CreateOrderCommand({
               // ...existing field mapping (customer info, address, date/time, payment, etc.)
               selectedServiceIds: this.selectedServiceIds(),
               selectedPackageIds: this.selectedPackageIds(),
               rooms: this.rooms(),
               bathrooms: this.bathrooms(),
               currencyId: quoted.currencyId,
               totalPrice: quoted.totalPrice,
             });
             // existing create call + navigate-on-success
             this.customerClient.orderClient.create(command).subscribe({ ... });
           }

      3. Ensure the `errors.order.quote_failed` translation key is added
         (see TASK-WLQ2 note — also add to all 5 locale files).

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/en.json
    change: |
      Under "errors" → "order" (create if missing):
        "quote_failed": "We couldn't calculate the price. Please try again."

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/cs.json
    change: |
      Under "errors" → "order":
        "quote_failed": "Nepodařilo se vypočítat cenu. Zkuste to prosím znovu."

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/sk.json
    change: |
      Under "errors" → "order":
        "quote_failed": "Nepodarilo sa vypočítať cenu. Skúste to znovu."

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/uk.json
    change: |
      Under "errors" → "order":
        "quote_failed": "Не вдалося розрахувати ціну. Спробуйте ще раз."

  - path: src/Cleansia.App/apps/cleansia.app/src/assets/i18n/ru.json
    change: |
      Under "errors" → "order":
        "quote_failed": "Не удалось рассчитать цену. Попробуйте ещё раз."

dependencies:
  - TASK-WLQ1
verification:
  - Happy path: pick services, wait for quote to land, submit → one
    /Quote + one /Create, Create body totalPrice matches last quote
  - Change selection, immediately submit before debounce fires → facade
    runs an extra /Quote (the cache-miss branch), then /Create
  - Simulate /Quote 500 at submit time → snackbar shown, sheet stays
    open, no /Create call
```

---

## Phase 3 — Guest flow + tests + cleanup

### TASK-WLQ4: Verify guest (unauthenticated) live quotes

```yaml
task: Confirm /Quote works for anonymous users; fix interceptor if it mandates auth
id: TASK-WLQ4
type: verification
priority: medium
specialist: frontend
app: customer-web
estimated_complexity: trivial
recommended_model: haiku

context: |
  /api/Order/Quote is [AllowAnonymous] server-side. The customer app's
  HTTP interceptor should already handle the "no token" case by simply
  omitting the Authorization header (same as /Create today). Verify.

  If the interceptor throws when no token is available, patch it to
  skip auth injection for anonymous calls instead of crashing.

files_to_read_first:
  - path: src/Cleansia.App/libs/core/customer-services/src/lib/interceptors/error.interceptor.ts
  - path: src/Cleansia.App/libs/core/customer-services/src/lib/client/customer-client.ts
    change: |
      Grep for anywhere an auth header is attached. Confirm behavior
      when the customer auth service has no stored token:
        - request goes out without Authorization → OK, backend accepts
        - request is blocked client-side → bug, fix by early-returning
          when endpoint is listed in an anonymous allowlist

files_to_modify:
  # Only if the verification finds a blocker. Default expectation is
  # "no code change needed".
  - path: src/Cleansia.App/libs/core/customer-services/src/lib/interceptors/error.interceptor.ts
    change: |
      If the interceptor rejects unauthenticated requests globally,
      add /api/Order/Quote (and /api/Order/Create) to its anonymous
      allowlist. Pattern should mirror whatever exists for the public
      /api/service/GetOverview endpoint.

dependencies:
  - TASK-WLQ1
verification:
  - Open wizard in a private-window / logged-out session
  - Pick a service — /Quote fires with NO Authorization header and returns 200
  - Submit works anonymously end-to-end (same as today's Create flow)
```

### TASK-WLQ5: Unit tests for the live-quote facade

```yaml
task: Jest tests for debounce, silent failure, cache reuse, empty-input reset
id: TASK-WLQ5
type: tests
priority: medium
specialist: frontend
app: customer-web
estimated_complexity: medium
recommended_model: sonnet

context: |
  Test only if the order-wizard lib already has a Jest harness
  (`libs/cleansia-customer-features/order-wizard/jest.config.*`).
  If it doesn't exist, skip test scaffolding and flag in the PR —
  setting up Jest for this lib is out of scope.

  Cover:
    1. Debounce — rapid-fire updates only trigger one /Quote call.
       Use fakeAsync + tick(399) → assert no call, tick(2) → assert
       one call.
    2. Silent failure — mock /Quote to reject; the prior quote signal
       value is preserved; no error snackbar.
    3. Empty inputs — set selections to empty; quote signal becomes
       null; no call fires.
    4. Submit cache reuse — seed a quote matching current inputs;
       call submit(); /Quote is NOT called, only /Create.
    5. Submit cache miss — seed a quote with stale inputs; call submit();
       one /Quote fires, then /Create with the fresh total.
    6. Submit failure when /Quote 500s — snackbar error; /Create never
       called.

files_to_create:
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.spec.ts
    change: |
      Standard TestBed setup with fakeAsync. Mock customerClient.orderClient
      via a provider swap. Assert signal values and call counts per the
      scenarios above.

dependencies:
  - TASK-WLQ1
  - TASK-WLQ3
verification:
  - npx nx test cleansia-customer-features-order-wizard → all new tests green
```

### TASK-WLQ6: Remove dead client-calc total code

```yaml
task: Delete the obsolete client-side total calculation paths
id: TASK-WLQ6
type: cleanup
priority: low
specialist: frontend
app: customer-web
estimated_complexity: trivial
recommended_model: haiku

context: |
  Once the template reads `facade.quote()?.totalPrice` and submit
  reuses the cached quote, the old `computeTotal()` / price helpers
  on the facade are dead weight. Grep, confirm zero remaining
  references, delete.

files_to_modify:
  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts
    change: |
      Delete `computeTotal()` or equivalently named methods. If the
      facade imports Service / Package price fields only to run the
      formula, trim those imports too.

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.models.ts
    change: |
      If this file defines local price-helper types (e.g. `PricedServiceRow`),
      grep for remaining references. Delete if unused.

  - path: src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/components/wizard-summary-step.component.ts
    change: |
      Same cleanup — delete any local subtotal math now sourced from the
      quote signal.

verification:
  - npx nx lint cleansia-customer-features-order-wizard — no unused-import warnings
  - Grep confirms no remaining reference to the deleted helpers
  - Wizard still renders correctly after rebuild

dependencies:
  - TASK-WLQ2
  - TASK-WLQ3
```

---

## Execution order

1. **TASK-WLQ1** — facade wiring (signals + debounced /Quote). No deps.
2. **TASK-WLQ2** and **TASK-WLQ3** — template update + submit reuse. Can run in parallel, both depend on WLQ1.
3. **TASK-WLQ4** — guest verification. Depends on WLQ1 for the interceptor context but can run alongside WLQ2/WLQ3.
4. **TASK-WLQ5** — tests. Depends on WLQ1 + WLQ3.
5. **TASK-WLQ6** — cleanup. Runs last, after WLQ2 + WLQ3 are merged so dead code is provably dead.

Parallelizable: WLQ2 + WLQ3 + WLQ4 together after WLQ1. WLQ5 + WLQ6 both after WLQ3.

Estimated tokens: ~30k total.

---

## Constraints

- Angular 19 signals, match house style (see `CLAUDE.md` — facades manage state via signals, components delegate all business logic).
- No backend changes.
- No mobile changes.
- Cleansia customer app uses SSR — guard the debounce effect with `isPlatformBrowser` so it only runs in the browser.
- All user-visible strings routed through `TranslatePipe` — no hardcoded literals.
- `ChangeDetectionStrategy.OnPush` preserved on all presentational components.

## Out of scope (followup specs)

- **Extras pricing** — backend does not price extras yet; once TASK-EX* lands, the live quote response will include extras subtotal and this facade will already display it.
- **Currency switcher UI** — when product defines the picker, the `currencyId` signal wires into `quoteInputs` and live quotes respond automatically.
- **Partner / admin wizard parity** — different feature set; neither surfaces a customer-facing total today.
- **Optimistic client estimate during the 400ms debounce** — current UX shows a spinner on first load and the prior total on subsequent refreshes. If UX flags the delay as too long, we can add a fast local estimate as a pre-flight number; noted but not planned.
- **Retry UI for persistent quote failures** — today we swallow errors during editing and only error at submit. A nicer "retry" button on the cart footer would help flaky-network users; out of scope here.
