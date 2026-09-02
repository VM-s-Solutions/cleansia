import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaScrollTopComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import {
  CustomerAuthService,
  LookupOrderResponse,
  LookupOrderBatchResponse,
} from '@cleansia/customer-services';
import { OrderStatus, PaymentStatus } from '@cleansia/models';
import {
  OrderStatusIconPipe,
  OrderStatusLabelPipe,
  PaymentStatusLabelPipe,
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
    CleansiaTextInputComponent,
    FoamEdgeComponent,
    OrderStatusLabelPipe,
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
  private readonly authService = inject(CustomerAuthService);

  routes = CleansiaCustomerRoute;
  readonly isLoggedIn = this.authService.isLoggedIn;

  /**
   * The order's own axis, drawn as the whole journey rather than only the
   * stops it has already made — a reader wants to know what is still to come,
   * and a list that stops at "Confirmed" says nothing about what happens next.
   * `Pending` is deliberately absent: it has no production writer and the state
   * it used to describe lives on the payment axis. -> /domain/order-lifecycle
   */
  private static readonly ORDER_STEPS: readonly { key: string; value: OrderStatus }[] = [
    { key: 'new', value: OrderStatus.New },
    { key: 'confirmed', value: OrderStatus.Confirmed },
    { key: 'on_the_way', value: OrderStatus.OnTheWay },
    { key: 'in_progress', value: OrderStatus.InProgress },
    { key: 'completed', value: OrderStatus.Completed },
  ];

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

  /** Both fields carry something. The server decides whether they match. */
  readonly canSubmit = computed(
    () => this.orderNumber().trim().length > 0 && this.email().trim().length > 0,
  );

  readonly isPaid = computed(
    () => this.manualResult()?.paymentStatus?.value === PaymentStatus.Paid,
  );

  /**
   * The sentence at the top. A status pill names the state; this says what it
   * MEANS for the person reading, which is the thing they came for.
   */
  readonly headline = computed(() => {
    const value = this.manualResult()?.orderStatus?.value;
    const key = TrackOrderComponent.ORDER_STEPS.find((s) => s.value === value)?.key;
    return this.translate.instant(
      value === OrderStatus.Cancelled
        ? 'pages.track_order.headline.cancelled'
        : `pages.track_order.headline.${key ?? 'new'}`,
    );
  });

  /**
   * Each step with the moment it happened, read off the order's own history
   * rather than assumed from its current state — an order can skip a state
   * (a cash job that is confirmed and started in one motion), and a timeline
   * that infers timestamps would invent them.
   */
  readonly orderSteps = computed(() => {
    const order = this.manualResult();
    const history = order?.statusHistory ?? [];
    const currentValue = order?.orderStatus?.value;
    const reached = new Set(history.map((h) => h.status?.value));

    return TrackOrderComponent.ORDER_STEPS.map((step) => {
      const entry = history.find((h) => h.status?.value === step.value);
      return {
        key: step.key,
        label: this.translate.instant(`pages.track_order.step.${step.key}`),
        when: entry ? this.formatDate(entry.createdOn) : '',
        done: reached.has(step.value) && step.value !== currentValue,
        current: step.value === currentValue,
      };
    });
  });

  /** Every service and package on the order, as chips. */
  readonly lineItems = computed(() => {
    const order = this.manualResult();
    if (!order) return [] as string[];
    return [
      ...(order.selectedServices ?? []).map((s) => s.name ?? ''),
      ...(order.selectedPackages ?? []).map((p) => p.name ?? ''),
    ].filter((n) => n.length > 0);
  });

  /**
   * Cash is settled with the cleaner after the work; a card was charged when
   * the booking was made. Saying the wrong one is the difference between a
   * customer having their wallet ready and not.
   */
  readonly priceNote = computed(() =>
    this.isPaid()
      ? 'pages.track_order.price_note_paid'
      : 'pages.track_order.price_note_cash',
  );

  /** Back to the form, with the fields kept so a typo can be corrected. */
  reset(): void {
    this.manualResult.set(null);
    this.error.set(null);
    this.searched.set(false);
    this.showManualLookup.set(true);
  }

  formatDuration(minutes: number | undefined): string {
    if (!minutes) return '—';
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    if (h === 0) return `${m} min`;
    return m === 0 ? `${h} h` : `${h} h ${m} min`;
  }

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
