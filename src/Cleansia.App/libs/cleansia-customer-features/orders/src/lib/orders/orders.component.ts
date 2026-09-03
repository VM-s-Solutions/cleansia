import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import {
  CleansiaButtonComponent,
} from '@cleansia/components';
import {
  loadCustomerOrders,
  selectCustomerOrders,
  selectCustomerOrdersTotal,
  selectCustomerOrderLoading,
} from '@cleansia/customer-stores';
import { OrderListItem } from '@cleansia/customer-services';
import { OrderStatus, PaymentStatus } from '@cleansia/models';
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
    RouterModule,
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

  /**
   * Orders that actually COMPLETED. The stat beside it is labelled "completed",
   * and it was showing `pastOrders` — which is "the date has passed", so a
   * cancelled booking counted as one. The Past Orders SECTION below still
   * groups by date, which is the right grouping for a list of what happened.
   */
  completedOrders = computed(() =>
    (this.orders() ?? []).filter((o) => o.orderStatus?.value === OrderStatus.Completed),
  );

  /**
   * What the customer has actually PAID. This summed every row on the page,
   * so a cancelled-and-refunded booking and an upcoming one not yet charged
   * both counted as money spent. Only a settled payment is spending.
   */
  totalSpent = computed(() => {
    const paid = (this.orders() ?? []).filter(
      (o) => o.paymentStatus?.value === PaymentStatus.Paid,
    );
    if (paid.length === 0) return this.formatPrice(0);
    const sum = paid.reduce((acc, o) => acc + (o.totalPrice || 0), 0);
    return this.formatPrice(sum, paid[0]?.currency);
  });

  // ---- The board's four filters ---------------------------------------------
  // -> "Objednavky zakaznika" board. They replace the Upcoming/Past SECTIONS the
  // page used to draw: the board lists one flat run of orders and lets the
  // customer narrow it, which is the same information without a heading that is
  // wrong the moment a booking crosses midnight.
  //
  // Filtering happens on the SERVER. GetCustomerOrders already takes an
  // OrderFilter and applies it through OrderSpecification before BOTH the count
  // and the page, the client exposes it, and the effect already threads it — so
  // narrowing here costs one dispatch. Filtering the loaded page instead would
  // search ten rows out of however many exist and leave the paginator reporting
  // the unfiltered total, which is wrong at any volume.
  readonly filters = ['all', 'upcoming', 'done', 'cancelled'] as const;
  readonly activeFilter = signal<(typeof this.filters)[number]>('all');

  /** The statuses a booking can be in while it is still ahead of the customer. */
  private static readonly OPEN_STATUSES = [
    OrderStatus.New,
    OrderStatus.Pending,
    OrderStatus.Confirmed,
    OrderStatus.OnTheWay,
    OrderStatus.InProgress,
  ];

  selectFilter(filter: (typeof this.filters)[number]): void {
    if (this.activeFilter() === filter) return;
    this.activeFilter.set(filter);
    // A narrowed list starts at its own first page; keeping the offset would
    // land on page 3 of a set that now has one page.
    this.first = 0;
    this.loadOrders();
  }

  /**
   * The row's one action, chosen by what the customer would want to do next.
   * A clean that is happening now is worth watching; one that has not started
   * is worth opening; one that is over is worth repeating.
   */
  actionFor(order: OrderListItem): 'track' | 'detail' | 'rebook' {
    switch (order.orderStatus?.value) {
      case OrderStatus.OnTheWay:
      case OrderStatus.InProgress:
        return 'track';
      case OrderStatus.Completed:
      case OrderStatus.Cancelled:
        return 'rebook';
      default:
        return 'detail';
    }
  }

  /** "Home cleaning, Window washing · 3 rooms" — the board's third line. */
  summaryOf(order: OrderListItem): string {
    const names = [
      ...(order.selectedPackages ?? []).map((p) => p.name),
      ...(order.selectedServices ?? []).map((s) => s.name),
    ].filter((n): n is string => !!n);

    const rooms = order.rooms
      ? this.translate.instant(
          `pages.orders.rooms_${this.pluralCategory(order.rooms)}`,
          { count: order.rooms },
        )
      : '';

    return [names.join(', '), rooms].filter(Boolean).join(' · ');
  }

  /**
   * Czech, Slovak, Russian and Ukrainian each split plurals three ways, and
   * ngx-translate has no plural support — so the CATEGORY comes from the
   * browser's own CLDR data and only the wording lives in the locale files.
   * Guessing one form per language would read wrong at 1 and at 5.
   */
  private pluralCategory(count: number): string {
    try {
      return new Intl.PluralRules(this.getLocale()).select(count);
    } catch {
      return 'other';
    }
  }

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
        ...this.filterArgs(),
      })
    );
  }

  /**
   * The active chip as query parameters. "Upcoming" is a date AND a status set:
   * a Confirmed booking whose date has passed is not ahead of anyone, and a
   * cancelled one is not upcoming however far away it is.
   */
  private filterArgs(): {
    orderStatuses?: OrderStatus[];
    cleaningDateFrom?: Date;
  } {
    switch (this.activeFilter()) {
      case 'upcoming':
        return {
          orderStatuses: OrdersComponent.OPEN_STATUSES,
          cleaningDateFrom: new Date(),
        };
      case 'done':
        return { orderStatuses: [OrderStatus.Completed] };
      case 'cancelled':
        return { orderStatuses: [OrderStatus.Cancelled] };
      default:
        return {};
    }
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
