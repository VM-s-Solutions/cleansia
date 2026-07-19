import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreatePayConfigCommand,
  EmployeePayConfigDto,
  UpdatePayConfigCommand,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PayConfigFormData, PayConfigFormFacade } from './pay-config-form.facade';

describe('PayConfigFormFacade', () => {
  let facade: PayConfigFormFacade;
  let detailsMock: jest.Mock;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: PayConfigFormData = {
    serviceId: 'svc-1',
    packageId: undefined,
    basePay: 500,
    extraPerRoom: 50,
    extraPerBathroom: 30,
    distanceRatePerKm: 10,
    minimumPay: 300,
    maximumPay: 2000,
    currencyId: 'cur-1',
    description: 'Standard rate',
  };

  beforeEach(() => {
    detailsMock = jest.fn();
    createMock = jest.fn();
    updateMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        PayConfigFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayConfigClient: {
              details: detailsMock,
              create: createMock,
              update: updateMock,
            },
            adminServiceClient: { getPaged: jest.fn().mockReturnValue(of(null)) },
            adminPackageClient: { getPaged: jest.fn().mockReturnValue(of(null)) },
            adminCurrencyClient: { getOverview: jest.fn().mockReturnValue(of([])) },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(PayConfigFormFacade);
  });

  it('loads the pay config detail through the generated client', () => {
    detailsMock.mockReturnValue(
      of(EmployeePayConfigDto.fromJS({ id: 'pc-1', basePay: 400 }))
    );

    facade.loadPayConfig('pc-1');

    expect(detailsMock).toHaveBeenCalledWith('pc-1');
    expect(facade.payConfig()?.basePay).toBe(400);
    expect(facade.loading()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('navigates back to the list when the detail load fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadPayConfig('pc-1');

    expect(facade.payConfig()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(navigate).toHaveBeenCalledWith([
      CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT,
    ]);
  });

  it('sends the generated create command and confirms on success', () => {
    createMock.mockReturnValue(of({ payConfigId: 'pc-9' }));

    facade.createPayConfig(formData);

    const command = createMock.mock.calls[0][0];
    expect(command).toBeInstanceOf(CreatePayConfigCommand);
    expect(command.serviceId).toBe('svc-1');
    expect(command.packageId).toBeUndefined();
    expect(command.basePay).toBe(500);
    expect(command.currencyId).toBe('cur-1');
    expect(command.description).toBe('Standard rate');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.pay_config_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalledWith([
      CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT,
    ]);
    expect(facade.saving()).toBe(false);
  });

  it('normalizes empty-string ids to undefined in the create command', () => {
    createMock.mockReturnValue(of({ payConfigId: 'pc-9' }));

    facade.createPayConfig({ ...formData, serviceId: '', description: '' });

    const command = createMock.mock.calls[0][0];
    expect(command.serviceId).toBeUndefined();
    expect(command.description).toBeUndefined();
  });

  it('sends the generated update command keyed by payConfigId', () => {
    updateMock.mockReturnValue(of({ payConfigId: 'pc-1' }));

    facade.updatePayConfig('pc-1', formData);

    expect(updateMock.mock.calls[0][0]).toBe('pc-1');
    const command = updateMock.mock.calls[0][1];
    expect(command).toBeInstanceOf(UpdatePayConfigCommand);
    expect(command.payConfigId).toBe('pc-1');
    expect(command.basePay).toBe(500);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.pay_config_form.messages.update_success'
    );
  });

  it('clears saving and stays on the form when the create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createPayConfig(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('clears saving and stays on the form when the update fails', () => {
    updateMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.updatePayConfig('pc-1', formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });
});
