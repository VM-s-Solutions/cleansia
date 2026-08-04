import { Injectable, inject } from '@angular/core';
import { ABSENT_RESOURCE_ERROR_CODES, extractApiErrorCode } from '@cleansia/services';
import { Observable, catchError, of, throwError } from 'rxjs';
import { AdminClient } from '../client/admin-base-client';
import { MaskedPayoutDetails } from '../client/admin-client';

@Injectable({
  providedIn: 'root',
})
export class AdminPayoutDetailsService {
  private readonly adminClient = inject(AdminClient);

  /**
   * A cleaner who has never saved a payout destination is answered with a 400
   * carrying `payout.not_found`, so the ordinary "nothing on file" case arrives
   * as a failure. It is normalized to `null` here — once, at the boundary — and
   * every other failure still throws, so the empty state and the error state
   * stay distinguishable to the facade.
   */
  getForEmployee(employeeId: string): Observable<MaskedPayoutDetails | null> {
    return this.adminClient.adminEmployeeClient.payoutDetails(employeeId).pipe(
      catchError((error: unknown) =>
        ABSENT_RESOURCE_ERROR_CODES.has(extractApiErrorCode(error) ?? '')
          ? of(null)
          : throwError(() => error)
      )
    );
  }
}
