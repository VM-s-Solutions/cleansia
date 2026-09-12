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
          // `?? []` at the payload boundary, because the reducer stores what the action carries —
          // it spreads `services` straight into state with no default of its own. The generated
          // client answers a 200 whose body is not a JSON array (and a 204) with NULL rather than
          // an empty list, from the `result200 = null as any` branch its declared
          // `ServiceListItem[]` return type denies. The `catchError` never fires on it — null is
          // not an error — so the null would settle into the store and every selector reading it
          // would throw on the next render.
          map((services) =>
            CatalogActions.loadCustomerServicesSuccess({
              services: services ?? [],
            })
          ),
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
          // Same null, same reason — see loadServices$ above.
          map((packages) =>
            CatalogActions.loadCustomerPackagesSuccess({
              packages: packages ?? [],
            })
          ),
          catchError((error) => of(CatalogActions.loadCustomerPackagesFailure({ error })))
        )
      )
    )
  );

  loadCurrencies$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CatalogActions.loadCustomerCurrencies),
      switchMap(() =>
        this.customerClient.currencyClient.getOverview().pipe(
          // Same null, same reason — see loadServices$ above.
          map((currencies) =>
            CatalogActions.loadCustomerCurrenciesSuccess({
              currencies: currencies ?? [],
            })
          ),
          catchError((error) => of(CatalogActions.loadCustomerCurrenciesFailure({ error })))
        )
      )
    )
  );
}
