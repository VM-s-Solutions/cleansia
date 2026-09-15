import { MarketListItem } from '@cleansia/customer-services';

export const CUSTOMER_MARKET_FEATURE_KEY = 'customerMarket';

/**
 * The markets a customer may browse in and the one chosen (ADR-0058). `selectedIsoCode` is always
 * a code from `markets` — a stored cookie value is compared against the list, never kept as is.
 * `loadFailed` is the no-market shape: readers send no country, the chip and the selector are not
 * drawn, and the list is retried on the next navigation.
 */
export interface CustomerMarketState {
  markets: MarketListItem[];
  selectedIsoCode: string | null;
  loadFailed: boolean;
}

export const customerMarketInitialState: CustomerMarketState = {
  markets: [],
  selectedIsoCode: null,
  loadFailed: false,
};
