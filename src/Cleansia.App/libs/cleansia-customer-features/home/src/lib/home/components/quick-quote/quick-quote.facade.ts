import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient, QuoteOrderCommand, QuoteOrderResponse } from '@cleansia/customer-services';
import { Subject, catchError, map, of, switchMap, takeUntil } from 'rxjs';

import { PROPERTY_SIZE_PRESETS, PropertySizePreset } from './property-size-presets';

/** The three states the calculator can be in, rendered explicitly. */
export type QuoteState = 'idle' | 'loading' | 'loaded' | 'error';

/**
 * The home-page price calculator.
 *
 * The price is never computed here. It comes from `QuoteOrder` through the
 * generated client — the same endpoint the order wizard calls — because the
 * quote is the server's answer and a second implementation on the client is a
 * second answer waiting to disagree with it.
 */
@Injectable()
export class QuickQuoteFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient);
  private readonly platformId = inject(PLATFORM_ID);

  /** Per-country size options — see `PROPERTY_SIZE_PRESETS`. */
  readonly sizes = inject(PROPERTY_SIZE_PRESETS);

  private readonly _serviceId = signal<string | null>(null);
  private readonly _size = signal<PropertySizePreset>(this.sizes[2] ?? this.sizes[0]);
  private readonly _cleaningDate = signal<string | null>(null);
  private readonly _cleaningTime = signal<string | null>(null);
  private readonly _state = signal<QuoteState>('idle');
  private readonly _quote = signal<QuoteOrderResponse | null>(null);

  readonly selectedServiceId = this._serviceId.asReadonly();
  readonly selectedSize = this._size.asReadonly();
  readonly cleaningDate = this._cleaningDate.asReadonly();
  readonly cleaningTime = this._cleaningTime.asReadonly();
  readonly state = this._state.asReadonly();
  readonly quote = this._quote.asReadonly();

  /**
   * The price the card is currently showing.
   *
   * Deliberately the LAST known price rather than null-while-loading: the block
   * used to be replaced by a one-line status on every request, so the card
   * changed height each time a chip was tapped. `_quote` is never cleared on
   * refresh, so the previous number stays put and is dimmed by the template
   * until the new one lands.
   */
  readonly displayPrice = computed(() => this._quote()?.totalPrice ?? null);

  /** What the small label above the number says, given the current state. */
  readonly priceLabelKey = computed(() => {
    if (this._state() === 'error') return 'pages.home.quote.error';
    if (this._quote()) return 'pages.home.quote.price_label';
    return 'pages.home.quote.pick_service';
  });

  /**
   * The estimate under the price.
   *
   * The artboard states it as "2 uklízeči · odhad 4 hodiny"; the crew count came
   * off on owner ruling, so only the duration is shown. It comes from the quote
   * endpoint, computed by the same `OrderDuration` the order itself uses, rather
   * than being a claim invented on the landing page.
   */
  readonly crewMinutes = computed(() => this._quote()?.estimatedDurationMinutes ?? null);

  /**
   * Everything the visitor chose here, in the shape the order wizard reads.
   *
   * Without this the Continue button was a bare `routerLink="/order"`: the
   * visitor picked a service and a size, watched a price appear, clicked
   * through — and re-entered both. A quote nobody can act on is a demo.
   */
  readonly continueQueryParams = computed(() => {
    const size = this._size();
    const params: Record<string, string> = {
      rooms: String(size.rooms),
      bathrooms: String(size.bathrooms),
    };

    const serviceId = this._serviceId();
    if (serviceId) {
      params['serviceId'] = serviceId;
    }

    const date = this._cleaningDate();
    if (date) {
      params['cleaningDate'] = date;
    }

    const time = this._cleaningTime();
    if (time) {
      params['cleaningTime'] = time;
    }

    return params;
  });
  readonly currencyCode = computed(() => this._quote()?.currencyCode ?? null);

  readonly isLoading = computed(() => this._state() === 'loading');

  selectService(serviceId: string): void {
    if (this._serviceId() === serviceId) {
      return;
    }
    this._serviceId.set(serviceId);
    this.refresh();
  }

  /**
   * An optional cleaning date.
   *
   * It is not decoration: `QuoteOrder` charges an express surcharge for a
   * cleaning booked soon, so a quote with no date is the base price and a quote
   * with one can legitimately be higher. Leaving it out meant the number above
   * the fold could disagree with the number at checkout, which is the one thing
   * a price calculator must not do.
   */
  selectDate(value: string | null): void {
    const next = value && value.length > 0 ? value : null;
    if (this._cleaningDate() === next) {
      return;
    }
    this._cleaningDate.set(next);
    this.refresh();
  }

  /**
   * The hour the clean should start.
   *
   * Only meaningful with a date, and only affects the quote through the express
   * surcharge, which is calculated from how soon the booking is. Without a date
   * it is remembered and applied as soon as one is picked.
   */
  selectTime(value: string | null): void {
    const next = value && value.length > 0 ? value : null;
    if (this._cleaningTime() === next) {
      return;
    }
    this._cleaningTime.set(next);
    if (this._cleaningDate()) {
      this.refresh();
    }
  }

  selectSize(size: PropertySizePreset): void {
    if (this._size().code === size.code) {
      return;
    }
    this._size.set(size);
    this.refresh();
  }

  /**
   * Ask the server for a price.
   *
   * Skipped during server rendering: the landing page is cached per
   * Accept-Language for 60 seconds, so a quote rendered there would be served
   * to somebody who never chose it.
   */
  /** The latest quote request. Older ones are cancelled, not merged. */
  private readonly pending$ = new Subject<QuoteOrderCommand>();

  constructor() {
    super();
    this.pending$
      .pipe(
        switchMap((command) =>
          this.client.orderClient.quote(command).pipe(
            map((result) => ({ result, failed: false })),
            catchError(() => of({ result: null, failed: true })),
          ),
        ),
        takeUntil(this.destroyed$),
      )
      .subscribe(({ result, failed }) => {
        if (failed) {
          this._state.set('error');
          return;
        }
        if (result) {
          this._quote.set(result);
        }
        // Always cleared on a reply, so a previous failure cannot outlive it.
        this._state.set('loaded');
      });
  }

  refresh(): void {
    const serviceId = this._serviceId();
    if (!serviceId || !isPlatformBrowser(this.platformId)) {
      return;
    }

    const size = this._size();
    const command = new QuoteOrderCommand();
    command.selectedServiceIds = [serviceId];
    command.selectedPackageIds = [];
    command.selectedExtraSlugs = [];
    command.rooms = size.rooms;
    command.bathrooms = size.bathrooms;

    const date = this._cleaningDate();
    if (date) {
      // Local time on purpose: the express surcharge is judged against the
      // customer's clock, and appending "Z" would shift a morning booking into
      // the previous day for anyone west of UTC.
      command.cleaningDate = new Date(`${date}T${this._cleaningTime() ?? '09:00'}`);
    }

    this._state.set('loading');

    // switchMap, not a fresh subscription per call: two chips tapped in quick
    // succession raced, and a slow FIRST reply could land after a fast second
    // one and put yesterday's price under today's selection. It also fixes a
    // stuck error — a failed earlier request used to leave `_state` on 'error'
    // while a later one succeeded, so the card showed "we could not price that"
    // as the caption directly above a correct, fresh number.
    this.pending$.next(command);
  }
}
