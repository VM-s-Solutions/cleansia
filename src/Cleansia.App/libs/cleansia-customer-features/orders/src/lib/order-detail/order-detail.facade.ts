import { isPlatformBrowser } from '@angular/common';
import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CancelOrderCommand,
  CancelOrderResponse,
  CustomerAuthService,
  CustomerClient,
  GetCancellationFeePreviewResponse,
  GetMyMembershipResponse,
  OrderItem,
  OrderStatus,
  SubmitOrderReviewCommand,
  SubmitOrderReviewReviewLineScore,
} from '@cleansia/customer-services';
import { ReviewLineScore } from './order-review-lines.models';
import { buildWorkContractAcceptanceLines } from './order-work-contract.models';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

/**
 * The statuses the server's CancellationAssessor lets a customer cancel from, less the dead
 * `Pending` it also lets through (no production writer; the guest flow withholds it too).
 */
const CANCELLABLE_ORDER_STATUSES: readonly OrderStatus[] = [
  OrderStatus.New,
  OrderStatus.Confirmed,
  OrderStatus.OnTheWay,
];

@Injectable()
export class OrderDetailFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  order = signal<OrderItem | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  /** Cached on first load — drives the "Make this recurring" CTA state. */
  membership = signal<GetMyMembershipResponse | null>(null);
  reviewSubmitting = signal(false);
  downloading = signal(false);

  readonly cancellationOpen = signal(false);
  readonly previewLoading = signal(false);
  readonly cancellationPreview = signal<GetCancellationFeePreviewResponse | null>(null);
  readonly cancellationPreviewFailed = signal(false);
  readonly cancelling = signal(false);
  readonly cancellationResult = signal<CancelOrderResponse | null>(null);

  readonly canCancel = computed(() => {
    const status = this.order()?.orderStatus?.value;
    return !this.cancellationResult() && status !== undefined &&
      CANCELLABLE_ORDER_STATUSES.includes(status);
  });

  /** One per crew member who accepted the contract for work; nothing before any acceptance. */
  readonly workContractAcceptances = computed(() => buildWorkContractAcceptanceLines(this.order()));

  readonly canConfirmCancellation = computed(() =>
    this.canCancel() && this.cancellationOpen() && !!this.cancellationPreview() &&
    !this.previewLoading() && !this.cancelling());

  /**
   * Best-effort membership fetch so the "Make this recurring" CTA can
   * either deep-link straight into the wizard (Plus) or route to subscribe
   * (non-Plus). Failure leaves membership=null which the template treats
   * as "not Plus" — safe fallback.
   */
  loadMembership(): void {
    // Guest checkout reaches order detail anonymously — skip the call to
    // avoid a noisy 401. Non-Plus is the safe fallback the template assumes.
    if (!this.authService.isLoggedIn()) {
      this.membership.set(null);
      return;
    }
    this.customerClient.membershipClient
      .getMine()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (m: GetMyMembershipResponse) => this.membership.set(m),
        error: () => this.membership.set(null),
      });
  }

  loadOrder(orderId: string): void {
    this.loading.set(true);
    this.customerClient.orderClient
      .getById(orderId)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
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

  downloadReceipt(): void {
    const order = this.order();
    if (!order?.id || this.downloading()) return;
    this.downloading.set(true);
    this.customerClient.orderClient
      .downloadReceipt(order.id)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
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
            this.translate.instant('pages.order_detail.download_error'),
          );
        },
      });
  }

  submitReview(rating: number, comment: string, lines: ReviewLineScore[] = []): void {
    const order = this.order();
    if (!order?.id || rating === 0) return;

    this.reviewSubmitting.set(true);
    const command = new SubmitOrderReviewCommand();
    command.orderId = order.id;
    command.rating = rating;
    command.comment = comment || undefined;
    // Per-item scores. The order-level rating above stays the headline and stays required — these add
    // what one number cannot say, which is that the oven was excellent and the bathroom was skipped.
    // Optional: a customer who just leaves five stars sends none, and that is the common case.
    command.lines = lines.length
      ? lines.map((line) => {
          const score = new SubmitOrderReviewReviewLineScore();
          score.serviceId = line.serviceId;
          score.packageId = line.packageId ?? undefined;
          score.rating = line.rating;
          return score;
        })
      : undefined;

    this.customerClient.orderClient
      .submitReview(command)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
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
            this.translate.instant('pages.order_detail.review.success'),
          );
        },
        error: (err: unknown) => {
          this.reviewSubmitting.set(false);
          this.snackbar.showApiError(err, 'pages.order_detail.review.error');
        },
      });
  }

  openCancellation(): void {
    const orderId = this.order()?.id;
    if (!orderId || !this.canCancel() || this.cancelling()) return;
    this.cancellationOpen.set(true);
    this.cancellationPreview.set(null);
    this.cancellationPreviewFailed.set(false);
    this.previewLoading.set(true);
    this.customerClient.orderClient
      .cancellationPreview(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.previewLoading.set(false)),
      )
      .subscribe((preview) => {
        if (preview && preview.orderId === orderId) this.cancellationPreview.set(preview);
        else this.cancellationPreviewFailed.set(true);
      });
  }

  closeCancellation(): void {
    if (this.cancelling()) return;
    this.cancellationOpen.set(false);
    this.cancellationPreview.set(null);
    this.cancellationPreviewFailed.set(false);
  }

  /**
   * The re-read runs on both branches: a refusal means the order moved on since the page loaded,
   * and the shared interceptor's toast says why while the page has to show the status it moved to.
   */
  cancelOrder(reason: string): void {
    const orderId = this.order()?.id;
    if (!orderId || !this.canConfirmCancellation()) return;
    const command = new CancelOrderCommand();
    command.orderId = orderId;
    command.reason = reason.trim() || undefined;
    this.cancelling.set(true);
    this.customerClient.orderClient
      .cancel(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.cancelling.set(false)),
      )
      .subscribe((result) => {
        if (result) this.cancellationResult.set(result);
        this.cancellationOpen.set(false);
        this.cancellationPreview.set(null);
        this.cancellationPreviewFailed.set(false);
        this.loadOrder(orderId);
      });
  }

  showRecurringPlusRequired(): void {
    this.snackbar.showError(
      this.translate.instant('recurring_booking.order_detail_make_recurring_plus_required'),
    );
  }
}
