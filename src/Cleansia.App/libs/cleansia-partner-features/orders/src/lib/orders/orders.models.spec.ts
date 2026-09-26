import { readFileSync } from 'fs';
import { join } from 'path';
import { OrderListItem, OrderStatus } from '@cleansia/partner-services';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
  ORDER_STATUS_FLOW,
} from './orders.models';

const PARTNER_LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia-partner.app/src/assets/i18n');

function resolveKey(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

function row(status: OrderStatus, availableSpots = 1): OrderListItem {
  return OrderListItem.fromJS({
    id: 'ord-1',
    orderStatus: { value: status },
    availableSpots,
  });
}

function takeAction() {
  const { actions } = getAvailableOrdersTableDefinition(
    { onTakeOrder: jest.fn(), isTakeInFlight: () => false },
    'cs'
  );
  return actions[0];
}

describe('getAvailableOrdersTableDefinition — take action visibility', () => {
  const isVisible = (item: OrderListItem): boolean =>
    takeAction().visible?.(item) ?? true;

  it('offers a New order, which is the whole cash pipeline', () => {
    expect(isVisible(row(OrderStatus.New))).toBe(true);
  });

  it('offers a Confirmed order', () => {
    expect(isVisible(row(OrderStatus.Confirmed))).toBe(true);
  });

  it('does not offer the dead Pending status', () => {
    expect(isVisible(row(OrderStatus.Pending))).toBe(false);
  });

  // Started is NOT over. NotifyOnTheWay writes the status on the ORDER, so one cleaner setting off
  // used to take a half-crewed job off every board with its other seats empty. Owner ruling
  // 2026-09-06 made a started job stay fillable. → OrderAvailability.OfferableStatuses
  it.each([OrderStatus.OnTheWay, OrderStatus.InProgress])(
    'still offers a started order with a free seat, status %s',
    (status) => {
      expect(isVisible(row(status))).toBe(true);
    }
  );

  it.each([OrderStatus.Completed, OrderStatus.Cancelled])(
    'does not offer the terminal status %s',
    (status) => {
      expect(isVisible(row(status))).toBe(false);
    }
  );

  it('does not offer an order whose seats are all taken', () => {
    expect(isVisible(row(OrderStatus.New, 0))).toBe(false);
  });

  it('disables the row that is already being taken', () => {
    const { actions } = getAvailableOrdersTableDefinition(
      { onTakeOrder: jest.fn(), isTakeInFlight: (item) => item.id === 'ord-1' },
      'cs'
    );

    expect(actions[0].disabled?.(row(OrderStatus.New))).toBe(true);
  });
});

const NBSP = String.fromCharCode(0xa0);

function bothTables(lang: string | undefined) {
  return [
    getAvailableOrdersTableDefinition({ onTakeOrder: jest.fn(), isTakeInFlight: () => false }, lang),
    getMyOrdersTableDefinition({ onStartOrder: jest.fn(), onCompleteOrder: jest.fn() }, lang),
  ];
}

function cellValues(lang: string | undefined, columnId: string, order: OrderListItem): unknown[] {
  return bothTables(lang).map((def) => def.columns.find((c) => c.id === columnId)?.getValue?.(order));
}

describe('the total price column names the order currency in the language of the session', () => {
  it('labels the total with the code the order carries', () => {
    const order = OrderListItem.fromJS({ totalPrice: 1200, currency: { code: 'EUR' } });
    expect(cellValues('cs', 'totalPrice', order)).toEqual([`1${NBSP}200,00${NBSP}€`, `1${NBSP}200,00${NBSP}€`]);
    expect(cellValues('en', 'totalPrice', order)).toEqual(['€1,200.00', '€1,200.00']);
  });

  // The server always sends the order's currency; a bare number is honest when it does not, and
  // crowns would mislabel every non-crown order.
  it('prints a bare number rather than a currency the order does not name', () => {
    const order = OrderListItem.fromJS({ totalPrice: 1200 });
    expect(cellValues('cs', 'totalPrice', order)).toEqual([`1${NBSP}200,00`, `1${NBSP}200,00`]);
  });
});

describe('the cleaning date column is the session-language stamp the order detail prints', () => {
  const order = OrderListItem.fromJS({ cleaningDateTime: '2026-09-01T11:00:00' });

  it('prints the day and the minute, not the en-GB slashes', () => {
    expect(cellValues('cs', 'cleaningDateTime', order)).toEqual(['1. 9. 2026 11:00', '1. 9. 2026 11:00']);
    expect(cellValues('en', 'cleaningDateTime', order)).toEqual(['Sep 1, 2026, 11:00 AM', 'Sep 1, 2026, 11:00 AM']);
  });

  it('prints nothing for an order with no date', () => {
    expect(cellValues('cs', 'cleaningDateTime', OrderListItem.fromJS({}))).toEqual(['', '']);
  });
});

describe('the help legend describes only statuses an order reaches', () => {
  it('leaves out Pending, which no order is ever written to', () => {
    const described = ORDER_STATUS_FLOW.map((item) => item.statusKey);

    expect(described).not.toContain('enums.order_status.pending');
    expect(described).toContain('enums.order_status.confirmed');
  });

  it('names and describes every row it lists, in all five partner locales', () => {
    for (const locale of PARTNER_LOCALES) {
      const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as unknown;
      const missing = ORDER_STATUS_FLOW.flatMap((item) => [item.statusKey, item.descriptionKey]).filter(
        (key) => {
          const value = resolveKey(bundle, key);
          return typeof value !== 'string' || !value.trim();
        }
      );

      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });
});
