import { MarketListItem } from '@cleansia/customer-services';
import { createAction, props } from '@ngrx/store';

export const loadMarkets = createAction('[Customer Market] Load Markets');
export const loadMarketsSuccess = createAction(
  '[Customer Market] Load Markets Success',
  props<{ markets: MarketListItem[]; selectedIsoCode: string | null }>()
);
export const loadMarketsFailure = createAction('[Customer Market] Load Markets Failure');

/** The customer's pick in the switcher; must name a listed market or it is ignored. */
export const chooseMarket = createAction(
  '[Customer Market] Select Market',
  props<{ isoCode: string }>()
);
