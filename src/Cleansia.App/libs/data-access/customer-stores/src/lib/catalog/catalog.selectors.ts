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

export const selectCustomerCatalogLoading = createSelector(
  selectCustomerCatalogState,
  (state: CustomerCatalogState) =>
    state.loading['services'] || state.loading['packages'] || false
);
