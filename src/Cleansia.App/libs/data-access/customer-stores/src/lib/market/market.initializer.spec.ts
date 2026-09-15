import { PLATFORM_ID, REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient, MarketListItem } from '@cleansia/customer-services';
import { Store } from '@ngrx/store';
import { of, throwError } from 'rxjs';
import * as MarketActions from './market.actions';
import { initializeMarket } from './market.initializer';

const CZE = MarketListItem.fromJS({ countryId: 'cze-id', isoCode: 'CZE', currencyCode: 'CZK', isDefault: true });
const SVK = MarketListItem.fromJS({ countryId: 'svk-id', isoCode: 'SVK', currencyCode: 'EUR', isDefault: false });

describe('initializeMarket', () => {
  let getOverview: jest.Mock;
  let dispatch: jest.Mock;

  const run = (platform: 'server' | 'browser', cookieHeader?: string): Promise<void> => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: PLATFORM_ID, useValue: platform },
        { provide: Store, useValue: { dispatch } },
        { provide: CustomerClient, useValue: { marketClient: { getOverview } } },
        {
          provide: REQUEST,
          useValue:
            platform === 'server'
              ? { headers: new Headers(cookieHeader ? { cookie: cookieHeader } : {}) }
              : null,
        },
      ],
    });
    return TestBed.runInInjectionContext(initializeMarket)();
  };

  beforeEach(() => {
    getOverview = jest.fn().mockReturnValue(of([CZE, SVK]));
    dispatch = jest.fn();
    document.cookie = 'preferred_market=; path=/; max-age=0';
  });

  // The closest pin to "a cookie-less request renders the default market": the server branch
  // reads only the request, and with no cookie the resolution is the `isDefault` row.
  describe('on the server', () => {
    it('resolves the default market for a request with no cookie and writes nothing', async () => {
      await run('server');

      expect(dispatch).toHaveBeenCalledWith(
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'CZE' }),
      );
      expect(document.cookie).not.toContain('preferred_market');
    });

    it('resolves the market the request cookie names', async () => {
      await run('server', 'preferred_language=cs; preferred_market=SVK');

      expect(dispatch).toHaveBeenCalledWith(
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'SVK' }),
      );
    });

    it('falls to the default for a cookie naming no listed market', async () => {
      await run('server', 'preferred_market=<script>alert(1)</script>');

      expect(dispatch).toHaveBeenCalledWith(
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'CZE' }),
      );
    });

    // The render must not fail on this call — it is the anonymous landing page's bootstrap.
    it('never throws when the list cannot be loaded, and marks the load failed', async () => {
      getOverview.mockReturnValue(throwError(() => new Error('offline')));

      await expect(run('server')).resolves.toBeUndefined();

      expect(dispatch).toHaveBeenCalledWith(MarketActions.loadMarketsFailure());
    });
  });

  describe('in the browser', () => {
    it('reads document.cookie and persists the resolved code', async () => {
      document.cookie = 'preferred_market=SVK; path=/';

      await run('browser');

      expect(dispatch).toHaveBeenCalledWith(
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'SVK' }),
      );
      expect(document.cookie).toContain('preferred_market=SVK');
    });

    it('overwrites a cookie naming a delisted market with the default', async () => {
      document.cookie = 'preferred_market=POL; path=/';

      await run('browser');

      expect(dispatch).toHaveBeenCalledWith(
        MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'CZE' }),
      );
      expect(document.cookie).toContain('preferred_market=CZE');
      expect(document.cookie).not.toContain('POL');
    });

    it('persists nothing and marks the load failed when the list cannot be loaded', async () => {
      getOverview.mockReturnValue(throwError(() => new Error('offline')));

      await run('browser');

      expect(dispatch).toHaveBeenCalledWith(MarketActions.loadMarketsFailure());
      expect(document.cookie).not.toContain('preferred_market');
    });

    // The generated client answers a non-array 200 body with null; that is not a list.
    it('treats a null body as a failed load', async () => {
      getOverview.mockReturnValue(of(null));

      await run('browser');

      expect(dispatch).toHaveBeenCalledWith(MarketActions.loadMarketsFailure());
    });
  });

  // The hydration invariant (ADR-0058 D3): the same cookie against the same list resolves the
  // same market on both branches, so the client's first render equals the server's.
  it('resolves the same market on both branches for the same cookie', async () => {
    await run('server', 'preferred_market=SVK');
    const server = dispatch.mock.calls[0][0];

    dispatch.mockClear();
    document.cookie = 'preferred_market=SVK; path=/';
    await run('browser');
    const browser = dispatch.mock.calls[0][0];

    expect(browser).toEqual(server);
  });
});
