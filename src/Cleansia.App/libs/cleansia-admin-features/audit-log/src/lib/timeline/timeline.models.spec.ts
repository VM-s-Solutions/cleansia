import { TimelineEntryDto, TimelineSource } from '@cleansia/admin-services';
import { CleansiaAdminRoute } from '@cleansia/services';
import {
  buildTimelineActorRoute,
  buildTimelineEntryRoute,
  getTimelineSourceClass,
  getTimelineSourceLabelKey,
  hasTimelineEntryDetail,
} from './timeline.models';

function entry(source: TimelineSource, id = 'row-1'): TimelineEntryDto {
  return TimelineEntryDto.fromJS({ source, id, occurredOn: '2026-09-13T10:00:00Z', success: true });
}

describe('timeline models', () => {
  it('routes a customer row to the customer entry and an admin row to the admin entry', () => {
    expect(buildTimelineEntryRoute(entry(TimelineSource.Customer))).toEqual([
      CleansiaAdminRoute.AUDIT_LOG,
      'customers',
      'entry',
      'row-1',
    ]);
    expect(buildTimelineEntryRoute(entry(TimelineSource.Admin))).toEqual([
      CleansiaAdminRoute.AUDIT_LOG,
      'entry',
      'row-1',
    ]);
  });

  it("links a customer row's actor to the customer page and no one else's", () => {
    expect(
      buildTimelineActorRoute(TimelineEntryDto.fromJS({ source: TimelineSource.Customer, actorId: 'u-1', id: 'row-1' }))
    ).toEqual(['/customers', 'u-1']);
    expect(
      buildTimelineActorRoute(TimelineEntryDto.fromJS({ source: TimelineSource.Customer, actorId: undefined, id: 'row-1' }))
    ).toBeNull();
    expect(
      buildTimelineActorRoute(TimelineEntryDto.fromJS({ source: TimelineSource.Admin, actorId: 'a-1', id: 'row-1' }))
    ).toBeNull();
    expect(
      buildTimelineActorRoute(TimelineEntryDto.fromJS({ source: TimelineSource.Employee, actorId: 'e-1', id: 'row-1' }))
    ).toBeNull();
  });

  it('gives an employee row no detail route — the badge is all there is', () => {
    expect(buildTimelineEntryRoute(entry(TimelineSource.Employee))).toBeNull();
    expect(hasTimelineEntryDetail(entry(TimelineSource.Employee))).toBe(false);
    expect(hasTimelineEntryDetail(entry(TimelineSource.Customer))).toBe(true);
    expect(hasTimelineEntryDetail(entry(TimelineSource.Admin))).toBe(true);
  });

  it('gives a row without an id no route regardless of source', () => {
    const noId = TimelineEntryDto.fromJS({ source: TimelineSource.Customer, success: true });
    expect(buildTimelineEntryRoute(noId)).toBeNull();
    expect(hasTimelineEntryDetail(noId)).toBe(false);
  });

  it('maps each source to its own label key and badge class', () => {
    expect(getTimelineSourceLabelKey(TimelineSource.Customer)).toBe(
      'pages.audit_log.timeline.source.customer'
    );
    expect(getTimelineSourceLabelKey(TimelineSource.Admin)).toBe(
      'pages.audit_log.timeline.source.admin'
    );
    expect(getTimelineSourceLabelKey(TimelineSource.Employee)).toBe(
      'pages.audit_log.timeline.source.employee'
    );
    expect(
      new Set([
        getTimelineSourceClass(TimelineSource.Customer),
        getTimelineSourceClass(TimelineSource.Admin),
        getTimelineSourceClass(TimelineSource.Employee),
      ]).size
    ).toBe(3);
  });
});
