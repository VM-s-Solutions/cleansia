import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
} from '@cleansia/customer-services';
import {
  SavedAddressStore,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PaymentType } from '@cleansia/partner-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { GuestOrderService } from '@cleansia-customer/orders';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { OrderWizardFacade } from './order-wizard.facade';

describe('OrderWizardFacade', () => {
  let facade: OrderWizardFacade;
  let store: MockStore;
  let orderClient: { quote: jest.Mock; createOrder: jest.Mock };
  let paymentClient: { createOrder: jest.Mock };
  let promoCodeClient: { validate: jest.Mock };
  let referralClient: { validate: jest.Mock };
  let countryClient: { getServiced: jest.Mock };
  let extraClient: { getOverview: jest.Mock };
  let userClient: { getCurrent: jest.Mock };
  let apiClient: { serviceCity: jest.Mock };
  let authService: { isLoggedIn: jest.Mock };
  let snackbar: { showError: jest.Mock };
  let router: { navigate: jest.Mock };
  let guestOrderService: { save: jest.Mock };

  const quoteResponse = { totalPrice: 1000, currencyId: 'czk' };

  function configure(): void {
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
    referralClient = {
      validate: jest.fn().mockReturnValue(of({ isValid: true, referrerFirstName: 'Pat' })),
    };
    countryClient = { getServiced: jest.fn().mockReturnValue(of([])) };
    extraClient = { getOverview: jest.fn().mockReturnValue(of([])) };
    userClient = { getCurrent: jest.fn().mockReturnValue(of({})) };
    apiClient = { serviceCity: jest.fn().mockReturnValue(of([])) };
    authService = { isLoggedIn: jest.fn().mockReturnValue(false) };
    snackbar = { showError: jest.fn() };
    router = { navigate: jest.fn() };
    guestOrderService = { save: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        OrderWizardFacade,
        provideMockStore(),
        { provide: PLATFORM_ID, useValue: 'server' },
        {
          provide: CustomerClient,
          useValue: {
            orderClient,
            paymentClient,
            promoCodeClient,
            referralClient,
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
          useValue: {
            addresses: signal([]),
            loaded: signal(true),
            defaultAddress: signal(null),
            refresh: jest.fn(),
          },
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

  describe('validateReferralCodeNow', () => {
    it('returns idle and skips the client for an empty code', async () => {
      const state = await facade.validateReferralCodeNow('');

      expect(state).toEqual({ kind: 'idle' });
      expect(referralClient.validate).not.toHaveBeenCalled();
    });

    it('resolves to valid when the backend accepts the code', async () => {
      const state = await facade.validateReferralCodeNow('friend');

      expect(state).toEqual({ kind: 'valid', referrerFirstName: 'Pat' });
    });

    it('resolves to invalid on a network error', async () => {
      referralClient.validate.mockReturnValue(throwError(() => new Error('boom')));

      const state = await facade.validateReferralCodeNow('x');

      expect(state).toEqual({ kind: 'invalid', error: null });
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
  });

  describe('initialize', () => {
    it('loads served countries and extras and reads auth state', () => {
      facade.initialize();

      expect(countryClient.getServiced).toHaveBeenCalledTimes(1);
      expect(extraClient.getOverview).toHaveBeenCalledTimes(1);
      expect(facade.isAuthenticated()).toBe(false);
    });
  });
});
