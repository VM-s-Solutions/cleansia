import { PLATFORM_ID, signal } from '@angular/core';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPricingFacade } from './order-pricing.facade';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

describe('OrderPricingFacade', () => {
  let facade: OrderPricingFacade;
  let orderClient: { quote: jest.Mock };
  let formData: ReturnType<typeof signal<OrderWizardFormData>>;
  let effectiveDiscount: ReturnType<typeof signal<number>>;

  const quoteResponse = {
    totalPrice: 1000,
    expressSurchargeApplied: false,
    expressSurchargeAmount: 0,
    currencyId: 'czk',
  };
  // Server totals already include the surcharge — 1000 base + 200 express.
  const expressQuoteResponse = {
    totalPrice: 1200,
    expressSurchargeApplied: true,
    expressSurchargeAmount: 200,
    currencyId: 'czk',
  };

  function pickExpressSlot(): void {
    const slot = new Date(Date.now() + 3 * 60 * 60 * 1000);
    const time = `${slot.getHours().toString().padStart(2, '0')}:${slot
      .getMinutes()
      .toString()
      .padStart(2, '0')}`;
    formData.update((d) => ({ ...d, cleaningDate: slot, cleaningTime: time }));
  }

  function build(platform: 'server' | 'browser'): void {
    orderClient = { quote: jest.fn().mockReturnValue(of(quoteResponse)) };
    formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA });
    effectiveDiscount = signal(0);

    TestBed.configureTestingModule({
      providers: [
        OrderPricingFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { orderClient } },
      ],
    });

    facade = TestBed.inject(OrderPricingFacade);
    facade.connect({ formData, effectiveDiscount });
  }

  describe('refreshQuoteNow', () => {
    beforeEach(() => build('server'));

    it('clears the quote and skips the network for an empty selection', async () => {
      const result = await facade.refreshQuoteNow();

      expect(result).toBeNull();
      expect(orderClient.quote).not.toHaveBeenCalled();
      expect(facade.quote()).toBeNull();
    });

    it('fetches, stores and stops quoting when a service is selected', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));

      const result = await facade.refreshQuoteNow();

      expect(orderClient.quote).toHaveBeenCalledTimes(1);
      expect(result).toEqual(quoteResponse);
      expect(facade.quote()).toEqual(quoteResponse);
      expect(facade.quoting()).toBe(false);
    });

    it('returns null and stops quoting on a quote error', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      orderClient.quote.mockReturnValue(throwError(() => new Error('boom')));

      const result = await facade.refreshQuoteNow();

      expect(result).toBeNull();
      expect(facade.quoting()).toBe(false);
    });
  });

  describe('server-verbatim pricing display', () => {
    beforeEach(() => build('server'));

    it('exposes the server-quoted total via totalPrice', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      await facade.refreshQuoteNow();

      expect(facade.totalPrice()).toBe(1000);
    });

    it('displays the server total unchanged for an express quote — no client gross-up', async () => {
      orderClient.quote.mockReturnValue(of(expressQuoteResponse));
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      pickExpressSlot();
      await facade.refreshQuoteNow();

      expect(facade.expressSurchargeApplied()).toBe(true);
      expect(facade.expressSurcharge()).toBe(200);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
      expect(facade.displayedTotalPrice()).toBe(1200);
    });

    it('subtracts the discount from the server total and keeps the surcharge line verbatim', async () => {
      orderClient.quote.mockReturnValue(of(expressQuoteResponse));
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      pickExpressSlot();
      await facade.refreshQuoteNow();
      effectiveDiscount.set(100);

      expect(facade.expressSurcharge()).toBe(200);
      expect(facade.displayedTotalPrice()).toBe(1100);
    });

    it('shows the bare discounted total and no surcharge for a standard quote', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      await facade.refreshQuoteNow();
      effectiveDiscount.set(150);

      expect(facade.expressSurchargeApplied()).toBe(false);
      expect(facade.expressSurcharge()).toBe(0);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
      expect(facade.displayedTotalPrice()).toBe(850);
    });
  });

  describe('live quote stream (browser)', () => {
    beforeEach(() => build('browser'));

    it('debounces selection changes into one quote call and populates quote()', fakeAsync(() => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      TestBed.flushEffects();
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1', 's2'] }));
      TestBed.flushEffects();
      tick(800);

      expect(orderClient.quote).toHaveBeenCalledTimes(1);
      expect(facade.quote()).toEqual(quoteResponse);
      expect(facade.quoting()).toBe(false);
    }));

    it('clears the quote and skips the network when selection empties', fakeAsync(() => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      TestBed.flushEffects();
      tick(800);
      orderClient.quote.mockClear();

      formData.update((d) => ({ ...d, selectedServiceIds: [] }));
      TestBed.flushEffects();
      tick(800);

      expect(orderClient.quote).not.toHaveBeenCalled();
      expect(facade.quote()).toBeNull();
      expect(facade.quoting()).toBe(false);
    }));

    it('keeps the prior quote and resets quoting on a stream error', fakeAsync(() => {
      orderClient.quote.mockReturnValueOnce(throwError(() => new Error('boom')));
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      TestBed.flushEffects();
      tick(800);

      expect(facade.quote()).toBeNull();
      expect(facade.quoting()).toBe(false);
    }));

    it('does not start the stream during SSR', fakeAsync(() => {
      TestBed.resetTestingModule();
      build('server');
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      TestBed.flushEffects();
      tick(800);

      expect(orderClient.quote).not.toHaveBeenCalled();
    }));
  });

  describe('cachedQuoteMatchesCurrentState', () => {
    beforeEach(() => build('server'));

    it('is false before any quote is fetched', () => {
      expect(facade.cachedQuoteMatchesCurrentState()).toBe(false);
    });

    it('is true right after a quote for the current inputs', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      await facade.refreshQuoteNow();

      expect(facade.cachedQuoteMatchesCurrentState()).toBe(true);
    });

    it('is false once the inputs change after a quote', async () => {
      formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
      await facade.refreshQuoteNow();
      formData.update((d) => ({ ...d, rooms: d.rooms + 1 }));

      expect(facade.cachedQuoteMatchesCurrentState()).toBe(false);
    });
  });
});
