import {
  AssignedEmployeeDto,
  OrderStatus,
  WorkContractAcceptanceDto,
} from '@cleansia/partner-services';
import {
  canAcceptWorkContract,
  canTakeOrder,
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
