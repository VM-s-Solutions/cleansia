import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  OrderStatusIconPipe,
  OrderStatusLabelPipe,
  OrderStatusSeverityPipe,
  PaymentStatusLabelPipe,
  PaymentStatusSeverityPipe,
} from '@cleansia/pipes';
import { LookupOrderResponse } from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { GuestOrderService } from '../track-order/guest-order.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { takeUntil } from 'rxjs';
import { GuestOrderLookupCacheService } from './guest-order-lookup-cache.service';
import { TrackOrderFacade } from '../track-order/track-order.facade';

/**
 * Read-only guest detail view for /orders/lookup/:orderId.
 *
 * Strict subset of OrderDetailComponent: no actions (no cancel, no review,
 * no receipt download, no issue reporting). The data source is
 * LookupOrderResponse (returned by /api/Order/Lookup and /LookupBatch),
 * not the authenticated OrderItem from GetById.
 *
 * Resolution order on load:
 *   1. In-memory cache (set by OrderLookupComponent on form submit).
 *   2. LookupBatch using the orderId + email pair from GuestOrderService
 *      (handles direct navigation / refresh as long as the device has
 *      saved the booking from a prior /track-order or /order success).
 *   3. Redirect to /orders/lookup so the user can re-enter the code.
 */
@Component({
  selector: 'cleansia-customer-guest-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    TagModule,
    SkeletonModule,
    TimelineModule,
    CleansiaButtonComponent,
    OrderStatusSeverityPipe,
    OrderStatusLabelPipe,
    PaymentStatusSeverityPipe,
    PaymentStatusLabelPipe,
    OrderStatusIconPipe,
  ],
  templateUrl: './guest-order-detail.component.html',
  providers: [TrackOrderFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuestOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly cache = inject(GuestOrderLookupCacheService);
  private readonly facade = inject(TrackOrderFacade);

  readonly order = signal<LookupOrderResponse | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly stars = [1, 2, 3, 4, 5];
  readonly hasReview = computed(() => false);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (!orderId) {
      this.redirectToLookup();
      return;
    }

    const cached = this.cache.get(orderId);
    if (cached) {
      this.order.set(cached.order);
      this.loading.set(false);
      return;
    }

    // Direct navigation / page refresh — try to recover via stored guest orders.
    const guestOrders = this.guestOrderService.getAll();
    const match = guestOrders.find((o) => o.orderId === orderId);
    if (!match) {
      this.redirectToLookup();
      return;
    }

    this.facade
      .lookupBatch([{ orderId: match.orderId, email: match.email }])
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (result) => {
          const found = result.orders?.find((o) => o.id === orderId) ?? null;
          if (!found) {
            this.redirectToLookup();
            return;
          }
          this.cache.set(orderId, found, match.email);
          this.order.set(found);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(
            this.translate.instant('pages.order_lookup.error')
          );
          this.loading.set(false);
        },
      });
  }

  private redirectToLookup(): void {
    this.router.navigate(['/' + CleansiaCustomerRoute.ORDERS, 'lookup']);
  }

  goBack(): void {
    this.router.navigate(['/' + CleansiaCustomerRoute.ORDERS, 'lookup']);
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  formatDate(date: Date | string | undefined): string {
    if (!date) return '';
    const d = date instanceof Date ? date : new Date(date);
    return d.toLocaleDateString(this.getLocale(), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number | undefined): string {
    if (price == null) return '';
    const code = this.order()?.currency?.code || 'CZK';
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: code,
      minimumFractionDigits: 0,
    }).format(price);
  }
}
