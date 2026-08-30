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
  private readonly _state = signal<QuoteState>('idle');
  private readonly _quote = signal<QuoteOrderResponse | null>(null);

  readonly selectedServiceId = this._serviceId.asReadonly();
  readonly selectedSize = this._size.asReadonly();
  readonly state = this._state.asReadonly();
  readonly quote = this._quote.asReadonly();

  readonly totalPrice = computed(() => this._quote()?.totalPrice ?? null);
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
