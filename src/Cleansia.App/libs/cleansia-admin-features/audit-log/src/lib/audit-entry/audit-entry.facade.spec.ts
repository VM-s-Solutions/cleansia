import { TestBed } from '@angular/core/testing';
import {
  AdminActionAuditDetailDto,
  AdminAuditLogClient,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { AuditEntryFacade } from './audit-entry.facade';

describe('AuditEntryFacade', () => {
  let facade: AuditEntryFacade;
  let auditClient: { getById: jest.Mock };

  const detail = AdminActionAuditDetailDto.fromJS({
    id: 'audit-1',
    actorEmail: 'admin@cleansia.cz',
    action: 'IssuePartialRefund',
    resourceType: 'Order',
    resourceId: 'order-1',
    success: true,
    occurredOn: '2026-06-22T10:00:00Z',
    beforeJson: '{"status":"Confirmed","amount":100}',
    afterJson: '{"status":"Refunded","amount":100}',
  });

  beforeEach(() => {
    auditClient = { getById: jest.fn().mockReturnValue(of(detail)) };

    TestBed.configureTestingModule({
      providers: [
        AuditEntryFacade,
        { provide: AdminAuditLogClient, useValue: auditClient },
      ],
    });

    facade = TestBed.inject(AuditEntryFacade);
  });

  it('starts in the loading state with no entry', () => {
    expect(facade.loading()).toBe(true);
    expect(facade.hasError()).toBe(false);
    expect(facade.entry()).toBeNull();
  });

  it('loads the entry by id and settles the loaded state with a field diff', () => {
    facade.loadEntry('audit-1');

    expect(auditClient.getById).toHaveBeenCalledWith('audit-1');
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
    expect(facade.entry()).toBe(detail);

    const changed = facade.diff().find((d) => d.field === 'status');
    expect(changed?.changed).toBe(true);
    expect(changed?.before).toBe('Confirmed');
    expect(changed?.after).toBe('Refunded');
    expect(facade.hasChanges()).toBe(true);
  });

  it('settles the empty state when both snapshots are absent', () => {
    auditClient.getById.mockReturnValue(
      of(
        AdminActionAuditDetailDto.fromJS({
          id: 'audit-2',
          success: true,
          occurredOn: '2026-06-22T10:00:00Z',
        })
      )
    );

    facade.loadEntry('audit-2');

    expect(facade.entry()).not.toBeNull();
    expect(facade.diff()).toEqual([]);
    expect(facade.hasChanges()).toBe(false);
    expect(facade.hasError()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('settles the error state and keeps loading cleared on failure', () => {
    auditClient.getById.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadEntry('audit-1');

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.entry()).toBeNull();
    expect(facade.diff()).toEqual([]);
  });

  it('clears a previous error when a reload succeeds', () => {
    auditClient.getById.mockReturnValueOnce(throwError(() => new Error('boom')));
    facade.loadEntry('audit-1');
    expect(facade.hasError()).toBe(true);

    auditClient.getById.mockReturnValue(of(detail));
    facade.loadEntry('audit-1');

    expect(facade.hasError()).toBe(false);
    expect(facade.entry()).toBe(detail);
  });
});
