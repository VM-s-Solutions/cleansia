import { isPlatformBrowser } from '@angular/common';
import { inject, PLATFORM_ID, REQUEST } from '@angular/core';
import { CustomerClient, MarketListItem } from '@cleansia/customer-services';
import { persistPreferredMarket, readPreferredMarket, resolveMarket } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { catchError, firstValueFrom, of } from 'rxjs';
import * as MarketActions from './market.actions';

/**
 * Resolves the market before the first component renders, on both branches (ADR-0058 D3).
 *
 * The server reads the cookie off the request and the browser off `document.cookie` — the same
 * cookie against the same list, so the two resolve the same market and issue the same catalogue
 * URLs. It never throws: this is the anonymous landing page's bootstrap, and a failed list is the
 * no-market shape (readers send no country, nothing is persisted), not a failed render.
 */
export function initializeMarket(): () => Promise<void> {
  const store = inject(Store);
  const client = inject(CustomerClient);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  const request = inject(REQUEST, { optional: true });

  return async () => {
    const cookieHeader = isBrowser ? document.cookie : request?.headers.get('cookie');
    const markets = await firstValueFrom(
      client.marketClient.getOverview().pipe(catchError(() => of<MarketListItem[] | null>(null))),
    );
    if (!markets) {
      store.dispatch(MarketActions.loadMarketsFailure());
      return;
    }
    const resolved = resolveMarket(markets, readPreferredMarket(cookieHeader));
    store.dispatch(
      MarketActions.loadMarketsSuccess({ markets, selectedIsoCode: resolved?.isoCode ?? null }),
    );
    if (isBrowser && resolved?.isoCode) persistPreferredMarket(resolved.isoCode);
  };
}
