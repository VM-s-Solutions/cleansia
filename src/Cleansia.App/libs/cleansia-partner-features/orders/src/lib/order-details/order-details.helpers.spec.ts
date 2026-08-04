import { AssignedEmployeeDto, OrderStatus } from '@cleansia/partner-services';
import { canTakeOrder } from './order-details.helpers';

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

  it.each([
    OrderStatus.OnTheWay,
    OrderStatus.InProgress,
    OrderStatus.Completed,
    OrderStatus.Cancelled,
  ])('does not show Take for status %s', (status) => {
    expect(canTakeOrder(status, [], EMPLOYEE_ID)).toBe(false);
  });

  it('does not show Take to a cleaner already assigned to the order', () => {
    expect(canTakeOrder(OrderStatus.New, assigned(EMPLOYEE_ID), EMPLOYEE_ID)).toBe(
      false
    );
  });

  it('still shows Take when somebody else holds a seat', () => {
    expect(canTakeOrder(OrderStatus.New, assigned('emp-2'), EMPLOYEE_ID)).toBe(true);
  });
});
