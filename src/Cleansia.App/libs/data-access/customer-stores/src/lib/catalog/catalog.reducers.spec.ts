import { CurrencyListItem, PackageListItem, ServiceListItem } from '@cleansia/customer-services';
import * as CatalogActions from './catalog.actions';
import { customerCatalogReducer } from './catalog.reducers';
import { customerCatalogInitialState } from './catalog.state';
import {
  selectCustomerCatalogLoading,
  selectCustomerDefaultCurrencyCode,
} from './catalog.selectors';

const SERVICES = [ServiceListItem.fromJS({ id: 'svc-1', name: 'Standard' })];
const PACKAGES = [PackageListItem.fromJS({ id: 'pkg-1', name: 'Deep clean' })];
const CURRENCIES = [
  CurrencyListItem.fromJS({ id: 'cur-1', code: 'CZK', isDefault: false }),
  CurrencyListItem.fromJS({ id: 'cur-2', code: 'EUR', isDefault: true }),
];
const API_ERROR = { message: 'offline' } as never;

describe('customerCatalogReducer', () => {
  it('starts with every list empty and nothing loading', () => {
    expect(customerCatalogReducer(undefined, { type: '@@init' })).toEqual({
      services: [],
      packages: [],
      currencies: [],
      loading: {},
    });
  });

  it('tracks the two loads independently, so one in flight does not blank the other', () => {
    const loadingServices = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerServices(),
    );
    const loadingBoth = customerCatalogReducer(
      loadingServices,
      CatalogActions.loadCustomerPackages(),
    );
    const servicesArrived = customerCatalogReducer(
      loadingBoth,
      CatalogActions.loadCustomerServicesSuccess({ services: SERVICES }),
    );

    expect(servicesArrived.loading).toEqual({ services: false, packages: true });
    expect(servicesArrived.services).toEqual(SERVICES);
    expect(servicesArrived.packages).toEqual([]);
  });

  it('holds the packages on success without disturbing the services', () => {
    const loaded = customerCatalogReducer(
      { ...customerCatalogInitialState, services: SERVICES },
      CatalogActions.loadCustomerPackagesSuccess({ packages: PACKAGES }),
    );

    expect(loaded).toEqual({
      services: SERVICES,
      packages: PACKAGES,
      currencies: [],
      loading: { packages: false },
    });
  });

  it('clears the loading flag on failure so the screen cannot spin forever', () => {
    const loading = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerServices(),
    );

    const failed = customerCatalogReducer(
      loading,
      CatalogActions.loadCustomerServicesFailure({ error: API_ERROR }),
    );

    expect(failed.loading).toEqual({ services: false });
    expect(selectCustomerCatalogLoading.projector(failed)).toBe(false);
  });

  // Pinned as-is, not endorsed: `CustomerCatalogState` carries no error field, so a failed catalog
  // read leaves exactly the state of a catalog that is genuinely empty. Anything rendering off
  // these selectors shows "no services available" for an outage. Adding the third state changes the
  // state shape and every consumer, so it is a follow-up — this test is here to make the change
  // visible when it happens rather than to bless the gap.
  it('leaves a failed load indistinguishable from an empty catalog', () => {
    const failed = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerServicesFailure({ error: API_ERROR }),
    );
    const emptyButFine = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerServicesSuccess({ services: [] }),
    );

    expect(failed).toEqual(emptyButFine);
  });

  it('keeps the catalog already on screen when a refresh fails', () => {
    const loaded = { ...customerCatalogInitialState, services: SERVICES };

    const failed = customerCatalogReducer(
      loaded,
      CatalogActions.loadCustomerServicesFailure({ error: API_ERROR }),
    );

    expect(failed.services).toEqual(SERVICES);
  });

  it('does not mutate the state it was given', () => {
    const before = { ...customerCatalogInitialState };

    customerCatalogReducer(
      before,
      CatalogActions.loadCustomerServicesSuccess({ services: SERVICES }),
    );

    expect(before).toEqual({ services: [], packages: [], currencies: [], loading: {} });
  });

  it('reports loading while either half is in flight', () => {
    const loadingPackages = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerPackages(),
    );

    expect(selectCustomerCatalogLoading.projector(loadingPackages)).toBe(true);
  });

  it('reports loading while the currencies are in flight, so a catalogue price is never shown unlabelled', () => {
    const loadingCurrencies = customerCatalogReducer(
      customerCatalogInitialState,
      CatalogActions.loadCustomerCurrencies(),
    );

    expect(selectCustomerCatalogLoading.projector(loadingCurrencies)).toBe(true);
    expect(loadingCurrencies.loading).toEqual({ currencies: true });
  });

  it('holds the currencies on success and clears their loading flag', () => {
    const loaded = customerCatalogReducer(
      customerCatalogReducer(customerCatalogInitialState, CatalogActions.loadCustomerCurrencies()),
      CatalogActions.loadCustomerCurrenciesSuccess({ currencies: CURRENCIES }),
    );

    expect(loaded.currencies).toEqual(CURRENCIES);
    expect(loaded.loading).toEqual({ currencies: false });
  });

  it('clears the currencies loading flag on failure', () => {
    const failed = customerCatalogReducer(
      customerCatalogReducer(customerCatalogInitialState, CatalogActions.loadCustomerCurrencies()),
      CatalogActions.loadCustomerCurrenciesFailure({ error: API_ERROR }),
    );

    expect(failed.loading).toEqual({ currencies: false });
    expect(failed.currencies).toEqual([]);
  });

  // The catalogue is priced in the platform DEFAULT currency, and the default is a flag on the
  // list rather than a position in it — the seeded CZK is first, and the day EUR becomes the
  // default it must be EUR that labels every price.
  it('names the default currency by its flag, not its position', () => {
    const loaded = { ...customerCatalogInitialState, currencies: CURRENCIES };

    expect(selectCustomerDefaultCurrencyCode.projector(loaded)).toBe('EUR');
  });

  it('has no default currency until the list arrives', () => {
    expect(selectCustomerDefaultCurrencyCode.projector(customerCatalogInitialState)).toBeNull();
  });
});
