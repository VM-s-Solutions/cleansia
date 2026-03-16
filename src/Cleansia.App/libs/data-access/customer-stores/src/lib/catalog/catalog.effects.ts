import { inject, Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { CustomerClient } from '@cleansia/customer-services';
import { catchError, map, of, switchMap } from 'rxjs';
import * as CatalogActions from './catalog.actions';

@Injectable()
export class CustomerCatalogEffects {
  private readonly actions$ = inject(Actions);
  private readonly customerClient = inject(CustomerClient);

  loadServices$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CatalogActions.loadCustomerServices),
      switchMap(() =>
        this.customerClient.serviceClient.getOverview().pipe(
          map((services) => CatalogActions.loadCustomerServicesSuccess({ services })),
          catchError((error) => of(CatalogActions.loadCustomerServicesFailure({ error })))
        )
      )
    )
  );

  loadPackages$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CatalogActions.loadCustomerPackages),
      switchMap(() =>
        this.customerClient.packageClient.getOverview().pipe(
          map((packages) => CatalogActions.loadCustomerPackagesSuccess({ packages })),
          catchError((error) => of(CatalogActions.loadCustomerPackagesFailure({ error })))
        )
      )
    )
  );
}
