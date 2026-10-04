import { computed, inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ExpressWaiverStatus,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  MembershipStatus,
  resolveExpressWaiverStatus,
  SwapMembershipPlanCommand,
} from '@cleansia/customer-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
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
  private readonly dialog = inject(DialogService);
  private readonly store = inject(Store);

  // Management state
  loading = signal(true);
  cancelling = signal(false);
  switching = signal(false);
  membership = signal<GetMyMembershipResponse | null>(null);
  plans = signal<GetMembershipPlansResponse[]>([]);
  /**
   * A subscription keeps its currency for life (ADR-0059 D2), so every figure on this screen is
   * labelled with the membership's own code — not the market the customer is browsing in now.
   */
  readonly currencyCode = computed(() => this.membership()?.currencyCode ?? null);
  /**
   * The plans a member may switch to: those the market prices in the membership's currency. A
   * swap is settled in that currency (the server picks that row), so a plan listed in another
   * cannot be offered at the price the market shows.
   */
  readonly switchablePlans = computed(() => {
    const currencyCode = this.currencyCode();
    return this.plans().filter((plan) => plan.currencyCode === currencyCode);
  });
  private readonly marketCountryId = toSignal(this.store.select(selectMarketCountryId), {
    initialValue: null,
  });

  // Subscribe state
  submitting = signal(false);

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

  /** When the running free trial ends and the first payment falls, or null. */
  readonly trialEndsOn = signal<Date | null>(null);

  /**
   * A renewal payment failed. `hasMembership` still counts the enrolment so no second subscription
   * starts, but every benefit is off, a plan switch is refused, and a cancel ends it now.
   */
  readonly paymentFailed = computed(() => this.membership()?.status === MembershipStatus.PastDue);

  /** A trialing member who cancels or switches is charged nothing before the trial ends. */
  readonly cancelDialogMessageKey = computed(() => {
    if (this.paymentFailed()) return 'pages.membership.cancel_dialog_message_past_due';
    return this.trialEndsOn()
      ? 'pages.membership.cancel_dialog_message_trial'
      : 'pages.membership.cancel_dialog_message';
  });
  readonly switchDialogMessageKey = computed(() =>
    this.trialEndsOn()
      ? 'pages.membership.switch_dialog_message_trial'
      : 'pages.membership.switch_dialog_message',
  );
  /** A swap during the trial keeps the trial and charges nothing, so no "immediately" or "pay difference". */
  readonly switchLeadKey = computed(() =>
    this.trialEndsOn() ? 'pages.membership.switch_lead_trial' : 'pages.membership.switch_lead',
  );
  readonly switchConfirmKey = computed(() =>
    this.trialEndsOn()
      ? 'pages.membership.switch_dialog_confirm_trial'
      : 'pages.membership.switch_dialog_confirm',
  );

  /** Refresh /membership/mine and update the loading flag. */
  refresh(onError?: () => void): void {
    this.loading.set(true);
    this.client
      .getMine()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (response) => {
          const now = new Date();
          const trialEnd = response?.hasMembership ? response.trialEndsAtUtc : undefined;
          this.membership.set(response);
          this.expressWaiverStatus.set(resolveExpressWaiverStatus(response));
          this.trialEndsOn.set(trialEnd && trialEnd.getTime() > now.getTime() ? trialEnd : null);
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
      .getPlans(this.marketCountryId() ?? undefined)
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

  /**
   * Asks on the app shell's confirm dialog, and cancels only on yes. `date` is the trial's end as
   * the screen prints it.
   */
  confirmCancel(date: string, onCancelled?: () => void): void {
    this.dialog
      .confirmTranslated(
        this.cancelDialogMessageKey(),
        'pages.membership.cancel_dialog_title',
        { date },
        {
          acceptLabelKey: 'pages.membership.cancel_dialog_confirm',
          rejectLabelKey: 'common.back',
        },
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (confirmed) this.cancel(onCancelled);
      });
  }

  /** Asks before switching to `planCode`; `price` and `date` as the screen prints them. */
  confirmSwapPlan(planCode: string, params: { price: string; date: string }): void {
    this.dialog
      .confirmTranslated(this.switchDialogMessageKey(), 'pages.membership.switch_dialog_title', params, {
        icon: 'pi pi-arrow-up-right',
        acceptLabelKey: this.switchConfirmKey(),
        rejectLabelKey: 'common.back',
      })
      .pipe(takeUntil(this.destroyed$))
      .subscribe((confirmed) => {
        if (confirmed) this.swapPlan(planCode);
      });
  }

  /** A paid period and a trial run to their end, a failed renewal ends now. */
  cancel(onCancelled?: () => void): void {
    const successKey = this.paymentFailed()
      ? 'pages.membership.cancel_success_past_due'
      : this.trialEndsOn()
        ? 'pages.membership.cancel_success_trial'
        : 'pages.membership.cancel_success';
    this.cancelling.set(true);
    this.client
      .cancel()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.cancelling.set(false);
          this.snackbar.showSuccessTranslated(successKey);
          this.refresh();
          onCancelled?.();
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
