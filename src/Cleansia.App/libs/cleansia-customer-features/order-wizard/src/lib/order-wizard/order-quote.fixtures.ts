import { IQuoteOrderResponse, QuoteOrderResponse } from '@cleansia/customer-services';

/**
 * `appliedDiscountSource` is the one field these fixtures leave off: the customer barrel
 * (`libs/core/customer-services/src/index.ts`) does not re-export the `AppliedDiscountSource` enum,
 * and no wizard surface reads it — the discount chips key off the amounts. Every other key stays
 * required.
 */
type QuoteFields = Omit<IQuoteOrderResponse, 'appliedDiscountSource'>;

/**
 * Quotes shaped and valued the way the backend composes them.
 *
 * **The numbers are the BACKEND's, taken from its own express-surcharge test** — a 1000 basket, a 10%
 * member, an express slot worth +200. Invent figures here and the wizard's fixtures stop describing
 * what the server actually returns. → /product/business-rules#price-stages
 */
const QUOTE_1000_NO_DISCOUNT: QuoteFields = {
  totalPrice: 1000,
  finalPriceAfterDiscount: 1000,
  originalSubtotal: 1000,
  tierDiscountAmount: undefined,
  membershipDiscountAmount: undefined,
  tierDiscountMinOrderAmount: undefined,
  currencyId: 'czk',
  currencyCode: 'CZK',
  servicesSubtotal: 1000,
  packagesSubtotal: 0,
  extrasSubtotal: 0,
  expressSurchargeApplied: false,
  expressSurchargeAmount: 0,
  exchangeRate: 1,
  expressSurchargeWaivedByMembership: false,
  expressUpgradesRemaining: undefined,
};

export function quoteFixture(overrides: Partial<QuoteFields> = {}): QuoteOrderResponse {
  return Object.assign(new QuoteOrderResponse(), QUOTE_1000_NO_DISCOUNT, overrides);
}

/** Standard slot, no discount. */
export const PLAIN_QUOTE = quoteFixture();

/** Standard slot, Plus 10%: charged 1000 − 100 = 900. */
export const DISCOUNTED_QUOTE = quoteFixture({
  finalPriceAfterDiscount: 900,
  membershipDiscountAmount: 100,
});

/** Express slot, no discount: charged 1000 × 1.2 = 1200, which the gross already carries in full. */
export const EXPRESS_QUOTE = quoteFixture({
  totalPrice: 1200,
  finalPriceAfterDiscount: 1200,
  originalSubtotal: 1200,
  expressSurchargeApplied: true,
  expressSurchargeAmount: 200,
});

/**
 * The defect's own case — express slot AND Plus 10%. The server charges
 * `(1000 − 100) × 1.2 = 1080`; `gross − discount` would say 1100.
 */
export const EXPRESS_DISCOUNTED_QUOTE = quoteFixture({
  totalPrice: 1200,
  finalPriceAfterDiscount: 1080,
  originalSubtotal: 1180,
  membershipDiscountAmount: 100,
  expressSurchargeApplied: true,
  expressSurchargeAmount: 200,
});

/** Express slot covered by a membership waiver: still express, surcharge nevertheless zero. */
export const WAIVED_EXPRESS_QUOTE = quoteFixture({
  expressSurchargeWaivedByMembership: true,
  expressUpgradesRemaining: 2,
});
