import { OrderItem, OrderStatus, PreferredOfferState } from '@cleansia/customer-services';
import { resolvePreferredOfferView } from './order-preferred-offer.models';

function buildOrder(fields: {
  orderStatus?: OrderStatus;
  preferredOffer?: {
    state: PreferredOfferState;
    cleanerName?: string;
    respondByUtc?: string;
    canChooseAnother: boolean;
  } | null;
}): OrderItem {
  return OrderItem.fromJS({
    id: 'ord-1',
    orderStatus: { value: fields.orderStatus ?? OrderStatus.New, name: 'New' },
    preferredOffer: fields.preferredOffer ?? undefined,
  });
}

describe('resolvePreferredOfferView', () => {
  it('reads no offer at all off an absent order', () => {
    const view = resolvePreferredOfferView(null);

    expect(view.state).toBe(PreferredOfferState.None);
    expect(view.canChooseAnother).toBe(false);
  });

  // A block the server did not send must render as "no offer" — never as a dropped order row.
  it('reads no offer off an order whose block the server did not send', () => {
    const view = resolvePreferredOfferView(buildOrder({ preferredOffer: null }));

    expect(view.state).toBe(PreferredOfferState.None);
    expect(view.cleanerName).toBe('');
    expect(view.respondByUtc).toBeNull();
    expect(view.canChooseAnother).toBe(false);
  });

  it('carries the running reservation through unchanged', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.AwaitingConfirmation,
          cleanerName: 'Anna Nováková',
          respondByUtc: '2026-09-01T18:00:00Z',
          canChooseAnother: false,
        },
      })
    );

    expect(view.state).toBe(PreferredOfferState.AwaitingConfirmation);
    expect(view.cleanerName).toBe('Anna Nováková');
    expect(view.respondByUtc).toEqual(new Date('2026-09-01T18:00:00Z'));
    expect(view.canChooseAnother).toBe(false);
  });

  it('offers the exit exactly where the server says it is open', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.Closed,
          cleanerName: 'Anna Nováková',
          canChooseAnother: true,
        },
      })
    );

    expect(view.canChooseAnother).toBe(true);
  });

  // PreferredOfferExit.IsOpen carries no offerability term, so a cancelled order with a future
  // cleaning time and no cleaner on it answers `true` — the server would take the request, grant a
  // reservation and push a named cleaner about work nobody will do. The button is withheld here.
  it('withholds the exit on a cancelled order the server would still accept', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        orderStatus: OrderStatus.Cancelled,
        preferredOffer: {
          state: PreferredOfferState.Closed,
          canChooseAnother: true,
          cleanerName: 'Anna Nováková',
        },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });

  it('withholds the exit on a completed order', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        orderStatus: OrderStatus.Completed,
        preferredOffer: {
          state: PreferredOfferState.Accepted,
          canChooseAnother: true,
          cleanerName: 'Anna Nováková',
        },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });

  it('keeps the exit on every live fulfilment state the server left open', () => {
    for (const status of [
      OrderStatus.New,
      OrderStatus.Pending,
      OrderStatus.Confirmed,
      OrderStatus.OnTheWay,
      OrderStatus.InProgress,
    ]) {
      const view = resolvePreferredOfferView(
        buildOrder({
          orderStatus: status,
          preferredOffer: {
            state: PreferredOfferState.Closed,
            canChooseAnother: true,
          },
        })
      );

      expect(view.canChooseAnother).toBe(true);
    }
  });

  it('never opens the exit the server closed', () => {
    const view = resolvePreferredOfferView(
      buildOrder({
        preferredOffer: {
          state: PreferredOfferState.AwaitingConfirmation,
          canChooseAnother: false,
        },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });
});
