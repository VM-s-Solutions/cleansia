import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaScrollTopComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import {
  CleansiaCustomerRoute,
  SnackbarService,
} from '@cleansia/services';
import { GuestOrderService } from '../track-order/guest-order.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';
import { GuestOrderLookupCacheService } from './guest-order-lookup-cache.service';
import { TrackOrderFacade } from '../track-order/track-order.facade';

/**
 * Guest order lookup entry form.
 *
 * Backend Lookup expects `orderNumber` + `email` (LookupBatch needs the
 * GUID `orderId`, which a guest doesn't have). We use single Lookup here
 * to resolve the orderId, cache the response in
 * GuestOrderLookupCacheService, and navigate to the read-only detail view.
 */
@Component({
  selector: 'cleansia-customer-order-lookup',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './order-lookup.component.html',
  providers: [TrackOrderFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderLookupComponent {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly cache = inject(GuestOrderLookupCacheService);
  private readonly facade = inject(TrackOrderFacade);

  readonly confirmationCode = signal('');
  readonly email = signal('');
  readonly loading = signal(false);
  readonly notFound = signal(false);

  submit(): void {
    const code = this.confirmationCode().trim();
    const email = this.email().trim();
    if (!code || !email || this.loading()) return;

    this.loading.set(true);
    this.notFound.set(false);

    this.facade
      .lookup(code, email)
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (order) => {
          this.loading.set(false);
          if (!order || !order.id) {
            this.notFound.set(true);
            return;
          }
          // Persist for "Recent Orders" on /track-order and for refresh-safe re-lookup.
          this.guestOrderService.save(order.id, email);
          this.cache.set(order.id, order, email);
          this.router.navigate([
            '/' + CleansiaCustomerRoute.ORDERS,
            'lookup',
            order.id,
          ]);
        },
        error: (err: { status?: number }) => {
          this.loading.set(false);
          // Lookup returns 400 when no match; treat as not-found inline.
          if (err?.status === 400 || err?.status === 404) {
            this.notFound.set(true);
          } else {
            this.snackbar.showError(
              this.translate.instant('pages.order_lookup.error')
            );
          }
        },
      });
  }
}
