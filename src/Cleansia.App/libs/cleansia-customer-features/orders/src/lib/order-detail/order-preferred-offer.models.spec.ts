import {
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  PreferredOfferState,
} from '@cleansia/customer-services';
import { resolvePreferredOfferView } from './order-preferred-offer.models';

function buildOrder(fields: {
  orderStatus?: OrderStatus;
  paymentType?: PaymentType;
  paymentStatus?: PaymentStatus;
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
    paymentType: { value: fields.paymentType ?? PaymentType.Cash, name: 'Cash' },
    paymentStatus: {
      value: fields.paymentStatus ?? PaymentStatus.Paid,
      name: 'Paid',
    },
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

  it('reads an absent flag as closed rather than as open', () => {
    const view = resolvePreferredOfferView(
      OrderItem.fromJS({
        id: 'ord-1',
        orderStatus: { value: OrderStatus.New, name: 'New' },
        preferredOffer: { state: PreferredOfferState.Closed },
      })
    );

    expect(view.canChooseAnother).toBe(false);
  });

  /**
   * `PreferredOfferExit.IsOpen` conjoins `OrderAvailability.IsOfferable`, so these arrive already
   * closed. Each is asserted through the flag, never through a status this file reads: a rule
   * restated here would be a second, weaker implementation of the server's gate.
   */
  describe('the closures the server now sends', () => {
    it('is closed on a cancelled order because the server closed it', () => {
      const view = resolvePreferredOfferView(
        buildOrder({
          orderStatus: OrderStatus.Cancelled,
          preferredOffer: {
            state: PreferredOfferState.Closed,
            cleanerName: 'Anna Nováková',
            canChooseAnother: false,
          },
        })
      );

      expect(view.canChooseAnother).toBe(false);
    });

    it('is closed on a completed order because the server closed it', () => {
      const view = resolvePreferredOfferView(
        buildOrder({
          orderStatus: OrderStatus.Completed,
          preferredOffer: {
            state: PreferredOfferState.Accepted,
            cleanerName: 'Anna Nováková',
            canChooseAnother: false,
          },
        })
      );

      expect(view.canChooseAnother).toBe(false);
    });

    // The money axis, which no status set can express: a card booking whose payment has not landed
    // is refused by IsOfferable while its fulfilment state is plain `New`.
    it('is closed on an unpaid card order that is in no closed fulfilment state', () => {
      const order = buildOrder({
        orderStatus: OrderStatus.New,
        paymentType: PaymentType.Card,
        paymentStatus: PaymentStatus.Pending,
        preferredOffer: {
          state: PreferredOfferState.Closed,
          cleanerName: 'Anna Nováková',
          canChooseAnother: false,
        },
      });

      expect(order.orderStatus.value).not.toBe(OrderStatus.Cancelled);
      expect(order.orderStatus.value).not.toBe(OrderStatus.Completed);
      expect(resolvePreferredOfferView(order).canChooseAnother).toBe(false);
    });
  });

  /**
   * The mutation guard for the deleted narrowing: the flag is the whole answer on EVERY fulfilment
   * state. Re-introducing a status veto here reddens the Cancelled and Completed rows.
   */
  it.each([
    OrderStatus.New,
    OrderStatus.Pending,
    OrderStatus.Confirmed,
    OrderStatus.OnTheWay,
    OrderStatus.InProgress,
    OrderStatus.Completed,
    OrderStatus.Cancelled,
  ])('re-derives no rule of its own on status %i', (status) => {
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
  });
});
