import { isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { CustomerClient } from '@cleansia/customer-services';
import { persistPreferredMarket, readPreferredMarket, resolveMarket } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { catchError, filter, map, of, switchMap, tap, withLatestFrom } from 'rxjs';
import * as MarketActions from './market.actions';
import { selectMarket, selectMarketLoadFailed } from './market.selectors';

@Injectable()
export class CustomerMarketEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** The browser-side reload: the bootstrap read lives in `initializeMarket`. */
  loadMarkets$ = createEffect(() =>
    this.actions$.pipe(
      ofType(MarketActions.loadMarkets),
      switchMap(() =>
        this.customerClient.marketClient.getOverview().pipe(
          map((markets) => {
            if (!markets) return MarketActions.loadMarketsFailure();
            const resolved = resolveMarket(markets, readPreferredMarket(document.cookie));
            return MarketActions.loadMarketsSuccess({
              markets,
              selectedIsoCode: resolved?.isoCode ?? null,
            });
          }),
          catchError(() => of(MarketActions.loadMarketsFailure()))
        )
      )
    )
  );

  /** A failed list is retried on the next navigation, not left failed for the session. */
  retryOnNavigation$ = createEffect(() =>
    this.router.events.pipe(
      filter((event) => this.isBrowser && event instanceof NavigationEnd),
      withLatestFrom(this.store.select(selectMarketLoadFailed)),
      filter(([, loadFailed]) => loadFailed),
      map(() => MarketActions.loadMarkets())
    )
  );

  /**
   * The pick is persisted from the state, not from the action: the reducer ignores a code naming no
   * listed market, and the cookie must never hold anything the list did not vouch for.
   */
  persistSelection$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(MarketActions.chooseMarket),
        filter(() => this.isBrowser),
        withLatestFrom(this.store.select(selectMarket)),
        map(([, market]) => market?.isoCode),
        filter((isoCode): isoCode is string => !!isoCode),
        tap((isoCode) => persistPreferredMarket(isoCode))
      ),
    { dispatch: false }
  );
}
