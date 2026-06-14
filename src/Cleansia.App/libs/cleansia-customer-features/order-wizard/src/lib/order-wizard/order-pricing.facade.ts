import { computed, inject, Injectable, Injector, PLATFORM_ID, Signal, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { toObservable } from '@angular/core/rxjs-interop';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  QuoteOrderCommand,
  QuoteOrderResponse,
} from '@cleansia/customer-services';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  finalize,
  firstValueFrom,
  of,
  switchMap,
  takeUntil,
  tap,
} from 'rxjs';
import {
  EXPRESS_LEAD_TIME_HOURS,
  EXPRESS_SURCHARGE_RATE,
  OrderWizardFormData,
  STANDARD_LEAD_TIME_HOURS,
} from './order-wizard.models';

/**
 * Snapshot of the wizard inputs that affect pricing. Sorted arrays so
 * "same set, different insertion order" hashes equal in `distinctUntilChanged`.
 */
interface QuoteInputs {
  selectedServiceIds: string[];
  selectedPackageIds: string[];
  selectedExtraSlugs: string[];
  rooms: number;
  bathrooms: number;
  currencyId: string | null;
  cleaningDate: string | null;
}

/** Dependencies the pricing engine reads from the orchestrating wizard facade. */
interface PricingConnection {
  formData: Signal<OrderWizardFormData>;
  effectiveDiscount: Signal<number>;
}

/**
 * Live server-quote engine + express-surcharge math for the booking wizard.
 *
 * The server is the single source of truth for the total — clients never
 * compute prices themselves. This collaborator debounces the wizard's pricing
 * inputs into `/api/Order/Quote` calls (mirroring the mobile pattern) and
 * derives the displayed total (express surcharge layered on the discounted
 * subtotal). The orchestrating facade owns the discount inputs and connects
 * them in via [connect].
 */
@Injectable()
export class OrderPricingFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly injector = inject(Injector);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private deps: PricingConnection | null = null;

  readonly quote = signal<QuoteOrderResponse | null>(null);
  readonly quoting = signal(false);
  /** Snapshot of inputs that produced the current `quote()`, for cache reuse. */
  private readonly lastQuotedInputs = signal<QuoteInputs | null>(null);

  /**
   * Server-quoted base total — does NOT include the express surcharge. Use
   * [displayedTotalPrice] for what the user sees in the summary.
   */
  readonly totalPrice = computed(() => this.quote()?.totalPrice ?? 0);

  private formData(): OrderWizardFormData {
    if (!this.deps) throw new Error('OrderPricingFacade used before connect()');
    return this.deps.formData();
  }

  private effectiveDiscount(): number {
    return this.deps?.effectiveDiscount() ?? 0;
  }

  /**
   * True when the currently-selected (date, time) falls inside the 2–4h lead
   * window. The backend's QuoteOrder endpoint does NOT know about the cleaning
   * date/time, so the express surcharge is applied client-side here. Backend
   * mirrors the same `EXPRESS_SURCHARGE_RATE = 0.20` in `BookingPolicy.cs`
   * and grosses up at CreateOrder time — keep those two constants in sync.
   */
  readonly isExpressSlot = computed(() => {
    const data = this.formData();
    if (!data.cleaningDate || !data.cleaningTime) return false;
    const [h, m] = data.cleaningTime.split(':').map(Number);
    if (Number.isNaN(h) || Number.isNaN(m)) return false;
    const slot = new Date(data.cleaningDate);
    slot.setHours(h, m, 0, 0);
    const hoursAhead = (slot.getTime() - Date.now()) / (1000 * 60 * 60);
    return hoursAhead >= EXPRESS_LEAD_TIME_HOURS && hoursAhead < STANDARD_LEAD_TIME_HOURS;
  });

  /**
   * 20% surcharge amount (CZK), or 0 when the slot isn't express. Computed
   * against the POST-discount subtotal so the surcharge tracks what the user
   * actually pays. Mirrors backend CreateOrder.Handler order: discount on raw,
   * then surcharge on the discounted price.
   */
  readonly expressSurcharge = computed(() => {
    if (!this.isExpressSlot()) return 0;
    const discounted = Math.max(0, this.totalPrice() - this.effectiveDiscount());
    return discounted * EXPRESS_SURCHARGE_RATE;
  });

  /**
   * Final price the user pays — raw subtotal minus best-of-three discount,
   * plus express surcharge on the discounted total. Sidebar + summary both
   * render this so they always agree.
   */
  readonly displayedTotalPrice = computed(() => {
    const discounted = Math.max(0, this.totalPrice() - this.effectiveDiscount());
    return discounted + this.expressSurcharge();
  });

  /** Inputs that affect the server quote. Sorted ids so the snapshot is stable. */
  private readonly quoteInputs = computed<QuoteInputs>(() => {
    const data = this.formData();
    const selectedExtraSlugs = Object.entries(data.extras)
      .filter(([, on]) => on)
      .map(([slug]) => slug)
      .sort();
    // Compose the actual slot moment for the quote, the same way submit() does,
    // so the backend's express-surcharge check stays consistent between /Quote
    // and /Create — otherwise the quote sees midnight (no surcharge) but Create
    // sees the real slot (surcharge applies) and PriceMatchesAsync rejects with
    // order.total_price.not_match.
    let cleaningDateIso: string | null = null;
    if (data.cleaningDate && data.cleaningTime) {
      const [h, m] = data.cleaningTime.split(':').map(Number);
      if (!Number.isNaN(h) && !Number.isNaN(m)) {
        const slot = new Date(
          data.cleaningDate.getFullYear(),
          data.cleaningDate.getMonth(),
          data.cleaningDate.getDate(),
          h,
          m,
          0,
          0,
        );
        cleaningDateIso = slot.toISOString();
      }
    }
    return {
      selectedServiceIds: [...data.selectedServiceIds].sort(),
      selectedPackageIds: [...data.selectedPackageIds].sort(),
      selectedExtraSlugs,
      rooms: data.rooms,
      bathrooms: data.bathrooms,
      currencyId: null,
      cleaningDate: cleaningDateIso,
    };
  });

  private isEmptyInputs(i: QuoteInputs): boolean {
    return i.selectedServiceIds.length === 0 && i.selectedPackageIds.length === 0;
  }

  private quoteInputsEqual(a: QuoteInputs | null, b: QuoteInputs | null): boolean {
    if (a === b) return true;
    if (!a || !b) return false;
    return JSON.stringify(a) === JSON.stringify(b);
  }

  private toQuoteCommand(inputs: QuoteInputs): QuoteOrderCommand {
    return new QuoteOrderCommand({
      selectedServiceIds: inputs.selectedServiceIds,
      selectedPackageIds: inputs.selectedPackageIds,
      rooms: inputs.rooms,
      bathrooms: inputs.bathrooms,
      currencyId: inputs.currencyId ?? undefined,
      selectedExtraSlugs: inputs.selectedExtraSlugs,
      cleaningDate: inputs.cleaningDate ? new Date(inputs.cleaningDate) : undefined,
    });
  }

  /**
   * Wire the pricing engine to the wizard's form state and start the debounced
   * live-quote loop. Called once from the orchestrating facade's constructor.
   */
  connect(deps: PricingConnection): void {
    this.deps = deps;
    // SSR guard: don't fire HTTP during server render. The signal stays null
    // on the server and the client picks up the debounce loop after hydrate.
    if (!this.isBrowser) return;

    toObservable(this.quoteInputs, { injector: this.injector })
      .pipe(
        // 800ms matches the typing-pause UX norm for booking wizards and keeps
        // us under the backend's interactive rate-limit (60/min).
        debounceTime(800),
        distinctUntilChanged((a, b) => this.quoteInputsEqual(a, b)),
        tap((inputs) => {
          if (this.isEmptyInputs(inputs)) {
            this.quote.set(null);
            this.lastQuotedInputs.set(null);
            this.quoting.set(false);
          }
        }),
        filter((inputs) => !this.isEmptyInputs(inputs)),
        // Skip when the most recent successful quote already matches the pending
        // inputs — re-issuing would burn a rate-limit token for the same answer.
        filter((inputs) => !this.quoteInputsEqual(inputs, this.lastQuotedInputs())),
        switchMap((inputs) => {
          this.quoting.set(true);
          return this.customerClient.orderClient.quote(this.toQuoteCommand(inputs)).pipe(
            takeUntil(this.destroyed$),
            // Silent during editing — keep the prior quote so the user doesn't
            // see a "—" flash on a transient backend hiccup. Errors surface
            // only at submit time.
            catchError(() => of(null)),
            tap((resp) => {
              if (resp) {
                this.quote.set(resp);
                this.lastQuotedInputs.set(inputs);
              }
            }),
            finalize(() => this.quoting.set(false)),
          );
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe();
  }

  /**
   * Imperative quote refresh. Used by submit when the cached quote's inputs
   * don't match the current wizard state — we await a fresh /Quote call before
   * /Create so the backend validator can't reject us for a stale total.
   */
  async refreshQuoteNow(): Promise<QuoteOrderResponse | null> {
    const inputs = this.quoteInputs();
    if (this.isEmptyInputs(inputs)) {
      this.quote.set(null);
      this.lastQuotedInputs.set(null);
      return null;
    }
    this.quoting.set(true);
    try {
      const resp = await firstValueFrom(
        this.customerClient.orderClient.quote(this.toQuoteCommand(inputs)).pipe(
          takeUntil(this.destroyed$),
          finalize(() => this.quoting.set(false)),
        ),
      );
      this.quote.set(resp);
      this.lastQuotedInputs.set(inputs);
      return resp;
    } catch {
      return null;
    }
  }

  /** True when the current input snapshot matches the inputs that produced `quote()`. */
  cachedQuoteMatchesCurrentState(): boolean {
    return !!this.quote() && this.quoteInputsEqual(this.quoteInputs(), this.lastQuotedInputs());
  }
}
