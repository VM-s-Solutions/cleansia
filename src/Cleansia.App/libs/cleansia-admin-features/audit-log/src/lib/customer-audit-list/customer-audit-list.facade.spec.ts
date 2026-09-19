import { TestBed } from '@angular/core/testing';
import {
  CustomerActionAuditDto,
  CustomerAuditClient,
  PagedDataOfCustomerActionAuditDto,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { CustomerAuditListFacade } from './customer-audit-list.facade';

describe('CustomerAuditListFacade', () => {
  let facade: CustomerAuditListFacade;
  let customerAuditClient: { getPaged: jest.Mock };

  const populatedPage = PagedDataOfCustomerActionAuditDto.fromJS({
    data: [
      CustomerActionAuditDto.fromJS({
        id: 'audit-1',
        userId: 'user-1',
        clientAudience: 'customer-web',
        action: 'customer.order.cancel',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
        occurredOn: '2026-09-13T10:00:00Z',
      }),
    ],
    total: 1,
  });

  const emptyPage = PagedDataOfCustomerActionAuditDto.fromJS({ data: [], total: 0 });

  beforeEach(() => {
    customerAuditClient = { getPaged: jest.fn().mockReturnValue(of(populatedPage)) };

    TestBed.configureTestingModule({
      providers: [
        CustomerAuditListFacade,
        { provide: CustomerAuditClient, useValue: customerAuditClient },
      ],
    });

    facade = TestBed.inject(CustomerAuditListFacade);
  });

  it('loads audits and settles the loaded-with-data state', () => {
    facade.loadAudits();

    expect(customerAuditClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.audits()).toHaveLength(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('settles the loaded-empty state when the page has no rows', () => {
    customerAuditClient.getPaged.mockReturnValue(of(emptyPage));

    facade.loadAudits();

    expect(facade.audits()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('settles the error state and keeps loading cleared on failure', () => {
    customerAuditClient.getPaged.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadAudits();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.audits()).toEqual([]);
  });

  it('clears a previous error flag when a reload succeeds', () => {
    customerAuditClient.getPaged.mockReturnValueOnce(throwError(() => new Error('boom')));
    facade.loadAudits();
    expect(facade.hasError()).toBe(true);

    customerAuditClient.getPaged.mockReturnValue(of(populatedPage));
    facade.loadAudits();

    expect(facade.hasError()).toBe(false);
    expect(facade.audits()).toHaveLength(1);
  });

  it('maps every filter field into the getPaged argument positions', () => {
    const from = new Date('2026-09-01T00:00:00Z');
    const to = new Date('2026-09-30T00:00:00Z');

    facade.applyFilter({
      userId: 'user-1',
      action: 'customer.order.cancel',
      resourceType: 'Order',
      resourceId: 'order-1',
      occurredFrom: from,
      occurredTo: to,
      success: false,
      clientAudience: 'customer-mobile',
    });

    const args = customerAuditClient.getPaged.mock.calls.at(-1);
    expect(args?.[0]).toBe('user-1');
    expect(args?.[1]).toBe('customer.order.cancel');
    expect(args?.[2]).toBe('Order');
    expect(args?.[3]).toBe('order-1');
    expect(args?.[4]).toBe(from);
    expect(args?.[5]).toBe(to);
    expect(args?.[6]).toBe(false);
    expect(args?.[7]).toBe('customer-mobile');
  });

  it('sends no filter positions at all before a filter is applied', () => {
    facade.loadAudits();

    const args = customerAuditClient.getPaged.mock.calls[0];
    expect(args.slice(0, 8)).toEqual(Array(8).fill(undefined));
  });

  it('resets the offset to zero when a filter is applied or reset', () => {
    facade.onPageChange(40, 20);
    facade.applyFilter({ userId: 'user-1' });
    expect(customerAuditClient.getPaged.mock.calls.at(-1)?.at(-2)).toBe(0);

    facade.onPageChange(40, 20);
    facade.resetFilter();
    const args = customerAuditClient.getPaged.mock.calls.at(-1);
    expect(args?.at(-2)).toBe(0);
    expect(args?.[0]).toBeUndefined();
  });

  it('forwards offset and limit on a page change', () => {
    facade.onPageChange(20, 50);

    const args = customerAuditClient.getPaged.mock.calls.at(-1);
    expect(args?.at(-2)).toBe(20);
    expect(args?.at(-1)).toBe(50);
  });

  it('forwards the sort definition on a sort change', () => {
    const sort = [new SortDefinition({ field: 'action', direction: SortDirection.Ascending })];

    facade.onSortChange(sort);

    const args = customerAuditClient.getPaged.mock.calls.at(-1);
    expect(args?.[8]).toBe(sort);
  });
});
