import { createReducer, on } from '@ngrx/store';
import * as CatalogActions from './catalog.actions';
import { customerCatalogInitialState } from './catalog.state';

export const customerCatalogReducer = createReducer(
  customerCatalogInitialState,
  on(CatalogActions.loadCustomerServices, (state) => ({
    ...state,
    loading: { ...state.loading, services: true },
  })),
  on(CatalogActions.loadCustomerServicesSuccess, (state, { services }) => ({
    ...state,
    services,
    loading: { ...state.loading, services: false },
  })),
  on(CatalogActions.loadCustomerServicesFailure, (state) => ({
    ...state,
    loading: { ...state.loading, services: false },
  })),
  on(CatalogActions.loadCustomerPackages, (state) => ({
    ...state,
    loading: { ...state.loading, packages: true },
  })),
  on(CatalogActions.loadCustomerPackagesSuccess, (state, { packages }) => ({
    ...state,
    packages,
    loading: { ...state.loading, packages: false },
  })),
  on(CatalogActions.loadCustomerPackagesFailure, (state) => ({
    ...state,
    loading: { ...state.loading, packages: false },
  }))
);
