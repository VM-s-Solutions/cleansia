import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaScrollTopComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import {
  LookupOrderResponse,
  LookupOrderBatchResponse,
} from '@cleansia/customer-services';
import {
  OrderStatusIconPipe,
  OrderStatusLabelPipe,
  OrderStatusSeverityPipe,
  PaymentStatusLabelPipe,
  PaymentStatusSeverityPipe,
} from '@cleansia/pipes';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { GuestOrderService } from './guest-order.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { takeUntil } from 'rxjs';
import { GuestOrderLookupCacheService } from '../order-lookup/guest-order-lookup-cache.service';
import { TrackOrderFacade } from './track-order.facade';

@Component({
  selector: 'cleansia-customer-track-order',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    TranslatePipe,
    TagModule,
    TimelineModule,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    CleansiaTitleComponent,
    OrderStatusSeverityPipe,
    OrderStatusLabelPipe,
    PaymentStatusSeverityPipe,
    PaymentStatusLabelPipe,
    OrderStatusIconPipe,
  ],
  templateUrl: './track-order.component.html',
  providers: [TrackOrderFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrackOrderComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly cache = inject(GuestOrderLookupCacheService);
  private readonly facade = inject(TrackOrderFacade);

  routes = CleansiaCustomerRoute;

  // Manual lookup
  orderNumber = signal('');
  email = signal('');

  // State
  loading = signal(false);
  recentOrders = signal<LookupOrderResponse[]>([]);
  manualResult = signal<LookupOrderResponse | null>(null);
  error = signal<string | null>(null);
  searched = signal(false);
  showManualLookup = signal(false);

  ngOnInit(): void {
    const params = this.route.snapshot.queryParams;
    if (params['orderNumber'] && params['email']) {
      this.orderNumber.set(params['orderNumber']);
      this.email.set(params['email']);
      this.showManualLookup.set(true);
      this.lookup();
    } else {
      this.loadGuestOrders();
    }
  }

  private loadGuestOrders(): void {
    const guestOrders = this.guestOrderService.getAll();
    if (guestOrders.length === 0) {
      this.showManualLookup.set(true);
      return;
    }

    this.loading.set(true);
    this.facade
      .lookupBatch(guestOrders.map((o) => ({ orderId: o.orderId, email: o.email })))
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (data: LookupOrderBatchResponse) => {
          this.recentOrders.set(data.orders || []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.showManualLookup.set(true);
        },
      });
  }

  toggleManualLookup(): void {
    this.showManualLookup.set(!this.showManualLookup());
  }

  lookup(): void {
    const orderNumber = this.orderNumber().trim();
    const email = this.email().trim();
    if (!orderNumber || !email) return;

    this.loading.set(true);
    this.error.set(null);
    this.manualResult.set(null);
    this.searched.set(true);

    this.facade
      .lookup(orderNumber, email)
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (data) => {
          this.manualResult.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(
            this.translate.instant('pages.track_order.not_found')
          );
          this.loading.set(false);
        },
      });
  }

  navigateToOrder(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER]);
  }

  viewDetails(orderId: string): void {
    // Cache the loaded order so the detail page does not re-fetch and the
    // guest does not have to re-enter their email.
    const order = this.recentOrders().find((o) => o.id === orderId);
    const email =
      this.guestOrderService.getAll().find((g) => g.orderId === orderId)?.email ?? '';
    if (order && email) {
      this.cache.set(orderId, order, email);
    }
    this.router.navigate(['/' + CleansiaCustomerRoute.ORDERS, 'lookup', orderId]);
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

  formatDate(date: string | Date | undefined): string {
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

  formatPriceFor(
    order: LookupOrderResponse,
    price: number | undefined
  ): string {
    if (price == null) return '';
    const code = order.currency?.code || 'CZK';
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: code,
      minimumFractionDigits: 0,
    }).format(price);
  }
}
