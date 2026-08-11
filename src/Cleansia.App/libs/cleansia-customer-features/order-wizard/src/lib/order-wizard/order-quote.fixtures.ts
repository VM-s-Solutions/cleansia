import { IQuoteOrderResponse, QuoteOrderResponse } from '@cleansia/customer-services';

/**
 * `appliedDiscountSource` is the one field these fixtures leave off: the customer barrel
 * (`libs/core/customer-services/src/index.ts`) does not re-export the `AppliedDiscountSource` enum,
 * and no wizard surface reads it — the discount chips key off the amounts. Every other key stays
 * required.
 */
type QuoteFields = Omit<IQuoteOrderResponse, 'appliedDiscountSource'>;

/**
 * Quotes shaped and valued the way `QuoteOrder.Handler` composes them.
 *
 * The numbers are the backend's, not a wizard author's reading of them: every figure below is the
 * arrangement in `src/Cleansia.Tests/Features/Orders/QuoteOrderExpressSurchargeDiscountBaseTests.cs`
 * — a 1000 Kč basket, a 10% Plus member, an express slot worth +200 — whose facts
 * (`TotalPrice == 1200`, `MembershipDiscountAmount == 100`, `FinalPriceAfterDiscount == 1080`,
 * `OriginalSubtotal == 1180`, and the non-express row `1000 / 100 / 900 / 1000`) are asserted there
 * against what `OrderFactory` persists.
 *
 * Typed through the generated response interface instead of an inline literal on purpose: its keys
 * are required, so a regenerated client that renames or drops a pricing field breaks these fixtures
 * at compile time rather than leaving the wizard green against a contract that moved. That guards
 * the response's SHAPE only. A server that changes a pricing VALUE without changing the shape still
 * cannot redden a client test — that needs a fixture generated from a real quote, which neither
 * stack produces today.
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
