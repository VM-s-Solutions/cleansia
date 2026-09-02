import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  GetMembershipPlansResponse,
} from '@cleansia/customer-services';
import { catchError, of, takeUntil } from 'rxjs';

/** `BillingInterval.Monthly` on the wire. */
const MONTHLY = 1;
/** `BillingInterval.Yearly` on the wire. */
const YEARLY = 2;

/**
 * The public Cleansia Plus page.
 *
 * Every number this page states about the membership is READ FROM THE PLAN,
 * never written here — `Membership/GetPlans` is `[AllowAnonymous]` and carries
 * the price, the discount rate, the cancellation window, the express allowance
 * and the trial length precisely so a pre-subscribe surface can say them. The
 * one time this repository hardcoded such a number in the UI it went stale in
 * five locales at once.
 *
 * Deliberately NOT the profile lib's `MembershipFacade`, which this duplicates
 * a dozen lines of: that facade also owns `getMine`, cancel, swap and Stripe
 * checkout, so importing it would pull the whole authenticated membership
 * bundle into the anonymous landing chunk. The shared part is one call.
 */
@Injectable()
export class PlusPageFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient).membershipClient;

  readonly loading = signal(true);
  readonly plans = signal<GetMembershipPlansResponse[]>([]);

  readonly monthlyPlan = computed(
    () => this.plans().find((p) => p.billingInterval === MONTHLY) ?? null,
  );
  readonly yearlyPlan = computed(
    () => this.plans().find((p) => p.billingInterval === YEARLY) ?? null,
  );

  /**
   * The plan the perk copy is written from. The two seeded plans carry
   * identical benefits and differ only in price and cadence, so either
   * answers "what does Plus do" — monthly first because it is the one the
   * page leads with, then whatever loaded.
   */
  private readonly benefitPlan = computed(
    () => this.monthlyPlan() ?? this.plans()[0] ?? null,
  );

  readonly discountPercent = computed(
    () => this.benefitPlan()?.discountPercentage ?? 0,
  );
  readonly cancellationHours = computed(
    () => this.benefitPlan()?.freeCancellationWindowHours ?? 0,
  );
  readonly expressPerMonth = computed(
    () => this.benefitPlan()?.expressUpgradesPerMonth ?? 0,
  );
  readonly trialDays = computed(() => this.benefitPlan()?.trialPeriodDays ?? 0);
  readonly yearlySavingsPercent = computed(
    () => this.yearlyPlan()?.savingsPercentVsMonthly ?? 0,
  );

  /**
   * A plan with no express allowance must not be described as having one. The
   * seeded plans both waive one a month, but `AllowsExpressUpgrade` is a
   * per-plan column an admin can turn off, and the perk card and the
   * comparison row both read this rather than assuming.
   */
  readonly hasExpressPerk = computed(
    () => (this.benefitPlan()?.allowsExpressUpgrade ?? false) && this.expressPerMonth() > 0,
  );

  /** True once the page can state a price. Until then the plan blocks stay out. */
  readonly hasPlans = computed(() => this.plans().length > 0);

  /**
   * Anonymous-friendly. A failure leaves `plans` empty, which the template
   * renders as the page without its pricing blocks rather than as an error —
   * the perks, the comparison and the FAQ are all still true and still worth
   * reading, and an error panel on a marketing page converts nobody.
   */
  load(): void {
    this.loading.set(true);
    this.client
      .getPlans()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of<GetMembershipPlansResponse[]>([])),
      )
      .subscribe((plans) => {
        this.plans.set(plans);
        this.loading.set(false);
      });
  }
}
