import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ExpressWaiverStatus,
  GetMembershipPlansResponse,
  GetMyMembershipResponse,
  QuotePlusSavingsQuery,
  QuotePlusSavingsResponse,
  resolveExpressWaiverStatus,
} from '@cleansia/customer-services';
import { catchError, finalize, of, takeUntil } from 'rxjs';

/**
 * The wizard's one read of the signed-in customer's membership.
 *
 * Two surfaces depend on it — the wider free-cancellation window on the summary card and the
 * express-surcharge waiver on the slot grid — and they must not issue two `/Membership/Mine`
 * calls for the same answer. Every count here is the server's; the client never derives a
 * remaining quota from the customer's own orders (ADR-0035 D7).
 *
 * A failed read degrades to the same silence as "no membership": this is an enrichment on the
 * most valuable screen in the product, and a red toast (or a claim rendered from stale state)
 * would be worse than saying nothing.
 */
@Injectable()
export class OrderMembershipFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly membership = signal<GetMyMembershipResponse | null>(null);
  readonly expressWaiverStatus = signal<ExpressWaiverStatus>('none');

  /** Plus free-cancellation window in hours; null when the customer has no active membership. */
  readonly freeCancellationWindowHours = computed<number | null>(() => {
    const membership = this.membership();
    if (!membership?.hasMembership) return null;
    return membership.freeCancellationWindowHours ?? null;
  });

  /** Server-computed waivers left this calendar month, BEFORE the booking being composed. */
  readonly expressUpgradesRemaining = computed(
    () => this.membership()?.expressUpgradesRemaining ?? 0,
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

  /**
   * The plans on offer, for the wizard's Plus step. Anonymous-readable, because
   * the step exists to talk to someone who has no membership and may have no
   * account either.
   *
   * Every number the step prints comes from here — price, discount, cancellation
   * window, express quota, trial length, the yearly saving — so an admin who
   * edits a plan edits the copy with it.
   */
  readonly plans = signal<GetMembershipPlansResponse[]>([]);

  /** What Plus would be worth on the basket as it stands. Null until asked. */
  readonly plusSavings = signal<QuotePlusSavingsResponse | null>(null);

  loadPlans(): void {
    if (!this.isBrowser || this.plans().length > 0) return;

    this.customerClient.membershipClient
      .getPlans()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as GetMembershipPlansResponse[])),
      )
      // A failed read leaves the step with no plans to show, which the template
      // renders as no plan cards — the same silence the membership read uses,
      // and better than a half-priced offer.
      // … and `?? []` because the generated client answers a 200 whose body is not a JSON array
      // with NULL, not an empty list — see `processGetPlans` in customer-client.ts, which falls to
      // `result200 = null as any` while its declared type promises an array. Nothing above catches
      // it: null is not an error, so `catchError` never fires and TypeScript never complains. Five
      // readers then index or measure this signal, and the first to run is the plus-savings effect
      // on step ONE, so the whole wizard goes down long before anyone reaches the Plus step.
      .subscribe((plans) => this.plans.set(plans ?? []));
  }

  /**
   * Ask what the basket would cost with a plan. Server-side because the 12% cap
   * on membership + tier, the express gross-up, and the fact that a subscriber
   * starting today is in a trial are all invisible from here.
   */
  loadPlusSavings(query: QuotePlusSavingsQuery): void {
    if (!this.isBrowser) return;

    this.customerClient.orderClient
      .quotePlusSavings(query)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
      )
      .subscribe((savings) => this.plusSavings.set(savings));
  }

  load(isAuthenticated: boolean): void {
    if (!this.isBrowser || !isAuthenticated) return;

    this.loading.set(true);
    this.loadFailed.set(false);
    this.customerClient.membershipClient
      .getMine()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((response) => {
        if (!response) {
          this.loadFailed.set(true);
          return;
        }
        this.membership.set(response);
        this.expressWaiverStatus.set(resolveExpressWaiverStatus(response, new Date()));
      });
  }
}
