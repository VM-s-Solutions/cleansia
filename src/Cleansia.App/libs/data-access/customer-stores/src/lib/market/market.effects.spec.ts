import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { CustomerClient, MarketListItem } from '@cleansia/customer-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { Subject, of, throwError } from 'rxjs';
import * as MarketActions from './market.actions';
import { CustomerMarketEffects } from './market.effects';
import { selectMarket, selectMarketLoadFailed } from './market.selectors';

const CZE = MarketListItem.fromJS({ countryId: 'cze-id', isoCode: 'CZE', currencyCode: 'CZK', isDefault: true });
const SVK = MarketListItem.fromJS({ countryId: 'svk-id', isoCode: 'SVK', currencyCode: 'EUR', isDefault: false });

describe('CustomerMarketEffects', () => {
  let actions$: Subject<Action>;
  let routerEvents$: Subject<unknown>;
  let getOverview: jest.Mock;
  let store: MockStore;

  const createEffects = (platform: 'browser' | 'server' = 'browser'): CustomerMarketEffects => {
    TestBed.configureTestingModule({
      providers: [
        CustomerMarketEffects,
        provideMockActions(() => actions$),
        provideMockStore({
          selectors: [
            { selector: selectMarketLoadFailed, value: false },
            { selector: selectMarket, value: CZE },
          ],
        }),
        { provide: PLATFORM_ID, useValue: platform },
        { provide: Router, useValue: { events: routerEvents$ } },
        { provide: CustomerClient, useValue: { marketClient: { getOverview } } },
      ],
    });
    store = TestBed.inject(MockStore);
    return TestBed.inject(CustomerMarketEffects);
  };

  const collect = (source: { subscribe: (fn: (a: Action) => void) => void }) => {
    const emitted: Action[] = [];
    source.subscribe((action) => emitted.push(action));
    return emitted;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    actions$ = new Subject<Action>();
    routerEvents$ = new Subject<unknown>();
    getOverview = jest.fn().mockReturnValue(of([CZE, SVK]));
    document.cookie = 'preferred_market=; path=/; max-age=0';
  });

  describe('loadMarkets$', () => {
    it('reloads the list and resolves against the cookie', () => {
      document.cookie = 'preferred_market=SVK; path=/';

      const emitted = collect(createEffects().loadMarkets$);
      actions$.next(MarketActions.loadMarkets());

      expect(emitted).toEqual([
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'SVK' }),
      ]);
    });

    it('maps a failure to loadMarketsFailure and stays alive for the next retry', () => {
      getOverview
        .mockReturnValueOnce(throwError(() => ({ message: 'offline' })))
        .mockReturnValueOnce(of([CZE]));

      const emitted = collect(createEffects().loadMarkets$);
      actions$.next(MarketActions.loadMarkets());
      actions$.next(MarketActions.loadMarkets());

      expect(emitted.map((a) => a.type)).toEqual([
        MarketActions.loadMarketsFailure.type,
        MarketActions.loadMarketsSuccess.type,
      ]);
    });

    it('treats a null body as a failure', () => {
      getOverview.mockReturnValue(of(null));

      const emitted = collect(createEffects().loadMarkets$);
      actions$.next(MarketActions.loadMarkets());

      expect(emitted).toEqual([MarketActions.loadMarketsFailure()]);
    });
  });

  describe('retryOnNavigation$', () => {
    it('reloads a failed list when the next navigation ends', () => {
      const effects = createEffects();
      store.overrideSelector(selectMarketLoadFailed, true);
      store.refreshState();

      const emitted = collect(effects.retryOnNavigation$);
      routerEvents$.next(new NavigationEnd(1, '/services', '/services'));

      expect(emitted).toEqual([MarketActions.loadMarkets()]);
    });

    it('does nothing while the list is loaded, and not before a navigation ends', () => {
      const emitted = collect(createEffects().retryOnNavigation$);
      routerEvents$.next(new NavigationEnd(1, '/services', '/services'));
      store.overrideSelector(selectMarketLoadFailed, true);
      store.refreshState();
      routerEvents$.next(new NavigationStart(2, '/plus'));

      expect(emitted).toEqual([]);
    });
  });

  describe('persistSelection$', () => {
    it('writes the cookie from the market the store settled on', () => {
      const effects = createEffects();
      store.overrideSelector(selectMarket, SVK);
      store.refreshState();

      effects.persistSelection$.subscribe();
      actions$.next(MarketActions.chooseMarket({ isoCode: 'SVK' }));

      expect(document.cookie).toContain('preferred_market=SVK');
    });

    it('writes nothing on the server', () => {
      const effects = createEffects('server');
      store.overrideSelector(selectMarket, SVK);
      store.refreshState();

      effects.persistSelection$.subscribe();
      actions$.next(MarketActions.chooseMarket({ isoCode: 'SVK' }));

      expect(document.cookie).not.toContain('preferred_market');
    });
  });
});
