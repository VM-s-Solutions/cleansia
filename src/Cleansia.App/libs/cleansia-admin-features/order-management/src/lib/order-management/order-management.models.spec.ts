import { OrderListItem } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { getOrderTableDefinition } from './order-management.models';

describe('order-management table definition', () => {
  const translate = { instant: (k: string) => k, currentLang: 'cs' } as unknown as TranslateService;
  const priceValue = (row: OrderListItem) =>
    getOrderTableDefinition({ onViewDetails: jest.fn() }, translate).columns
      .find((c) => c.id === 'totalPrice')
      ?.getValue?.(row);

  it('labels the total with the currency the order carries, in the language of the session', () => {
    const row = OrderListItem.fromJS({ totalPrice: 45, currency: { symbol: '€', code: 'EUR' } });

    expect(priceValue(row)).toBe('45,00 €');
  });

  // The server always sends the order's currency; when it does not, a bare number is honest and
  // "Kc" is a guess that would mislabel every non-crown order.
  it('prints a bare number rather than a currency the order does not name', () => {
    const row = OrderListItem.fromJS({ totalPrice: 45 });

    expect(priceValue(row)).toBe('45,00');
  });
});
