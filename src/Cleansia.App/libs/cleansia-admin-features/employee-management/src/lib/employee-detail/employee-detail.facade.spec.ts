import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminEmployeeDetail,
  CreatePayConfigCommand,
  UpdatePayConfigCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

describe('EmployeeDetailFacade — pay config overrides', () => {
  let facade: EmployeeDetailFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let employeeSummaryMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const rateData = {
    basePay: 500,
    extraPerRoom: 50,
    extraPerBathroom: 30,
    distanceRatePerKm: 10,
    minimumPay: 300,
    maximumPay: 2000,
  };

  beforeEach(() => {
    createMock = jest.fn();
    updateMock = jest.fn();
    deleteMock = jest.fn();
    employeeSummaryMock = jest.fn().mockReturnValue(of(null));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayConfigClient: {
              create: createMock,
              update: updateMock,
              delete: deleteMock,
              employeeSummary: employeeSummaryMock,
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

  it('sends a create command, closes the dialog and reloads the summary on success', () => {
    createMock.mockReturnValue(of({ payConfigId: 'pc-9' }));
    facade.payConfigDialogOpen.set(true);

    facade.createEmployeePayConfig({
      serviceId: 'svc-1',
      packageId: '',
      currencyId: 'cur-1',
      ...rateData,
      description: 'note',
    });

    const command = createMock.mock.calls[0][0];
    expect(command).toBeInstanceOf(CreatePayConfigCommand);
    expect(command.serviceId).toBe('svc-1');
    expect(command.packageId).toBeUndefined();
    expect(command.currencyId).toBe('cur-1');
    expect(command.basePay).toBe(500);
    expect(facade.payConfigDialogOpen()).toBe(false);
    expect(facade.savingPayConfig()).toBe(false);
    expect(employeeSummaryMock).toHaveBeenCalledWith('emp-1');
    expect(snackbar.showSuccess).toHaveBeenCalled();
  });

  it('keeps the dialog open and reports the error when the create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.payConfigDialogOpen.set(true);

    facade.createEmployeePayConfig({
      serviceId: 'svc-1',
      currencyId: 'cur-1',
      ...rateData,
    });

    expect(facade.payConfigDialogOpen()).toBe(true);
    expect(facade.savingPayConfig()).toBe(false);
    expect(snackbar.showError).toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('sends an update command keyed by config id and closes the dialog on success', () => {
    updateMock.mockReturnValue(of({ payConfigId: 'pc-1' }));
    facade.payConfigDialogOpen.set(true);

    facade.updateSinglePayConfig('pc-1', rateData);

    expect(updateMock.mock.calls[0][0]).toBe('pc-1');
    const command = updateMock.mock.calls[0][1];
    expect(command).toBeInstanceOf(UpdatePayConfigCommand);
    expect(command.payConfigId).toBe('pc-1');
    expect(command.basePay).toBe(500);
    expect(facade.payConfigDialogOpen()).toBe(false);
    expect(facade.savingPayConfig()).toBe(false);
    expect(employeeSummaryMock).toHaveBeenCalledWith('emp-1');
    expect(snackbar.showSuccess).toHaveBeenCalled();
  });

  it('keeps the dialog open and reports the error when the update fails', () => {
    updateMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.payConfigDialogOpen.set(true);

    facade.updateSinglePayConfig('pc-1', rateData);

    expect(facade.payConfigDialogOpen()).toBe(true);
    expect(facade.savingPayConfig()).toBe(false);
    expect(snackbar.showError).toHaveBeenCalled();
  });

  it('deletes an override and reloads the summary on success', () => {
    deleteMock.mockReturnValue(of({ payConfigId: 'pc-1' }));

    facade.deleteEmployeePayConfig('pc-1');

    expect(deleteMock).toHaveBeenCalledWith('pc-1');
    expect(employeeSummaryMock).toHaveBeenCalledWith('emp-1');
    expect(snackbar.showSuccess).toHaveBeenCalled();
  });

  it('reports the error and skips reload when the delete fails', () => {
    deleteMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.deleteEmployeePayConfig('pc-1');

    expect(snackbar.showError).toHaveBeenCalled();
    expect(employeeSummaryMock).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });
});
