import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient } from '@cleansia/customer-services';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { AmountDueRow, toAmountDueRow } from './amount-due.models';

/**
 * What the customer owes and has not settled, each amount paid through its pay link: a Stripe
 * Checkout page the browser leaves for, which returns the customer to the amount's order.
 */
@Injectable()
export class AmountDueFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private orderId: string | null = null;

  readonly rows = signal<AmountDueRow[]>([]);
  readonly loading = signal(true);
  readonly hasError = signal(false);
  readonly payingId = signal<string | null>(null);

  init(orderId: string | null): void {
    this.orderId = orderId;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.hasError.set(false);
    this.customerClient.receivableClient
      .getMine()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((receivables) => {
        this.rows.set(
          (receivables ?? [])
            .filter((receivable) => !this.orderId || receivable.orderId === this.orderId)
            .map(toAmountDueRow),
        );
      });
  }

  pay(id: string, beforeLeaving: () => void): void {
    if (this.payingId()) return;
    this.payingId.set(id);
    this.customerClient.receivableClient
      .createPayLink(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
      )
      .subscribe((response) => {
        if (!response?.checkoutUrl) {
          this.payingId.set(null);
          this.load();
          return;
        }
        beforeLeaving();
        if (this.isBrowser) window.location.href = response.checkoutUrl;
      });
  }
}
