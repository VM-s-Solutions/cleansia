import { OrderListItem, OrderStatus } from '@cleansia/partner-services';
import { getAvailableOrdersTableDefinition } from './orders.models';

function row(status: OrderStatus, availableSpots = 1): OrderListItem {
  return OrderListItem.fromJS({
    id: 'ord-1',
    orderStatus: { value: status },
    availableSpots,
  });
}

function takeAction() {
  const { actions } = getAvailableOrdersTableDefinition({
    onTakeOrder: jest.fn(),
    isTakeInFlight: () => false,
  });
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
    const { actions } = getAvailableOrdersTableDefinition({
      onTakeOrder: jest.fn(),
      isTakeInFlight: (item) => item.id === 'ord-1',
    });

    expect(actions[0].disabled?.(row(OrderStatus.New))).toBe(true);
  });
});
