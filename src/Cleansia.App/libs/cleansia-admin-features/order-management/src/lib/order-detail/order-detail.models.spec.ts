import { TimelineEntryDto, TimelineSource } from '@cleansia/admin-services';
import { resolveIncidentSubject } from './order-detail.models';

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
