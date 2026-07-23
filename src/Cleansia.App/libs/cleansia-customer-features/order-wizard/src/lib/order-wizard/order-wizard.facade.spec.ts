import { PLATFORM_ID, signal } from '@angular/core';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AddressDto,
  CustomerAuthService,
  CustomerClient,
  PaymentType,
} from '@cleansia/customer-services';
import {
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { GuestOrderService } from '@cleansia-customer/orders';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { OrderPricingFacade } from './order-pricing.facade';
import { OrderPromoFacade } from './order-promo.facade';
import { OrderSavedAddressFacade } from './order-saved-address.facade';
import { OrderServiceAreaFacade } from './order-service-area.facade';
import { OrderWizardFacade } from './order-wizard.facade';

describe('OrderWizardFacade', () => {
  let facade: OrderWizardFacade;
  let store: MockStore;
  let orderClient: { quote: jest.Mock; createOrder: jest.Mock };
  let paymentClient: { createOrder: jest.Mock };
  let promoCodeClient: { validate: jest.Mock };
  let countryClient: { getServiced: jest.Mock };
  let extraClient: { getOverview: jest.Mock };
  let userClient: { getCurrent: jest.Mock };
  let apiClient: { serviceCity: jest.Mock };
  let authService: { isLoggedIn: jest.Mock };
  let snackbar: { showError: jest.Mock };
  let router: { navigate: jest.Mock };
  let guestOrderService: { save: jest.Mock };
  let savedAddressStore: {
    addresses: ReturnType<typeof signal>;
    loaded: ReturnType<typeof signal>;
    defaultAddress: ReturnType<typeof signal>;
    refresh: jest.Mock;
    add: jest.Mock;
  };

  const quoteResponse = {
    totalPrice: 1000,
    expressSurchargeApplied: false,
    expressSurchargeAmount: 0,
    currencyId: 'czk',
  };

  const savedAddress = {
    id: 'addr-1',
    street: 'Wenceslas 1',
    city: 'Prague',
    zipCode: '11000',
    countryId: 'cz',
    state: '',
    latitude: 50.08,
    longitude: 14.42,
  };

  function configure(platform: 'server' | 'browser' = 'server'): void {
    orderClient = {
      quote: jest.fn().mockReturnValue(of(quoteResponse)),
      createOrder: jest.fn().mockReturnValue(of({ id: 'order-1' })),
    };
    paymentClient = {
      createOrder: jest.fn().mockReturnValue(of({ id: 'order-1', stripeSessionId: '' })),
    };
    promoCodeClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, discountAmount: 100 })),
    };
    countryClient = { getServiced: jest.fn().mockReturnValue(of([])) };
    extraClient = { getOverview: jest.fn().mockReturnValue(of([])) };
    userClient = { getCurrent: jest.fn().mockReturnValue(of({})) };
    apiClient = { serviceCity: jest.fn().mockReturnValue(of([])) };
    authService = { isLoggedIn: jest.fn().mockReturnValue(false) };
    snackbar = { showError: jest.fn() };
    router = { navigate: jest.fn() };
    guestOrderService = { save: jest.fn() };
    savedAddressStore = {
      addresses: signal([]),
      loaded: signal(true),
      defaultAddress: signal(null),
      refresh: jest.fn(),
      add: jest.fn().mockResolvedValue({ id: 'addr-new' }),
    };

    TestBed.configureTestingModule({
      providers: [
        OrderPricingFacade,
        OrderPromoFacade,
        OrderSavedAddressFacade,
        OrderServiceAreaFacade,
        OrderWizardFacade,
        provideMockStore(),
        { provide: PLATFORM_ID, useValue: platform },
        {
          provide: CustomerClient,
          useValue: {
            orderClient,
            paymentClient,
            promoCodeClient,
            countryClient,
            extraClient,
            userClient,
            apiClient,
          },
        },
        { provide: CustomerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: Router, useValue: router },
        { provide: GuestOrderService, useValue: guestOrderService },
        {
          provide: SavedAddressStore,
          useValue: savedAddressStore,
        },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'en',
            getDefaultLang: () => 'en',
          },
        },
      ],
    });

    store = TestBed.inject(MockStore);
    store.overrideSelector(selectCustomerServices, []);
    store.overrideSelector(selectCustomerPackages, []);
    facade = TestBed.inject(OrderWizardFacade);
  }

  beforeEach(() => configure());

  describe('validatePromoCodeNow', () => {
    it('returns idle and skips the client for an empty code', async () => {
      const state = await facade.validatePromoCodeNow('   ');

      expect(state).toEqual({ kind: 'idle' });
      expect(promoCodeClient.validate).not.toHaveBeenCalled();
    });

    it('resolves to valid when the backend accepts the code', async () => {
      const state = await facade.validatePromoCodeNow('save10');

      expect(promoCodeClient.validate).toHaveBeenCalledTimes(1);
      expect(state).toEqual({ kind: 'valid', discount: 100 });
      expect(facade.promoCodeState()).toEqual({ kind: 'valid', discount: 100 });
    });

    it('resolves to invalid when the backend rejects the code', async () => {
      promoCodeClient.validate.mockReturnValue(of({ isValid: false, errorCode: 'promo.expired' }));

      const state = await facade.validatePromoCodeNow('bad');

      expect(state).toEqual({ kind: 'invalid', error: 'promo.expired' });
    });

    it('resolves to invalid on a network error', async () => {
      promoCodeClient.validate.mockReturnValue(throwError(() => new Error('boom')));

      const state = await facade.validatePromoCodeNow('bad');

      expect(state).toEqual({ kind: 'invalid', error: null });
    });
  });

  describe('promo code mutators', () => {
    it('setPromoCode persists the raw input into form data', () => {
      facade.setPromoCode('save10');

      expect(facade.promoCode()).toBe('save10');
      expect(facade.formData().promoCode).toBe('save10');
    });

    it('a valid promo code normalizes and is stored uppercased', async () => {
      await facade.validatePromoCodeNow('save10');

      expect(facade.promoCode()).toBe('SAVE10');
      expect(facade.formData().promoCode).toBe('SAVE10');
    });

    it('validates the promo against the displayed total (post-surcharge subtotal)', async () => {
      await facade.validatePromoCodeNow('save10');

      const command = promoCodeClient.validate.mock.calls[0][0];
      expect(command.orderSubtotal).toBe(facade.displayedTotalPrice() ?? 0);
    });

    it('clearPromoCode resets the state and wipes the form value', async () => {
      await facade.validatePromoCodeNow('save10');

      facade.clearPromoCode();

      expect(facade.promoCodeState()).toEqual({ kind: 'idle' });
      expect(facade.promoCode()).toBe('');
      expect(facade.formData().promoCode).toBe('');
    });

    it('effectivePromoDiscount reflects the applied valid discount', async () => {
      await facade.validatePromoCodeNow('save10');

      expect(facade.effectivePromoDiscount()).toBe(100);

      facade.clearPromoCode();

      expect(facade.effectivePromoDiscount()).toBe(0);
    });
  });

  describe('city-serviced check', () => {
    beforeEach(() => {
      TestBed.resetTestingModule();
      configure('browser');
    });

    function setAddress(partial: Partial<{ city: string; countryId: string }>): void {
      facade.updateFormData({
        address: new AddressDto({
          street: 'Main 1',
          city: partial.city ?? 'Prague',
          zipCode: '11000',
          countryId: partial.countryId ?? 'cz',
          state: '',
        }),
      });
    }

    it('starts idle', () => {
      expect(facade.cityServiced()).toBe('idle');
    });

    it('stays idle when city or country is missing', () => {
      facade.updateFormData({
        address: new AddressDto({ street: '', city: '', zipCode: '', countryId: 'cz', state: '' }),
      });

      expect(facade.cityServiced()).toBe('idle');
      expect(apiClient.serviceCity).not.toHaveBeenCalled();
    });

    it('transitions idle → pending → ok when the city is served', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Prague' }]));

      setAddress({ city: 'Prague' });

      expect(facade.cityServiced()).toBe('ok');
    });

    it('transitions to rejected when the city is not served', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Brno' }]));

      setAddress({ city: 'Prague' });

      expect(facade.cityServiced()).toBe('rejected');
    });

    it('transitions to error (pass-through) on a network failure', () => {
      apiClient.serviceCity.mockReturnValue(throwError(() => new Error('boom')));

      setAddress({ city: 'Prague' });

      expect(facade.cityServiced()).toBe('error');
    });

    it('skips re-querying when the city/country key is unchanged', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Prague' }]));

      setAddress({ city: 'Prague' });
      setAddress({ city: 'Prague' });

      expect(apiClient.serviceCity).toHaveBeenCalledTimes(1);
    });

    it('matches the city case-insensitively', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'PRAGUE' }]));

      setAddress({ city: 'prague' });

      expect(facade.cityServiced()).toBe('ok');
    });
  });

  describe('refreshQuoteNow', () => {
    it('returns null and clears the quote when there is no selection', async () => {
      const result = await facade.refreshQuoteNow();

      expect(result).toBeNull();
      expect(orderClient.quote).not.toHaveBeenCalled();
      expect(facade.quote()).toBeNull();
    });

    it('fetches and stores a fresh quote when a service is selected', async () => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });

      const result = await facade.refreshQuoteNow();

      expect(orderClient.quote).toHaveBeenCalledTimes(1);
      expect(result).toEqual(quoteResponse);
      expect(facade.quote()).toEqual(quoteResponse);
      expect(facade.quoting()).toBe(false);
    });

    it('returns null and stops quoting on a quote error', async () => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      orderClient.quote.mockReturnValue(throwError(() => new Error('boom')));

      const result = await facade.refreshQuoteNow();

      expect(result).toBeNull();
      expect(facade.quoting()).toBe(false);
    });
  });

  describe('canProceed', () => {
    function fillValidContactAndAddress(): void {
      facade.applyAddressSuggestion({
        street: 'Wenceslas Square',
        city: 'Prague',
        zipCode: '11000',
        latitude: 50.08,
        longitude: 14.42,
      });
      facade.updateFormData({
        address: new AddressDto({
          street: 'Wenceslas Square',
          city: 'Prague',
          zipCode: '11000',
          countryId: 'cz',
          state: '',
        }),
        addressLatitude: 50.08,
        addressLongitude: 14.42,
        customerFirstName: 'Anna',
        customerLastName: 'Brown',
        customerEmail: 'anna@example.com',
        customerPhone: '+420123456789',
      });
    }

    it('step 0 requires at least one selected service or package', () => {
      facade.goToStep(0);
      expect(facade.canProceed()).toBe(false);

      facade.updateFormData({ selectedServiceIds: ['s1'] });
      expect(facade.canProceed()).toBe(true);
    });

    it('step 0 passes with only a package selected', () => {
      facade.goToStep(0);
      facade.updateFormData({ selectedPackageIds: ['p1'] });
      expect(facade.canProceed()).toBe(true);
    });

    it('step 1 requires valid contact, address and phone', () => {
      facade.goToStep(1);
      expect(facade.canProceed()).toBe(false);

      fillValidContactAndAddress();
      expect(facade.canProceed()).toBe(true);
    });

    it('step 1 rejects a custom address without coordinates', () => {
      facade.goToStep(1);
      facade.updateFormData({
        address: new AddressDto({
          street: 'Wenceslas Square',
          city: 'Prague',
          zipCode: '11000',
          countryId: 'cz',
          state: '',
        }),
        addressLatitude: null,
        addressLongitude: null,
        customerFirstName: 'Anna',
        customerLastName: 'Brown',
        customerEmail: 'anna@example.com',
        customerPhone: '+420123456789',
      });

      expect(facade.canProceed()).toBe(false);
    });

    it('step 1 rejects an invalid email', () => {
      facade.goToStep(1);
      fillValidContactAndAddress();
      facade.updateFormData({ customerEmail: 'not-an-email' });

      expect(facade.canProceed()).toBe(false);
    });

    it('step 1 is blocked when the city-serviced check rejected', () => {
      TestBed.resetTestingModule();
      configure('browser');
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Brno' }]));
      facade.goToStep(1);
      fillValidContactAndAddress();

      expect(facade.cityServiced()).toBe('rejected');
      expect(facade.canProceed()).toBe(false);
    });

    it('step 2 requires a cleaning date', () => {
      facade.goToStep(2);
      expect(facade.canProceed()).toBe(false);

      facade.updateFormData({ cleaningDate: new Date('2026-07-01T00:00:00Z') });
      expect(facade.canProceed()).toBe(true);
    });

    it('step 3 (payment) always passes', () => {
      facade.goToStep(3);
      expect(facade.canProceed()).toBe(true);
    });
  });

  describe('prefillFromRebook', () => {
    beforeEach(() => {
      store.overrideSelector(selectCustomerServices, [
        { id: 's1' },
        { id: 's2' },
      ] as never);
      store.overrideSelector(selectCustomerPackages, [{ id: 'p1' }] as never);
      store.refreshState();
    });

    it('prefills available services/packages and reports none unavailable', () => {
      const missing = facade.prefillFromRebook({
        selectedServiceIds: ['s1', 's2'],
        selectedPackageIds: ['p1'],
        selectedServiceNames: ['Svc 1', 'Svc 2'],
        selectedPackageNames: ['Pkg 1'],
        rooms: 3,
        bathrooms: 2,
      });

      expect(missing).toEqual([]);
      expect(facade.formData().selectedServiceIds).toEqual(['s1', 's2']);
      expect(facade.formData().selectedPackageIds).toEqual(['p1']);
      expect(facade.formData().rooms).toBe(3);
      expect(facade.formData().bathrooms).toBe(2);
    });

    it('drops unavailable items and returns their names', () => {
      const missing = facade.prefillFromRebook({
        selectedServiceIds: ['s1', 'gone'],
        selectedPackageIds: ['missing-pkg'],
        selectedServiceNames: ['Svc 1', 'Old Service'],
        selectedPackageNames: ['Old Package'],
        rooms: 1,
        bathrooms: 1,
      });

      expect(missing).toEqual(['Old Service', 'Old Package']);
      expect(facade.formData().selectedServiceIds).toEqual(['s1']);
      expect(facade.formData().selectedPackageIds).toEqual([]);
    });

    it('applies the address when the rebook carries one', () => {
      facade.prefillFromRebook({
        selectedServiceIds: ['s1'],
        selectedPackageIds: [],
        selectedServiceNames: ['Svc 1'],
        selectedPackageNames: [],
        rooms: 1,
        bathrooms: 1,
        address: { street: 'Old St 5', city: 'Brno', zipCode: '60200', countryId: 'cz', state: '' },
      });

      expect(facade.formData().address.street).toBe('Old St 5');
      expect(facade.formData().address.city).toBe('Brno');
    });
  });

  describe('saved-address handling', () => {
    beforeEach(() => {
      savedAddressStore.addresses.set([savedAddress] as never);
    });

    it('selectSavedAddress copies the record into form data and marks it selected', () => {
      facade.selectSavedAddress('addr-1');

      expect(facade.selectedSavedAddressId()).toBe('addr-1');
      expect(facade.isSavedAddressSelected()).toBe(true);
      expect(facade.formData().address.street).toBe('Wenceslas 1');
      expect(facade.formData().address.city).toBe('Prague');
      expect(facade.formData().addressLatitude).toBe(50.08);
      expect(facade.formData().addressLongitude).toBe(14.42);
    });

    it('selectSavedAddress is a no-op for an unknown id', () => {
      facade.selectSavedAddress('nope');

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(facade.isSavedAddressSelected()).toBe(false);
    });

    it('updateAddressFromForm clears the saved-address binding and coordinates', () => {
      facade.selectSavedAddress('addr-1');

      facade.updateAddressFromForm(
        new AddressDto({ street: 'New St 9', city: 'Plzen', zipCode: '30100', countryId: 'cz', state: '' }),
      );

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(facade.formData().address.street).toBe('New St 9');
      expect(facade.formData().addressLatitude).toBeNull();
      expect(facade.formData().addressLongitude).toBeNull();
    });

    it('applyAddressSuggestion captures coordinates and clears the saved binding', () => {
      facade.selectSavedAddress('addr-1');

      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(facade.formData().address.street).toBe('Park Ave 2');
      expect(facade.formData().addressLatitude).toBe(49.83);
      expect(facade.formData().addressLongitude).toBe(18.28);
    });

    it('saveCurrentAddressAsSaved returns false without coordinates and skips the store', async () => {
      facade.updateAddressFromForm(
        new AddressDto({ street: 'New St 9', city: 'Plzen', zipCode: '30100', countryId: 'cz', state: '' }),
      );

      const saved = await facade.saveCurrentAddressAsSaved('Home');

      expect(saved).toBe(false);
      expect(savedAddressStore.add).not.toHaveBeenCalled();
    });

    it('saveCurrentAddressAsSaved persists and selects the new id when coordinates exist', async () => {
      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });

      const saved = await facade.saveCurrentAddressAsSaved('Home');

      expect(saved).toBe(true);
      expect(savedAddressStore.add).toHaveBeenCalledTimes(1);
      expect(facade.selectedSavedAddressId()).toBe('addr-new');
    });
  });

  describe('submitOrder', () => {
    beforeEach(() => {
      facade.updateFormData({
        selectedServiceIds: ['s1'],
        cleaningDate: new Date('2026-07-01T00:00:00Z'),
        cleaningTime: '10:00',
        customerFirstName: 'A',
        customerLastName: 'B',
        customerEmail: 'a@b.com',
        customerPhone: '+420123456789',
      });
    });

    it('does nothing without a cleaning date', async () => {
      facade.updateFormData({ cleaningDate: null });

      await facade.submitOrder();

      expect(paymentClient.createOrder).not.toHaveBeenCalled();
      expect(orderClient.createOrder).not.toHaveBeenCalled();
    });

    it('surfaces an error when the quote cannot be resolved', async () => {
      orderClient.quote.mockReturnValue(throwError(() => new Error('boom')));

      await facade.submitOrder();

      expect(snackbar.showError).toHaveBeenCalledWith('pages.order.quote_failed');
      expect(facade.submitting()).toBe(false);
    });

    it('routes through the payment client on a card order with a stripe redirect', async () => {
      facade.updateFormData({ paymentType: PaymentType.Card });
      paymentClient.createOrder.mockReturnValue(
        of({ id: 'order-1', stripeSessionId: '' }),
      );

      await facade.submitOrder();

      expect(paymentClient.createOrder).toHaveBeenCalledTimes(1);
      expect(guestOrderService.save).toHaveBeenCalledWith('order-1', 'a@b.com');
      expect(router.navigate).toHaveBeenCalledWith(
        [CleansiaCustomerRoute.CHECKOUT_SUCCESS],
        { queryParams: { type: 'card' } },
      );
      expect(facade.submitting()).toBe(false);
    });

    it('routes through the order client on a cash order', async () => {
      facade.updateFormData({ paymentType: PaymentType.Cash });

      await facade.submitOrder();

      expect(orderClient.createOrder).toHaveBeenCalledTimes(1);
      expect(router.navigate).toHaveBeenCalledWith(
        [CleansiaCustomerRoute.CHECKOUT_SUCCESS],
        { queryParams: { type: 'cash' } },
      );
      expect(facade.submitting()).toBe(false);
    });

    it('shows an error and clears submitting when create fails', async () => {
      facade.updateFormData({ paymentType: PaymentType.Cash });
      orderClient.createOrder.mockReturnValue(throwError(() => new Error('boom')));

      await facade.submitOrder();

      expect(snackbar.showError).toHaveBeenCalledWith('pages.order.submit_error');
      expect(facade.submitting()).toBe(false);
    });

    it('sends customerAddress and no savedAddressId for a custom address', async () => {
      facade.updateFormData({ paymentType: PaymentType.Cash });

      await facade.submitOrder();

      const command = orderClient.createOrder.mock.calls[0][0];
      expect(command.savedAddressId).toBeUndefined();
      expect(command.customerAddress).toBeDefined();
    });

    it('sends savedAddressId and no customerAddress for a saved address', async () => {
      savedAddressStore.addresses.set([savedAddress] as never);
      facade.selectSavedAddress('addr-1');
      facade.updateFormData({ paymentType: PaymentType.Cash });

      await facade.submitOrder();

      const command = orderClient.createOrder.mock.calls[0][0];
      expect(command.savedAddressId).toBe('addr-1');
      expect(command.customerAddress).toBeUndefined();
    });

    it('saves a new address before submitting when requested', async () => {
      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });
      facade.updateFormData({ paymentType: PaymentType.Cash });

      await facade.submitOrder({ label: 'Home' });

      expect(savedAddressStore.add).toHaveBeenCalledTimes(1);
      const command = orderClient.createOrder.mock.calls[0][0];
      expect(command.savedAddressId).toBe('addr-new');
    });
  });

  describe('initialize', () => {
    it('loads served countries and extras and reads auth state', () => {
      facade.initialize();

      expect(countryClient.getServiced).toHaveBeenCalledTimes(1);
      expect(extraClient.getOverview).toHaveBeenCalledTimes(1);
      expect(facade.isAuthenticated()).toBe(false);
    });
  });

  describe('pricing display', () => {
    it('reflects the server-quoted total in totalPrice', async () => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.refreshQuoteNow();

      expect(facade.totalPrice()).toBe(1000);
    });

    it('renders the express quote verbatim — server total, no client gross-up', async () => {
      orderClient.quote.mockReturnValue(
        of({
          totalPrice: 1200,
          expressSurchargeApplied: true,
          expressSurchargeAmount: 200,
          currencyId: 'czk',
        }),
      );
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.refreshQuoteNow();

      expect(facade.expressSurchargeApplied()).toBe(true);
      expect(facade.expressSurcharge()).toBe(200);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
      expect(facade.displayedTotalPrice()).toBe(1200);
    });

    it('charges no surcharge and shows the bare total for a standard quote', async () => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      await facade.refreshQuoteNow();

      expect(facade.expressSurchargeApplied()).toBe(false);
      expect(facade.expressSurcharge()).toBe(0);
      expect(facade.displayedTotalPrice()).toBe(1000);
    });
  });

  describe('live quote stream', () => {
    beforeEach(() => {
      TestBed.resetTestingModule();
      configure('browser');
    });

    it('debounces selection changes into a single quote call and populates quote()', fakeAsync(() => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      TestBed.flushEffects();
      facade.updateFormData({ selectedServiceIds: ['s1', 's2'] });
      TestBed.flushEffects();
      tick(800);

      expect(orderClient.quote).toHaveBeenCalledTimes(1);
      expect(facade.quote()).toEqual(quoteResponse);
      expect(facade.quoting()).toBe(false);
    }));

    it('clears the quote and skips the network when the selection becomes empty', fakeAsync(() => {
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      TestBed.flushEffects();
      tick(800);
      orderClient.quote.mockClear();

      facade.updateFormData({ selectedServiceIds: [] });
      TestBed.flushEffects();
      tick(800);

      expect(orderClient.quote).not.toHaveBeenCalled();
      expect(facade.quote()).toBeNull();
      expect(facade.quoting()).toBe(false);
    }));

    it('keeps the prior quote and stops quoting when the quote call errors', fakeAsync(() => {
      orderClient.quote.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.updateFormData({ selectedServiceIds: ['s1'] });
      TestBed.flushEffects();
      tick(800);

      expect(facade.quote()).toBeNull();
      expect(facade.quoting()).toBe(false);
    }));
  });
});
