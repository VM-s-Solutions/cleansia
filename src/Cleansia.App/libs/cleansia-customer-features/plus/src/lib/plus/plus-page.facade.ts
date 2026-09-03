import { inject, Injectable } from '@angular/core';
import { MembershipPlanFactsService } from '@cleansia/customer-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';

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

  load(): void {
    this.facts.load();
  }
}
