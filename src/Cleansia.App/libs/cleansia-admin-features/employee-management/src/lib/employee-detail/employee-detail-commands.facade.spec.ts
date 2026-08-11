import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminEmployeeDetail,
  AdminUpdateEmployeeAvailabilityRequest,
  ApproveEmployeeRequest,
  BulkCreateEmployeePayConfigsCommand,
  RejectEmployeeRequest,
  TimeRange,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

describe('EmployeeDetailFacade — approval, availability and grade commands', () => {
  let facade: EmployeeDetailFacade;
  let approveMock: jest.Mock;
  let rejectMock: jest.Mock;
  let updateAvailabilityMock: jest.Mock;
  let detailsMock: jest.Mock;
  let bulkCreateMock: jest.Mock;
  let employeeSummaryMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    approveMock = jest.fn().mockReturnValue(of({ employeeId: 'emp-1' }));
    rejectMock = jest.fn().mockReturnValue(of({ employeeId: 'emp-1' }));
    updateAvailabilityMock = jest.fn().mockReturnValue(of({ employeeId: 'emp-1' }));
    detailsMock = jest.fn().mockReturnValue(of(null));
    bulkCreateMock = jest.fn().mockReturnValue(of({ createdCount: 3 }));
    employeeSummaryMock = jest.fn().mockReturnValue(of(null));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeClient: {
              approve: approveMock,
              reject: rejectMock,
              updateAvailability: updateAvailabilityMock,
              details: detailsMock,
            },
            adminPayConfigClient: {
              bulkCreateForEmployee: bulkCreateMock,
              employeeSummary: employeeSummaryMock,
            },
            adminCountryClient: {
              getOverview: jest.fn().mockReturnValue(of([])),
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: DialogService, useValue: { open: jest.fn() } },
        {
          provide: EmployeeDocumentsFacade,
          useValue: { loadEmployeeDocuments: jest.fn(), ngOnDestroy: jest.fn() },
        },
      ],
    });

    facade = TestBed.inject(EmployeeDetailFacade);
    facade.employee.set({ id: 'emp-1' } as AdminEmployeeDetail);
  });

  it('does nothing at all when no employee is loaded', () => {
    facade.employee.set(null);

    facade.approveEmployee('country-1');
    facade.rejectEmployee('incomplete profile');
    facade.saveAvailability({});
    facade.bulkApplyGrade('senior', 'cur-1', true);

    expect(approveMock).not.toHaveBeenCalled();
    expect(rejectMock).not.toHaveBeenCalled();
    expect(updateAvailabilityMock).not.toHaveBeenCalled();
    expect(bulkCreateMock).not.toHaveBeenCalled();
  });

  it('re-reads the employee after an approve lands, and not when it fails', () => {
    facade.approveEmployee('country-1', 'documents verified');
    expect(detailsMock).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.employee_detail.messages.employee_approve_success'
    );

    approveMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.approveEmployee('country-1', 'documents verified');
    expect(detailsMock).toHaveBeenCalledTimes(1);
  });

  it('leaves the availability editor open and reports the error when the save fails', () => {
    updateAvailabilityMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.editingAvailability.set(true);

    facade.saveAvailability({ monday: [] });

    expect(facade.editingAvailability()).toBe(true);
    expect(facade.savingAvailability()).toBe(false);
    expect(snackbar.showError).toHaveBeenCalledWith(
      'pages.employee_detail.messages.availability_save_error'
    );
  });

  it('closes the availability editor once the save lands', () => {
    facade.editingAvailability.set(true);

    facade.saveAvailability({ monday: [] });

    expect(facade.editingAvailability()).toBe(false);
    expect(facade.savingAvailability()).toBe(false);
  });

  it('clears the bulk-grade flag whether the apply lands or fails', () => {
    facade.bulkApplyGrade('senior', 'cur-1', true);
    expect(facade.bulkApplyingGrade()).toBe(false);

    bulkCreateMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.bulkApplyGrade('senior', 'cur-1', true);
    expect(facade.bulkApplyingGrade()).toBe(false);
    expect(snackbar.showError).toHaveBeenCalled();
  });

  describe('command bodies on the wire', () => {
    it('serializes an approve with the work country and the notes', () => {
      facade.approveEmployee('country-1', 'documents verified');

      const request: ApproveEmployeeRequest = approveMock.mock.calls[0][1];
      expect(request).toBeInstanceOf(ApproveEmployeeRequest);
      expect(request.toJSON()).toEqual({
        workCountryId: 'country-1',
        notes: 'documents verified',
      });
    });

    it('leaves the approve notes undefined when the caller omits them', () => {
      facade.approveEmployee('country-1');

      const request: ApproveEmployeeRequest = approveMock.mock.calls[0][1];
      expect(request.toJSON()).toEqual({
        workCountryId: 'country-1',
        notes: undefined,
      });
    });

    it('serializes a reject with the reason', () => {
      facade.rejectEmployee('incomplete profile');

      const request: RejectEmployeeRequest = rejectMock.mock.calls[0][1];
      expect(request).toBeInstanceOf(RejectEmployeeRequest);
      expect(request.toJSON()).toEqual({ reason: 'incomplete profile' });
    });

    it('serializes the weekly availability map verbatim', () => {
      facade.saveAvailability({
        monday: [TimeRange.fromJS({ start: '08:00', end: '16:00' })],
        friday: [],
      });

      const request: AdminUpdateEmployeeAvailabilityRequest =
        updateAvailabilityMock.mock.calls[0][1];
      expect(request).toBeInstanceOf(AdminUpdateEmployeeAvailabilityRequest);
      expect(request.toJSON()).toEqual({
        availability: {
          monday: [{ start: '08:00', end: '16:00' }],
          friday: [],
        },
      });
    });

    it('serializes a bulk grade apply with the employee, grade, currency and overwrite flag', () => {
      facade.bulkApplyGrade('senior', 'cur-1', true);

      const command: BulkCreateEmployeePayConfigsCommand =
        bulkCreateMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(BulkCreateEmployeePayConfigsCommand);
      expect(command.toJSON()).toEqual({
        employeeId: 'emp-1',
        grade: 'senior',
        currencyId: 'cur-1',
        overwriteExisting: true,
      });
    });
  });
});
