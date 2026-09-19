import { CurrencyListItem, PackageListItem, ServiceListItem } from '@cleansia/customer-services';

export const CUSTOMER_CATALOG_FEATURE_KEY = 'customerCatalog';

export interface CustomerCatalogState {
  services: ServiceListItem[];
  /** The country the loaded services are priced for; null is the platform default. */
  servicesCountryId: string | null;
  packages: PackageListItem[];
  packagesCountryId: string | null;
  currencies: CurrencyListItem[];
  loading: Record<string, boolean>;
}

export const customerCatalogInitialState: CustomerCatalogState = {
  services: [],
  servicesCountryId: null,
  packages: [],
  packagesCountryId: null,
  currencies: [],
  loading: {},
};
