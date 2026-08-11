import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ApproveEmployeeRequest,
  ContractStatus,
  RejectEmployeeRequest,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { EmployeeManagementFacade } from './employee-management.facade';

describe('EmployeeManagementFacade', () => {
  let facade: EmployeeManagementFacade;
  let getPagedMock: jest.Mock;
  let approveMock: jest.Mock;
  let rejectMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of({ data: [], total: 0 }));
    approveMock = jest.fn().mockReturnValue(of({ employeeId: 'emp-1' }));
    rejectMock = jest.fn().mockReturnValue(of({ employeeId: 'emp-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeeManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeClient: {
              getPaged: getPagedMock,
              approve: approveMock,
              reject: rejectMock,
            },
            adminCountryClient: {
              getOverview: jest.fn().mockReturnValue(of([])),
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: DialogService, useValue: { open: jest.fn() } },
      ],
    });

    facade = TestBed.inject(EmployeeManagementFacade);
  });

  it('starts empty and initially loading', () => {
    expect(facade.employees()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.initialLoading()).toBe(true);
  });

  it('drops the initial-loading latch on an empty page', () => {
    facade.loadEmployees();

    expect(facade.employees()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('drops the initial-loading latch when the page read fails', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadEmployees();

    expect(facade.employees()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('holds the page and the total once a read lands', () => {
    getPagedMock.mockReturnValue(of({ data: [{ id: 'emp-1' }], total: 4 }));

    facade.loadEmployees();

    expect(facade.employees()).toEqual([{ id: 'emp-1' }]);
    expect(facade.totalRecords()).toBe(4);
  });

  it('re-reads the list after an approve lands, and not when it fails', () => {
    facade.approveEmployee('emp-1', 'country-1');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.employee_management.messages.approve_success'
    );

    approveMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.approveEmployee('emp-1', 'country-1');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('re-reads the list after a reject lands, and not when it fails', () => {
    facade.rejectEmployee('emp-1', 'incomplete profile');
    expect(getPagedMock).toHaveBeenCalledTimes(1);

    rejectMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.rejectEmployee('emp-1', 'incomplete profile');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('returns to the first page when a filter is applied and when it is reset', () => {
    facade.onPageChange(40, 20);
    facade.applyFilter({ contractStatuses: [ContractStatus.Pending] });
    expect(getPagedMock.mock.calls.at(-1)?.[5]).toBe(0);

    facade.onPageChange(40, 20);
    facade.resetFilter();
    expect(getPagedMock.mock.calls.at(-1)?.[5]).toBe(0);
  });

  describe('command bodies on the wire', () => {
    it('serializes an approve with the work country and the notes', () => {
      facade.approveEmployee('emp-1', 'country-1', 'documents verified');

      const request: ApproveEmployeeRequest = approveMock.mock.calls[0][1];
      expect(request).toBeInstanceOf(ApproveEmployeeRequest);
      expect(request.toJSON()).toEqual({
        workCountryId: 'country-1',
        notes: 'documents verified',
      });
    });

    it('leaves the approve notes undefined when the caller omits them', () => {
      facade.approveEmployee('emp-1', 'country-1');

      const request: ApproveEmployeeRequest = approveMock.mock.calls[0][1];
      expect(request.toJSON()).toEqual({
        workCountryId: 'country-1',
        notes: undefined,
      });
    });

    it('serializes a reject with the reason', () => {
      facade.rejectEmployee('emp-1', 'incomplete profile');

      const request: RejectEmployeeRequest = rejectMock.mock.calls[0][1];
      expect(request).toBeInstanceOf(RejectEmployeeRequest);
      expect(request.toJSON()).toEqual({ reason: 'incomplete profile' });
    });
  });
});
