import { inject, Injectable } from '@angular/core';
import { CustomerAuthService, CustomerClient, GetCurrentUserQuery } from '@cleansia/customer-services';
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
        this.customerClient.userClient
          .getCurrent(new GetCurrentUserQuery())
          .pipe(
            map((user) =>
              CustomerUserActions.loadCustomerUserSuccess({ user })
            ),
            catchError((error) =>
              of(CustomerUserActions.loadCustomerUserFailure({ error }))
            )
          )
      )
    )
  );

  logout$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerUserActions.customerLogout),
      mergeMap(() => this.authService.logout().pipe(
        map(() => CustomerUserActions.customerLogoutSuccess()),
        catchError((error) => of(CustomerUserActions.customerLogoutFailure({ error })))
      ))
    )
  );
}
