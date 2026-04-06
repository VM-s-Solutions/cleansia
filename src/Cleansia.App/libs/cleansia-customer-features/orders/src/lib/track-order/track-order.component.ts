import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaScrollTopComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import {
  CustomerOrderClient,
  CUSTOMER_API_BASE_URL,
  LookupOrderResponse,
  LookupOrderBatchQuery,
  LookupOrderBatchOrderLookupItem,
  LookupOrderBatchResponse,
} from '@cleansia/customer-services';
import { OrderStatus, PaymentStatus } from '@cleansia/partner-services';
import { CleansiaCustomerRoute, GuestOrderService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';

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
  ],
  templateUrl: './track-order.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrackOrderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly orderClient = new CustomerOrderClient(
    this.http,
    inject(CUSTOMER_API_BASE_URL, { optional: true }) ?? 'http://localhost:5003'
  );

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
    const items = guestOrders.map(
      (o) =>
        new LookupOrderBatchOrderLookupItem({
          orderId: o.orderId,
          email: o.email,
        })
    );

    this.orderClient
      .lookupBatch(new LookupOrderBatchQuery({ items }))
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

    this.orderClient.lookup(orderNumber, email).subscribe({
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

  getOrderStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case OrderStatus.Pending:
        return 'warn';
      case OrderStatus.Confirmed:
        return 'info';
      case OrderStatus.InProgress:
        return 'info';
      case OrderStatus.Completed:
        return 'success';
      case OrderStatus.Cancelled:
        return 'danger';
      default:
        return 'info';
    }
  }

  getPaymentStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case PaymentStatus.Pending:
        return 'warn';
      case PaymentStatus.Paid:
        return 'success';
      case PaymentStatus.Failed:
        return 'danger';
      case PaymentStatus.Refunded:
        return 'info';
      case PaymentStatus.Disputed:
        return 'danger';
      default:
        return 'info';
    }
  }

  getStatusIcon(value: number): string {
    switch (value) {
      case OrderStatus.Pending:
        return 'pi pi-clock';
      case OrderStatus.Confirmed:
        return 'pi pi-check';
      case OrderStatus.InProgress:
        return 'pi pi-spin pi-spinner';
      case OrderStatus.Completed:
        return 'pi pi-check-circle';
      case OrderStatus.Cancelled:
        return 'pi pi-times-circle';
      default:
        return 'pi pi-circle';
    }
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = { cs: 'cs-CZ', en: 'en-US' };
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
