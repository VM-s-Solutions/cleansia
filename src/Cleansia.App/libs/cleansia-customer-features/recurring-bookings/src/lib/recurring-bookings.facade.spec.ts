import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  DeleteRecurringBookingCommand,
  PackageListItem,
  PaymentType,
  QuoteOrderResponse,
  RecurringBookingTemplateDto,
  SavedAddressDto,
  ServiceListItem,
  SetRecurringBookingActiveCommand,
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
import { SnackbarService } from '@cleansia/services';
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
  let orderClient: { quote: jest.Mock };
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
    orderClient = { quote: jest.fn() };
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

    TestBed.configureTestingModule({
      providers: [
        RecurringBookingsFacade,
        provideMockStore(),
        {
          provide: CustomerClient,
          useValue: {
            recurringBookingClient: client,
            orderClient,
            membershipClient: { getMine: jest.fn().mockReturnValue(of({ hasMembership: true })) },
          },
        },
        { provide: SavedAddressStore, useValue: savedAddressStore },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
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
      });

      await facade.submit();

      expect(client.create).toHaveBeenCalledTimes(1);
      const body = JSON.parse(JSON.stringify(client.create.mock.calls[0][0]));
      expect(body).not.toHaveProperty('currencyId');
      expect(body.savedAddressId).toBe('addr-sk');
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
