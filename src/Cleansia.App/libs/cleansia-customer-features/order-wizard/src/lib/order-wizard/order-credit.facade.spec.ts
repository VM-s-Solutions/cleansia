import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CustomerClient } from '@cleansia/customer-services';
import { of } from 'rxjs';
import { OrderPricingFacade } from './order-pricing.facade';
import { quoteFixture } from './order-quote.fixtures';
import {
  capCreditForOrder,
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
} from './order-wizard.models';

/**
 * The credit lines in the booking summary.
 *
 * <p>Owner ruling 2026-09-05: credit applies automatically and can never settle a whole booking. The
 * customer therefore has to be TOLD, before they are redirected to Stripe, how much of their balance
 * this booking takes and what the card is left to pay — a smaller charge discovered afterwards is a
 * support ticket, not a feature.</p>
 *
 * <p>The cap is applied on the client because the price it caps against is only final here: a promo
 * code is entered at checkout and applied at create time, so the server's quote cannot know it. That
 * is the same arrangement `composeFinalPriceForUnquotedDiscount` uses, and these tests exist because
 * the arrangement's whole risk is the client's answer drifting from the server's.</p>
 */
describe('credit in the booking summary', () => {
  let facade: OrderPricingFacade;
  let orderClient: { quote: jest.Mock };
  let formData: ReturnType<typeof signal<OrderWizardFormData>>;
  let promoDiscount: ReturnType<typeof signal<number>>;

  function build(): void {
    orderClient = { quote: jest.fn() };
    formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA });
    promoDiscount = signal(0);

    TestBed.configureTestingModule({
      providers: [
        OrderPricingFacade,
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CustomerClient, useValue: { orderClient } },
      ],
    });

    facade = TestBed.inject(OrderPricingFacade);
    facade.connect({ formData, promoDiscount });
  }

  /** Drives one quote through the facade the way the wizard does — the same helper the sibling spec uses. */
  async function quoteWith(quote: ReturnType<typeof quoteFixture>): Promise<void> {
    build();
    orderClient.quote.mockReturnValue(of(quote));
    formData.update((d) => ({ ...d, selectedServiceIds: ['svc-1'] }));
    await facade.refreshQuoteNow();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('capCreditForOrder — the rule, mirrored from BookingPolicy', () => {
    it('spends the whole balance when it fits under the share', () => {
      expect(capCreditForOrder(500, 2000, 0.8)).toBe(500);
    });

    it('caps at the share when the balance is larger', () => {
      expect(capCreditForOrder(2000, 1000, 0.8)).toBe(800);
    });

    /**
     * THE RULING, as a property: the card always pays something. Asserted across a spread of prices
     * rather than one example, because the failure mode is a rounding edge at one price.
     */
    it.each([1, 7, 99.99, 1000, 13333.33])(
      'never settles the whole booking (%p)',
      (price: number) => {
        expect(capCreditForOrder(1_000_000, price, 0.8)).toBeLessThan(price);
      },
    );

    it('floors to whole cents, so the shown figure is the charged figure', () => {
      // 80% of 33.33 is 26.664.
      expect(capCreditForOrder(1000, 33.33, 0.8)).toBe(26.66);
    });

    it.each([
      [0, 1000, 0.8],
      [-100, 1000, 0.8],
      [500, 0, 0.8],
      [500, 1000, 0],
    ])('answers zero for (%p, %p, %p)', (balance, price, share) => {
      expect(capCreditForOrder(balance, price, share)).toBe(0);
    });
  });

  describe('the summary signals', () => {
    it('shows nothing for a customer who has never been credited', async () => {
      await quoteWith(
        quoteFixture({ totalPrice: 2000, finalPriceAfterDiscount: 2000, creditBalance: 0 }),
      );

      expect(facade.creditBalance()).toBe(0);
      expect(facade.creditApplied()).toBe(0);
      expect(facade.amountDueOnCard()).toBe(2000);
    });

    it('applies the balance and leaves the rest on the card', async () => {
      await quoteWith(
        quoteFixture({ totalPrice: 2000, finalPriceAfterDiscount: 2000, creditBalance: 500 }),
      );

      expect(facade.creditApplied()).toBe(500);
      expect(facade.amountDueOnCard()).toBe(1500);
    });

    it('caps a large balance at the share, and keeps the remainder on the balance', async () => {
      await quoteWith(
        quoteFixture({ totalPrice: 1000, finalPriceAfterDiscount: 1000, creditBalance: 2000 }),
      );

      expect(facade.creditApplied()).toBe(800);
      expect(facade.amountDueOnCard()).toBe(200);
      // The whole balance is still reported, so the summary can say "800 of your 2000 applies here".
      expect(facade.creditBalance()).toBe(2000);
    });

    /**
     * THE ONE THE CLIENT-SIDE CAP EXISTS FOR. A promo is entered at checkout and priced at create
     * time, so the quote's own total is not what will be charged. Capping against the quote would
     * preview more credit than the checkout takes, and the card would quietly be asked for more than
     * the summary said.
     */
    it('caps against the promo-discounted price, not the quoted one', async () => {
      await quoteWith(
        quoteFixture({ totalPrice: 1000, finalPriceAfterDiscount: 1000, creditBalance: 2000 }),
      );
      expect(facade.creditApplied()).toBe(800);

      promoDiscount.set(500);

      // 80% of the 500 actually being charged, not of the 1000 quoted.
      expect(facade.displayedTotalPrice()).toBe(500);
      expect(facade.creditApplied()).toBe(400);
      expect(facade.amountDueOnCard()).toBe(100);
    });

    it('adds up: the two lines always equal the price being charged', async () => {
      await quoteWith(
        quoteFixture({ totalPrice: 999.99, finalPriceAfterDiscount: 999.99, creditBalance: 333 }),
      );

      expect(facade.creditApplied() + facade.amountDueOnCard()).toBeCloseTo(
        facade.displayedTotalPrice(),
        2,
      );
    });
  });
});
