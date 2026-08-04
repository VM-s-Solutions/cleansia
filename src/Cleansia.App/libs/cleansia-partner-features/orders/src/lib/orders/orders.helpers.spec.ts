import { OrderStatus } from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { buildOrderStatusOptions } from './orders.helpers';

const translate = {
  instant: (key: string) => key,
} as unknown as TranslateService;

describe('buildOrderStatusOptions', () => {
  const values = (): number[] =>
    buildOrderStatusOptions(translate).map((option) => option.value as number);

  it('can name every status the server may still offer a cleaner', () => {
    expect(values()).toEqual(
      expect.arrayContaining([OrderStatus.New, OrderStatus.Confirmed])
    );
  });

  it('does not offer the dead Pending status', () => {
    expect(values()).not.toContain(OrderStatus.Pending);
  });

  it('keeps the post-take statuses so a cleaner can still filter their own work', () => {
    expect(values()).toEqual(
      expect.arrayContaining([
        OrderStatus.OnTheWay,
        OrderStatus.InProgress,
        OrderStatus.Completed,
        OrderStatus.Cancelled,
      ])
    );
  });

  it('labels every option from the order-status translation namespace', () => {
    const labels = buildOrderStatusOptions(translate).map((option) => option.label);

    expect(labels.every((label) => label.startsWith('enums.order_status.'))).toBe(
      true
    );
  });
});
