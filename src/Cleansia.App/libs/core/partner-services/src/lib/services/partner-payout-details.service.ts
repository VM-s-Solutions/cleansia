import { Injectable, inject } from '@angular/core';
import { ABSENT_RESOURCE_ERROR_CODES, extractApiErrorCode } from '@cleansia/services';
import { Observable, catchError, of, throwError } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import { MyPayoutDetails } from '../client/partner-client';

@Injectable({
  providedIn: 'root',
})
export class PartnerPayoutDetailsService {
  private readonly partnerClient = inject(PartnerClient);

  /**
   * A cleaner who has never saved a payout destination is answered with a 400
   * carrying `payout.not_found`, so the ordinary first visit arrives as a
   * failure. It is normalized to `null` here — once, at the boundary — and
   * every other failure still throws, so "no details yet" and "the network is
   * broken" stay distinguishable to every caller.
   */
  getMine(): Observable<MyPayoutDetails | null> {
    return this.partnerClient.employeeClient.getMyPayoutDetails().pipe(
      catchError((error: unknown) =>
        ABSENT_RESOURCE_ERROR_CODES.has(extractApiErrorCode(error) ?? '')
          ? of(null)
          : throwError(() => error)
      )
    );
  }
}
