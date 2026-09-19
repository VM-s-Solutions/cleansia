import { TestBed } from '@angular/core/testing';
import {
  CustomerActionAuditDetailDto,
  CustomerAuditClient,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { CustomerAuditEntryFacade } from './customer-audit-entry.facade';

describe('CustomerAuditEntryFacade', () => {
  let facade: CustomerAuditEntryFacade;
  let customerAuditClient: { getById: jest.Mock };

  const entry = CustomerActionAuditDetailDto.fromJS({
    id: 'audit-1',
    userId: 'user-1',
    clientAudience: 'customer-web',
    ipAddress: '10.0.0.1',
    deviceLabel: 'iPhone',
    action: 'customer.order.cancel',
    resourceType: 'Order',
    resourceId: 'order-1',
    success: true,
    occurredOn: '2026-09-13T10:00:00Z',
    payloadJson: '{"tier":"plus","feeRate":0.5}',
  });

  beforeEach(() => {
    customerAuditClient = { getById: jest.fn().mockReturnValue(of(entry)) };

    TestBed.configureTestingModule({
      providers: [
        CustomerAuditEntryFacade,
        { provide: CustomerAuditClient, useValue: customerAuditClient },
      ],
    });

    facade = TestBed.inject(CustomerAuditEntryFacade);
  });

  it('loads the entry by id and derives the payload rows from it', () => {
    facade.loadEntry('audit-1');

    expect(customerAuditClient.getById).toHaveBeenCalledWith('audit-1');
    expect(facade.entry()?.id).toBe('audit-1');
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
    expect(facade.payload().rows).toEqual([
      { key: 'tier', value: 'plus' },
      { key: 'feeRate', value: '0.5' },
    ]);
    expect(facade.hasPayload()).toBe(true);
    expect(facade.rawJson()).toBe('{\n  "tier": "plus",\n  "feeRate": 0.5\n}');
  });

  it('reports no payload for a failure row that recorded none', () => {
    customerAuditClient.getById.mockReturnValue(
      of(CustomerActionAuditDetailDto.fromJS({ ...entry, success: false, payloadJson: undefined }))
    );

    facade.loadEntry('audit-1');

    expect(facade.hasPayload()).toBe(false);
    expect(facade.payload()).toEqual({ rows: [], diff: [] });
  });

  it('settles the error state, clears the entry and stops loading on failure', () => {
    customerAuditClient.getById.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadEntry('audit-1');

    expect(facade.hasError()).toBe(true);
    expect(facade.entry()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(facade.hasPayload()).toBe(false);
  });

  it('toggles the raw JSON view off by default and back', () => {
    expect(facade.showRawJson()).toBe(false);
    facade.toggleRawJson();
    expect(facade.showRawJson()).toBe(true);
    facade.toggleRawJson();
    expect(facade.showRawJson()).toBe(false);
  });
});
