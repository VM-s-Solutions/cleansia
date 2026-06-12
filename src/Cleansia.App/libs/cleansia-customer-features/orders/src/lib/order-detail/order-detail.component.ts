import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  OrderStatusIconPipe,
  OrderStatusLabelPipe,
  OrderStatusSeverityPipe,
  PaymentStatusLabelPipe,
  PaymentStatusSeverityPipe,
} from '@cleansia/pipes';
import { OrderStatus } from '@cleansia/customer-services';
import {
  RECURRING_PREFILL_STORAGE_KEY,
  RecurringPrefillParams,
} from '@cleansia-customer/recurring-bookings';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { OrderDetailFacade } from './order-detail.facade';

@Component({
  selector: 'cleansia-customer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
  providers: [OrderDetailFacade],
  templateUrl: './order-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly facade = inject(OrderDetailFacade);

  // Re-expose facade signals so existing template bindings keep working.
  readonly order = this.facade.order;
  readonly loading = this.facade.loading;
  readonly error = this.facade.error;
  readonly membership = this.facade.membership;
  readonly reviewSubmitting = this.facade.reviewSubmitting;
  readonly downloading = this.facade.downloading;

  // Rating
  reviewRating = signal(0);
  reviewComment = signal('');
  reviewHover = signal(0);
  isCompleted = computed(
    () => this.order()?.orderStatus?.value === OrderStatus.Completed
  );
  isInProgress = computed(
    () => this.order()?.orderStatus?.value === OrderStatus.InProgress
  );
  hasReview = computed(() => !!this.order()?.review);
  stars = [1, 2, 3, 4, 5];

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.loadOrder(orderId);
    }
    this.facade.loadMembership();
  }

  /**
   * Path B entry — stash the order's services/packages/rooms/payment/time
   * in sessionStorage and navigate into the recurring wizard. Non-Plus
   * users are routed to the subscribe page first; the wizard would
   * otherwise let them fill out a schedule they can't actually save.
   */
  makeRecurring(): void {
    const order = this.order();
    if (!order?.id) return;

    const isPlus = this.membership()?.hasMembership === true;
    if (!isPlus) {
      this.facade.showRecurringPlusRequired();
      this.router.navigate([CleansiaCustomerRoute.MEMBERSHIP, 'subscribe']);
      return;
    }

    // Derive HH:mm from the order's cleaningDateTime in the user's local TZ.
    let timeOfDay: string | null = null;
    if (order.cleaningDateTime) {
      const dt = new Date(order.cleaningDateTime);
      if (!Number.isNaN(dt.getTime())) {
        const hh = String(dt.getHours()).padStart(2, '0');
        const mm = String(dt.getMinutes()).padStart(2, '0');
        timeOfDay = `${hh}:${mm}`;
      }
    }

    const prefill: RecurringPrefillParams = {
      selectedServiceIds: (order.selectedServices || [])
        .map((s) => s.id)
        .filter((id): id is string => !!id),
      selectedPackageIds: (order.selectedPackages || [])
        .map((p) => p.id)
        .filter((id): id is string => !!id),
      selectedServiceNames: (order.selectedServices || []).map((s) => s.name || ''),
      selectedPackageNames: (order.selectedPackages || []).map((p) => p.name || ''),
      rooms: order.rooms ?? 0,
      bathrooms: order.bathrooms ?? 0,
      paymentType: order.paymentType?.value ?? 1,
      timeOfDay,
    };

    if (this.isBrowser) {
      sessionStorage.setItem(RECURRING_PREFILL_STORAGE_KEY, JSON.stringify(prefill));
    }
    this.router.navigate(
      [CleansiaCustomerRoute.MEMBERSHIP, 'recurring', 'create'],
      { queryParams: { prefill: 'true' } },
    );
  }

  loadOrder(orderId: string): void {
    this.facade.loadOrder(orderId);
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS]);
  }

  downloadReceipt(): void {
    this.facade.downloadReceipt();
  }

  reportIssue(): void {
    const order = this.order();
    if (!order?.id) return;
    this.router.navigate([CleansiaCustomerRoute.DISPUTES], {
      queryParams: { orderId: order.id },
    });
  }

  setRating(star: number): void {
    this.reviewRating.set(star);
  }

  submitReview(): void {
    this.facade.submitReview(this.reviewRating(), this.reviewComment());
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

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
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
