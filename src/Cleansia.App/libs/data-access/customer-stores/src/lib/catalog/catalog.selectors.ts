import { createFeatureSelector, createSelector } from '@ngrx/store';
import {
  CUSTOMER_CATALOG_FEATURE_KEY,
  CustomerCatalogState,
} from './catalog.state';

export const selectCustomerCatalogState =
  createFeatureSelector<CustomerCatalogState>(CUSTOMER_CATALOG_FEATURE_KEY);

export const selectCustomerServices = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => state.services
);

export const selectCustomerPackages = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => state.packages
);

export const selectCustomerCurrencies = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => state.currencies
);

/**
 * The code every catalogue price is in. `ServiceListItem`, `PackageListItem` and `ExtraListItem`
 * carry a price and no currency because the catalogue is priced in the platform default, and the
 * default is a flag on the currency list rather than a position in it. Null until the list arrives.
 */
export const selectCustomerDefaultCurrencyCode = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) =>
    state.currencies.find((currency) => currency.isDefault)?.code ?? null
);

export const selectCustomerCatalogLoading = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) =>
    state.loading['services'] ||
    state.loading['packages'] ||
    state.loading['currencies'] ||
    false
);
