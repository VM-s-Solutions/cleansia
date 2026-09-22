import { OrderItem } from '@cleansia/customer-services';
import { buildWorkContractAcceptanceLines } from './order-work-contract.models';

function order(overrides: Record<string, unknown>): OrderItem {
  return OrderItem.fromJS({
    id: 'ord-1',
    displayOrderNumber: 'ORD-1',
    assignedEmployees: [
      { id: 'seat-a', employeeId: 'emp-a', fullName: 'Petra' },
      { id: 'seat-b', employeeId: 'emp-b', fullName: 'Jana' },
    ],
    workContractAcceptances: [],
    ...overrides,
  });
}

describe('buildWorkContractAcceptanceLines', () => {
  it('reads one line per acceptance, named from the crew entry that holds the seat', () => {
    const lines = buildWorkContractAcceptanceLines(
      order({
        workContractAcceptances: [
          { id: 'acc-b', orderEmployeeId: 'seat-b', employeeId: 'emp-b', acceptedOn: '2026-09-21T10:15:00Z', documentVersion: '2026-09-20', language: 'cs' },
          { id: 'acc-a', orderEmployeeId: 'seat-a', employeeId: 'emp-a', acceptedOn: '2026-09-21T09:00:00Z', documentVersion: '2026-09-20', language: 'uk' },
        ],
      }),
    );

    expect(lines).toEqual([
      { id: 'acc-b', cleanerName: 'Jana', acceptedOn: new Date('2026-09-21T10:15:00Z'), documentVersion: '2026-09-20' },
      { id: 'acc-a', cleanerName: 'Petra', acceptedOn: new Date('2026-09-21T09:00:00Z'), documentVersion: '2026-09-20' },
    ]);
  });

  it('reads no line, and no placeholder, when nobody has accepted', () => {
    expect(buildWorkContractAcceptanceLines(order({}))).toEqual([]);
    expect(buildWorkContractAcceptanceLines(order({ workContractAcceptances: undefined }))).toEqual([]);
    expect(buildWorkContractAcceptanceLines(null)).toEqual([]);
  });

  // The server lists an acceptance only for a current seat, so every one has a crew entry; a row
  // that names no seat on the crew has nobody to be attributed to and is not a line.
  it('drops an acceptance whose seat is not on the crew', () => {
    const lines = buildWorkContractAcceptanceLines(
      order({
        workContractAcceptances: [
          { id: 'acc-x', orderEmployeeId: 'seat-gone', employeeId: 'emp-x', acceptedOn: '2026-09-21T10:15:00Z', documentVersion: '2026-09-20', language: 'cs' },
        ],
      }),
    );

    expect(lines).toEqual([]);
  });
});
