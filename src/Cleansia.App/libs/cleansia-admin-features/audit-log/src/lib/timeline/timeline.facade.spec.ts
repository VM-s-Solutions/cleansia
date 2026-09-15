import { TestBed } from '@angular/core/testing';
import {
  CustomerAuditClient,
  PagedDataOfTimelineEntryDto,
  TimelineEntryDto,
  TimelineSource,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { TimelineFacade } from './timeline.facade';

describe('TimelineFacade', () => {
  let facade: TimelineFacade;
  let customerAuditClient: { timeline: jest.Mock };

  const populatedPage = PagedDataOfTimelineEntryDto.fromJS({
    data: [
      TimelineEntryDto.fromJS({
        source: TimelineSource.Customer,
        id: 'row-1',
        occurredOn: '2026-09-13T10:00:00Z',
        action: 'customer.order.cancel',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
      }),
      TimelineEntryDto.fromJS({
        source: TimelineSource.Employee,
        id: 'row-2',
        occurredOn: '2026-09-12T10:00:00Z',
        action: 'employee.order.dropped',
        resourceType: 'Order',
        resourceId: 'order-1',
        success: true,
      }),
    ],
    total: 2,
  });

  const emptyPage = PagedDataOfTimelineEntryDto.fromJS({ data: [], total: 0 });

  beforeEach(() => {
    customerAuditClient = {
      timeline: jest.fn().mockReturnValue(of(populatedPage)),
    };

    TestBed.configureTestingModule({
      providers: [
        TimelineFacade,
        { provide: CustomerAuditClient, useValue: customerAuditClient },
      ],
    });

    facade = TestBed.inject(TimelineFacade);
  });

  it('loads a user timeline with the user key only, twenty rows from the top', () => {
    facade.loadForUser('user-1');

    expect(customerAuditClient.timeline).toHaveBeenCalledTimes(1);
    const args = customerAuditClient.timeline.mock.calls[0];
    expect(args[0]).toBe('user-1');
    expect(args[1]).toBeUndefined();
    expect(args[2]).toBeUndefined();
    expect(args[3]).toBeUndefined();
    expect(args[4]).toBe(0);
    expect(args[5]).toBe(20);

    expect(facade.entries()).toHaveLength(2);
    expect(facade.totalRecords()).toBe(2);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('loads a resource timeline with the resource pair and no user key', () => {
    facade.loadForResource('Order', 'order-1');

    const args = customerAuditClient.timeline.mock.calls[0];
    expect(args[0]).toBeUndefined();
    expect(args[1]).toBe('Order');
    expect(args[2]).toBe('order-1');
  });

  it('settles the empty state when the page has no rows', () => {
    customerAuditClient.timeline.mockReturnValue(of(emptyPage));

    facade.loadForUser('user-1');

    expect(facade.entries()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('settles the error state, clears loading and leaves the initial load over', () => {
    customerAuditClient.timeline.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadForUser('user-1');

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.entries()).toEqual([]);
  });

  it('clears a previous error when a reload succeeds', () => {
    customerAuditClient.timeline.mockReturnValueOnce(throwError(() => new Error('boom')));
    facade.loadForUser('user-1');
    expect(facade.hasError()).toBe(true);

    customerAuditClient.timeline.mockReturnValue(of(populatedPage));
    facade.onPageChange(0, 20);

    expect(facade.hasError()).toBe(false);
    expect(facade.entries()).toHaveLength(2);
  });

  it('pages lazily with the same key and the requested window', () => {
    facade.loadForUser('user-1');
    facade.onPageChange(40, 20);

    const args = customerAuditClient.timeline.mock.calls.at(-1);
    expect(args?.[0]).toBe('user-1');
    expect(args?.[4]).toBe(40);
    expect(args?.[5]).toBe(20);
  });

  it('restarts from the first page when the key changes', () => {
    facade.loadForUser('user-1');
    facade.onPageChange(40, 20);
    facade.loadForResource('Order', 'order-1');

    const args = customerAuditClient.timeline.mock.calls.at(-1);
    expect(args?.[1]).toBe('Order');
    expect(args?.[4]).toBe(0);
  });

  it('does nothing on a page change before a key is set', () => {
    facade.onPageChange(20, 20);

    expect(customerAuditClient.timeline).not.toHaveBeenCalled();
  });
});
