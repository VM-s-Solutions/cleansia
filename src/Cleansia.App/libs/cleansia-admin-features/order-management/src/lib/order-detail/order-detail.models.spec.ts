import { readFileSync } from 'fs';
import { join } from 'path';
import { DirtinessLevel, OrderItem, OrderStatus, TimelineEntryDto, TimelineSource } from '@cleansia/admin-services';
import { buildCrewEntries, dirtinessLevelLabelKey, resolveIncidentSubject } from './order-detail.models';

const ADMIN_LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia-admin.app/src/assets/i18n');

function entry(
  source: TimelineSource,
  actorId: string | undefined,
  success: boolean
): TimelineEntryDto {
  return TimelineEntryDto.fromJS({
    source,
    id: `${source}-${actorId ?? 'guest'}-${success}`,
    occurredOn: '2026-09-14T10:00:00Z',
    actorId,
    action: 'x',
    resourceType: 'Order',
    resourceId: 'order-1',
    success,
  });
}

describe('resolveIncidentSubject', () => {
  // A successful customer act on an order is the owner's — every other customer act on it is refused.
  // Admin and cleaner rows name their own actors, and a stranger's refused probe names the stranger.
  it('picks the customer whose own act on the order succeeded, past admin, cleaner and refused rows', () => {
    const entries = [
      entry(TimelineSource.Admin, 'admin-1', true),
      entry(TimelineSource.Customer, 'stranger-1', false),
      entry(TimelineSource.Employee, 'emp-1', true),
      entry(TimelineSource.Customer, 'cust-1', true),
      entry(TimelineSource.Customer, 'cust-1', false),
    ];

    expect(resolveIncidentSubject(entries)).toBe('cust-1');
  });

  it('names nobody for a guest booking, whose rows carry no actor', () => {
    const entries = [
      entry(TimelineSource.Customer, undefined, true),
      entry(TimelineSource.Employee, 'emp-1', true),
    ];

    expect(resolveIncidentSubject(entries)).toBeNull();
  });

  it('names nobody when the trail is empty', () => {
    expect(resolveIncidentSubject([])).toBeNull();
  });
});

// The acceptance carries no name; the crew entry it pairs with (by seat id) is where the name is.
// A seat with no row IS the pending state — the only writer of such a seat is an admin placement.
describe('buildCrewEntries', () => {
  it('pairs each seat with its own acceptance and leaves the placed seat pending', () => {
    const order = OrderItem.fromJS({
      assignedEmployees: [
        { id: 'seat-1', employeeId: 'emp-1', fullName: 'Alice' },
        { id: 'seat-2', employeeId: 'emp-2', fullName: 'Bob' },
      ],
      workContractAcceptances: [
        {
          id: 'acc-1',
          orderEmployeeId: 'seat-1',
          employeeId: 'emp-1',
          acceptedOn: '2026-09-21T10:00:00Z',
          documentVersion: '2026-09-20',
          language: 'cs',
        },
      ],
    });

    const crew = buildCrewEntries(order);

    expect(crew.map((entry) => entry.employee.fullName)).toEqual(['Alice', 'Bob']);
    expect(crew[0].acceptance?.id).toBe('acc-1');
    expect(crew[1].acceptance).toBeNull();
  });

  // The row of a re-taken order names the old seat; a cleaner an admin re-added sits on a new one.
  it('pairs by the seat, not by the cleaner', () => {
    const order = OrderItem.fromJS({
      assignedEmployees: [{ id: 'seat-2', employeeId: 'emp-1', fullName: 'Alice' }],
      workContractAcceptances: [
        { id: 'acc-old', orderEmployeeId: 'seat-1', employeeId: 'emp-1', acceptedOn: '2026-09-21T10:00:00Z' },
      ],
    });

    expect(buildCrewEntries(order)[0].acceptance).toBeNull();
  });

  it('lists nobody without an order or a crew', () => {
    expect(buildCrewEntries(null)).toEqual([]);
    expect(buildCrewEntries(OrderItem.fromJS({ workContractAcceptances: [{ id: 'acc-1' }] }))).toEqual([]);
  });

  // Owner ruling 2026-10-26: on a crew job each cleaner marks their own part done before any of them
  // completes it. The card says when a seat marked, and "not yet" only where a mark is still to come.
  describe('when each seat marked its part done', () => {
    const MARKED = '2026-10-27T10:40:00Z';
    const order = (status: OrderStatus, seats: { employeeId: string; completionConfirmedAt?: string }[]) =>
      OrderItem.fromJS({
        orderStatus: { value: status },
        assignedEmployees: seats.map((seat) => ({ id: `seat-${seat.employeeId}`, fullName: seat.employeeId, ...seat })),
      });

    it('gives a marked seat its time and an unmarked one of a crew job in progress not yet', () => {
      const crew = buildCrewEntries(
        order(OrderStatus.InProgress, [{ employeeId: 'emp-1', completionConfirmedAt: MARKED }, { employeeId: 'emp-2' }])
      );

      expect(crew[0].markedDoneAt?.toISOString()).toBe(new Date(MARKED).toISOString());
      expect(crew[0].markPending).toBe(false);
      expect(crew[1].markedDoneAt).toBeNull();
      expect(crew[1].markPending).toBe(true);
    });

    it('expects no mark from the one cleaner of a job, who completes it outright', () => {
      const [solo] = buildCrewEntries(order(OrderStatus.InProgress, [{ employeeId: 'emp-1' }]));

      expect(solo.markedDoneAt).toBeNull();
      expect(solo.markPending).toBe(false);
    });

    it.each([OrderStatus.Confirmed, OrderStatus.Completed, OrderStatus.Cancelled])(
      'expects no mark on a crew job in status %s, and still shows one that was made',
      (status) => {
        const crew = buildCrewEntries(
          order(status, [{ employeeId: 'emp-1', completionConfirmedAt: MARKED }, { employeeId: 'emp-2' }])
        );

        expect(crew.map((entry) => entry.markPending)).toEqual([false, false]);
        expect(crew[0].markedDoneAt?.toISOString()).toBe(new Date(MARKED).toISOString());
      }
    );
  });
});

describe('dirtinessLevelLabelKey', () => {
  it('names each level the customer can book', () => {
    expect(dirtinessLevelLabelKey(DirtinessLevel.Normal)).toBe('enums.dirtiness_level.normal');
    expect(dirtinessLevelLabelKey(DirtinessLevel.Increased)).toBe('enums.dirtiness_level.increased');
    expect(dirtinessLevelLabelKey(DirtinessLevel.Heavy)).toBe('enums.dirtiness_level.heavy');
  });

  // The server stores Normal for a client that sent no level, so an absent one reads the same way.
  it('reads an absent level as normal', () => {
    expect(dirtinessLevelLabelKey(undefined)).toBe('enums.dirtiness_level.normal');
  });

  it.each(ADMIN_LOCALES)('has every level, both order detail labels and the crew card mark in %s', (locale) => {
    const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as unknown;
    const keys = [
      ...[DirtinessLevel.Normal, DirtinessLevel.Increased, DirtinessLevel.Heavy].map(dirtinessLevelLabelKey),
      'pages.order_detail.dirtiness_level',
      'pages.order_detail.dirtiness_surcharge',
      'pages.order_detail.completion.label',
      'pages.order_detail.completion.pending',
    ];
    const missing = keys.filter((key) => {
      const value = key
        .split('.')
        .reduce<unknown>(
          (node, segment) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined),
          bundle
        );
      return typeof value !== 'string' || !value.trim();
    });

    expect(missing).toEqual([]);
  });
});
