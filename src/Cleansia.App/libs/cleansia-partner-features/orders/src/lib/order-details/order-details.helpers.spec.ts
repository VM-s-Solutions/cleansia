import {
  AssignedEmployeeDto,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  WorkContractAcceptanceDto,
} from '@cleansia/partner-services';
import {
  canAcceptWorkContract,
  canMarkCashCollected,
  canTakeOrder,
  cashCollectionRefusal,
  cashRefusedOnCardOrder,
  findCallerWorkContractAcceptance,
} from './order-details.helpers';

const EMPLOYEE_ID = 'emp-1';

const assigned = (employeeId: string): AssignedEmployeeDto[] => [
  AssignedEmployeeDto.fromJS({ employeeId }),
];

describe('canTakeOrder', () => {
  it('shows Take on a New order — a cash job stays New until the take confirms it', () => {
    expect(canTakeOrder(OrderStatus.New, [], EMPLOYEE_ID)).toBe(true);
  });

  it('shows Take on a Confirmed order that still has room', () => {
    expect(canTakeOrder(OrderStatus.Confirmed, [], EMPLOYEE_ID)).toBe(true);
  });

  it('does not show Take for the dead Pending status', () => {
    expect(canTakeOrder(OrderStatus.Pending, [], EMPLOYEE_ID)).toBe(false);
  });

  // Started is NOT over. NotifyOnTheWay writes the status on the ORDER, so one cleaner setting off
  // used to take a half-crewed job off every board with its other seats empty. Owner ruling
  // 2026-09-06 made a started job stay fillable. → OrderAvailability.OfferableStatuses
  it.each([OrderStatus.OnTheWay, OrderStatus.InProgress])(
    'still shows Take on a started order with room, status %s',
    (status) => {
      expect(canTakeOrder(status, [], EMPLOYEE_ID)).toBe(true);
    }
  );

  it.each([OrderStatus.Completed, OrderStatus.Cancelled])(
    'does not show Take for the terminal status %s',
    (status) => {
      expect(canTakeOrder(status, [], EMPLOYEE_ID)).toBe(false);
    }
  );

  it('does not show Take to a cleaner already assigned to the order', () => {
    expect(canTakeOrder(OrderStatus.New, assigned(EMPLOYEE_ID), EMPLOYEE_ID)).toBe(
      false
    );
  });

  it('still shows Take when somebody else holds a seat', () => {
    expect(canTakeOrder(OrderStatus.New, assigned('emp-2'), EMPLOYEE_ID)).toBe(true);
  });
});

describe('the contract for work on the job detail', () => {
  const seat = (id: string, employeeId: string): AssignedEmployeeDto =>
    AssignedEmployeeDto.fromJS({ id, employeeId, fullName: 'Jan Novak' });
  const acceptance = (orderEmployeeId: string, id = `acc-${orderEmployeeId}`): WorkContractAcceptanceDto =>
    WorkContractAcceptanceDto.fromJS({
      id,
      orderEmployeeId,
      employeeId: 'masked',
      acceptedOn: '2026-09-21T10:00:00Z',
      documentVersion: '2026-09-20',
      language: 'cs',
    });

  describe('findCallerWorkContractAcceptance', () => {
    it("returns the row of the caller's own seat", () => {
      const crew = [seat('seat-1', 'emp-2'), seat('seat-2', EMPLOYEE_ID)];
      const rows = [acceptance('seat-1'), acceptance('seat-2')];

      expect(findCallerWorkContractAcceptance(crew, rows, EMPLOYEE_ID)?.id).toBe('acc-seat-2');
    });

    it('returns nothing while the caller is on the crew without a row — the admin placement', () => {
      const crew = [seat('seat-1', 'emp-2'), seat('seat-2', EMPLOYEE_ID)];

      expect(findCallerWorkContractAcceptance(crew, [acceptance('seat-1')], EMPLOYEE_ID)).toBeNull();
    });

    it('returns nothing for a caller who is not on the crew', () => {
      expect(
        findCallerWorkContractAcceptance([seat('seat-1', 'emp-2')], [acceptance('seat-1')], EMPLOYEE_ID)
      ).toBeNull();
    });

    it('returns nothing when the order carries no rows', () => {
      expect(findCallerWorkContractAcceptance([seat('seat-1', EMPLOYEE_ID)], undefined, EMPLOYEE_ID)).toBeNull();
    });
  });

  describe('canAcceptWorkContract', () => {
    const crew = [seat('seat-1', EMPLOYEE_ID)];

    it.each([OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay, OrderStatus.InProgress])(
      'offers the acceptance to a placed cleaner without a row on a live order, status %s',
      (status) => {
        expect(canAcceptWorkContract(status, crew, [], EMPLOYEE_ID)).toBe(true);
      }
    );

    it.each([OrderStatus.Completed, OrderStatus.Cancelled])(
      'offers nothing on an order that is over, status %s',
      (status) => {
        expect(canAcceptWorkContract(status, crew, [], EMPLOYEE_ID)).toBe(false);
      }
    );

    it('offers nothing once the seat has its row', () => {
      expect(canAcceptWorkContract(OrderStatus.Confirmed, crew, [acceptance('seat-1')], EMPLOYEE_ID)).toBe(false);
    });

    it('offers nothing to a cleaner who is not on the crew', () => {
      expect(canAcceptWorkContract(OrderStatus.Confirmed, crew, [], 'emp-2')).toBe(false);
    });
  });
});

// Owner ruling 2026-09-24: cash only for a signed-in customer whose booking needs one cleaner. A
// card order the booking could not have paid in cash is refused cash at the door as well. The crew
// size is on the job sheet; whether the customer was a guest is not, so the server alone refuses that half.
describe('collecting cash', () => {
  interface JobSheet {
    orderStatus?: number;
    paymentStatus?: number;
    paymentType?: number;
    requiredEmployees?: number;
    creditAppliedAmount?: number;
    employeeId?: string;
  }

  const jobSheet = (overrides: JobSheet): OrderItem =>
    OrderItem.fromJS({
      orderStatus: { value: overrides.orderStatus ?? OrderStatus.InProgress },
      paymentStatus: { value: overrides.paymentStatus ?? PaymentStatus.Pending },
      paymentType: { value: overrides.paymentType ?? PaymentType.Cash },
      requiredEmployees: 'requiredEmployees' in overrides ? overrides.requiredEmployees : 1,
      creditAppliedAmount: overrides.creditAppliedAmount ?? 0,
      assignedEmployees: [{ employeeId: overrides.employeeId ?? EMPLOYEE_ID }],
    });

  const collectable = (overrides: JobSheet): boolean =>
    canMarkCashCollected(jobSheet(overrides), EMPLOYEE_ID);

  it('offers it on a card order one cleaner does — the server repairs or refuses it', () => {
    expect(collectable({ paymentType: PaymentType.Card, requiredEmployees: 1 })).toBe(true);
  });

  it('does not offer it on a card order that needs more than one cleaner', () => {
    expect(collectable({ paymentType: PaymentType.Card, requiredEmployees: 2 })).toBe(false);
  });

  it.each([1, 2, 3])('offers it on an order booked as cash whatever the crew, %s cleaners', (crew) => {
    expect(collectable({ paymentType: PaymentType.Cash, requiredEmployees: crew })).toBe(true);
  });

  it('offers it when the job sheet names no crew — the server decides', () => {
    expect(collectable({ paymentType: PaymentType.Card, requiredEmployees: undefined })).toBe(true);
  });

  it('does not offer it on an order already paid', () => {
    expect(collectable({ paymentStatus: PaymentStatus.Paid })).toBe(false);
  });

  it.each([PaymentStatus.Pending, PaymentStatus.Failed])(
    'offers it on an order still awaiting payment, status %s',
    (status) => {
      expect(collectable({ paymentType: PaymentType.Card, paymentStatus: status })).toBe(true);
    }
  );

  // Taking the cash would write Paid over the refund or the dispute.
  it.each([PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded, PaymentStatus.Disputed])(
    'does not offer it on a refunded or disputed order, status %s',
    (status) => {
      expect(collectable({ paymentType: PaymentType.Card, paymentStatus: status })).toBe(false);
      expect(collectable({ paymentType: PaymentType.Cash, paymentStatus: status })).toBe(false);
    }
  );

  it.each([PaymentType.Cash, PaymentType.Card])(
    'does not offer it on an order partly paid from credit, payment type %s',
    (paymentType) => {
      expect(collectable({ paymentType, creditAppliedAmount: 150 })).toBe(false);
    }
  );

  it.each([OrderStatus.Confirmed, OrderStatus.OnTheWay, OrderStatus.Completed])(
    'does not offer it outside the work itself, status %s',
    (status) => {
      expect(collectable({ orderStatus: status })).toBe(false);
    }
  );

  it('does not offer it to a cleaner who is not on the crew', () => {
    expect(collectable({ employeeId: 'emp-2' })).toBe(false);
  });

  it('names a card order that needs a crew as the reason, and nothing else', () => {
    expect(cashRefusedOnCardOrder(PaymentType.Card, 2)).toBe(true);
    expect(cashRefusedOnCardOrder(PaymentType.Card, 1)).toBe(false);
    expect(cashRefusedOnCardOrder(PaymentType.Cash, 2)).toBe(false);
    expect(cashRefusedOnCardOrder(PaymentType.Card, undefined)).toBe(false);
  });

  it('gives the sentence for each refusal the job sheet shows, the credit first as the server does', () => {
    expect(cashCollectionRefusal(jobSheet({ creditAppliedAmount: 150 }))).toBe(
      'api.credit.cash_not_collectable_on_credit_order'
    );
    expect(
      cashCollectionRefusal(jobSheet({ paymentType: PaymentType.Card, requiredEmployees: 2 }))
    ).toBe('api.order.cash_not_allowed_on_card_order');
    expect(
      cashCollectionRefusal(
        jobSheet({ paymentType: PaymentType.Card, requiredEmployees: 2, creditAppliedAmount: 150 })
      )
    ).toBe('api.credit.cash_not_collectable_on_credit_order');
    expect(cashCollectionRefusal(jobSheet({ paymentType: PaymentType.Card }))).toBeNull();
  });
});
