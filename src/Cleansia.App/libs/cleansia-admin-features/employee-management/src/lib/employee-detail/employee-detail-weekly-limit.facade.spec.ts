import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminEmployeeDetail,
  AdminSetEmployeeWeeklyOrderLimitRequest,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

/**
 * The weekly cap's write side.
 *
 * The case that matters is CLEARING. `null` here means "no limit", a real value the endpoint
 * accepts — not "not supplied". That distinction is the whole reason the backend has a narrow
 * command instead of a field on the employee update, which merges every field with `?? existing`
 * and structurally cannot express it. A facade that dropped the null would make the cap
 * one-way: settable, never removable.
 */
describe('EmployeeDetailFacade — weekly order limit', () => {
  let facade: EmployeeDetailFacade;
  let weeklyOrderLimitMock: jest.Mock;
  let detailsMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    weeklyOrderLimitMock = jest.fn();
    detailsMock = jest.fn().mockReturnValue(of(null));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeClient: {
              weeklyOrderLimit: weeklyOrderLimitMock,
              details: detailsMock,
            },
            adminPayConfigClient: {
              employeeSummary: jest.fn().mockReturnValue(of(null)),
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

  it('sends the cap as a typed request', () => {
    weeklyOrderLimitMock.mockReturnValue(of({ employeeId: 'emp-1', weeklyOrderLimit: 5 }));

    facade.setWeeklyOrderLimit(5);

    expect(weeklyOrderLimitMock).toHaveBeenCalledTimes(1);
    const [employeeId, request] = weeklyOrderLimitMock.mock.calls[0];
    expect(employeeId).toBe('emp-1');
    expect(request).toBeInstanceOf(AdminSetEmployeeWeeklyOrderLimitRequest);
    expect(request.weeklyOrderLimit).toBe(5);
  });

  /** Clearing is half of what this endpoint exists for. */
  it('sends undefined when the cap is cleared, so the server reads it as unlimited', () => {
    weeklyOrderLimitMock.mockReturnValue(
      of({ employeeId: 'emp-1', weeklyOrderLimit: undefined })
    );

    facade.setWeeklyOrderLimit(null);

    const [, request] = weeklyOrderLimitMock.mock.calls[0];
    expect(request.weeklyOrderLimit).toBeUndefined();
    expect(request.toJSON()).toEqual({ weeklyOrderLimit: undefined });
  });

  it('closes the editor, confirms, and reloads the detail on success', () => {
    weeklyOrderLimitMock.mockReturnValue(of({ employeeId: 'emp-1', weeklyOrderLimit: 3 }));
    facade.editingWeeklyLimit.set(true);

    facade.setWeeklyOrderLimit(3);

    expect(facade.editingWeeklyLimit()).toBe(false);
    expect(facade.savingWeeklyLimit()).toBe(false);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.employee_detail.messages.weekly_limit_save_success'
    );
    expect(detailsMock).toHaveBeenCalledWith('emp-1');
  });

  /**
   * A rejected save must leave the admin's number on screen to correct — the interceptor has already
   * surfaced the reason, and closing the editor would discard what they typed and tell them nothing.
   */
  it('keeps the editor open and claims nothing when the save fails', () => {
    weeklyOrderLimitMock.mockReturnValue(throwError(() => new Error('400')));
    facade.editingWeeklyLimit.set(true);

    facade.setWeeklyOrderLimit(0);

    expect(facade.editingWeeklyLimit()).toBe(true);
    expect(facade.savingWeeklyLimit()).toBe(false);
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
    expect(detailsMock).not.toHaveBeenCalled();
  });

  it('does nothing at all without a loaded employee', () => {
    facade.employee.set(null);

    facade.setWeeklyOrderLimit(5);

    expect(weeklyOrderLimitMock).not.toHaveBeenCalled();
  });
});
