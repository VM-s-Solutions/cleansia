import { PackageListItem, ServiceListItem } from '@cleansia/partner-services';

export const CUSTOMER_CATALOG_FEATURE_KEY = 'customerCatalog';

export interface CustomerCatalogState {
  services: ServiceListItem[];
  packages: PackageListItem[];
  loading: Record<string, boolean>;
}

export const customerCatalogInitialState: CustomerCatalogState = {
  services: [],
  packages: [],
  loading: {},
};
