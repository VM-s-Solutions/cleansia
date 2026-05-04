import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
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
  CustomerClient,
  GetMyMembershipResponse,
  SubmitOrderReviewCommand,
} from '@cleansia/customer-services';
import {
  OrderItem,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/partner-services';
import {
  RECURRING_PREFILL_STORAGE_KEY,
  RecurringPrefillParams,
} from '@cleansia-customer/recurring-bookings';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';

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
  ],
  templateUrl: './order-detail.component.html',
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  order = signal<OrderItem | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  /** Cached on first load — drives the "Make this recurring" CTA state. */
  membership = signal<GetMyMembershipResponse | null>(null);

  // Rating
  reviewRating = signal(0);
  reviewComment = signal('');
  reviewHover = signal(0);
  reviewSubmitting = signal(false);
  downloading = signal(false);
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
    // Best-effort membership fetch so the "Make this recurring" CTA can
    // either deep-link straight into the wizard (Plus) or route to subscribe
    // (non-Plus). Failure leaves membership=null which the template treats
    // as "not Plus" — safe fallback.
    this.customerClient.membershipClient.getMine().subscribe({
      next: (m: GetMyMembershipResponse) => this.membership.set(m),
      error: () => this.membership.set(null),
    });
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
      this.snackbar.showError(
        this.translate.instant('recurring_booking.order_detail_make_recurring_plus_required'),
      );
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
    this.loading.set(true);
    this.customerClient.orderClient.getById(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load order');
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS]);
  }

  downloadReceipt(): void {
    const order = this.order();
    if (!order?.id || this.downloading()) return;
    this.downloading.set(true);
    this.customerClient.orderClient.downloadReceipt(order.id).subscribe({
      next: (file) => {
        this.downloading.set(false);
        if (!this.isBrowser) return;
        const url = URL.createObjectURL(file.data);
        const a = document.createElement('a');
        a.href = url;
        a.download = file.fileName || `receipt-${order.displayOrderNumber}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloading.set(false);
        this.snackbar.showError(
          this.translate.instant('pages.order_detail.download_error')
        );
      },
    });
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
    const order = this.order();
    if (!order?.id || this.reviewRating() === 0) return;

    this.reviewSubmitting.set(true);
    const command = new SubmitOrderReviewCommand({
      orderId: order.id,
      rating: this.reviewRating(),
      comment: this.reviewComment() || undefined,
      userId: undefined, // Set by backend from JWT
    });

    this.customerClient.orderClient.submitReview(command).subscribe({
      next: (review) => {
        const current = this.order();
        if (current) {
          // customer-generated OrderReviewDto is structurally identical to the
          // partner-generated one used on OrderItem.review — cast bridges the nominal gap.
          current.review = review as unknown as OrderItem['review'];
          this.order.set({ ...current } as OrderItem);
        }
        this.reviewSubmitting.set(false);
        this.snackbar.showSuccess(
          this.translate.instant('pages.order_detail.review.success')
        );
      },
      error: (err: any) => {
        this.reviewSubmitting.set(false);
        this.snackbar.showApiError(err, 'pages.order_detail.review.error');
      },
    });
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
    const localeMap: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      pl: 'pl-PL',
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
