import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { CustomerClient } from '../client/customer-base-client';
import { GetMembershipPlansResponse } from '../client/customer-client';

/** `BillingInterval.Monthly` as it arrives on the wire. */
const MONTHLY = 1;
/** `BillingInterval.Yearly` as it arrives on the wire. */
const YEARLY = 2;

type FactsState = 'idle' | 'loading' | 'loaded' | 'failed';

/**
 * What Cleansia Plus costs and what it does, read from the catalogue rather
 * than written down.
 *
 * `Membership/GetPlans` is `[AllowAnonymous]` and its DTO carries the price,
 * the discount rate, the free-cancellation window, the express allowance, the
 * trial length and the annual saving — its own comment says it exists so a
 * pre-subscribe surface can state them without a copy of each number in five
 * locales. Three surfaces now say them: the home page's Plus band, the public
 * `/plus` page and the signed-in subscribe screen. This is the second time the
 * same eight lines would have been written, which is the point at which they
 * stop being duplication and start being drift — the home band had 199 CZK,
 * 5%, 4 h and 14 days baked into its translations while `/plus` read the
 * plan, so a price change in the admin would have made the two pages disagree
 * with each other on the same site.
 *
 * Root-provided: it is a cache of two rows that every one of those surfaces
 * wants, and fetching it three times per session would be three requests for
 * an answer that does not change — for one market. The plans are priced per
 * market (ADR-0059 D3), so the cache is keyed by the country it was read for
 * and re-read when the customer switches market.
 */
@Injectable({ providedIn: 'root' })
export class MembershipPlanFactsService {
  private readonly client = inject(CustomerClient).membershipClient;

  private readonly plansSignal = signal<GetMembershipPlansResponse[]>([]);
  private readonly stateSignal = signal<FactsState>('idle');
  /** The country the current list answers for; `undefined` until a read is under way. */
  private loadedFor: string | null | undefined;

  readonly plans = this.plansSignal.asReadonly();
  readonly loading = computed(() => this.stateSignal() === 'loading');

  /** The currency every listed plan is priced in — the response's own code, never a default. */
  readonly currencyCode = computed(() => this.plansSignal()[0]?.currencyCode ?? null);

  /**
   * The endpoint answered and listed nothing: Plus is not on sale in this market. Distinct from a
   * failed read, which leaves the page without its pricing rather than claiming Plus is absent.
   */
  readonly unavailable = computed(
    () => this.stateSignal() === 'loaded' && this.plansSignal().length === 0,
  );

  readonly monthlyPlan = computed(
    () => this.plansSignal().find((p) => p.billingInterval === MONTHLY) ?? null,
  );
  readonly yearlyPlan = computed(
    () => this.plansSignal().find((p) => p.billingInterval === YEARLY) ?? null,
  );

  /**
   * The plan the benefit copy is written from. Both seeded plans carry
   * identical benefits and differ only in price and cadence, so either answers
   * "what does Plus do" — monthly first because it is the one every surface
   * leads with, then whatever loaded.
   */
  private readonly benefitPlan = computed(
    () => this.monthlyPlan() ?? this.plansSignal()[0] ?? null,
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
   * A plan with no express allowance must not be described as having one.
   * `AllowsExpressUpgrade` is a per-plan column an admin can switch off, and
   * every surface that mentions the perk reads this rather than assuming.
   */
  readonly hasExpressPerk = computed(
    () =>
      (this.benefitPlan()?.allowsExpressUpgrade ?? false) &&
      this.expressPerMonth() > 0,
  );

  /** True once a price can be stated. Until then the pricing blocks stay out. */
  readonly hasPlans = computed(() => this.plansSignal().length > 0);

  /**
   * Anonymous-friendly, and idempotent per market: the first caller fetches
   * and everyone after it reads the same rows until the market changes. A
   * failure leaves the list empty, which every consumer renders as the page
   * without its pricing rather than as an error — the perks and the rules are
   * all still true without a price, and an error panel on a marketing page
   * converts nobody.
   *
   * `countryId` is the chosen market's (ADR-0058 D4); null sends none, which
   * the server prices in the platform default.
   */
  load(countryId: string | null): void {
    if (this.loadedFor === countryId && this.stateSignal() !== 'failed') return;
    this.loadedFor = countryId;
    this.stateSignal.set('loading');
    this.client
      .getPlans(countryId ?? undefined)
      .pipe(catchError(() => of<GetMembershipPlansResponse[] | null>(null)))
      .subscribe((plans) => {
        // A newer market read is under way; this answer is for a market no longer chosen.
        if (this.loadedFor !== countryId) return;
        // Null is the generated client's answer to a non-array 200 body, not an empty market.
        if (!plans) {
          // A failed load must not be cached as "done" — the next surface the
          // customer opens should try again rather than inheriting an empty
          // list for the rest of the session.
          this.plansSignal.set([]);
          this.stateSignal.set('failed');
          return;
        }
        this.plansSignal.set(plans);
        this.stateSignal.set('loaded');
      });
  }
}
