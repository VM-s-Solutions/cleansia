import { TestBed } from '@angular/core/testing';
import {
  AdminActionAuditDto,
  AdminAuditLogClient,
  PagedDataOfAdminActionAuditDto,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { AuditLogFacade } from './audit-log.facade';

describe('AuditLogFacade', () => {
  let facade: AuditLogFacade;
  let auditClient: { getPaged: jest.Mock };

  const populatedPage = PagedDataOfAdminActionAuditDto.fromJS({
    data: [
      AdminActionAuditDto.fromJS({
        id: 'audit-1',
        actorEmail: 'admin@cleansia.cz',
        action: 'IssuePartialRefund',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
        occurredOn: '2026-06-22T10:00:00Z',
      }),
    ],
    total: 1,
  });

  const emptyPage = PagedDataOfAdminActionAuditDto.fromJS({
    data: [],
    total: 0,
  });

  beforeEach(() => {
    auditClient = { getPaged: jest.fn().mockReturnValue(of(populatedPage)) };

    TestBed.configureTestingModule({
      providers: [
        AuditLogFacade,
        { provide: AdminAuditLogClient, useValue: auditClient },
      ],
    });

    facade = TestBed.inject(AuditLogFacade);
  });

  it('loads audits and settles the loaded-with-data state', () => {
    facade.loadAudits();

    expect(auditClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.audits()).toHaveLength(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('settles the loaded-empty state when the page has no rows', () => {
    auditClient.getPaged.mockReturnValue(of(emptyPage));

    facade.loadAudits();

    expect(facade.audits()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('settles the error state and keeps loading cleared on failure', () => {
    auditClient.getPaged.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadAudits();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.audits()).toEqual([]);
  });

  it('clears a previous error flag when a reload succeeds', () => {
    auditClient.getPaged.mockReturnValueOnce(
      throwError(() => new Error('boom'))
    );
    facade.loadAudits();
    expect(facade.hasError()).toBe(true);

    auditClient.getPaged.mockReturnValue(of(populatedPage));
    facade.loadAudits();

    expect(facade.hasError()).toBe(false);
    expect(facade.audits()).toHaveLength(1);
  });

  it('maps every filter field into the getPaged argument positions', () => {
    const from = new Date('2026-06-01T00:00:00Z');
    const to = new Date('2026-06-30T00:00:00Z');

    facade.applyFilter({
      actorId: 'actor-1',
      actorEmail: 'admin@cleansia.cz',
      action: 'IssuePartialRefund',
      resourceType: 'Order',
      resourceId: 'order-1',
      occurredFrom: from,
      occurredTo: to,
      success: false,
    });

    const args = auditClient.getPaged.mock.calls.at(-1);
    expect(args?.[0]).toBe('actor-1');
    expect(args?.[1]).toBe('admin@cleansia.cz');
    expect(args?.[2]).toBe('IssuePartialRefund');
    expect(args?.[3]).toBe('Order');
    expect(args?.[4]).toBe('order-1');
    expect(args?.[5]).toBe(from);
    expect(args?.[6]).toBe(to);
    expect(args?.[7]).toBe(false);
  });

  it('resets the offset to zero when a filter is applied', () => {
    facade.onPageChange(40, 20);
    facade.applyFilter({ actorEmail: 'admin@cleansia.cz' });

    const args = auditClient.getPaged.mock.calls.at(-1);
    expect(args?.at(-2)).toBe(0);
  });

  it('forwards offset and limit on a page change', () => {
    facade.onPageChange(20, 50);

    const args = auditClient.getPaged.mock.calls.at(-1);
    expect(args?.at(-2)).toBe(20);
    expect(args?.at(-1)).toBe(50);
  });

  it('pins the resource filter so the per-resource history cannot be widened', () => {
    facade.loadResourceHistory('Order', 'order-1');

    let args = auditClient.getPaged.mock.calls.at(-1);
    expect(args?.[3]).toBe('Order');
    expect(args?.[4]).toBe('order-1');

    facade.applyFilter({ resourceType: 'Dispute', resourceId: 'dispute-9' });

    args = auditClient.getPaged.mock.calls.at(-1);
    expect(args?.[3]).toBe('Order');
    expect(args?.[4]).toBe('order-1');
  });
});
