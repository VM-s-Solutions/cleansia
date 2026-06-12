import { TestBed } from '@angular/core/testing';
import {
  AdminDisputeClient,
  DisputeListItem,
  DisputeStatus,
  PagedDataOfDisputeListItem,
} from '@cleansia/admin-services';
import { of, throwError } from 'rxjs';
import { DisputesManagementFacade } from './disputes-management.facade';

describe('DisputesManagementFacade', () => {
  let facade: DisputesManagementFacade;
  let disputeClient: { getPaged: jest.Mock };

  const page = PagedDataOfDisputeListItem.fromJS({
    data: [
      DisputeListItem.fromJS({
        id: 'dispute-1',
        displayOrderNumber: 'ORD-1',
        customerName: 'Jane',
      }),
    ],
    total: 1,
  });

  beforeEach(() => {
    disputeClient = { getPaged: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        DisputesManagementFacade,
        { provide: AdminDisputeClient, useValue: disputeClient },
      ],
    });

    facade = TestBed.inject(DisputesManagementFacade);
  });

  it('loads disputes and stores data + total', () => {
    disputeClient.getPaged.mockReturnValue(of(page));

    facade.loadDisputes();

    expect(disputeClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.disputes().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('passes the customer-name and status filter into getPaged', () => {
    disputeClient.getPaged.mockReturnValue(of(page));

    facade.applyFilter({
      customerName: 'Jane',
      statuses: [DisputeStatus.Pending],
    });

    const args = disputeClient.getPaged.mock.calls[0];
    expect(args[2]).toBe('Jane');
    expect(args[4]).toEqual([DisputeStatus.Pending]);
  });

  it('resets offset to zero when a filter is applied', () => {
    disputeClient.getPaged.mockReturnValue(of(page));

    facade.onPageChange(40, 20);
    facade.applyFilter({ customerName: 'Jane' });

    const lastArgs = disputeClient.getPaged.mock.calls.at(-1);
    expect(lastArgs?.at(-2)).toBe(0);
  });

  it('forwards offset and limit on page change', () => {
    disputeClient.getPaged.mockReturnValue(of(page));

    facade.onPageChange(20, 50);

    const args = disputeClient.getPaged.mock.calls[0];
    expect(args.at(-2)).toBe(20);
    expect(args.at(-1)).toBe(50);
  });

  it('sets the error flag and clears loading on failure', () => {
    disputeClient.getPaged.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadDisputes();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.disputes().length).toBe(0);
  });
});
