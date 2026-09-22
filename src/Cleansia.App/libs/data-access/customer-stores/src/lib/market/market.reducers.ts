import { createReducer, on } from '@ngrx/store';
import * as MarketActions from './market.actions';
import { customerMarketInitialState } from './market.state';

export const customerMarketReducer = createReducer(
  customerMarketInitialState,
  on(MarketActions.loadMarketsSuccess, (state, { markets, selectedIsoCode }) => ({
    ...state,
    markets,
    selectedIsoCode,
    loadFailed: false,
  })),
  on(MarketActions.loadMarketsFailure, (state) => ({
    ...state,
    markets: [],
    selectedIsoCode: null,
    loadFailed: true,
  })),
  on(MarketActions.chooseMarket, (state, { isoCode }) =>
    state.markets.some((market) => market.isoCode === isoCode)
      ? { ...state, selectedIsoCode: isoCode }
      : state
  )
);
