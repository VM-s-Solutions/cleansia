import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  EmployeePayConfigDto,
  PagedDataOfEmployeePayConfigDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PayConfigManagementFacade } from './pay-config-management.facade';

describe('PayConfigManagementFacade', () => {
  let facade: PayConfigManagementFacade;
  let getPagedMock: jest.Mock;
  let deleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const populatedPage = PagedDataOfEmployeePayConfigDto.fromJS({
    data: [
      {
        id: 'pc-1',
        serviceId: 'svc-1',
        serviceName: 'Deep clean',
        basePay: 500,
        currencyCode: 'CZK',
      },
    ],
    total: 1,
  });

  const emptyPage = PagedDataOfEmployeePayConfigDto.fromJS({
    data: [],
    total: 0,
  });

  beforeEach(() => {
    getPagedMock = jest.fn().mockReturnValue(of(populatedPage));
    deleteMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PayConfigManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayConfigClient: {
              getPaged: getPagedMock,
              delete: deleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(PayConfigManagementFacade);
  });

  it('loads pay configs through the generated client and settles the loaded state', () => {
    facade.loadPayConfigs();

    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(facade.payConfigs()).toHaveLength(1);
    expect(facade.payConfigs()[0].id).toBe('pc-1');
    expect(facade.totalRecords()).toBe(1);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('settles the loaded-empty state when the page has no rows', () => {
    getPagedMock.mockReturnValue(of(emptyPage));

    facade.loadPayConfigs();

    expect(facade.payConfigs()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('clears loading and initial loading when the load fails', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadPayConfigs();

    expect(facade.payConfigs()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('maps filter fields into the generated getPaged argument positions', () => {
    facade.applyFilter({ serviceId: 'svc-1', packageId: 'pkg-1' });

    const args = getPagedMock.mock.calls.at(-1);
    expect(args?.[0]).toBeUndefined();
    expect(args?.[1]).toBe('svc-1');
    expect(args?.[2]).toBe('pkg-1');
    expect(args?.[3]).toBeUndefined();
    expect(args?.[4]).toBeUndefined();
    expect(args?.[5]).toBe(0);
    expect(args?.[6]).toBe(20);
  });

  it('omits empty-string filter values from the query', () => {
    facade.applyFilter({ serviceId: '', packageId: '' });

    const args = getPagedMock.mock.calls.at(-1);
    expect(args?.[1]).toBeUndefined();
    expect(args?.[2]).toBeUndefined();
  });

  it('resets the offset to zero when a filter is applied', () => {
    facade.onPageChange(40, 20);
    facade.applyFilter({ serviceId: 'svc-1' });

    const args = getPagedMock.mock.calls.at(-1);
    expect(args?.[5]).toBe(0);
  });

  it('forwards offset and limit on a page change', () => {
    facade.onPageChange(20, 50);

    const args = getPagedMock.mock.calls.at(-1);
    expect(args?.[5]).toBe(20);
    expect(args?.[6]).toBe(50);
  });

  it('deletes through the generated client, then reloads and confirms', () => {
    deleteMock.mockReturnValue(of({ payConfigId: 'pc-1' }));

    facade.deletePayConfig(EmployeePayConfigDto.fromJS({ id: 'pc-1' }));

    expect(deleteMock).toHaveBeenCalledWith('pc-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.pay_config_management.messages.delete_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('does not reload or confirm when the delete fails', () => {
    deleteMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.deletePayConfig(EmployeePayConfigDto.fromJS({ id: 'pc-1' }));

    expect(snackbar.showSuccess).not.toHaveBeenCalled();
    expect(getPagedMock).not.toHaveBeenCalled();
  });
});
