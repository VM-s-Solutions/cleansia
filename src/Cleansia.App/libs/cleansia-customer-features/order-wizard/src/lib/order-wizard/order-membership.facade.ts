import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  ExpressWaiverStatus,
  GetMyMembershipResponse,
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
