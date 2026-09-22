import { PromoCodeListItem, PromoCodeType } from '@cleansia/admin-services';
import { formatDiscount, formatMinimumOrder } from './promo-codes-list.models';

const promo = (overrides: Partial<PromoCodeListItem>): PromoCodeListItem =>
  ({
    type: PromoCodeType.FixedDiscount,
    discountAmount: 250,
    minimumOrderAmount: 1250,
    currencyCode: 'CZK',
    ...overrides,
  }) as PromoCodeListItem;

describe('formatDiscount', () => {
  it('prints a fixed discount as money in the promo currency and the session language', () => {
    expect(formatDiscount(promo({}), 'cs')).toBe('250,00 Kč');
    expect(formatDiscount(promo({ discountAmount: 25, currencyCode: 'EUR' }), 'en')).toBe('€25.00');
  });

  it('prints a percent discount as a whole percentage of the stored fraction', () => {
    expect(formatDiscount(promo({ type: PromoCodeType.PercentDiscount, discountPercent: 0.15 }), 'cs')).toBe('15%');
  });
});

describe('formatMinimumOrder', () => {
  it('prints the minimum order as money in the promo currency and the session language', () => {
    expect(formatMinimumOrder(promo({}), 'cs')).toBe('1 250,00 Kč');
    expect(formatMinimumOrder(promo({ minimumOrderAmount: 25, currencyCode: 'EUR' }), 'en')).toBe('€25.00');
  });

  it('prints a dash when the promo sets no minimum', () => {
    expect(formatMinimumOrder(promo({ minimumOrderAmount: undefined }), 'cs')).toBe('—');
  });
});
