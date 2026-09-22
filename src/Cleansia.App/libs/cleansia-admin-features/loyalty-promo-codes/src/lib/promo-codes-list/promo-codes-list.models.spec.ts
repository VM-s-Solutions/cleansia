import { PromoCodeListItem } from '@cleansia/admin-services';
import { formatMinimumOrder } from './promo-codes-list.models';

const promo = (overrides: Partial<PromoCodeListItem>): PromoCodeListItem =>
  ({ minimumOrderAmount: 1250, currencyCode: 'CZK', ...overrides }) as PromoCodeListItem;

describe('formatMinimumOrder', () => {
  it('prints the minimum order as money in the promo currency and the session language', () => {
    expect(formatMinimumOrder(promo({}), 'cs')).toBe('1 250,00 Kč');
    expect(formatMinimumOrder(promo({ minimumOrderAmount: 25, currencyCode: 'EUR' }), 'en')).toBe('€25.00');
  });

  it('prints a dash when the promo sets no minimum', () => {
    expect(formatMinimumOrder(promo({ minimumOrderAmount: undefined }), 'cs')).toBe('—');
  });
});
