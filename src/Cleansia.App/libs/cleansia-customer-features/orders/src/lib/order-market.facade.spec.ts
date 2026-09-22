import { TestBed } from '@angular/core/testing';
import { MarketListItem, OrderItem, OrderListItem } from '@cleansia/customer-services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { readFileSync } from 'fs';
import { join } from 'path';
import { OrderMarketFacade } from './order-market.facade';

describe('OrderMarketFacade', () => {
  let facade: OrderMarketFacade;
  let store: MockStore;
  let translate: TranslateService;
  const markets = [
    MarketListItem.fromJS({ countryId: 'cz', isoCode: 'CZE', name: 'Czechia', currencyCode: 'CZK', translations: { cs: { name: 'Česko' } } }),
    MarketListItem.fromJS({ countryId: 'sk', isoCode: 'SVK', name: 'Slovakia', currencyCode: 'EUR', translations: { cs: { name: 'Slovensko' } } }),
  ];
  const state = (selectedIsoCode: string) => ({ customerMarket: { markets, selectedIsoCode, loadFailed: false } });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [OrderMarketFacade, provideMockStore({ initialState: state('CZE') })],
    });
    store = TestBed.inject(MockStore);
    translate = TestBed.inject(TranslateService);
    for (const lang of ['en', 'cs']) translate.setTranslation(lang, { pages: { orders: {
      market_label: '{{country}} · {{currency}}', market_unknown: lang === 'cs' ? 'Trh není k dispozici' : 'Market unavailable',
    } } });
    translate.use('en');
    facade = TestBed.inject(OrderMarketFacade);
  });

  it('keeps each historical order in its own market when the browsing market changes', () => {
    const cz = OrderListItem.fromJS({ countryId: 'cz', currency: { code: 'CZK' } });
    const sk = OrderItem.fromJS({ countryId: 'sk', currency: { code: 'EUR' } });
    expect(facade.label(cz)).toBe('Czechia · CZK');
    expect(facade.label(sk)).toBe('Slovakia · EUR');
    store.setState(state('SVK'));
    expect(facade.label(cz)).toBe('Czechia · CZK');
    expect(facade.label(sk)).toBe('Slovakia · EUR');
  });

  it('uses the order currency even when the directory currency differs and refreshes the language', () => {
    const order = OrderListItem.fromJS({ countryId: 'cz', currency: { code: 'EUR' } });
    expect(facade.label(order)).toBe('Czechia · EUR');
    translate.use('cs');
    expect(facade.label(order)).toBe('Česko · EUR');
  });

  it('does not guess the country from the selected market or currency', () => {
    expect(facade.label(OrderListItem.fromJS({ countryId: 'retired', currency: { code: 'EUR' } })))
      .toBe('Market unavailable · EUR');
    expect(facade.label(null)).toBe('Market unavailable · —');
  });

  it('renders the resolver on both list and detail', () => {
    expect(readFileSync(join(__dirname, 'orders/orders.component.html'), 'utf8')).toContain('market.label(order)');
    expect(readFileSync(join(__dirname, 'order-detail/order-detail.component.html'), 'utf8')).toContain('market.label(order())');
  });
});
