import { inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CreateMembershipCheckoutSessionCommand,
  CustomerClient,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { catchError, of, takeUntil } from 'rxjs';

/**
 * Shared facade for the membership management + subscribe flows. Wraps the
 * MembershipClient endpoints behind signal-driven state and unified error
 * handling so both components stay UI-only.
 *
 * NOTE: Always go through CustomerClient — direct injection of MembershipClient
 * hits NSwag's empty-string default baseUrl and bypasses CUSTOMER_API_BASE_URL.
 */
@Injectable()
export class MembershipFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly client = this.customerClient.membershipClient;
  private readonly snackbar = inject(SnackbarService);

  // Management state
  loading = signal(true);
  cancelling = signal(false);
  switching = signal(false);
  membership = signal<GetMyMembershipResponse | null>(null);
  plans = signal<GetMembershipPlansResponse[]>([]);

  // Subscribe state
  submitting = signal(false);

  /** Refresh /membership/mine and update the loading flag. */
  refresh(onError?: () => void): void {
    this.loading.set(true);
    this.client
      .getMine()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (response) => {
          this.membership.set(response);
          this.loading.set(false);
        },
        error: (err) => {
          this.snackbar.showApiError(err, 'membership.not_found');
          this.loading.set(false);
          onError?.();
        },
      });
  }

  /**
   * Anonymous-friendly endpoint — no auth required, but interceptor adds
   * bearer if present. Failing silently is fine; the switch CTA just won't show.
   */
  loadPlans(onLoaded?: (plans: GetMembershipPlansResponse[]) => void): void {
    this.client
      .getPlans()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of<GetMembershipPlansResponse[]>([])),
      )
      .subscribe((plans) => {
        this.plans.set(plans);
        onLoaded?.(plans);
      });
  }

  /** Cancel-at-period-end. The benefit window is unaffected until period end. */
  cancel(): void {
    this.cancelling.set(true);
    this.client
      .cancel()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.cancelling.set(false);
          this.snackbar.showSuccessTranslated('pages.membership.cancel_success');
          this.refresh();
        },
        error: (err) => {
          this.cancelling.set(false);
          this.snackbar.showApiError(err, 'membership.not_found');
        },
      });
  }

  /**
   * Swap to a target plan code (typically the yearly plan). Backend handles
   * the actual Stripe swap + invoice; on success we re-fetch /mine.
   */
  swapPlan(planCode: string): void {
    this.switching.set(true);
    const command = new SwapMembershipPlanCommand({
      newPlanCode: planCode,
    });
    this.client
      .swapPlan(command)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.switching.set(false);
          this.snackbar.showSuccessTranslated('pages.membership.switch_success');
          this.refresh();
        },
        error: (err) => {
          this.switching.set(false);
          this.snackbar.showApiError(err, 'membership.swap_same_plan');
        },
      });
  }

  /**
   * Start a Stripe-hosted Checkout Session and redirect the browser to it.
   * Calls the optional `onMissingUrl` callback if the API returns no URL —
   * the component is then responsible for resetting `submitting` if needed.
   */
  createCheckoutSession(
    planCode: string,
    successUrl: string,
    cancelUrl: string,
  ): void {
    if (this.submitting()) return;
    this.submitting.set(true);

    const command = new CreateMembershipCheckoutSessionCommand({
      planCode,
      successUrl,
      cancelUrl,
    });

    this.client
      .createCheckoutSession(command)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (response) => {
          if (response?.checkoutUrl && typeof window !== 'undefined') {
            window.location.href = response.checkoutUrl;
          } else {
            this.submitting.set(false);
            this.snackbar.showErrorTranslated('membership.stripe_customer_required');
          }
        },
        error: (err) => {
          this.submitting.set(false);
          this.snackbar.showApiError(err, 'membership.stripe_customer_required');
        },
      });
  }
}
