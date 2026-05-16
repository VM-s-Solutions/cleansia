import {
  ApiException,
  PackageListItem,
  ServiceListItem,
} from '@cleansia/customer-services';
import { createAction, props } from '@ngrx/store';

export const loadCustomerServices = createAction('[Customer Catalog] Load Services');
export const loadCustomerServicesSuccess = createAction(
  '[Customer Catalog] Load Services Success',
  props<{ services: ServiceListItem[] }>()
);
export const loadCustomerServicesFailure = createAction(
  '[Customer Catalog] Load Services Failure',
  props<{ error: ApiException }>()
);

export const loadCustomerPackages = createAction('[Customer Catalog] Load Packages');
export const loadCustomerPackagesSuccess = createAction(
  '[Customer Catalog] Load Packages Success',
  props<{ packages: PackageListItem[] }>()
);
export const loadCustomerPackagesFailure = createAction(
  '[Customer Catalog] Load Packages Failure',
  props<{ error: ApiException }>()
);
