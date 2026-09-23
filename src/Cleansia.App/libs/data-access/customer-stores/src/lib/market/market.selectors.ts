import { MarketListItem } from '@cleansia/customer-services';
import { createFeatureSelector, createSelector } from '@ngrx/store';
import { CUSTOMER_MARKET_FEATURE_KEY, CustomerMarketState } from './market.state';

export const selectCustomerMarketState =
  createFeatureSelector<CustomerMarketState>(CUSTOMER_MARKET_FEATURE_KEY);

export const selectMarkets = createSelector(
  selectCustomerMarketState,
  (state: CustomerMarketState) => state.markets
);

/** The chosen market, or null when none resolved (the list failed or is empty). */
export const selectMarket = createSelector(
  selectCustomerMarketState,
  (state: CustomerMarketState): MarketListItem | null =>
    state.markets.find((market) => market.isoCode === state.selectedIsoCode) ?? null
);

/** What every pre-address reader sends as `countryId`; null means "send none". */
export const selectMarketCountryId = createSelector(
  selectMarket,
  (market: MarketListItem | null) => market?.countryId ?? null
);

export const selectMarketCurrencyCode = createSelector(
  selectMarket,
  (market: MarketListItem | null) => market?.currencyCode ?? null
);

export const selectMarketNoShowCredit = createSelector(
  selectMarket,
  (market: MarketListItem | null) => market?.noShowCredit ?? null
);

export const selectMarketLoadFailed = createSelector(
  selectCustomerMarketState,
  (state: CustomerMarketState) => state.loadFailed
);

/** A switcher is a control only with something to choose between. */
export const selectHasMarketChoice = createSelector(
  selectMarkets,
  (markets: MarketListItem[]) => markets.length >= 2
);
