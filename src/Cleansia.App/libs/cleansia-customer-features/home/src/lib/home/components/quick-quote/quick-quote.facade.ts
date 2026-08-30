import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient, QuoteOrderCommand, QuoteOrderResponse } from '@cleansia/customer-services';
import { catchError, finalize, of, takeUntil } from 'rxjs';

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
  private readonly _state = signal<QuoteState>('idle');
  private readonly _quote = signal<QuoteOrderResponse | null>(null);

  readonly selectedServiceId = this._serviceId.asReadonly();
  readonly selectedSize = this._size.asReadonly();
  readonly cleaningDate = this._cleaningDate.asReadonly();
  readonly state = this._state.asReadonly();
  readonly quote = this._quote.asReadonly();

  readonly totalPrice = computed(() => this._quote()?.totalPrice ?? null);

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

    return params;
  });
  readonly currencyCode = computed(() => this._quote()?.currencyCode ?? null);

  /** Crew size follows the same rule the backend uses: ceil(estimate / 120 min). */
  readonly isLoading = computed(() => this._state() === 'loading');
  readonly hasFailed = computed(() => this._state() === 'error');

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
      command.cleaningDate = new Date(date);
    }

    this._state.set('loading');

    this.client.orderClient
      .quote(command)
      .pipe(
        catchError(() => {
          this._state.set('error');
          return of(null);
        }),
        finalize(() => {
          if (this._state() === 'loading') {
            this._state.set('loaded');
          }
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe((result) => {
        if (result) {
          this._quote.set(result);
        }
      });
  }
}
