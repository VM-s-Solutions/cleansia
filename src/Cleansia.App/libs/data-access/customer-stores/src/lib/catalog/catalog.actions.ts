import {
  ApiException,
  CurrencyListItem,
  PackageListItem,
  ServiceListItem,
} from '@cleansia/customer-services';
import { createAction, props } from '@ngrx/store';

/**
 * `countryId` names the market the catalogue is priced for — the service address's country. Null
 * is the platform default, which is what every surface without an address reads.
 */
export const loadCustomerServices = createAction(
  '[Customer Catalog] Load Services',
  (countryId: string | null = null) => ({ countryId })
);
export const loadCustomerServicesSuccess = createAction(
  '[Customer Catalog] Load Services Success',
  props<{ services: ServiceListItem[]; countryId: string | null }>()
);
export const loadCustomerServicesFailure = createAction(
  '[Customer Catalog] Load Services Failure',
  props<{ error: ApiException }>()
);

export const loadCustomerPackages = createAction(
  '[Customer Catalog] Load Packages',
  (countryId: string | null = null) => ({ countryId })
);
export const loadCustomerPackagesSuccess = createAction(
  '[Customer Catalog] Load Packages Success',
  props<{ packages: PackageListItem[]; countryId: string | null }>()
);
export const loadCustomerPackagesFailure = createAction(
  '[Customer Catalog] Load Packages Failure',
  props<{ error: ApiException }>()
);

export const loadCustomerCurrencies = createAction('[Customer Catalog] Load Currencies');
export const loadCustomerCurrenciesSuccess = createAction(
  '[Customer Catalog] Load Currencies Success',
  props<{ currencies: CurrencyListItem[] }>()
);
export const loadCustomerCurrenciesFailure = createAction(
  '[Customer Catalog] Load Currencies Failure',
  props<{ error: ApiException }>()
);
