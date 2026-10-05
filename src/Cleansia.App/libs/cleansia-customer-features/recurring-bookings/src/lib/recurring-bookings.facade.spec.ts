import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ConsentType,
  CreateRecurringBookingCommand,
  CustomerClient,
  CustomerOrderClient,
  DeleteRecurringBookingCommand,
  DirtinessLevel,
  GetMyServingCleanersResponse,
  MembershipStatus,
  PackageClient,
  PackageListItem,
  PaymentType,
  QuoteOrderResponse,
  RecurringBookingTemplateDto,
  SavedAddressDto,
  ServiceClient,
  ServiceListItem,
  SetRecurringBookingActiveCommand,
  UpdateRecurringBookingCommand,
} from '@cleansia/customer-services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerPackagesCatalogue,
  selectCustomerServices,
  selectCustomerServicesCatalogue,
  selectMarketCountryId,
} from '@cleansia/customer-stores';
import { DialogService, HttpErrorInterceptorFn, SnackbarService } from '@cleansia/services';
import { Action } from '@ngrx/store';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Observable, of, Subject, throwError } from 'rxjs';
import { RecurringBookingsFacade } from './recurring-bookings.facade';
import { RecurrenceFrequency, RecurringPrefillParams } from './recurring-bookings.models';

describe('RecurringBookingsFacade', () => {
  let facade: RecurringBookingsFacade;
  let store: MockStore;
  let client: {
    getMine: jest.Mock;
    create: jest.Mock;
    update: jest.Mock;
    setActive: jest.Mock;
    delete: jest.Mock;
  };
  let orderClient: { quote: jest.Mock; myServingCleaners: jest.Mock };
  let quoteRoute: jest.SpyInstance;
  let membershipClient: { getMine: jest.Mock };
  let gdprClient: { consentsGet: jest.Mock };
  let savedAddressStore: {
    addresses: ReturnType<typeof signal<SavedAddressDto[]>>;
    loaded: ReturnType<typeof signal<boolean>>;
    refresh: jest.Mock;
  };
  let snackbar: {
    showError: jest.Mock;
    showSuccess: jest.Mock;
    showInfoTranslated: jest.Mock;
  };
  let dialog: { confirmTranslated: jest.Mock };

  const template = (overrides?: Partial<RecurringBookingTemplateDto>): RecurringBookingTemplateDto =>
    RecurringBookingTemplateDto.fromJS({
      id: 't1',
      isActive: true,
      ...overrides,
    });

  beforeEach(() => {
    client = {
      getMine: jest.fn().mockReturnValue(of([])),
      create: jest.fn(),
      update: jest.fn(),
      setActive: jest.fn().mockReturnValue(of(undefined)),
      delete: jest.fn().mockReturnValue(of(undefined)),
    };
    orderClient = { quote: jest.fn(), myServingCleaners: jest.fn().mockReturnValue(of([])) };
    // The facade quotes through its own toast-suppressing client, not CustomerClient's, so the
    // generated client's quote is routed to the mock the cases below arm.
    quoteRoute = jest
      .spyOn(CustomerOrderClient.prototype, 'quote')
      .mockImplementation((body) => orderClient.quote(body));
    membershipClient = {
      getMine: jest.fn().mockReturnValue(of({ hasMembership: true, status: MembershipStatus.Active })),
    };
    gdprClient = { consentsGet: jest.fn().mockReturnValue(of([])) };
    savedAddressStore = {
      addresses: signal<SavedAddressDto[]>([]),
      loaded: signal(true),
      refresh: jest.fn().mockResolvedValue(true),
    };
    snackbar = {
      showError: jest.fn(),
      showSuccess: jest.fn(),
      showInfoTranslated: jest.fn(),
    };
    dialog = { confirmTranslated: jest.fn().mockReturnValue(of(false)) };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        RecurringBookingsFacade,
        provideMockStore(),
        {
          provide: CustomerClient,
          useValue: {
            recurringBookingClient: client,
            orderClient,
            membershipClient,
            gdprClient,
          },
        },
        { provide: SavedAddressStore, useValue: savedAddressStore },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: dialog },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'en' } },
      ],
    });

    store = TestBed.inject(MockStore);
    store.overrideSelector(selectCustomerServices, []);
    store.overrideSelector(selectCustomerPackages, []);
    store.overrideSelector(selectCustomerServicesCatalogue, { services: [], countryId: null });
    store.overrideSelector(selectCustomerPackagesCatalogue, { packages: [], countryId: null });
    store.overrideSelector(selectMarketCountryId, null);
    facade = TestBed.inject(RecurringBookingsFacade);
  });

  afterEach(() => quoteRoute.mockRestore());

  // A failed renewal keeps the enrolment alive, but the server refuses a schedule while it is unpaid.
  it('shows a member whose renewal payment failed the paywall, not the list', async () => {
    membershipClient.getMine.mockReturnValue(
      of({ hasMembership: true, status: MembershipStatus.PastDue }),
    );

    await facade.initialize();

    expect(facade.isMember()).toBe(false);
    expect(client.getMine).not.toHaveBeenCalled();
  });

  // The catalogue is priced per market and the server withholds what has no price in the saved
  // address's currency, so the form re-reads it for the country of the address chosen and trims
  // the selection to what that list offers.
  describe('the catalogue follows the saved address country', () => {
    const slovakAddress = SavedAddressDto.fromJS({ id: 'addr-sk', countryId: 'svk' });
    const otherSlovakAddress = SavedAddressDto.fromJS({ id: 'addr-sk-2', countryId: 'svk' });

    it('reads the catalogue for the platform default once before an address is chosen', async () => {
      const dispatch = jest.spyOn(store, 'dispatch');

      await facade.initialize();
      TestBed.flushEffects();

      expect(dispatch).toHaveBeenCalledWith(loadCustomerServices(null));
      expect(dispatch).toHaveBeenCalledWith(loadCustomerPackages(null));
      const dispatched = dispatch.mock.calls.map(([action]) => action as unknown as Action);
      expect(dispatched.filter((action) => action.type === loadCustomerServices.type)).toHaveLength(1);
    });

    it('prices the form for the chosen market before an address is chosen', async () => {
      store.overrideSelector(selectMarketCountryId, 'svk');
      store.refreshState();
      const dispatch = jest.spyOn(store, 'dispatch');

      await facade.initialize();
      TestBed.flushEffects();

      expect(dispatch).toHaveBeenCalledWith(loadCustomerServices('svk'));
      expect(dispatch).toHaveBeenCalledWith(loadCustomerPackages('svk'));
    });

    it("re-reads services and packages for the chosen address's country", async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress]);
      jest.spyOn(store, 'dispatch');

      facade.updateFormData({ savedAddressId: 'addr-sk' });
      TestBed.flushEffects();

      expect(store.dispatch).toHaveBeenCalledWith(loadCustomerServices('svk'));
      expect(store.dispatch).toHaveBeenCalledWith(loadCustomerPackages('svk'));
    });

    it('does not re-read when another address in the same country is chosen', async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress, otherSlovakAddress]);
      facade.updateFormData({ savedAddressId: 'addr-sk' });
      TestBed.flushEffects();
      jest.spyOn(store, 'dispatch');

      facade.updateFormData({ savedAddressId: 'addr-sk-2' });
      TestBed.flushEffects();

      expect(store.dispatch).not.toHaveBeenCalled();
    });

    it('drops a selected service the country does not offer and says so', async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress]);
      facade.updateFormData({ selectedServiceIds: ['s1', 's2'], savedAddressId: 'addr-sk' });

      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: [ServiceListItem.fromJS({ id: 's1' })],
        countryId: 'svk',
      });
      store.refreshState();

      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith(
        'pages.order.wizard.catalogue_changed_for_country',
      );
    });

    it('drops a selected package the country does not offer and says so', async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress]);
      facade.updateFormData({ selectedPackageIds: ['p1', 'p2'], savedAddressId: 'addr-sk' });

      store.overrideSelector(selectCustomerPackagesCatalogue, {
        packages: [PackageListItem.fromJS({ id: 'p2' })],
        countryId: 'svk',
      });
      store.refreshState();

      expect(facade.formData().selectedPackageIds).toEqual(['p2']);
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith(
        'pages.order.wizard.catalogue_changed_for_country',
      );
    });

    it('keeps the selection while the list on screen is still the default-priced one', async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress]);
      facade.updateFormData({ selectedServiceIds: ['s1', 's2'], savedAddressId: 'addr-sk' });

      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: [ServiceListItem.fromJS({ id: 's1' })],
        countryId: null,
      });
      store.refreshState();

      expect(facade.formData().selectedServiceIds).toEqual(['s1', 's2']);
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalled();
    });

    it('says nothing when every selection survives the new country', async () => {
      await facade.initialize();
      savedAddressStore.addresses.set([slovakAddress]);
      facade.updateFormData({ selectedServiceIds: ['s1'], savedAddressId: 'addr-sk' });

      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: [ServiceListItem.fromJS({ id: 's1' }), ServiceListItem.fromJS({ id: 's2' })],
        countryId: 'svk',
      });
      store.refreshState();

      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalled();
    });
  });

  // The server refuses to price a schedule holding a service or package retired since it was saved,
  // so its card has no price; the card says why. Each schedule is judged against the catalogue of its
  // own market, read for the list, and never before that read has landed.
  describe('a schedule holding something its market no longer lists', () => {
    const addresses = [
      SavedAddressDto.fromJS({ id: 'addr-cz', countryId: 'cze' }),
      SavedAddressDto.fromJS({ id: 'addr-cz-2', countryId: 'cze' }),
      SavedAddressDto.fromJS({ id: 'addr-sk', countryId: 'svk' }),
      SavedAddressDto.fromJS({ id: 'addr-none' }),
    ];
    const catalogues: Record<string, { services: string[]; packages: string[] }> = {
      cze: { services: ['s1'], packages: ['p1'] },
      svk: { services: ['s-sk'], packages: [] },
      default: { services: ['s1'], packages: [] },
    };
    let servicesRead: jest.SpyInstance;
    let packagesRead: jest.SpyInstance;
    const schedule = (id: string, selection: Partial<RecurringBookingTemplateDto>) =>
      template({ id, savedAddressId: 'addr-cz', selectedServiceIds: [], selectedPackageIds: [], ...selection });
    const read = async (...schedules: RecurringBookingTemplateDto[]) => {
      facade.templates.set(schedules);
      facade.readScheduleMarkets();
      await Promise.resolve();
      await Promise.resolve();
    };

    beforeEach(() => {
      savedAddressStore.addresses.set(addresses);
      servicesRead = jest
        .spyOn(ServiceClient.prototype, 'getOverview')
        .mockImplementation((countryId) =>
          of(catalogues[countryId ?? 'default'].services.map((id) => ServiceListItem.fromJS({ id }))),
        );
      packagesRead = jest
        .spyOn(PackageClient.prototype, 'getOverview')
        .mockImplementation((countryId) =>
          of(catalogues[countryId ?? 'default'].packages.map((id) => PackageListItem.fromJS({ id }))),
        );
    });

    afterEach(() => {
      servicesRead.mockRestore();
      packagesRead.mockRestore();
    });

    it('is said of a schedule holding a retired service', async () => {
      const holding = schedule('t1', { selectedServiceIds: ['s1', 'gone'] });
      await read(holding);

      expect(facade.holdsRetiredEntry(holding)).toBe(true);
    });

    it('is said of a schedule holding a retired package', async () => {
      const holding = schedule('t1', { selectedPackageIds: ['p1', 'gone'] });
      await read(holding);

      expect(facade.holdsRetiredEntry(holding)).toBe(true);
    });

    it('is not said when the market still lists everything the schedule holds', async () => {
      const listed = schedule('t1', { selectedServiceIds: ['s1'], selectedPackageIds: ['p1'] });
      await read(listed);

      expect(facade.holdsRetiredEntry(listed)).toBe(false);
    });

    it("is not said while the schedule's market has not been read, or when the read failed", async () => {
      const holding = schedule('t1', { selectedServiceIds: ['gone'] });
      facade.templates.set([holding]);
      expect(facade.holdsRetiredEntry(holding)).toBe(false);

      servicesRead.mockReturnValue(throwError(() => new Error('offline')));
      await read(holding);

      expect(facade.holdsRetiredEntry(holding)).toBe(false);
    });

    it('is not said of a schedule whose address, and so whose market, is unknown', async () => {
      const orphan = schedule('t1', { savedAddressId: 'addr-gone', selectedServiceIds: ['gone'] });
      await read(orphan);

      expect(servicesRead).not.toHaveBeenCalled();
      expect(facade.holdsRetiredEntry(orphan)).toBe(false);
    });

    it("judges each schedule against its own market's catalogue, not the one the store holds", async () => {
      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: [ServiceListItem.fromJS({ id: 's-sk' })],
        countryId: 'cze',
      });
      store.refreshState();
      const czech = schedule('t-cz', { selectedServiceIds: ['s-sk'] });
      const slovak = schedule('t-sk', { savedAddressId: 'addr-sk', selectedServiceIds: ['s-sk'] });
      const noCountry = schedule('t-none', { savedAddressId: 'addr-none', selectedServiceIds: ['s1'] });
      await read(czech, slovak, noCountry);

      expect(facade.holdsRetiredEntry(czech)).toBe(true);
      expect(facade.holdsRetiredEntry(slovak)).toBe(false);
      expect(facade.holdsRetiredEntry(noCountry)).toBe(false);
    });

    it('reads each market once, the platform default for an address with no country', async () => {
      const schedules = [
        schedule('t1', {}),
        schedule('t2', { savedAddressId: 'addr-cz-2' }),
        schedule('t3', { savedAddressId: 'addr-sk' }),
        schedule('t4', { savedAddressId: 'addr-none' }),
      ];
      await read(...schedules);
      await read(...schedules);

      expect(servicesRead.mock.calls.map(([countryId]) => countryId)).toEqual(['cze', 'svk', undefined]);
      expect(packagesRead.mock.calls.map(([countryId]) => countryId)).toEqual(['cze', 'svk', undefined]);
    });
  });

  // A schedule opened for edit is trimmed to what its market lists today. What goes was retired since
  // the schedule was saved, which "not offered at this address" would misstate: nobody changed the
  // address. That message stays for the customer changing it.
  describe('a schedule loaded for edit loses what its market no longer lists', () => {
    const slovakAddress = SavedAddressDto.fromJS({ id: 'addr-sk', countryId: 'svk' });
    const czechAddress = SavedAddressDto.fromJS({ id: 'addr-cz', countryId: 'cze' });
    const listed = (countryId: string | null, services: string[], packages: string[]) => {
      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: services.map((id) => ServiceListItem.fromJS({ id })),
        countryId,
      });
      store.overrideSelector(selectCustomerPackagesCatalogue, {
        packages: packages.map((id) => PackageListItem.fromJS({ id })),
        countryId,
      });
      store.refreshState();
      TestBed.flushEffects();
    };
    const stored = template({
      savedAddressId: 'addr-sk',
      selectedServiceIds: ['s1', 'retired-service'],
      selectedPackageIds: ['p1', 'retired-package'],
    });
    const selection = () => ({
      services: facade.formData().selectedServiceIds,
      packages: facade.formData().selectedPackageIds,
    });

    beforeEach(() => savedAddressStore.addresses.set([slovakAddress, czechAddress]));

    it('drops it as the form loads and says it is no longer offered', async () => {
      await facade.initialize();
      listed('svk', ['s1'], ['p1']);

      facade.loadForEdit(stored);
      TestBed.flushEffects();

      expect(selection()).toEqual({ services: ['s1'], packages: ['p1'] });
      expect(snackbar.showInfoTranslated).toHaveBeenCalledTimes(1);
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith('recurring_booking.selection_no_longer_offered');
    });

    it("waits for the list priced for the schedule's address, and still says no longer offered", async () => {
      await facade.initialize();
      facade.loadForEdit(stored);
      TestBed.flushEffects();
      expect(selection()).toEqual({
        services: ['s1', 'retired-service'],
        packages: ['p1', 'retired-package'],
      });

      listed('svk', ['s1'], ['p1']);

      expect(selection()).toEqual({ services: ['s1'], packages: ['p1'] });
      expect(snackbar.showInfoTranslated).toHaveBeenCalledTimes(1);
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith('recurring_booking.selection_no_longer_offered');
    });

    it('says nothing when the market still lists everything', async () => {
      await facade.initialize();
      listed('svk', ['s1', 'retired-service'], ['p1', 'retired-package']);

      facade.loadForEdit(stored);
      TestBed.flushEffects();

      expect(selection().services).toHaveLength(2);
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalled();
    });

    it('still says "not offered at this address" when the customer then moves it', async () => {
      await facade.initialize();
      listed('svk', ['s1', 'retired-service'], ['p1', 'retired-package']);
      facade.loadForEdit(stored);
      TestBed.flushEffects();

      facade.updateFormData({ savedAddressId: 'addr-cz' });
      TestBed.flushEffects();
      listed('cze', ['s1'], ['p1']);

      expect(selection()).toEqual({ services: ['s1'], packages: ['p1'] });
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith(
        'pages.order.wizard.catalogue_changed_for_country',
      );
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalledWith(
        'recurring_booking.selection_no_longer_offered',
      );
    });
  });

  // "Make this recurring" arrives with the order's services before the customer has touched the
  // address, and the default-priced list lands before the one priced for their address. Checking
  // the prefill against the first list and again against the second told the customer twice.
  describe('a prefill from an order is checked once, against the list priced for the address', () => {
    const slovakAddress = SavedAddressDto.fromJS({ id: 'addr-sk', countryId: 'svk', isDefault: true });
    const prefill = (overrides?: Partial<RecurringPrefillParams>): RecurringPrefillParams => ({
      selectedServiceIds: ['s1', 's2'],
      selectedPackageIds: [],
      selectedServiceNames: ['Basic', 'Windows'],
      selectedPackageNames: [],
      rooms: 3,
      bathrooms: 1,
      paymentType: 2,
      timeOfDay: '09:00',
      ...overrides,
    });
    const listLands = (services: string[], countryId: string | null) => {
      store.overrideSelector(selectCustomerServices, services.map((id) => ServiceListItem.fromJS({ id })));
      store.overrideSelector(selectCustomerServicesCatalogue, {
        services: services.map((id) => ServiceListItem.fromJS({ id })),
        countryId,
      });
      store.overrideSelector(selectCustomerPackagesCatalogue, { packages: [], countryId });
      store.refreshState();
      TestBed.flushEffects();
    };

    it('holds the prefill while the list on screen is priced for another market than the address', async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      await facade.initialize();
      facade.prefill(prefill());

      listLands(['s1'], null);

      expect(facade.formData().selectedServiceIds).toEqual([]);
      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalled();
    });

    it('trims once against the address list and says so once', async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      await facade.initialize();
      facade.prefill(prefill());
      listLands(['s1'], null);

      listLands(['s1'], 'svk');

      expect(facade.formData()).toMatchObject({
        selectedServiceIds: ['s1'],
        rooms: 3,
        bathrooms: 1,
        paymentType: 2,
        timeOfDay: '09:00',
      });
      expect(snackbar.showSuccess).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccess).toHaveBeenCalledWith('recurring_booking.prefill_dropped_items');
      expect(snackbar.showInfoTranslated).not.toHaveBeenCalled();
    });

    it('applies once and never again when the list is re-read later', async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      await facade.initialize();
      facade.prefill(prefill());
      listLands(['s1', 's2'], 'svk');
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      listLands(['s1', 's2'], 'svk');

      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
      expect(snackbar.showSuccess).not.toHaveBeenCalled();
    });

    it('checks against the default-priced list when the customer has no saved address', async () => {
      await facade.initialize();
      facade.prefill(prefill());

      listLands(['s2'], null);

      expect(facade.formData().selectedServiceIds).toEqual(['s2']);
      expect(snackbar.showSuccess).toHaveBeenCalledTimes(1);
    });

    it('holds the prefill until the addresses have loaded, since they decide the market', async () => {
      savedAddressStore.loaded.set(false);
      let finishLoading: (value: boolean) => void = () => undefined;
      savedAddressStore.refresh.mockReturnValue(
        new Promise<boolean>((resolve) => {
          finishLoading = resolve;
        }),
      );
      const loading = facade.ensureAddresses();
      facade.prefill(prefill());

      listLands(['s1'], null);
      expect(facade.formData().selectedServiceIds).toEqual([]);

      savedAddressStore.addresses.set([slovakAddress]);
      finishLoading(true);
      await loading;
      TestBed.flushEffects();
      listLands(['s1'], 'svk');

      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
      expect(snackbar.showSuccess).toHaveBeenCalledTimes(1);
    });
  });

  // A package plus a service it already includes books that service on every clean twice, and the
  // owner ruled it stays that way. The form marks the pair and asks before either half is added.
  describe('a package and a service it already includes', () => {
    const windows = ServiceListItem.fromJS({ id: 'windows', name: 'Windows' });
    const oven = ServiceListItem.fromJS({ id: 'oven', name: 'Oven' });
    const ironing = ServiceListItem.fromJS({ id: 'ironing', name: 'Ironing' });
    const deep = PackageListItem.fromJS({
      id: 'deep',
      name: 'Deep clean',
      includedServices: [
        { serviceId: 'windows', name: 'Windows' },
        { serviceId: 'oven', name: 'Oven' },
      ],
    });
    const kitchen = PackageListItem.fromJS({
      id: 'kitchen',
      name: 'Kitchen',
      includedServices: [{ serviceId: 'oven', name: 'Oven' }],
    });

    beforeEach(() => {
      store.overrideSelector(selectCustomerServices, [windows, oven, ironing]);
      store.overrideSelector(selectCustomerPackages, [deep, kitchen]);
      store.refreshState();
    });

    const chosen = () => ({
      services: facade.formData().selectedServiceIds,
      packages: facade.formData().selectedPackageIds,
    });

    it('marks a service with the chosen packages that include it, and nothing else', () => {
      expect(facade.packageNamesIncluding('oven')).toBeNull();

      facade.updateFormData({ selectedPackageIds: ['deep', 'kitchen'] });

      expect(facade.packageNamesIncluding('oven')).toBe('Deep clean, Kitchen');
      expect(facade.packageNamesIncluding('windows')).toBe('Deep clean');
      expect(facade.packageNamesIncluding('ironing')).toBeNull();
    });

    // One chosen package says "package"; two or more say "packages", on the line and the question.
    it('says packages only for a service two or more chosen packages include', () => {
      facade.updateFormData({ selectedPackageIds: ['deep'] });

      expect(facade.isInManyPackages('oven')).toBe(false);

      facade.updateFormData({ selectedPackageIds: ['deep', 'kitchen'] });

      expect(facade.isInManyPackages('oven')).toBe(true);
      expect(facade.isInManyPackages('windows')).toBe(false);
      expect(facade.isInManyPackages('ironing')).toBe(false);
    });

    it('asks before adding a service a chosen package includes, with Cancel as the default', () => {
      facade.updateFormData({ selectedPackageIds: ['deep'] });

      facade.toggleService('windows');

      expect(dialog.confirmTranslated).toHaveBeenCalledWith(
        'pages.order.package_overlap.service_message',
        'pages.order.package_overlap.service_title',
        { service: 'Windows', package: 'Deep clean' },
        { acceptLabelKey: 'pages.order.package_overlap.add_again' },
      );
    });

    it('keeps the form as it was when the customer cancels', () => {
      facade.updateFormData({ selectedPackageIds: ['deep'] });
      dialog.confirmTranslated.mockReturnValue(of(false));

      facade.toggleService('windows');

      expect(chosen()).toEqual({ services: [], packages: ['deep'] });
    });

    it('adds the service a second time when the customer confirms', () => {
      facade.updateFormData({ selectedPackageIds: ['deep'] });
      dialog.confirmTranslated.mockReturnValue(of(true));

      facade.toggleService('windows');

      expect(chosen()).toEqual({ services: ['windows'], packages: ['deep'] });
    });

    // Two chosen packages already book it, so adding it is a third time: "twice" would be false.
    it('asks with the once-more message and the packages title when two chosen packages include the service', () => {
      facade.updateFormData({ selectedPackageIds: ['deep', 'kitchen'] });

      facade.toggleService('oven');

      expect(dialog.confirmTranslated).toHaveBeenCalledWith(
        'pages.order.package_overlap.service_message_many',
        'pages.order.package_overlap.service_title_many',
        { service: 'Oven', package: 'Deep clean, Kitchen' },
        { acceptLabelKey: 'pages.order.package_overlap.add_again' },
      );
    });

    it('adds a service no chosen package includes without asking', () => {
      facade.updateFormData({ selectedPackageIds: ['deep'] });

      facade.toggleService('ironing');

      expect(dialog.confirmTranslated).not.toHaveBeenCalled();
      expect(chosen().services).toEqual(['ironing']);
    });

    it('asks before adding a package that includes a service already chosen on its own', () => {
      facade.updateFormData({ selectedServiceIds: ['oven', 'windows', 'ironing'] });

      facade.togglePackage('deep');

      expect(dialog.confirmTranslated).toHaveBeenCalledWith(
        'pages.order.package_overlap.package_message',
        'pages.order.package_overlap.package_title',
        { package: 'Deep clean', services: 'Windows, Oven' },
        { acceptLabelKey: 'pages.order.package_overlap.add_package' },
      );
      expect(chosen().packages).toEqual([]);
    });

    it('asks before adding a package that includes a service another chosen package already books', () => {
      facade.updateFormData({ selectedPackageIds: ['kitchen'] });

      facade.togglePackage('deep');

      expect(dialog.confirmTranslated).toHaveBeenCalledWith(
        'pages.order.package_overlap.package_message',
        'pages.order.package_overlap.package_title',
        { package: 'Deep clean', services: 'Oven' },
        { acceptLabelKey: 'pages.order.package_overlap.add_package' },
      );
      expect(chosen().packages).toEqual(['kitchen']);
    });

    it('adds the overlapping package when the customer confirms, and keeps the other one', () => {
      facade.updateFormData({ selectedPackageIds: ['kitchen'] });
      dialog.confirmTranslated.mockReturnValue(of(true));

      facade.togglePackage('deep');

      expect(chosen()).toEqual({ services: [], packages: ['kitchen', 'deep'] });
    });

    it('adds the package when the customer confirms, and keeps the service', () => {
      facade.updateFormData({ selectedServiceIds: ['oven'] });
      dialog.confirmTranslated.mockReturnValue(of(true));

      facade.togglePackage('kitchen');

      expect(chosen()).toEqual({ services: ['oven'], packages: ['kitchen'] });
    });

    it('adds a package without asking when nothing it includes is chosen', () => {
      facade.updateFormData({ selectedServiceIds: ['ironing'] });

      facade.togglePackage('deep');

      expect(dialog.confirmTranslated).not.toHaveBeenCalled();
      expect(chosen().packages).toEqual(['deep']);
    });

    it('never asks when either half of the pair is removed', () => {
      facade.updateFormData({ selectedServiceIds: ['windows'], selectedPackageIds: ['deep'] });

      facade.toggleService('windows');
      facade.togglePackage('deep');

      expect(dialog.confirmTranslated).not.toHaveBeenCalled();
      expect(chosen()).toEqual({ services: [], packages: [] });
    });

    it('only marks a pair the form is handed, as repeating an order and editing a schedule hand it over', () => {
      facade.prefillFromOrder({
        selectedServiceIds: ['windows'],
        selectedPackageIds: ['deep'],
        selectedServiceNames: ['Windows'],
        selectedPackageNames: ['Deep clean'],
        rooms: 2,
        bathrooms: 1,
        paymentType: 0,
        timeOfDay: '',
      });
      expect(chosen()).toEqual({ services: ['windows'], packages: ['deep'] });

      facade.loadForEdit(
        template({ selectedServiceIds: ['oven'], selectedPackageIds: ['kitchen', 'deep'] }),
      );

      expect(dialog.confirmTranslated).not.toHaveBeenCalled();
      expect(chosen()).toEqual({ services: ['oven'], packages: ['kitchen', 'deep'] });
      expect(facade.packageNamesIncluding('oven')).toBe('Deep clean, Kitchen');
    });
  });

  describe('the currency a schedule is priced in', () => {
    it('prices a quote that names no currency as a bare number, never in a guessed unit', async () => {
      orderClient.quote.mockReturnValue(
        of(QuoteOrderResponse.fromJS({ totalPrice: 1000, finalPriceAfterDiscount: 900 })),
      );
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      await facade.quoteForm();

      expect(facade.formPrice()).toEqual({ amount: 900, currency: '' });
    });

    // The form's price threaded the quote's currency on one line and hardcoded CZK on the next.
    it("carries the quote's own currency onto the form price", async () => {
      orderClient.quote.mockReturnValue(
        of(QuoteOrderResponse.fromJS({ totalPrice: 1000, finalPriceAfterDiscount: 900, currencyCode: 'EUR' })),
      );
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      await facade.quoteForm();

      expect(facade.formPrice()).toEqual({ amount: 900, currency: 'EUR' });
    });

    it('prices a card in the currency its quote came back in', async () => {
      orderClient.quote.mockReturnValue(
        of(QuoteOrderResponse.fromJS({ totalPrice: 1000, finalPriceAfterDiscount: 1000, currencyCode: 'EUR' })),
      );

      await facade.quoteTemplate(template({ selectedServiceIds: ['s1'] }));

      expect(facade.templatePrices()['t1']).toEqual({ amount: 1000, currency: 'EUR' });
    });
  });

  // A schedule is priced in the currency of the country its saved address is in — the server
  // derives it from the country the quote names, and from the saved address itself on create.
  describe('the market a schedule is quoted for', () => {
    const slovakAddress = SavedAddressDto.fromJS({ id: 'addr-sk', countryId: 'svk' });
    const quoted = () =>
      of(QuoteOrderResponse.fromJS({ totalPrice: 40, finalPriceAfterDiscount: 40, currencyCode: 'EUR' }));

    it("names the chosen saved address's country on the form quote", async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      orderClient.quote.mockReturnValue(quoted());
      facade.updateFormData({ selectedServiceIds: ['s1'], savedAddressId: 'addr-sk' });

      await facade.quoteForm();

      expect(orderClient.quote.mock.calls[0][0]).toMatchObject({ countryId: 'svk' });
      expect(orderClient.quote.mock.calls[0][0].currencyId).toBeUndefined();
    });

    it("names the template's saved address country on a card quote", async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      orderClient.quote.mockReturnValue(quoted());

      await facade.quoteTemplate(template({ selectedServiceIds: ['s1'], savedAddressId: 'addr-sk' }));

      expect(orderClient.quote.mock.calls[0][0].countryId).toBe('svk');
    });

    it('names no country before an address is chosen, which the server reads as the default', async () => {
      orderClient.quote.mockReturnValue(quoted());
      facade.updateFormData({ selectedServiceIds: ['s1'], savedAddressId: null });

      await facade.quoteForm();

      expect(orderClient.quote.mock.calls[0][0].countryId).toBeUndefined();
    });

    it('sends no currency on create — the server derives it from the saved address', async () => {
      savedAddressStore.addresses.set([slovakAddress]);
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        savedAddressId: 'addr-sk',
        startsOn: new Date('2026-10-01T00:00:00Z'),
        dirtinessLevel: DirtinessLevel.Normal,
      });

      await facade.submit();

      expect(client.create).toHaveBeenCalledTimes(1);
      const body = JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
      expect(body).not.toHaveProperty('currencyId');
      expect(body.savedAddressId).toBe('addr-sk');
    });
  });

  // Decision 34: a schedule carries the level its customer picks, which prices every clean it books
  // and, through the crew the quote gives, decides whether it can be paid in cash.
  describe('the dirtiness level of a schedule', () => {
    const quoted = () =>
      of(QuoteOrderResponse.fromJS({ totalPrice: 1300, finalPriceAfterDiscount: 1300, currencyCode: 'CZK' }));
    const createBody = () => JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
    const updateBody = () => JSON.parse(JSON.stringify(client.update.mock.calls[0][0]));
    const completeForm = () =>
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-10-01T00:00:00Z'),
        earlyPerformanceRequested: true,
        termsAccepted: true,
      });
    const stored = (dirtinessLevel?: DirtinessLevel) =>
      template({
        id: 't1',
        timeOfDay: '10:00',
        rooms: 2,
        bathrooms: 1,
        savedAddressId: 'addr-1',
        selectedServiceIds: ['s1'],
        paymentType: PaymentType.Card,
        startsOn: new Date('2026-10-01T00:00:00Z'),
        dirtinessLevel,
      });

    it('asks a new schedule for a level rather than assuming one', () => {
      completeForm();

      expect(facade.formData().dirtinessLevel).toBeNull();
      expect(facade.missing()).toEqual(['dirtiness']);
    });

    it('does not create a schedule before a level is chosen', async () => {
      completeForm();

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(client.create).not.toHaveBeenCalled();
    });

    it('quotes the form at normal until a level is chosen, then at the chosen one', async () => {
      orderClient.quote.mockReturnValue(quoted());
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();
      const unchosen = facade.pricedSelection();

      facade.updateFormData({ dirtinessLevel: DirtinessLevel.Normal });
      expect(facade.pricedSelection()).toBe(unchosen);

      facade.updateFormData({ dirtinessLevel: DirtinessLevel.Heavy });
      expect(facade.pricedSelection()).not.toBe(unchosen);
      await facade.quoteForm();

      expect(orderClient.quote.mock.calls[0][0].dirtinessLevel).toBe(DirtinessLevel.Normal);
      expect(orderClient.quote.mock.calls[1][0].dirtinessLevel).toBe(DirtinessLevel.Heavy);
    });

    it("prices a card at its schedule's own level", async () => {
      orderClient.quote.mockReturnValue(quoted());

      await facade.quoteTemplate(stored(DirtinessLevel.Increased));

      expect(orderClient.quote.mock.calls[0][0].dirtinessLevel).toBe(DirtinessLevel.Increased);
    });

    it('creates the schedule at the level chosen', async () => {
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      completeForm();
      facade.updateFormData({ dirtinessLevel: DirtinessLevel.Heavy });

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(createBody().dirtinessLevel).toBe(DirtinessLevel.Heavy);
    });

    it('opens a schedule for edit at its own level and sends it back unchanged', async () => {
      client.update.mockReturnValue(of(stored(DirtinessLevel.Increased)));
      facade.loadForEdit(stored(DirtinessLevel.Increased));

      expect(facade.formData().dirtinessLevel).toBe(DirtinessLevel.Increased);
      await facade.submit();

      expect(updateBody().dirtinessLevel).toBe(DirtinessLevel.Increased);
    });

    it('sends the level chosen on edit', async () => {
      client.update.mockReturnValue(of(stored(DirtinessLevel.Heavy)));
      facade.loadForEdit(stored(DirtinessLevel.Increased));
      facade.updateFormData({ dirtinessLevel: DirtinessLevel.Heavy });

      await facade.submit();

      expect(updateBody().dirtinessLevel).toBe(DirtinessLevel.Heavy);
    });

    it('reads a schedule that names no level as normal, the level the server stored for it', () => {
      facade.loadForEdit(stored(undefined));

      expect(facade.formData().dirtinessLevel).toBe(DirtinessLevel.Normal);
      expect(facade.missing()).toEqual([]);
    });

    it('asks again after the wizard is reset', () => {
      facade.loadForEdit(stored(DirtinessLevel.Heavy));

      facade.resetWizard();

      expect(facade.formData().dirtinessLevel).toBeNull();
    });
  });

  describe('refreshList — the three data states', () => {
    it('starts empty and not loading', () => {
      expect(facade.templates()).toEqual([]);
      expect(facade.listLoading()).toBe(false);
      expect(facade.listLoaded()).toBe(false);
    });

    it('loads templates and clears loading on success', async () => {
      const t = template();
      client.getMine.mockReturnValue(of([t]));

      await facade.refreshList();

      expect(client.getMine).toHaveBeenCalledTimes(1);
      expect(facade.templates()).toEqual([t]);
      expect(facade.listLoaded()).toBe(true);
      expect(facade.listLoading()).toBe(false);
    });

    it('surfaces an error snackbar and clears loading on failure', async () => {
      client.getMine.mockReturnValue(throwError(() => new Error('boom')));

      await facade.refreshList();

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.list_load_failed');
      expect(facade.listLoading()).toBe(false);
    });

    it('skips a concurrent refresh while one is in flight', async () => {
      let resolveFirst!: (v: RecurringBookingTemplateDto[]) => void;
      client.getMine.mockReturnValueOnce(
        new Observable<RecurringBookingTemplateDto[]>((sub) => {
          resolveFirst = (v) => {
            sub.next(v);
            sub.complete();
          };
        }),
      );

      const first = facade.refreshList();
      expect(facade.listLoading()).toBe(true);

      await facade.refreshList();
      expect(client.getMine).toHaveBeenCalledTimes(1);

      resolveFirst([]);
      await first;
      expect(facade.listLoading()).toBe(false);
    });
  });

  describe('toggleActive', () => {
    it('flips the template active flag optimistically on success', async () => {
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      expect(client.setActive).toHaveBeenCalledTimes(1);
      expect(facade.templates()[0].isActive).toBe(false);
      expect(facade.mutatingId()).toBeNull();
    });

    it('shows an error and clears the mutating flag on failure', async () => {
      client.setActive.mockReturnValue(throwError(() => new Error('boom')));
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.toggle_failed');
      expect(facade.mutatingId()).toBeNull();
    });

    it('ignores a toggle while another mutation is in flight', async () => {
      facade.mutatingId.set('other');

      await facade.toggleActive(template({ id: 't1' }));

      expect(client.setActive).not.toHaveBeenCalled();
    });
  });

  // Asked on the app shell's one confirm dialog, through the shared DialogService.
  describe('confirmDeleteTemplate', () => {
    const weekly = () => template({ id: 't1', frequency: RecurrenceFrequency.Weekly });

    it('asks in the schedule\'s own words, with a red Delete and Cancel', async () => {
      await facade.confirmDeleteTemplate(weekly());

      expect(dialog.confirmTranslated).toHaveBeenCalledWith(
        'recurring_booking.delete_dialog_compound',
        'recurring_booking.delete_dialog_title',
        { schedule: 'recurring_booking.cadence_weekly' },
        {
          acceptLabelKey: 'recurring_booking.delete_dialog_confirm',
          rejectLabelKey: 'global.cancel',
          danger: true,
        },
      );
    });

    it('deletes the schedule and clears the form once the customer says yes', async () => {
      dialog.confirmTranslated.mockReturnValue(of(true));
      facade.templates.set([weekly(), template({ id: 't2' })]);
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      const deleted = await facade.confirmDeleteTemplate(weekly());

      expect(deleted).toBe(true);
      expect(client.delete).toHaveBeenCalledTimes(1);
      expect(facade.templates().map((t) => t.id)).toEqual(['t2']);
      expect(facade.formData().selectedServiceIds).toEqual([]);
    });

    it('deletes nothing when the customer cancels', async () => {
      dialog.confirmTranslated.mockReturnValue(of(false));
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      const deleted = await facade.confirmDeleteTemplate(weekly());

      expect(deleted).toBe(false);
      expect(client.delete).not.toHaveBeenCalled();
      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
    });
  });

  describe('deleteTemplate', () => {
    it('removes the template and shows success on a successful delete', async () => {
      facade.templates.set([template({ id: 't1' }), template({ id: 't2' })]);

      await facade.deleteTemplate('t1');

      expect(client.delete).toHaveBeenCalledTimes(1);
      expect(facade.templates().map((t) => t.id)).toEqual(['t2']);
      expect(snackbar.showSuccess).toHaveBeenCalledWith('recurring_booking.delete_success');
      expect(facade.mutatingId()).toBeNull();
    });

    it('shows an error and clears the mutating flag on a failed delete', async () => {
      client.delete.mockReturnValue(throwError(() => new Error('boom')));
      facade.templates.set([template({ id: 't1' })]);

      await facade.deleteTemplate('t1');

      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.delete_failed');
      expect(facade.mutatingId()).toBeNull();
    });
  });

  describe('submit', () => {
    beforeEach(() => {
      facade.updateFormData({
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-07-01T00:00:00Z'),
        selectedServiceIds: ['s1'],
        dirtinessLevel: DirtinessLevel.Normal,
      });
    });

    it('inserts the created template and shows success', async () => {
      const created = template({ id: 'new' });
      client.create.mockReturnValue(of(created));
      client.getMine.mockReturnValue(of([created]));

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(client.create).toHaveBeenCalledTimes(1);
      expect(facade.templates().some((t) => t.id === 'new')).toBe(true);
      expect(snackbar.showSuccess).toHaveBeenCalledWith('recurring_booking.create_success');
      expect(facade.submitting()).toBe(false);
    });

    it('shows an error and returns false on a failed create', async () => {
      client.create.mockReturnValue(throwError(() => new Error('boom')));

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.create_failed');
      expect(facade.submitting()).toBe(false);
    });

    it('does not call the client when required fields are missing', async () => {
      facade.updateFormData({ savedAddressId: null });

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(client.create).not.toHaveBeenCalled();
    });
  });

  describe('the request to start within the withdrawal period', () => {
    beforeEach(() => {
      facade.updateFormData({
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-07-01T00:00:00Z'),
        selectedServiceIds: ['s1'],
        dirtinessLevel: DirtinessLevel.Normal,
        termsAccepted: true,
      });
    });

    it('is missing on a new schedule until the customer makes it', () => {
      expect(facade.missing()).toEqual(['earlyPerformance']);

      facade.updateFormData({ earlyPerformanceRequested: true });

      expect(facade.missing()).toEqual([]);
    });

    it('rides the create command once made', async () => {
      client.create.mockReturnValue(of(template({ id: 'new' })));
      facade.updateFormData({ earlyPerformanceRequested: true });

      await facade.submit();

      const body = JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
      expect(body.earlyPerformanceRequested).toBe(true);
    });

    it('is not asked of a schedule being edited', () => {
      facade.loadForEdit(
        template({
          savedAddressId: 'addr-1',
          selectedServiceIds: ['s1'],
          timeOfDay: '10:00',
          paymentType: PaymentType.Card,
          startsOn: new Date('2026-07-01T00:00:00Z'),
          dirtinessLevel: DirtinessLevel.Normal,
        }),
      );

      expect(facade.missing()).toEqual([]);
    });

    it('is asked again after the wizard is reset', () => {
      facade.updateFormData({ earlyPerformanceRequested: true });

      facade.resetWizard();

      expect(facade.formData().earlyPerformanceRequested).toBe(false);
    });
  });

  // A new schedule is a new booking (owner ruling 2026-09-28): the server refuses it without the tick
  // while either consent on record is not an acceptance of the text in force.
  describe('the terms tick on a new schedule', () => {
    const consent = (type: ConsentType, coversCurrentVersion = true) => ({
      id: `c${type}`,
      consentType: type,
      isGranted: true,
      grantedAt: '2026-09-14T10:00:00Z',
      createdOn: '2026-09-14T10:00:00Z',
      documentVersion: '2026-09-14',
      coversCurrentVersion,
    });
    const bothInForce = () => [consent(ConsentType.TermsOfService), consent(ConsentType.PrivacyPolicy)];
    const createBody = () => JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
    const termsRefused = () =>
      throwError(() => ({
        detail: 'A validation problem occurred.',
        errors: { TermsAccepted: 'consent.terms_not_accepted' },
      }));

    beforeEach(() => {
      facade.updateFormData({
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-07-01T00:00:00Z'),
        selectedServiceIds: ['s1'],
        dirtinessLevel: DirtinessLevel.Normal,
        earlyPerformanceRequested: true,
      });
    });

    it('is asked until the consents on record are read', () => {
      expect(facade.termsAsked()).toBe(true);
      expect(facade.missing()).toEqual(['terms']);
    });

    it('is not asked once both consents cover the texts in force, and asserts nothing', async () => {
      gdprClient.consentsGet.mockReturnValue(of(bothInForce()));
      client.create.mockReturnValue(of(template({ id: 'new' })));

      facade.loadConsentState();
      await facade.submit();

      expect(facade.termsAsked()).toBe(false);
      expect(facade.missing()).toEqual([]);
      expect(createBody()).not.toHaveProperty('termsAccepted');
    });

    it.each([
      {
        what: 'the terms accepted are an older text',
        consents: [consent(ConsentType.TermsOfService, false), consent(ConsentType.PrivacyPolicy)],
      },
      { what: 'only the terms are on record', consents: [consent(ConsentType.TermsOfService)] },
    ])('is asked when $what', ({ consents }) => {
      gdprClient.consentsGet.mockReturnValue(of(consents));

      facade.loadConsentState();

      expect(facade.termsAsked()).toBe(true);
    });

    it('is asked when the consents cannot be read', () => {
      gdprClient.consentsGet.mockReturnValue(throwError(() => new Error('offline')));

      facade.loadConsentState();

      expect(facade.termsAsked()).toBe(true);
    });

    it('holds the schedule until ticked, then sends the tick', async () => {
      client.create.mockReturnValue(of(template({ id: 'new' })));
      expect(facade.missing()).toEqual(['terms']);

      facade.updateFormData({ termsAccepted: true });
      await facade.submit();

      expect(facade.missing()).toEqual([]);
      expect(createBody().termsAccepted).toBe(true);
    });

    it('sends no tick it did not ask for, however the form was left', async () => {
      gdprClient.consentsGet.mockReturnValue(of(bothInForce()));
      client.create.mockReturnValue(of(template({ id: 'new' })));
      facade.loadConsentState();

      facade.updateFormData({ termsAccepted: true });
      await facade.submit();

      expect(createBody()).not.toHaveProperty('termsAccepted');
    });

    it('is not asked of a schedule being edited', () => {
      facade.loadForEdit(
        template({
          savedAddressId: 'addr-1',
          selectedServiceIds: ['s1'],
          timeOfDay: '10:00',
          paymentType: PaymentType.Card,
          startsOn: new Date('2026-07-01T00:00:00Z'),
          dirtinessLevel: DirtinessLevel.Normal,
        }),
      );

      expect(facade.termsAsked()).toBe(false);
      expect(facade.missing()).toEqual([]);
    });

    it('asks again when the server refuses the schedule over the terms, without a second error', async () => {
      gdprClient.consentsGet.mockReturnValue(of(bothInForce()));
      client.create.mockReturnValue(termsRefused());
      facade.loadConsentState();

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(facade.termsAsked()).toBe(true);
      expect(facade.missing()).toEqual(['terms']);
      expect(snackbar.showError).not.toHaveBeenCalled();
    });

    it('is asked again after the wizard is reset', () => {
      facade.updateFormData({ termsAccepted: true });

      facade.resetWizard();

      expect(facade.formData().termsAccepted).toBe(false);
    });
  });

  // Owner ruling 2026-09-24: a schedule is always an account's, so cash turns on the crew alone —
  // one cleaner, from the server's quote. A cash choice that stops being allowed is taken away, never
  // swapped for card.
  describe('paying a schedule in cash', () => {
    const crewOf = (requiredEmployees: number) =>
      QuoteOrderResponse.fromJS({
        totalPrice: 1000,
        finalPriceAfterDiscount: 1000,
        currencyCode: 'CZK',
        requiredEmployees,
      });

    const completeForm = (paymentType: PaymentType) =>
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-10-01T00:00:00Z'),
        dirtinessLevel: DirtinessLevel.Normal,
        paymentType,
      });

    it('starts a new schedule on card', () => {
      expect(facade.formData().paymentType).toBe(PaymentType.Card);
    });

    it('offers cash once the quote says one cleaner does each clean', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      await facade.quoteForm();

      expect(facade.cashEligibility()).toEqual({ kind: 'available' });
    });

    it('refuses cash when the quote says two cleaners are needed', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(2)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      await facade.quoteForm();

      expect(facade.cashEligibility()).toEqual({ kind: 'needs_card', requiredCleaners: 2 });
    });

    it('decides nothing about cash before the form has a quote', () => {
      expect(facade.cashEligibility()).toEqual({ kind: 'pending' });
    });

    it('keeps the answer for the latest selection when quotes land out of order', async () => {
      const slow = new Subject<QuoteOrderResponse>();
      orderClient.quote.mockReturnValueOnce(slow).mockReturnValueOnce(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1', 's2'] });
      const older = facade.quoteForm();
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();

      slow.next(crewOf(2));
      slow.complete();
      await older;

      expect(facade.cashEligibility()).toEqual({ kind: 'available' });
    });

    it('cannot choose cash while it is not allowed', () => {
      facade.selectPayment(PaymentType.Cash);

      expect(facade.formData().paymentType).toBe(PaymentType.Card);
    });

    it('takes an ineligible cash away from a legacy template opened for edit, without choosing card', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(2)));
      facade.loadForEdit(
        template({ selectedServiceIds: ['s1'], paymentType: PaymentType.Cash, requiresPaymentMethodChange: true }),
      );

      await facade.quoteForm();
      TestBed.flushEffects();

      expect(facade.formData().paymentType).toBeNull();
      expect(facade.cashCleared()).toBe(true);
      expect(facade.missing()).toContain('payment');
      expect(snackbar.showInfoTranslated).toHaveBeenCalledWith('recurring_booking.cash_cleared');
    });

    it('takes cash prefilled from an order away when the selection needs two cleaners', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(2)));
      facade.prefillFromOrder({
        selectedServiceIds: ['s1'],
        selectedPackageIds: [],
        selectedServiceNames: ['Basic'],
        selectedPackageNames: [],
        rooms: 2,
        bathrooms: 1,
        paymentType: PaymentType.Cash,
        timeOfDay: '09:00',
      });

      await facade.quoteForm();
      TestBed.flushEffects();

      expect(facade.formData().paymentType).toBeNull();
    });

    it('keeps an allowed cash choice', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();

      facade.selectPayment(PaymentType.Cash);
      TestBed.flushEffects();

      expect(facade.formData().paymentType).toBe(PaymentType.Cash);
      expect(facade.cashCleared()).toBe(false);
    });

    it('never sends cash the quote taken at submit refuses', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(2)));
      completeForm(PaymentType.Cash);

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(client.create).not.toHaveBeenCalled();
      expect(facade.formData().paymentType).toBeNull();
    });

    it('does not send cash when the crew could not be checked', async () => {
      orderClient.quote.mockReturnValue(throwError(() => new Error('offline')));
      completeForm(PaymentType.Cash);

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(client.create).not.toHaveBeenCalled();
      expect(facade.formData().paymentType).toBe(PaymentType.Cash);
      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.cash_unchecked');
    });

    it('sends cash on a schedule one cleaner does alone', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      completeForm(PaymentType.Cash);

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(client.create.mock.calls[0][0].paymentType).toBe(PaymentType.Cash);
    });

    it('sends card without asking for a quote first', async () => {
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      completeForm(PaymentType.Card);

      await facade.submit();

      expect(orderClient.quote).not.toHaveBeenCalled();
      expect(client.create).toHaveBeenCalledTimes(1);
    });

    it('takes cash off the form when the server refuses it, leaving the interceptor toast alone', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      client.update.mockReturnValue(
        throwError(() => ({ errors: { PaymentType: 'order.cash_not_available' } })),
      );
      facade.loadForEdit(template({ id: 't1', paymentType: PaymentType.Cash }));
      completeForm(PaymentType.Cash);

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(facade.formData().paymentType).toBeNull();
      expect(facade.cashCleared()).toBe(true);
    });

    it.each(['order.cash_unpaid_receivable', 'order.cash_open_bookings_limit_reached'])(
      'takes cash off the form when the server refuses it with %s, leaving the interceptor toast alone',
      async (code) => {
        orderClient.quote.mockReturnValue(of(crewOf(1)));
        client.create.mockReturnValue(throwError(() => ({ errors: { PaymentType: code } })));
        completeForm(PaymentType.Cash);

        const ok = await facade.submit();

        expect(ok).toBe(false);
        expect(snackbar.showError).not.toHaveBeenCalled();
        expect(facade.formData().paymentType).toBeNull();
        expect(facade.cashCleared()).toBe(true);
      },
    );

    it('prices the same selection whatever the day, the time, the cadence or the payment', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();
      const priced = facade.pricedSelection();

      facade.selectPayment(PaymentType.Cash);
      facade.updateFormData({
        timeOfDay: '11:00',
        dayOfWeek: 2,
        frequency: RecurrenceFrequency.Monthly,
        startsOn: new Date('2026-11-01T00:00:00Z'),
      });
      TestBed.flushEffects();

      expect(facade.pricedSelection()).toBe(priced);
      expect(facade.cashEligibility()).toEqual({ kind: 'available' });
      expect(facade.formData().paymentType).toBe(PaymentType.Cash);

      facade.updateFormData({ rooms: 5 });

      expect(facade.pricedSelection()).not.toBe(priced);
      expect(facade.cashEligibility()).toEqual({ kind: 'pending' });
    });

    it('keeps cash open while an unchanged selection is quoted again', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();
      facade.selectPayment(PaymentType.Cash);

      const inFlight = new Subject<QuoteOrderResponse>();
      orderClient.quote.mockReturnValue(inFlight);
      const requote = facade.quoteForm();

      expect(facade.cashSelectable()).toBe(true);
      expect(facade.cashReason()).toBeNull();

      inFlight.next(crewOf(1));
      inFlight.complete();
      await requote;
      TestBed.flushEffects();

      expect(facade.formData().paymentType).toBe(PaymentType.Cash);
    });

    it('names why cash is not available, with the crew the quote gave', async () => {
      expect(facade.cashReason()).toEqual({ key: 'recurring_booking.cash_pending', params: {} });

      orderClient.quote.mockReturnValue(of(crewOf(3)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();

      expect(facade.cashSelectable()).toBe(false);
      expect(facade.cashReason()).toEqual({
        key: 'recurring_booking.cash_needs_card',
        params: { count: 3 },
      });

      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ rooms: 3 });
      await facade.quoteForm();

      expect(facade.cashSelectable()).toBe(true);
      expect(facade.cashReason()).toBeNull();
    });

    it('stops saying cash was taken away once the selection allows it again', async () => {
      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.quoteForm();
      facade.selectPayment(PaymentType.Cash);

      orderClient.quote.mockReturnValue(of(crewOf(2)));
      facade.updateFormData({ selectedServiceIds: ['s1', 's2'] });
      await facade.quoteForm();
      TestBed.flushEffects();
      expect(facade.cashClearedNotice()).toBe(true);

      orderClient.quote.mockReturnValue(of(crewOf(1)));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      expect(facade.cashClearedNotice()).toBe(true);

      await facade.quoteForm();

      expect(facade.cashClearedNotice()).toBe(false);
      expect(facade.formData().paymentType).toBeNull();
    });
  });

  // The update replaces every field it is sent, so a field the form does not edit has to travel
  // back as the schedule has it — an omitted one is a cleared one.
  describe('editing keeps what the form does not edit', () => {
    const endsOn = new Date('2027-03-31T00:00:00Z');
    const stored = (overrides?: Partial<RecurringBookingTemplateDto>) =>
      template({
        id: 't1',
        frequency: RecurrenceFrequency.Weekly,
        dayOfWeek: 4,
        timeOfDay: '10:00',
        rooms: 2,
        bathrooms: 1,
        savedAddressId: 'addr-1',
        selectedServiceIds: ['s1'],
        selectedPackageIds: [],
        paymentType: PaymentType.Card,
        startsOn: new Date('2026-10-01T00:00:00Z'),
        endsOn,
        preferredEmployeeId: 'e-1',
        ...overrides,
      });
    const notEligible = () =>
      throwError(() => ({
        detail: 'A validation problem occurred.',
        errors: { PreferredEmployeeId: 'order.preferred_employee.not_eligible' },
      }));
    const updateBody = (call: number) =>
      JSON.parse(JSON.stringify(client.update.mock.calls[call][0] as UpdateRecurringBookingCommand));

    beforeEach(() => client.update.mockReturnValue(of(stored())));

    it('sends the preferred cleaner and the end date the schedule already has', async () => {
      facade.loadForEdit(stored());
      facade.updateFormData({ timeOfDay: '11:00' });

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(updateBody(0)).toMatchObject({
        templateId: 't1',
        timeOfDay: '11:00',
        preferredEmployeeId: 'e-1',
        endsOn: '2027-03-31T00:00:00.000Z',
      });
    });

    it('sends neither when the schedule has neither', async () => {
      facade.loadForEdit(stored({ preferredEmployeeId: undefined, endsOn: undefined }));

      await facade.submit();

      expect(updateBody(0)).not.toHaveProperty('preferredEmployeeId');
      expect(updateBody(0)).not.toHaveProperty('endsOn');
    });

    it('keeps the start date before the end date the schedule has', () => {
      expect(facade.latestStartsOn()).toBeNull();

      facade.loadForEdit(stored());

      expect(facade.latestStartsOn()).toEqual(new Date('2027-03-30T00:00:00Z'));
    });

    it('keeps the preferred cleaner and says so when the server no longer accepts them', async () => {
      client.update.mockReturnValueOnce(notEligible());
      facade.loadForEdit(stored());

      const ok = await facade.submit();

      expect(ok).toBe(false);
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(facade.formData().preferredEmployeeId).toBe('e-1');
      expect(facade.preferredCleanerRefused()).toBe(true);
    });

    it('saves without the preferred cleaner only when the customer chooses to', async () => {
      client.update.mockReturnValueOnce(notEligible());
      facade.loadForEdit(stored());
      await facade.submit();

      const ok = await facade.saveWithoutPreferredCleaner();

      expect(ok).toBe(true);
      expect(client.update).toHaveBeenCalledTimes(2);
      expect(updateBody(1)).not.toHaveProperty('preferredEmployeeId');
      expect(updateBody(1).endsOn).toBe('2027-03-31T00:00:00.000Z');
      expect(facade.preferredCleanerRefused()).toBe(false);
      expect(facade.formData().preferredEmployeeId).toBeNull();
    });

    it('keeps the preferred cleaner when saving without them never goes out', async () => {
      client.update.mockReturnValueOnce(notEligible());
      facade.loadForEdit(stored());
      await facade.submit();
      facade.updateFormData({ timeOfDay: '' });

      const ok = await facade.saveWithoutPreferredCleaner();

      expect(ok).toBe(false);
      expect(client.update).toHaveBeenCalledTimes(1);
      expect(facade.formData().preferredEmployeeId).toBe('e-1');

      facade.updateFormData({ timeOfDay: '10:00' });
      await facade.submit();

      expect(updateBody(1).preferredEmployeeId).toBe('e-1');
    });

    it('keeps the preferred cleaner when saving without them fails', async () => {
      client.update
        .mockReturnValueOnce(notEligible())
        .mockReturnValueOnce(throwError(() => new Error('offline')));
      facade.loadForEdit(stored());
      await facade.submit();

      const ok = await facade.saveWithoutPreferredCleaner();

      expect(ok).toBe(false);
      expect(updateBody(1)).not.toHaveProperty('preferredEmployeeId');
      expect(facade.formData().preferredEmployeeId).toBe('e-1');

      await facade.submit();

      expect(updateBody(2).preferredEmployeeId).toBe('e-1');
    });

    it('puts the notice down when the wizard is reset', async () => {
      client.update.mockReturnValueOnce(notEligible());
      facade.loadForEdit(stored());
      await facade.submit();

      facade.resetWizard();

      expect(facade.preferredCleanerRefused()).toBe(false);
    });

    it('still names every other refusal with the generic update message', async () => {
      client.update.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.loadForEdit(stored());

      await facade.submit();

      expect(facade.preferredCleanerRefused()).toBe(false);
      expect(snackbar.showError).toHaveBeenCalledWith('recurring_booking.update_failed');
    });

    it('creates a schedule with neither, even after an edit was abandoned', async () => {
      facade.loadForEdit(stored());
      facade.resetWizard();
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-10-01T00:00:00Z'),
        dirtinessLevel: DirtinessLevel.Normal,
      });

      await facade.submit();

      const body = JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
      expect(body).not.toHaveProperty('preferredEmployeeId');
      expect(body).not.toHaveProperty('endsOn');
      expect(facade.latestStartsOn()).toBeNull();
    });
  });

  // Decision 44: the schedule form offers the cleaners who have served this customer, asked with no
  // slot because a schedule has no single instant, and the one refusal handling covers create and edit.
  describe('the favourite cleaner on a schedule', () => {
    const cleaner = (employeeId: string, fullName: string) =>
      GetMyServingCleanersResponse.fromJS({
        employeeId,
        fullName,
        lastServedOn: '2026-09-01T10:00:00Z',
        isAvailableForRequestedSlot: null,
      });
    const notEligible = () =>
      throwError(() => ({
        detail: 'A validation problem occurred.',
        errors: { PreferredEmployeeId: 'order.preferred_employee.not_eligible' },
      }));
    const createBody = (call: number) =>
      JSON.parse(JSON.stringify(client.create.mock.calls[call][0] as CreateRecurringBookingCommand));
    const updateBody = (call: number) =>
      JSON.parse(JSON.stringify(client.update.mock.calls[call][0] as UpdateRecurringBookingCommand));
    const completeForm = () =>
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        savedAddressId: 'addr-1',
        startsOn: new Date('2026-10-01T00:00:00Z'),
        dirtinessLevel: DirtinessLevel.Normal,
      });
    const storedWith = (preferredEmployeeId: string) =>
      template({
        id: 't1',
        timeOfDay: '10:00',
        savedAddressId: 'addr-1',
        selectedServiceIds: ['s1'],
        paymentType: PaymentType.Card,
        startsOn: new Date('2026-10-01T00:00:00Z'),
        preferredEmployeeId,
      });

    it('asks for the cleaners with no slot and offers each by name', async () => {
      orderClient.myServingCleaners.mockReturnValue(
        of([cleaner('e-1', 'Jana Nováková'), cleaner('e-2', 'Petr Svoboda')]),
      );

      await facade.loadServingCleaners();

      expect(orderClient.myServingCleaners).toHaveBeenCalledWith();
      expect(facade.preferredCleanerVisible()).toBe(true);
      expect(facade.preferredCleanerOptions()).toEqual([
        { label: 'Jana Nováková', value: 'e-1', disabled: false },
        { label: 'Petr Svoboda', value: 'e-2', disabled: false },
      ]);
    });

    it('is loading only while the cleaners are being read', async () => {
      const roster = new Subject<GetMyServingCleanersResponse[]>();
      orderClient.myServingCleaners.mockReturnValue(roster);

      const loading = facade.loadServingCleaners();
      expect(facade.servingCleanersLoading()).toBe(true);

      roster.next([cleaner('e-1', 'Jana Nováková')]);
      roster.complete();
      await loading;

      expect(facade.servingCleanersLoading()).toBe(false);
    });

    it('offers no picker when nobody has cleaned for the customer yet', async () => {
      await facade.loadServingCleaners();

      expect(facade.preferredCleanerVisible()).toBe(false);
    });

    it('hides the picker without an error when the cleaners cannot be read', async () => {
      orderClient.myServingCleaners.mockReturnValue(throwError(() => new Error('offline')));

      await facade.loadServingCleaners();

      expect(facade.preferredCleanerVisible()).toBe(false);
      expect(facade.servingCleanersLoading()).toBe(false);
      expect(snackbar.showError).not.toHaveBeenCalled();
    });

    it('sends the chosen cleaner when the schedule is created', async () => {
      client.create.mockReturnValue(of(template({ id: 't-new' })));
      completeForm();
      facade.selectPreferredCleaner('e-1');

      const ok = await facade.submit();

      expect(ok).toBe(true);
      expect(createBody(0).preferredEmployeeId).toBe('e-1');
    });

    it('keeps a refused cleaner on a new schedule and offers creating it without them', async () => {
      client.create.mockReturnValueOnce(notEligible());
      completeForm();
      facade.selectPreferredCleaner('e-1');

      const refused = await facade.submit();

      expect(refused).toBe(false);
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(facade.preferredCleanerRefused()).toBe(true);
      expect(facade.formData().preferredEmployeeId).toBe('e-1');

      client.create.mockReturnValueOnce(of(template({ id: 't-new' })));
      const ok = await facade.saveWithoutPreferredCleaner();

      expect(ok).toBe(true);
      expect(createBody(1)).not.toHaveProperty('preferredEmployeeId');
      expect(facade.formData().preferredEmployeeId).toBeNull();
    });

    it('sends another cleaner chosen on edit', async () => {
      client.update.mockReturnValue(of(template({ id: 't1' })));
      facade.loadForEdit(storedWith('e-1'));
      facade.selectPreferredCleaner('e-2');

      await facade.submit();

      expect(updateBody(0).preferredEmployeeId).toBe('e-2');
    });

    it('sends no cleaner once the customer clears the one the schedule had', async () => {
      client.update.mockReturnValue(of(template({ id: 't1' })));
      facade.loadForEdit(storedWith('e-1'));
      facade.selectPreferredCleaner(null);

      await facade.submit();

      expect(updateBody(0)).not.toHaveProperty('preferredEmployeeId');
    });

    it('puts a refusal notice down once another cleaner is chosen', async () => {
      client.create.mockReturnValueOnce(notEligible());
      completeForm();
      facade.selectPreferredCleaner('e-1');
      await facade.submit();

      facade.selectPreferredCleaner('e-2');

      expect(facade.preferredCleanerRefused()).toBe(false);
      expect(facade.formData().preferredEmployeeId).toBe('e-2');
    });
  });

  it('re-exposes the catalog signals from the NgRx store', () => {
    store.overrideSelector(selectCustomerServices, [
      { id: 's1' },
    ] as never);
    store.refreshState();

    expect(facade.services().length).toBe(1);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a toggle with the template id and the flipped flag', async () => {
      facade.templates.set([template({ id: 't1', isActive: true })]);

      await facade.toggleActive(template({ id: 't1', isActive: true }));

      const command: SetRecurringBookingActiveCommand =
        client.setActive.mock.calls[0][0];
      expect(command).toBeInstanceOf(SetRecurringBookingActiveCommand);
      expect(command.toJSON()).toEqual({ templateId: 't1', isActive: false });
    });

    it('serializes a delete with the template id', async () => {
      facade.templates.set([template({ id: 't1' })]);

      await facade.deleteTemplate('t1');

      const command: DeleteRecurringBookingCommand =
        client.delete.mock.calls[0][0];
      expect(command).toBeInstanceOf(DeleteRecurringBookingCommand);
      expect(command.toJSON()).toEqual({ templateId: 't1' });
    });
  });
});

// The server refuses to quote a service or package retired since the schedule was made, so such a
// schedule has no price to state. These run the real interceptor chain: a card without a price is
// honest, the shared error toast on every visit to the list is not.
describe('RecurringBookingsFacade — a quote the server refuses', () => {
  let facade: RecurringBookingsFacade;
  let httpMock: HttpTestingController;
  let showError: jest.Mock;

  /** The blob read resolves on the FileReader's load event, which is a macrotask behind the flush. */
  const flushAsyncErrorHandling = async (): Promise<void> => {
    for (let tick = 0; tick < 5; tick++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
  };

  const refuse = async (pending: Promise<void>, field: string, code: string): Promise<void> => {
    httpMock
      .expectOne((request) => request.url.endsWith('/api/Order/Quote'))
      .flush(new Blob([JSON.stringify({ errors: { [field]: code } })], { type: 'application/json' }), {
        status: 400,
        statusText: 'Bad Request',
      });
    await pending;
    await flushAsyncErrorHandling();
  };

  beforeEach(() => {
    showError = jest.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([HttpErrorInterceptorFn])),
        provideHttpClientTesting(),
        RecurringBookingsFacade,
        provideMockStore({
          selectors: [
            { selector: selectCustomerServices, value: [] },
            { selector: selectCustomerPackages, value: [] },
            { selector: selectCustomerServicesCatalogue, value: { services: [], countryId: null } },
            { selector: selectCustomerPackagesCatalogue, value: { packages: [], countryId: null } },
            { selector: selectMarketCountryId, value: null },
          ],
        }),
        {
          provide: SavedAddressStore,
          useValue: { addresses: signal<SavedAddressDto[]>([]), loaded: signal(true), refresh: jest.fn() },
        },
        {
          provide: SnackbarService,
          useValue: { showError, showSuccess: jest.fn(), showInfoTranslated: jest.fn() },
        },
        { provide: DialogService, useValue: {} },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'en' } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    facade = TestBed.inject(RecurringBookingsFacade);
  });

  afterEach(() => httpMock.verify());

  it('leaves the card of a schedule with a retired service without a price, and says nothing', async () => {
    const pending = facade.quoteTemplate(
      RecurringBookingTemplateDto.fromJS({ id: 't1', selectedServiceIds: ['retired'], rooms: 2, bathrooms: 1 }),
    );

    await refuse(pending, 'SelectedServiceIds', 'order.selected_services.invalid');

    expect(facade.templatePrices()).toEqual({});
    expect(showError).not.toHaveBeenCalled();
  });

  it('leaves the edit form of a schedule with a retired package without a price, and says nothing', async () => {
    facade.updateFormData({ selectedPackageIds: ['retired'] });

    await refuse(facade.quoteForm(), 'SelectedPackageIds', 'order.selected_package.invalid');

    expect(facade.formPrice()).toBeNull();
    expect(showError).not.toHaveBeenCalled();
  });
});
