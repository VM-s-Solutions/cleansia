import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
} from '@cleansia/components';
import {
  loadCustomerOrders,
  selectCustomerOrders,
  selectCustomerOrdersTotal,
  selectCustomerOrderLoading,
} from '@cleansia/customer-stores';
import { OrderListItem } from '@cleansia/partner-services';
import {
  OrderStatusLabelPipe,
  OrderStatusSeverityPipe,
  PaymentStatusSeverityPipe,
} from '@cleansia/pipes';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';

@Component({
  selector: 'cleansia-customer-orders',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    TableModule,
    TagModule,
    SkeletonModule,
    CleansiaButtonComponent,
    PaginatorModule,
    OrderStatusSeverityPipe,
    OrderStatusLabelPipe,
    PaymentStatusSeverityPipe,
  ],
  templateUrl: './orders.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersComponent implements OnInit {
  private readonly store = inject(Store);
  readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  orders = toSignal(this.store.select(selectCustomerOrders), {
    initialValue: [],
  });
  upcomingOrders = computed(() => {
    const rawOrders = this.orders();
    if (!rawOrders) return [];
    const now = new Date();
    return rawOrders.filter((o) => new Date(o.cleaningDateTime) >= now);
  });
  pastOrders = computed(() => {
    const rawOrders = this.orders();
    if (!rawOrders) return [];
    const now = new Date();
    return rawOrders.filter((o) => new Date(o.cleaningDateTime) < now);
  });

  totalSpent = computed(() => {
    const rawOrders = this.orders();
    if (!rawOrders || rawOrders.length === 0) return this.formatPrice(0);
    const sum = rawOrders.reduce((acc, o) => acc + (o.totalPrice || 0), 0);
    const currency = rawOrders[0]?.currency;
    return this.formatPrice(sum, currency);
  });

  totalRecords = toSignal(this.store.select(selectCustomerOrdersTotal), {
    initialValue: 0,
  });
  loading = toSignal(this.store.select(selectCustomerOrderLoading('paged')), {
    initialValue: false,
  });

  rows = 10;
  first = 0;

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.store.dispatch(
      loadCustomerOrders({
        offset: this.first,
        limit: this.rows,
      })
    );
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.loadOrders();
  }

  viewOrder(order: OrderListItem): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS, order.id]);
  }

  rebookOrder(order: OrderListItem): void {
    const rebookData = {
      selectedServiceIds: (order.selectedServices || []).map((s) => s.id).filter(Boolean),
      selectedPackageIds: (order.selectedPackages || []).map((p) => p.id).filter(Boolean),
      selectedServiceNames: (order.selectedServices || []).map((s) => s.name || ''),
      selectedPackageNames: (order.selectedPackages || []).map((p) => p.name || ''),
      rooms: order.rooms,
      bathrooms: order.bathrooms,
    };
    if (this.isBrowser) {
      sessionStorage.setItem('cleansia_rebook_data', JSON.stringify(rebookData));
    }
    this.router.navigate(['/order'], { queryParams: { rebook: 'true' } });
  }

  isUpcoming(order: OrderListItem): boolean {
    return new Date(order.cleaningDateTime) >= new Date();
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

  formatDate(date: Date): string {
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number, currency?: { code?: string }): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: currency?.code || 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }
}
