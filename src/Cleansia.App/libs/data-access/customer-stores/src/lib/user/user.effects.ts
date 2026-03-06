import { inject, Injectable } from '@angular/core';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import { UserListItem } from '@cleansia/partner-services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, mergeMap, of } from 'rxjs';
import * as CustomerUserActions from './user.actions';

@Injectable()
export class CustomerUserEffects {
  private readonly customerClient = inject(CustomerClient);
  private readonly actions$ = inject(Actions);
  private readonly authService = inject(CustomerAuthService);

  loadCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerUserActions.loadCustomerUser),
      mergeMap(() =>
        this.customerClient.authClient.login === undefined
          ? of(
              CustomerUserActions.loadCustomerUserFailure({
                error: { message: 'Not implemented' } as any,
              })
            )
          : of(
              CustomerUserActions.loadCustomerUserSuccess({
                user: {} as UserListItem,
              })
            )
      )
    )
  );

  logout$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerUserActions.customerLogout),
      mergeMap(() => {
        this.authService.logout();
        return of(CustomerUserActions.customerLogoutSuccess());
      })
    )
  );
}
