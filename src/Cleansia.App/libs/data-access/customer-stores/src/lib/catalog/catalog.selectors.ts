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

/**
 * A catalogue list together with the country it was priced for, so a reader can tell a list that
 * has landed for the address's market from the one it is still waiting to replace.
 */
export const selectCustomerServicesCatalogue = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => ({
    services: state.services,
    countryId: state.servicesCountryId,
  })
);

export const selectCustomerPackagesCatalogue = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => ({
    packages: state.packages,
    countryId: state.packagesCountryId,
  })
);

export const selectCustomerCurrencies = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) => state.currencies
);

/**
 * The code a catalogue price is in where no item is at hand to read it from: every list item
 * carries its own `currencyCode`, and this is the platform default — a flag on the currency list
 * rather than a position in it. Null until the list arrives.
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
