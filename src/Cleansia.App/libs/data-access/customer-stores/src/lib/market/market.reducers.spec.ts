import { MarketListItem } from '@cleansia/customer-services';
import * as MarketActions from './market.actions';
import { customerMarketReducer } from './market.reducers';
import {
  selectHasMarketChoice,
  selectMarket,
  selectMarketCountryId,
  selectMarketCurrencyCode,
  selectMarketInsuranceCoverageAmount,
  selectMarketLoadFailed,
  selectMarketNoShowCredit,
  selectMarkets,
} from './market.selectors';
import { customerMarketInitialState, CustomerMarketState } from './market.state';

const CZE = MarketListItem.fromJS({
  countryId: 'cze-id',
  isoCode: 'CZE',
  isoAlpha2: 'CZ',
  currencyCode: 'CZK',
  isDefault: true,
  noShowCredit: 250,
  insuranceCoverageAmount: null,
});
const SVK = MarketListItem.fromJS({
  countryId: 'svk-id',
  isoCode: 'SVK',
  isoAlpha2: 'SK',
  currencyCode: 'EUR',
  isDefault: false,
  noShowCredit: null,
  insuranceCoverageAmount: 1000000,
});

describe('customerMarketReducer', () => {
  it('starts with no markets, nothing selected and no failure', () => {
    expect(customerMarketReducer(undefined, { type: '@@init' })).toEqual({
      markets: [],
      selectedIsoCode: null,
      loadFailed: false,
    });
  });

  it('holds the list and the resolved code on success, and clears a previous failure', () => {
    const failed = customerMarketReducer(customerMarketInitialState, MarketActions.loadMarketsFailure());

    const loaded = customerMarketReducer(
      failed,
      MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'SVK' }),
    );

    expect(loaded).toEqual({ markets: [CZE, SVK], selectedIsoCode: 'SVK', loadFailed: false });
  });

  // The failure shape (ADR-0058 D3): no market, so readers send no country and nothing renders a
  // guessed unit. A list from an earlier success is dropped rather than kept, because the code it
  // was resolved against may no longer be listed.
  it('marks the load failed and leaves nothing selected', () => {
    const loaded = customerMarketReducer(
      customerMarketInitialState,
      MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'SVK' }),
    );

    const failed = customerMarketReducer(loaded, MarketActions.loadMarketsFailure());

    expect(failed).toEqual({ markets: [], selectedIsoCode: null, loadFailed: true });
  });

  it('switches the selected code when the customer picks a listed market', () => {
    const loaded = customerMarketReducer(
      customerMarketInitialState,
      MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'CZE' }),
    );

    const switched = customerMarketReducer(loaded, MarketActions.chooseMarket({ isoCode: 'SVK' }));

    expect(switched.selectedIsoCode).toBe('SVK');
  });

  it('ignores a pick that names no listed market', () => {
    const loaded = customerMarketReducer(
      customerMarketInitialState,
      MarketActions.loadMarketsSuccess({ markets: [CZE, SVK], selectedIsoCode: 'CZE' }),
    );

    const unchanged = customerMarketReducer(loaded, MarketActions.chooseMarket({ isoCode: 'POL' }));

    expect(unchanged.selectedIsoCode).toBe('CZE');
  });
});

describe('market selectors', () => {
  const loaded: CustomerMarketState = { markets: [CZE, SVK], selectedIsoCode: 'SVK', loadFailed: false };
  const failed: CustomerMarketState = { markets: [], selectedIsoCode: null, loadFailed: true };

  it('project the selected market and its facts', () => {
    expect(selectMarkets.projector(loaded)).toEqual([CZE, SVK]);
    expect(selectMarket.projector(loaded)).toBe(SVK);
    expect(selectMarketCountryId.projector(SVK)).toBe('svk-id');
    expect(selectMarketCurrencyCode.projector(SVK)).toBe('EUR');
    expect(selectMarketNoShowCredit.projector(CZE)).toBe(250);
    expect(selectMarketNoShowCredit.projector(SVK)).toBeNull();
    expect(selectMarketInsuranceCoverageAmount.projector(SVK)).toBe(1000000);
    expect(selectMarketInsuranceCoverageAmount.projector(CZE)).toBeNull();
    expect(selectMarketLoadFailed.projector(loaded)).toBe(false);
  });

  it('answer null for every fact when no market resolved', () => {
    expect(selectMarket.projector(failed)).toBeNull();
    expect(selectMarketCountryId.projector(null)).toBeNull();
    expect(selectMarketCurrencyCode.projector(null)).toBeNull();
    expect(selectMarketNoShowCredit.projector(null)).toBeNull();
    expect(selectMarketInsuranceCoverageAmount.projector(null)).toBeNull();
    expect(selectMarketLoadFailed.projector(failed)).toBe(true);
  });

  // The selector is a control only with something to choose between; with one market the chip is
  // a static label and the navbar/footer pills are not drawn at all (design spec D-2/D-3).
  it('offer a choice only with two or more markets', () => {
    expect(selectHasMarketChoice.projector([CZE, SVK])).toBe(true);
    expect(selectHasMarketChoice.projector([CZE])).toBe(false);
    expect(selectHasMarketChoice.projector([])).toBe(false);
  });
});
