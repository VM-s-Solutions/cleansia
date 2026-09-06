import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ExpressWaiverStatus,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  resolveExpressWaiverStatus,
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
 *
 * NOTE: Web subscribes ONLY via Stripe-hosted Checkout, and that flow lives on the /plus page
 * (`PlusPageFacade.startCheckout`) — this facade manages an EXISTING membership. It deliberately
 * never calls the `subscribe` endpoint: that one is the native-SDK SetupIntent/PaymentSheet flow
 * consumed by the MOBILE app. The split is intentional, so the absence of a subscribe() call here
 * is by design, not a missing feature.
 *
 * It used to carry its own `createCheckoutSession` too — a second web checkout entry point with no
 * caller, left behind when /membership/subscribe was retired into /plus.
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

  // Express-surcharge waiver. `expressUpgradesRemaining` is 0 both for an exhausted member and
  // for one still inside the trial, so the state is resolved once against the load instant
  // rather than re-derived per render.
  expressWaiverStatus = signal<ExpressWaiverStatus>('none');
  readonly expressUpgradesRemaining = computed(
    () => this.membership()?.expressUpgradesRemaining ?? 0,
  );
  readonly expressWaiverAdvertised = computed(
    () => this.expressWaiverStatus() !== 'none',
  );
  readonly expressWaiverAvailable = computed(
    () => this.expressWaiverStatus() === 'available',
  );
  readonly expressWaiverExhausted = computed(
    () => this.expressWaiverStatus() === 'exhausted',
  );
  readonly expressWaiverPendingTrial = computed(
    () => this.expressWaiverStatus() === 'trial',
  );

  /** Refresh /membership/mine and update the loading flag. */
  refresh(onError?: () => void): void {
    this.loading.set(true);
    this.client
      .getMine()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (response) => {
          this.membership.set(response);
          this.expressWaiverStatus.set(resolveExpressWaiverStatus(response, new Date()));
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
        // The same null the wizard's read can get — see order-membership.facade.ts. Coalesced ONCE
        // and handed to both: the callback's argument is indexed by its callers just as the signal
        // is, so passing the raw value through would leave half the fix undone.
        const list = plans ?? [];
        this.plans.set(list);
        onLoaded?.(list);
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
    const command = new SwapMembershipPlanCommand();
    command.newPlanCode = planCode;

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

}
