import { inject, Injectable, signal } from '@angular/core';
import {
  CreateMembershipCheckoutSessionCommand,
  CustomerClient,
  GetMyMembershipResponse,
  MembershipPlanFactsService,
} from '@cleansia/customer-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, of, take, takeUntil } from 'rxjs';

/**
 * The public Cleansia Plus page.
 *
 * Every number this page states about the membership is READ FROM THE PLAN,
 * never written here — `Membership/GetPlans` is `[AllowAnonymous]` and carries
 * the price, the discount rate, the cancellation window, the express allowance
 * and the trial length precisely so a pre-subscribe surface can say them.
 *
 * The reading itself lives in `MembershipPlanFactsService` rather than here:
 * the home page's Plus band states the same figures, and it had them baked
 * into five locales' worth of translations while this page read the catalogue,
 * so the two could disagree the first time a price moved. This facade is the
 * page's own seam onto that service — the page binds to a facade like every
 * other component here, and the service stays a plain cache.
 */
@Injectable()
export class PlusPageFacade extends UnsubscribeControlDirective {
  private readonly facts = inject(MembershipPlanFactsService);
  private readonly membershipClient = inject(CustomerClient).membershipClient;
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly loading = this.facts.loading;
  readonly plans = this.facts.plans;

  readonly monthlyPlan = this.facts.monthlyPlan;
  readonly yearlyPlan = this.facts.yearlyPlan;
  readonly discountPercent = this.facts.discountPercent;
  readonly cancellationHours = this.facts.cancellationHours;
  readonly expressPerMonth = this.facts.expressPerMonth;
  readonly trialDays = this.facts.trialDays;
  readonly yearlySavingsPercent = this.facts.yearlySavingsPercent;
  readonly hasExpressPerk = this.facts.hasExpressPerk;
  readonly hasPlans = this.facts.hasPlans;

  /**
   * The caller's own membership, once this page became the ONE surface for the
   * product. Null until it is known — and it stays null for an anonymous
   * visitor, who is the majority of this page's traffic and must never wait on
   * an authenticated call to read the marketing.
   */
  readonly membership = signal<GetMyMembershipResponse | null>(null);
  readonly isMember = signal(false);
  readonly submitting = signal(false);

  load(): void {
    this.facts.load();
  }

  refreshMembership(): void {
    this.membershipClient
      .getMine()
      .pipe(take(1), takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((response) => {
        this.membership.set(response);
        this.isMember.set(response?.hasMembership === true);
      });
  }

  /**
   * Start Stripe's hosted Checkout for a plan.
   *
   * Moved here from the retired `/membership/subscribe`, which was a second
   * sales page for the same product. The success URL still lands on
   * `/membership/welcome` — that is the page Stripe has always returned to and
   * the one that celebrates the purchase; only the CANCEL url changes, because
   * the page the customer backed out of is now this one.
   */
  startCheckout(planCode: string): void {
    if (this.submitting() || !planCode) return;
    this.submitting.set(true);

    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    const command = new CreateMembershipCheckoutSessionCommand();
    command.planCode = planCode;
    command.successUrl = `${origin}/membership/welcome`;
    command.cancelUrl = `${origin}/plus`;

    this.membershipClient
      .createCheckoutSession(command)
      .pipe(take(1), takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((response) => {
        if (response?.checkoutUrl) {
          window.location.href = response.checkoutUrl;
          return;
        }
        this.submitting.set(false);
        this.snackbar.showError(this.translate.instant('pages.plus.checkout_failed'));
      });
  }
}
