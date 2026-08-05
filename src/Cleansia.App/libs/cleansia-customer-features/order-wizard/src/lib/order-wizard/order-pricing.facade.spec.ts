import { PLATFORM_ID, signal } from '@angular/core';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderPricingFacade } from './order-pricing.facade';
import {
  DISCOUNTED_QUOTE,
  EXPRESS_DISCOUNTED_QUOTE,
  EXPRESS_QUOTE,
  PLAIN_QUOTE,
  WAIVED_EXPRESS_QUOTE,
} from './order-quote.fixtures';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

describe('OrderPricingFacade', () => {
  let facade: OrderPricingFacade;
  let orderClient: { quote: jest.Mock };
  let formData: ReturnType<typeof signal<OrderWizardFormData>>;
  let promoDiscount: ReturnType<typeof signal<number>>;

  function pickExpressSlot(): void {
    const slot = new Date(Date.now() + 3 * 60 * 60 * 1000);
    const time = `${slot.getHours().toString().padStart(2, '0')}:${slot
      .getMinutes()
      .toString()
      .padStart(2, '0')}`;
    formData.update((d) => ({ ...d, cleaningDate: slot, cleaningTime: time }));
  }

  function build(platform: 'server' | 'browser'): void {
    orderClient = { quote: jest.fn().mockReturnValue(of(PLAIN_QUOTE)) };
    formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA });
    promoDiscount = signal(0);

    TestBed.configureTestingModule({
      providers: [
        OrderPricingFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { orderClient } },
      ],
    });

    facade = TestBed.inject(OrderPricingFacade);
    facade.connect({ formData, promoDiscount });
  }

  async function quoteWith(response: unknown): Promise<void> {
    orderClient.quote.mockReturnValue(of(response));
    formData.update((d) => ({ ...d, selectedServiceIds: ['s1'] }));
    await facade.refreshQuoteNow();
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
      expect(result).toEqual(PLAIN_QUOTE);
      expect(facade.quote()).toEqual(PLAIN_QUOTE);
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

  describe('the price shown is the price charged', () => {
    beforeEach(() => build('server'));

    // The defect. The server charges ApplyExpressSurcharge(raw - discount) = (1000 - 100) * 1.2;
    // `gross - discount` reads 1200 - 100 and over-displays by a fifth of every discount.
    it('shows the quoted final price on an express booking that also has a discount', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);
      pickExpressSlot();

      expect(facade.displayedTotalPrice()).toBe(1080);
    });

    it('bills the surcharge on the discounted subtotal, so the breakdown rows sum to the total', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);
      pickExpressSlot();

      expect(facade.expressSurcharge()).toBe(180);
      expect(
        facade.preSurchargeSubtotal() - facade.effectiveDiscount() + facade.expressSurcharge(),
      ).toBe(facade.displayedTotalPrice());
    });

    it('still submits the undiscounted gross, which is what CreateOrder validates', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);
      pickExpressSlot();

      expect(facade.totalPrice()).toBe(1200);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
    });
  });

  describe('the zero-delta cases are unchanged', () => {
    beforeEach(() => build('server'));

    it('a discount without an express slot subtracts plainly', async () => {
      await quoteWith(DISCOUNTED_QUOTE);

      expect(facade.displayedTotalPrice()).toBe(900);
      expect(facade.expressSurchargeApplied()).toBe(false);
      expect(facade.expressSurcharge()).toBe(0);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
    });

    it('an express slot without a discount shows the gross verbatim', async () => {
      await quoteWith(EXPRESS_QUOTE);
      pickExpressSlot();

      expect(facade.expressSurchargeApplied()).toBe(true);
      expect(facade.expressSurcharge()).toBe(200);
      expect(facade.preSurchargeSubtotal()).toBe(1000);
      expect(facade.displayedTotalPrice()).toBe(1200);
    });

    it('neither surcharge nor discount leaves the bare subtotal', async () => {
      await quoteWith(PLAIN_QUOTE);

      expect(facade.displayedTotalPrice()).toBe(1000);
      expect(facade.expressSurcharge()).toBe(0);
    });

    it('reports the waiver and charges nothing extra for a waived express quote', async () => {
      await quoteWith(WAIVED_EXPRESS_QUOTE);
      pickExpressSlot();

      expect(facade.expressSurchargeWaived()).toBe(true);
      expect(facade.expressSurchargeApplied()).toBe(false);
      expect(facade.expressSurcharge()).toBe(0);
      expect(facade.displayedTotalPrice()).toBe(1000);
    });

    it('keeps the waiver flag false when the surcharge was actually charged', async () => {
      await quoteWith(EXPRESS_QUOTE);
      pickExpressSlot();

      expect(facade.expressSurchargeWaived()).toBe(false);
    });

    it('is 0 with no quote at all', () => {
      expect(facade.displayedTotalPrice()).toBe(0);
      expect(facade.expressSurcharge()).toBe(0);
    });
  });

  describe('a promo the quote could not price', () => {
    beforeEach(() => build('server'));

    it('is ignored while it loses to the quoted discount', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);
      pickExpressSlot();
      promoDiscount.set(60);

      expect(facade.effectiveDiscount()).toBe(100);
      expect(facade.displayedTotalPrice()).toBe(1080);
    });

    it('replaces the quoted discount when it wins, surcharged the way the server surcharges', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);
      pickExpressSlot();
      promoDiscount.set(200);

      expect(facade.effectiveDiscount()).toBe(200);
      expect(facade.displayedTotalPrice()).toBe(960);
    });

    it('subtracts plainly with no express slot', async () => {
      await quoteWith(DISCOUNTED_QUOTE);
      promoDiscount.set(200);

      expect(facade.displayedTotalPrice()).toBe(800);
    });

    it('never drives the total below zero', async () => {
      await quoteWith(EXPRESS_QUOTE);
      pickExpressSlot();
      promoDiscount.set(5000);

      expect(facade.displayedTotalPrice()).toBe(0);
    });
  });

  describe('discount signals read the quote', () => {
    beforeEach(() => build('server'));

    it('splits the quoted amounts into their tier and membership parts', async () => {
      await quoteWith(EXPRESS_DISCOUNTED_QUOTE);

      expect(facade.membershipDiscount()).toBe(100);
      expect(facade.tierDiscount()).toBe(0);
      expect(facade.quotedDiscount()).toBe(100);
    });

    it('reports no discount before a quote arrives', () => {
      expect(facade.membershipDiscount()).toBe(0);
      expect(facade.tierDiscount()).toBe(0);
      expect(facade.effectiveDiscount()).toBe(0);
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
      expect(facade.quote()).toEqual(PLAIN_QUOTE);
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
