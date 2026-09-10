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
  distinctUntilChanged,
  EMPTY,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  shareReplay,
  Subject,
  switchMap,
  takeUntil,
  tap,
  timer,
} from 'rxjs';
import {
  capCreditForOrder,
  composeFinalPriceForUnquotedDiscount,
  composeSlotMoment,
  OrderWizardFormData,
} from './order-wizard.models';

const QUOTE_DEBOUNCE_MS = 200;

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
  /**
   * Promo amount the customer applied at checkout. The only discount the quote cannot fold in
   * itself — `QuoteOrderCommand` carries no promo code, by design (the code is entered after the
   * quote step and `CreateOrder` resolves it at submit).
   */
  promoDiscount: Signal<number>;
}

/**
 * Live server-quote engine for the booking wizard.
 *
 * The server is the single source of truth for the total — clients never compute prices themselves.
 * This collaborator debounces the wizard's pricing inputs into `/api/Order/Quote` calls (mirroring
 * the mobile pattern) and renders the server's own figures. The orchestrating facade connects the
 * promo amount in via [connect].
 *
 * Two totals arrive on every quote and they answer different questions. `totalPrice` is the
 * UNDISCOUNTED gross including any express surcharge — the number `CreateOrder.PriceMatchesAsync`
 * demands back unchanged, and the only number that may be submitted.
 * `finalPriceAfterDiscount` is what the customer is charged, composed the way `OrderFactory` persists
 * it: the discount comes off the pre-surcharge subtotal and the surcharge goes on top of the
 * remainder. Those two orderings differ by a fifth of the discount on every express booking, so the
 * displayed price is taken from the quote and never recomposed from `totalPrice` and a discount.
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
  private readonly cancelQuote$ = new Subject<void>();
  private pendingQuote: {
    inputs: QuoteInputs;
    response$: Observable<QuoteOrderResponse | null>;
  } | null = null;

  /**
   * Undiscounted gross, surcharge included — the value `submitOrder` resubmits verbatim. This is
   * NOT the price the customer pays; that is [displayedTotalPrice].
   */
  readonly totalPrice = computed(() => this.quote()?.totalPrice ?? 0);

  private formData(): OrderWizardFormData {
    if (!this.deps) throw new Error('OrderPricingFacade used before connect()');
    return this.deps.formData();
  }

  private promoDiscount(): number {
    return this.deps?.promoDiscount() ?? 0;
  }

  /** Server verdict — true when the quoted slot carried the express surcharge. */
  readonly expressSurchargeApplied = computed(
    () => this.quote()?.expressSurchargeApplied ?? false,
  );

  /**
   * The slot IS express and the surcharge was nevertheless not charged, because a membership
   * waiver covered it. `expressSurchargeApplied === false` alone cannot say this — it is equally
   * true for a slot that is not express at all.
   */
  readonly expressSurchargeWaived = computed(
    () => this.quote()?.expressSurchargeWaivedByMembership ?? false,
  );

  readonly tierDiscount = computed(() => this.quote()?.tierDiscountAmount ?? 0);
  readonly membershipDiscount = computed(() => this.quote()?.membershipDiscountAmount ?? 0);

  /** Tier + membership: the discount the quote already folded into `finalPriceAfterDiscount`. */
  readonly quotedDiscount = computed(() => this.tierDiscount() + this.membershipDiscount());

  /**
   * The discount the server will actually apply. Mirrors `OrderFactory.ResolveLoy003Discount`: a
   * winning promo replaces the tier/membership pair outright, it never stacks on it.
   */
  readonly effectiveDiscount = computed(() =>
    Math.max(this.quotedDiscount(), this.promoDiscount()),
  );

  /**
   * Pre-surcharge subtotal — the base the server resolves every discount and every minimum-order
   * floor against (`rawSubtotal = TotalPrice - ExpressSurchargeAmount`).
   */
  readonly preSurchargeSubtotal = computed(
    () => this.totalPrice() - (this.quote()?.expressSurchargeAmount ?? 0),
  );

  /**
   * Price the customer is charged, taken from the quote. `quote.expressSurchargeAmount` is the
   * surcharge on the UNDISCOUNTED subtotal, so `totalPrice - discount` is not this number and no
   * amount of client arithmetic over those two produces it.
   */
  readonly displayedTotalPrice = computed(() => {
    const quote = this.quote();
    if (!quote) return 0;
    return this.promoDiscount() > this.quotedDiscount()
      ? composeFinalPriceForUnquotedDiscount(
          this.preSurchargeSubtotal(),
          quote.totalPrice,
          this.promoDiscount(),
        )
      : quote.finalPriceAfterDiscount;
  });

  /**
   * The customer's spendable credit, and how much of it this booking would take.
   *
   * Owner ruling 2026-09-05: credit applies AUTOMATICALLY — there is no "use my credit" toggle, and
   * the wizard's job is to say so plainly before the customer is sent to Stripe rather than let them
   * discover a smaller charge afterwards. `creditBalance` is the whole balance so the summary can say
   * "500 of your 800 applies here"; the card always pays the rest, which is the second half of the
   * same ruling.
   *
   * Capped against `displayedTotalPrice` — the price actually being charged, promo included — through
   * the one shared function that mirrors the server rule. → capCreditForOrder
   */
  readonly creditBalance = computed(() => this.quote()?.creditBalance ?? 0);

  readonly creditApplied = computed(() =>
    capCreditForOrder(
      this.creditBalance(),
      this.displayedTotalPrice(),
      this.quote()?.creditMaxShareOfOrder ?? 0,
    ),
  );

  /** What the card is asked for once credit has settled its share. */
  readonly amountDueOnCard = computed(() => this.displayedTotalPrice() - this.creditApplied());

  /**
   * Express surcharge line item — the surcharge actually billed, which the server takes on the
   * DISCOUNTED subtotal. Derived as the gap the charged price leaves above that subtotal so the
   * breakdown rows sum to the total; `quote.expressSurchargeAmount` answers a different question
   * and would leave the card short by a fifth of the discount.
   */
  readonly expressSurcharge = computed(() => {
    if (!this.quote()) return 0;
    return this.displayedTotalPrice() - (this.preSurchargeSubtotal() - this.effectiveDiscount());
  });

  /** Inputs that affect the server quote. Sorted ids so the snapshot is stable. */
  private readonly quoteInputs = computed<QuoteInputs>(() => {
    const data = this.formData();
    const selectedExtraSlugs = Object.entries(data.extras)
      .filter(([, on]) => on)
      .map(([slug]) => slug)
      .sort();
    // → composeSlotMoment: the date alone is midnight, which sits in a
    // different express band from the slot.
    const cleaningDateIso = composeSlotMoment(data.cleaningDate, data.cleaningTime)?.toISOString() ?? null;

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
    const command = new QuoteOrderCommand();
    command.selectedServiceIds = inputs.selectedServiceIds;
    command.selectedPackageIds = inputs.selectedPackageIds;
    command.rooms = inputs.rooms;
    command.bathrooms = inputs.bathrooms;
    command.currencyId = inputs.currencyId ?? undefined;
    command.selectedExtraSlugs = inputs.selectedExtraSlugs;
    command.cleaningDate = inputs.cleaningDate
      ? new Date(inputs.cleaningDate)
      : undefined;
    return command;
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
        distinctUntilChanged((a, b) => this.quoteInputsEqual(a, b)),
        switchMap((inputs) => {
          if (this.pendingQuote && !this.quoteInputsEqual(inputs, this.pendingQuote.inputs)) {
            this.cancelQuote$.next();
          }
          if (this.isEmptyInputs(inputs)) {
            this.quote.set(null);
            this.lastQuotedInputs.set(null);
            this.quoting.set(false);
            return EMPTY;
          }
          if (this.quoteInputsEqual(inputs, this.lastQuotedInputs())) {
            this.quoting.set(false);
            return EMPTY;
          }

          this.quoting.set(true);
          return timer(QUOTE_DEBOUNCE_MS).pipe(
            switchMap(() => {
              if (this.quoteInputsEqual(inputs, this.lastQuotedInputs())) {
                this.quoting.set(false);
                return EMPTY;
              }
              return this.requestQuote(inputs);
            }),
          );
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe();
  }

  private requestQuote(inputs: QuoteInputs): Observable<QuoteOrderResponse | null> {
    if (this.pendingQuote && this.quoteInputsEqual(inputs, this.pendingQuote.inputs)) {
      return this.pendingQuote.response$;
    }
    this.cancelQuote$.next();
    this.quoting.set(true);

    const response$ = this.customerClient.orderClient.quote(this.toQuoteCommand(inputs)).pipe(
      takeUntil(this.cancelQuote$),
      takeUntil(this.destroyed$),
      catchError(() => of(null)),
      map((resp) => this.quoteInputsEqual(inputs, this.quoteInputs()) ? resp : null),
      tap((resp) => {
        if (resp) {
          this.quote.set(resp);
          this.lastQuotedInputs.set(inputs);
        }
      }),
      finalize(() => {
        if (this.pendingQuote?.response$ === response$) {
          this.pendingQuote = null;
          this.quoting.set(false);
        }
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
    );
    this.pendingQuote = { inputs, response$ };
    return response$;
  }

  /**
   * Imperative quote refresh. Used by submit when the cached quote's inputs
   * don't match the current wizard state — we await a fresh /Quote call before
   * /Create so the backend validator can't reject us for a stale total.
   */
  async refreshQuoteNow(): Promise<QuoteOrderResponse | null> {
    const inputs = this.quoteInputs();
    if (this.isEmptyInputs(inputs)) {
      this.cancelQuote$.next();
      this.quote.set(null);
      this.lastQuotedInputs.set(null);
      this.quoting.set(false);
      return null;
    }
    try {
      return await firstValueFrom(this.requestQuote(inputs));
    } catch {
      return null;
    }
  }

  /** True when the current input snapshot matches the inputs that produced `quote()`. */
  cachedQuoteMatchesCurrentState(): boolean {
    return !!this.quote() && this.quoteInputsEqual(this.quoteInputs(), this.lastQuotedInputs());
  }
}
