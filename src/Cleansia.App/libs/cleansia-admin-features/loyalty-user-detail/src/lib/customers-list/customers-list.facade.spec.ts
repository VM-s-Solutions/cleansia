import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminCustomerClient,
  AdminCustomerListItem,
  PagedDataOfAdminCustomerListItem,
  SortDirection,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { CustomersListFacade } from './customers-list.facade';

describe('CustomersListFacade', () => {
  let facade: CustomersListFacade;
  let getPaged: jest.Mock;
  let navigate: jest.Mock;

  const customer = AdminCustomerListItem.fromJS({
    id: 'user-1',
    firstName: 'Jana',
    lastName: 'Nováková',
    email: 'jana@example.cz',
    phoneNumber: '+420601234567',
    isActive: true,
    isEmailConfirmed: true,
    createdOn: '2026-09-01T10:00:00Z',
  });
  const page = PagedDataOfAdminCustomerListItem.fromJS({ data: [customer.toJSON()], total: 41 });

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page));
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        CustomersListFacade,
        { provide: AdminCustomerClient, useValue: { getPaged } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'cs', onLangChange: EMPTY },
        },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(CustomersListFacade);
  });

  it('opens on the active customers, first page of twenty, and stores the rows and total', () => {
    facade.loadCustomers();

    expect(getPaged).toHaveBeenCalledWith(undefined, true, undefined, 0, 20);
    expect(facade.customers()).toHaveLength(1);
    expect(facade.totalRecords()).toBe(41);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('passes the paging and the sort of the table to the client', () => {
    facade.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 41 });
    facade.onSortChange({ field: 'createdOn', order: -1 });

    const [searchTerm, isActive, sort, offset, limit] = getPaged.mock.calls.at(-1) ?? [];
    expect(searchTerm).toBeUndefined();
    expect(isActive).toBe(true);
    expect(sort?.map((s: { toJSON: () => unknown }) => s.toJSON())).toEqual([
      { field: 'createdOn', direction: SortDirection.Descending },
    ]);
    expect(offset).toBe(40);
    expect(limit).toBe(20);
  });

  it('settles the error state, keeps no rows and clears loading when the read fails', () => {
    getPaged.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCustomers();

    expect(facade.hasError()).toBe(true);
    expect(facade.customers()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('opens the customer detail with the e-mail for its header', () => {
    facade.openCustomer(customer);

    expect(navigate).toHaveBeenCalledWith(['customers', 'user-1'], {
      queryParams: { email: 'jana@example.cz' },
    });
  });

  describe('filters', () => {
    beforeEach(() => jest.useFakeTimers());
    afterEach(() => jest.useRealTimers());

    it('sends the trimmed search only once typing settles, as one request', () => {
      facade.filterForm.patchValue({ searchTerm: 'ja' });
      jest.advanceTimersByTime(200);
      facade.filterForm.patchValue({ searchTerm: ' jana ' });
      jest.advanceTimersByTime(499);

      expect(getPaged).not.toHaveBeenCalled();

      jest.advanceTimersByTime(1);

      expect(getPaged).toHaveBeenCalledTimes(1);
      expect(getPaged).toHaveBeenCalledWith('jana', true, undefined, 0, 20);
      expect(facade.filters.chips()).toEqual([
        { key: 'searchTerm', label: 'pages.customers.filters.search', value: 'jana' },
      ]);
    });

    it('starts on the first page again when the search changes', () => {
      facade.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 41 });
      facade.filterForm.patchValue({ searchTerm: '601' });
      jest.advanceTimersByTime(500);

      const [searchTerm, , , offset] = getPaged.mock.calls.at(-1) ?? [];
      expect(searchTerm).toBe('601');
      expect(offset).toBe(0);
    });

    it('maps All to no flag and Inactive to false, naming a status other than Active in a chip', () => {
      facade.filterForm.patchValue({ status: 'all' });
      jest.advanceTimersByTime(500);
      expect(getPaged.mock.calls.at(-1)?.[1]).toBeUndefined();
      expect(facade.filters.chips()).toEqual([
        {
          key: 'status',
          label: 'pages.customers.filters.status',
          value: 'pages.customers.filters.status_all',
        },
      ]);

      facade.filterForm.patchValue({ status: 'inactive' });
      jest.advanceTimersByTime(500);
      expect(getPaged.mock.calls.at(-1)?.[1]).toBe(false);
    });

    it('resets to the active customers with no search and no chip', () => {
      facade.filterForm.patchValue({ searchTerm: 'jana', status: 'all' });
      jest.advanceTimersByTime(500);

      facade.filters.reset();

      expect(facade.filters.chips()).toEqual([]);
      const [searchTerm, isActive, , offset] = getPaged.mock.calls.at(-1) ?? [];
      expect(searchTerm).toBeUndefined();
      expect(isActive).toBe(true);
      expect(offset).toBe(0);
    });
  });
});
