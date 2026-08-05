import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ClosePayPeriodCommand,
  PayPeriodStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PayPeriodManagementFacade } from './pay-period-management.facade';

describe('PayPeriodManagementFacade', () => {
  let facade: PayPeriodManagementFacade;
  let getPagedMock: jest.Mock;
  let closeMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of({ data: [], total: 0 }));
    closeMock = jest.fn().mockReturnValue(of({ payPeriodId: 'period-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PayPeriodManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPayPeriodClient: { getPaged: getPagedMock, close: closeMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(PayPeriodManagementFacade);
  });

  it('starts empty and initially loading', () => {
    expect(facade.payPeriods()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.initialLoading()).toBe(true);
  });

  it('drops the initial-loading latch on an empty page', () => {
    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('drops the initial-loading latch when the page read fails', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([]);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('holds the page and the total once a read lands', () => {
    getPagedMock.mockReturnValue(of({ data: [{ id: 'p-1' }], total: 7 }));

    facade.loadPayPeriods();

    expect(facade.payPeriods()).toEqual([{ id: 'p-1' }]);
    expect(facade.totalRecords()).toBe(7);
  });

  it('sends the filter to the server and returns to the first page', () => {
    facade.onPageChange(40, 20);
    facade.applyFilter({ status: PayPeriodStatus.Closed, year: 2026 });

    const [status, year, , offset] = getPagedMock.mock.calls.at(-1) ?? [];
    expect(status).toBe(PayPeriodStatus.Closed);
    expect(year).toBe(2026);
    expect(offset).toBe(0);
  });

  it('clears the filter on reset', () => {
    facade.applyFilter({ status: PayPeriodStatus.Closed, year: 2026 });
    facade.resetFilter();

    const [status, year] = getPagedMock.mock.calls.at(-1) ?? [];
    expect(status).toBeUndefined();
    expect(year).toBeUndefined();
  });

  it('re-reads the list after a close lands, and not when it fails', () => {
    facade.closePayPeriod('period-1', 'done');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pay_periods.messages.close_success'
    );

    closeMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.closePayPeriod('period-1', 'done');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  describe('command bodies on the wire', () => {
    it('serializes a close with the period id and the notes', () => {
      facade.closePayPeriod('period-1', 'all invoices generated');

      const command: ClosePayPeriodCommand = closeMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(ClosePayPeriodCommand);
      expect(command.toJSON()).toEqual({
        payPeriodId: 'period-1',
        notes: 'all invoices generated',
      });
    });

    it('leaves the notes undefined when the caller omits them', () => {
      facade.closePayPeriod('period-1');

      const command: ClosePayPeriodCommand = closeMock.mock.calls[0][0];
      expect(command.toJSON()).toEqual({
        payPeriodId: 'period-1',
        notes: undefined,
      });
    });
  });
});
