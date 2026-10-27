import { OrderItem } from '@cleansia/customer-services';
import { OrderStatus } from '@cleansia/models';
import { statusReachedAt } from './order-status-history.models';

const at = (minute: number): Date => new Date(Date.UTC(2026, 9, 25, 8, minute));

function history(...rows: [OrderStatus, number][]) {
  return (
    OrderItem.fromJS({
      statusHistory: rows.map(([status, minute]) => ({
        status: { value: status, name: OrderStatus[status] },
        createdOn: at(minute).toISOString(),
      })),
    }).statusHistory ?? []
  );
}

describe('statusReachedAt', () => {
  it('reads each status off its row on a booking that never went back to New', () => {
    const rows = history([OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.OnTheWay, 2]);

    expect(statusReachedAt(rows, OrderStatus.New)).toEqual(at(0));
    expect(statusReachedAt(rows, OrderStatus.Confirmed)).toEqual(at(1));
    expect(statusReachedAt(rows, OrderStatus.OnTheWay)).toEqual(at(2));
    expect(statusReachedAt(rows, OrderStatus.InProgress)).toBeUndefined();
  });

  it('has not reached Confirmed again after the last cleaner left and the booking went back to New', () => {
    const rows = history([OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2]);

    expect(statusReachedAt(rows, OrderStatus.Confirmed)).toBeUndefined();
  });

  it('dates Confirmed by the take that followed the walk-back', () => {
    const rows = history([OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2], [OrderStatus.Confirmed, 3]);

    expect(statusReachedAt(rows, OrderStatus.Confirmed)).toEqual(at(3));
  });

  it('reads only the pass after the latest walk-back, however many there were', () => {
    const twice = history(
      [OrderStatus.New, 0],
      [OrderStatus.Confirmed, 1],
      [OrderStatus.New, 2],
      [OrderStatus.Confirmed, 3],
      [OrderStatus.New, 4],
    );

    expect(statusReachedAt(twice, OrderStatus.Confirmed)).toBeUndefined();
    expect(statusReachedAt([...twice, ...history([OrderStatus.Confirmed, 5])], OrderStatus.Confirmed)).toEqual(at(5));
  });

  it('keeps the booking time for New after a walk-back', () => {
    const rows = history([OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2]);

    expect(statusReachedAt(rows, OrderStatus.New)).toEqual(at(0));
  });

  it('does not tick Confirmed on a walked-back booking the sweep later cancelled', () => {
    const rows = history([OrderStatus.New, 0], [OrderStatus.Confirmed, 1], [OrderStatus.New, 2], [OrderStatus.Cancelled, 3]);

    expect(statusReachedAt(rows, OrderStatus.Confirmed)).toBeUndefined();
    expect(statusReachedAt(rows, OrderStatus.Cancelled)).toEqual(at(3));
  });

  it('orders the rows by time, since the server sends them in no guaranteed order', () => {
    const rows = history([OrderStatus.Confirmed, 3], [OrderStatus.New, 2], [OrderStatus.Confirmed, 1], [OrderStatus.New, 0]);

    expect(statusReachedAt(rows, OrderStatus.Confirmed)).toEqual(at(3));
    expect(statusReachedAt(rows, OrderStatus.New)).toEqual(at(0));
  });

  it('reads every row as the pass when the history has no New row', () => {
    expect(statusReachedAt(history([OrderStatus.Confirmed, 1]), OrderStatus.Confirmed)).toEqual(at(1));
  });

  it('has reached nothing without a history', () => {
    expect(statusReachedAt(undefined, OrderStatus.New)).toBeUndefined();
    expect(statusReachedAt([], OrderStatus.Confirmed)).toBeUndefined();
  });
});
