import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  DirtinessLevel,
  GetPropertySizePresetsPropertySizePresetDto,
  MarketListItem,
  QuoteOrderCommand,
  QuoteOrderResponse,
} from '@cleansia/customer-services';
import { chooseMarket, selectMarket, selectMarkets } from '@cleansia/customer-stores';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, Subject, of, throwError } from 'rxjs';
import { QuickQuoteFacade } from './quick-quote.facade';

const CZE = MarketListItem.fromJS({
  countryId: 'cze-id',
  isoCode: 'CZE',
  isoAlpha2: 'CZ',
  currencyCode: 'CZK',
  isDefault: true,
});
const SVK = MarketListItem.fromJS({
  countryId: 'svk-id',
  isoCode: 'SVK',
  isoAlpha2: 'SK',
  currencyCode: 'EUR',
  isDefault: false,
});

function preset(code: string, rooms: number, bathrooms: number): GetPropertySizePresetsPropertySizePresetDto {
  return GetPropertySizePresetsPropertySizePresetDto.fromJS({ code, label: code, sortOrder: rooms, rooms, bathrooms });
}

const CZ_SIZES = [preset('CZ_1KK', 1, 1), preset('CZ_2KK', 2, 1), preset('CZ_3KK', 3, 1)];
const SK_SIZES = [preset('SK_2', 2, 1), preset('SK_4', 4, 2)];

describe('QuickQuoteFacade', () => {
  let facade: QuickQuoteFacade;
  let store: MockStore;
  let quote: jest.Mock;
  let getPropertySizes: jest.Mock;
  let langChange$: Subject<{ lang: string }>;

  function build(market: MarketListItem | null = CZE, platform: 'browser' | 'server' = 'browser'): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        QuickQuoteFacade,
        provideMockStore({
          selectors: [
            { selector: selectMarkets, value: market ? [CZE, SVK] : [] },
            { selector: selectMarket, value: market },
          ],
        }),
        { provide: PLATFORM_ID, useValue: platform },
        {
          provide: CustomerClient,
          useValue: { orderClient: { quote }, countryClient: { getPropertySizes } },
        },
        { provide: TranslateService, useValue: { currentLang: 'cs', onLangChange: langChange$ } },
      ],
    });
    store = TestBed.inject(MockStore);
    jest.spyOn(store, 'dispatch');
    facade = TestBed.inject(QuickQuoteFacade);
  }

  const lastCommand = (): QuoteOrderCommand => quote.mock.calls[quote.mock.calls.length - 1][0];

  beforeEach(() => {
    langChange$ = new Subject();
    quote = jest.fn().mockReturnValue(of(QuoteOrderResponse.fromJS({ totalPrice: 1890, currencyCode: 'CZK' })));
    getPropertySizes = jest.fn().mockImplementation((isoCode: string) => of(isoCode === 'SVK' ? SK_SIZES : CZ_SIZES));
    build();
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('the market the quote is priced in', () => {
    it('sends the chosen market as the country', () => {
      facade.selectService('svc-1');

      expect(lastCommand().countryId).toBe('cze-id');
    });

    it('re-quotes in the new market when the customer switches', () => {
      facade.selectService('svc-1');
      store.overrideSelector(selectMarket, SVK);
      store.refreshState();

      expect(quote).toHaveBeenCalledTimes(2);
      expect(lastCommand().countryId).toBe('svk-id');
    });

    // The no-market shape (ADR-0058 D3): nothing names a country and the quote's own code is the
    // only unit on screen.
    it('sends no country when no market resolved, and labels the amount from the quote', () => {
      build(null);

      facade.selectService('svc-1');

      expect(lastCommand().countryId).toBeUndefined();
      expect(facade.hasMarket()).toBe(false);
      expect(facade.amountLabel()).toBe('1890 CZK');
    });

    // With a market the chip beside the number is the unit, so the number stands alone.
    it('prints the number alone when the chip carries the unit', () => {
      facade.selectService('svc-1');

      expect(facade.hasMarket()).toBe(true);
      expect(facade.amountLabel()).toBe('1890');
    });

    it('hands the pick to the store', () => {
      facade.chooseMarket('SVK');

      expect(store.dispatch).toHaveBeenCalledWith(chooseMarket({ isoCode: 'SVK' }));
    });
  });

  // The quote refuses a start in the past or under the 2 h floor, as the booking does, so only a start the
  // visitor chose is sent, and a chosen time the new day cannot take is dropped.
  describe('the start the quote is priced at', () => {
    beforeEach(() => jest.useFakeTimers({ now: new Date(2026, 10, 16, 14, 0) }));
    afterEach(() => jest.useRealTimers());

    it('sends no start for a date with no time', () => {
      facade.selectService('svc-1');
      facade.selectDate('2026-11-16');

      expect(lastCommand().cleaningDate).toBeUndefined();
    });

    it('sends the chosen date and time as a local start', () => {
      facade.selectService('svc-1');
      facade.selectDate('2026-11-17');
      facade.selectTime('10:00');

      expect(lastCommand().cleaningDate).toEqual(new Date(2026, 10, 17, 10, 0));
    });

    it('drops a time picked for another day when today cannot take it, and sends no start', () => {
      facade.selectService('svc-1');
      facade.selectDate('2026-11-17');
      facade.selectTime('10:00');

      facade.selectDate('2026-11-16');

      expect(facade.cleaningTime()).toBeNull();
      expect(lastCommand().cleaningDate).toBeUndefined();
    });

    it('keeps a time today can still take', () => {
      facade.selectService('svc-1');
      facade.selectDate('2026-11-17');
      facade.selectTime('17:00');

      facade.selectDate('2026-11-16');

      expect(facade.cleaningTime()).toBe('17:00');
      expect(lastCommand().cleaningDate).toEqual(new Date(2026, 10, 16, 17, 0));
    });
  });

  describe('the dirtiness level', () => {
    it('is quoted as Normal until the visitor picks one, then at the level picked', () => {
      facade.selectService('svc-1');
      expect(lastCommand().dirtinessLevel).toBe(DirtinessLevel.Normal);

      facade.selectDirtinessLevel(DirtinessLevel.Heavy);

      expect(quote).toHaveBeenCalledTimes(2);
      expect(lastCommand().dirtinessLevel).toBe(DirtinessLevel.Heavy);
    });

    it('states the surcharge inside the price, named for the level the quote priced', () => {
      quote.mockReturnValue(
        of(
          QuoteOrderResponse.fromJS({
            totalPrice: 3024,
            currencyCode: 'CZK',
            dirtinessLevel: DirtinessLevel.Heavy,
            dirtinessSurchargeAmount: 1134,
          }),
        ),
      );

      facade.selectService('svc-1');

      expect(facade.dirtinessSurcharge()).toEqual({
        lineKey: 'pages.order.dirtiness.heavy.surcharge_line',
        amount: '1134',
      });
    });

    it('states no surcharge when the level adds none', () => {
      facade.selectService('svc-1');

      expect(facade.dirtinessSurcharge()).toBeNull();
    });
  });

  describe('property sizes', () => {
    it('reads the market’s presets in the current language and pre-selects the third', () => {
      expect(getPropertySizes).toHaveBeenCalledWith('CZE', 'cs');
      expect(facade.sizes().map((s) => s.code)).toEqual(['CZ_1KK', 'CZ_2KK', 'CZ_3KK']);
      expect(facade.selectedSize().code).toBe('CZ_3KK');
    });

    it('re-reads the presets when the market changes and re-quotes with the new size', () => {
      facade.selectService('svc-1');
      store.overrideSelector(selectMarket, SVK);
      store.refreshState();

      expect(getPropertySizes).toHaveBeenLastCalledWith('SVK', 'cs');
      expect(facade.sizes().map((s) => s.code)).toEqual(['SK_2', 'SK_4']);
      expect(facade.selectedSize().code).toBe('SK_2');
      expect(lastCommand().rooms).toBe(2);
    });

    it('re-reads the labels when the language changes', () => {
      langChange$.next({ lang: 'en' });

      expect(getPropertySizes).toHaveBeenLastCalledWith('CZE', 'en');
    });

    it('keeps the customer’s pick across a label reload', () => {
      facade.selectSize(facade.sizes()[0]);
      langChange$.next({ lang: 'en' });

      expect(facade.selectedSize().code).toBe('CZ_1KK');
    });

    it('offers no sizes without a market, and still quotes a default home', () => {
      getPropertySizes.mockClear();
      build(null);

      facade.selectService('svc-1');

      expect(getPropertySizes).not.toHaveBeenCalled();
      expect(facade.sizes()).toEqual([]);
      expect(lastCommand().rooms).toBe(3);
      expect(lastCommand().bathrooms).toBe(1);
    });

    it('offers no sizes when the read fails, rather than failing the calculator', () => {
      getPropertySizes.mockReturnValue(throwError(() => new Error('offline')));
      build();

      expect(facade.sizes()).toEqual([]);
      expect(facade.showSizeRow()).toBe(false);
    });
  });

  describe('the size row while the presets are in flight', () => {
    let presets$: Subject<GetPropertySizePresetsPropertySizePresetDto[]>;

    beforeEach(() => {
      presets$ = new Subject();
      getPropertySizes.mockReturnValue(presets$);
      build();
    });

    it('is shown from the first render, before the market’s presets arrive', () => {
      expect(facade.sizes()).toEqual([]);
      expect(facade.showSizeRow()).toBe(true);
    });

    it('stays shown when the presets arrive', () => {
      presets$.next(CZ_SIZES);

      expect(facade.sizes().map((s) => s.code)).toEqual(['CZ_1KK', 'CZ_2KK', 'CZ_3KK']);
      expect(facade.showSizeRow()).toBe(true);
    });

    it('is withdrawn when the read fails', () => {
      presets$.error(new Error('offline'));

      expect(facade.showSizeRow()).toBe(false);
    });

    it('is never shown without a market', () => {
      build(null);

      expect(facade.showSizeRow()).toBe(false);
    });
  });

  describe('the size row in a server render', () => {
    it.each<[string, MarketListItem | null]>([
      ['with a market', CZE],
      ['without one', null],
    ])('is drawn in flight %s, as the browser first renders it, and nothing is read', (_, market) => {
      getPropertySizes.mockClear();

      build(market, 'server');

      expect(getPropertySizes).not.toHaveBeenCalled();
      expect(facade.sizes()).toEqual([]);
      expect(facade.showSizeRow()).toBe(true);
    });
  });

  it('never quotes during a server render', () => {
    build(CZE, 'server');

    facade.selectService('svc-1');

    expect(quote).not.toHaveBeenCalled();
  });

  it('is untouched by a language service that never emits', () => {
    langChange$ = new Subject();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        QuickQuoteFacade,
        provideMockStore({
          selectors: [
            { selector: selectMarkets, value: [CZE] },
            { selector: selectMarket, value: CZE },
          ],
        }),
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CustomerClient, useValue: { orderClient: { quote }, countryClient: { getPropertySizes } } },
        { provide: TranslateService, useValue: { currentLang: 'en', onLangChange: EMPTY } },
      ],
    });

    expect(() => TestBed.inject(QuickQuoteFacade)).not.toThrow();
  });
});
