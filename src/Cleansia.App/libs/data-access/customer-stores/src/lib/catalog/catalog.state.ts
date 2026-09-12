import { CurrencyListItem, PackageListItem, ServiceListItem } from '@cleansia/customer-services';

export const CUSTOMER_CATALOG_FEATURE_KEY = 'customerCatalog';

export interface CustomerCatalogState {
  services: ServiceListItem[];
  packages: PackageListItem[];
  currencies: CurrencyListItem[];
  loading: Record<string, boolean>;
}

export const customerCatalogInitialState: CustomerCatalogState = {
  services: [],
  packages: [],
  currencies: [],
  loading: {},
};
