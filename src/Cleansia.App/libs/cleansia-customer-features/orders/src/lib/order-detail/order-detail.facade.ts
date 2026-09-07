import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerAuthService,
  CustomerClient,
  GetMyMembershipResponse,
  OrderItem,
  SubmitOrderReviewCommand,
  SubmitOrderReviewReviewLineScore,
} from '@cleansia/customer-services';
import { ReviewLineScore } from './order-review-lines.models';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

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

  showRecurringPlusRequired(): void {
    this.snackbar.showError(
      this.translate.instant('recurring_booking.order_detail_make_recurring_plus_required'),
    );
  }
}
