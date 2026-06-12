import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import {
  EmployeeItem,
  PagedDataOfPayPeriodDto,
  PartnerClient,
  PeriodPaySummaryDto,
} from '@cleansia/partner-services';
import { of, throwError } from 'rxjs';
import { PeriodPayFacade } from './period-pay.facade';

describe('PeriodPayFacade', () => {
  let facade: PeriodPayFacade;
  let employeeClient: { getCurrentEmployee: jest.Mock };
  let payPeriodClient: { getPagedPayPeriods: jest.Mock };
  let employeePayrollClient: { getPeriodPays: jest.Mock };

  const employee = EmployeeItem.fromJS({ id: 'emp-1' });

  const periodsPage = PagedDataOfPayPeriodDto.fromJS({
    data: [
      { id: 'period-2', periodLabel: '16.5. - 31.5.2026', status: 'Open' },
      { id: 'period-1', periodLabel: '1.5. - 15.5.2026', status: 'Paid' },
    ],
    total: 2,
  });

  const summary = PeriodPaySummaryDto.fromJS({
    payPeriodId: 'period-2',
    employeeId: 'emp-1',
    totalOrders: 2,
    grandTotal: 3500,
    hasInvoice: false,
    orderPays: [{ id: 'pay-1', orderNumber: 'ORD-1', totalPay: 1500 }],
  });

  beforeEach(() => {
    employeeClient = { getCurrentEmployee: jest.fn(() => of(employee)) };
    payPeriodClient = { getPagedPayPeriods: jest.fn(() => of(periodsPage)) };
    employeePayrollClient = { getPeriodPays: jest.fn(() => of(summary)) };

    TestBed.configureTestingModule({
      providers: [
        PeriodPayFacade,
        {
          provide: PartnerClient,
          useValue: { employeeClient, payPeriodClient, employeePayrollClient },
        },
      ],
    });

    facade = TestBed.inject(PeriodPayFacade);
  });

  it('loads periods, auto-selects the latest and loads its summary', () => {
    facade.init();

    expect(payPeriodClient.getPagedPayPeriods).toHaveBeenCalledTimes(1);
    expect(facade.payPeriods().length).toBe(2);
    expect(facade.selectedPeriodId()).toBe('period-2');
    expect(employeePayrollClient.getPeriodPays).toHaveBeenCalledTimes(1);
    expect(facade.summary()?.grandTotal).toBe(3500);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('scopes the summary call to the session employee and the selected period', () => {
    facade.init();

    expect(employeePayrollClient.getPeriodPays).toHaveBeenCalledWith('emp-1', 'period-2');
  });

  it('derives the status key of the selected period', () => {
    facade.init();

    expect(facade.selectedPeriodStatus()).toBe('open');

    facade.selectPeriod('period-1');

    expect(facade.selectedPeriodStatus()).toBe('paid');
    expect(employeePayrollClient.getPeriodPays).toHaveBeenLastCalledWith('emp-1', 'period-1');
  });

  it('syncs a connected control on auto-select without re-triggering a load', () => {
    const control = new FormControl<string | null>(null);
    facade.connectPeriodControl(control);

    facade.init();

    expect(control.value).toBe('period-2');
    expect(employeePayrollClient.getPeriodPays).toHaveBeenCalledTimes(1);
  });

  it('loads the summary for a period picked through the connected control', () => {
    const control = new FormControl<string | null>(null);
    facade.connectPeriodControl(control);
    facade.init();

    control.setValue('period-1');

    expect(facade.selectedPeriodId()).toBe('period-1');
    expect(employeePayrollClient.getPeriodPays).toHaveBeenLastCalledWith('emp-1', 'period-1');
  });

  it('shows the empty state when there are no pay periods', () => {
    payPeriodClient.getPagedPayPeriods.mockReturnValue(
      of(PagedDataOfPayPeriodDto.fromJS({ data: [], total: 0 }))
    );

    facade.init();

    expect(facade.payPeriods().length).toBe(0);
    expect(facade.summary()).toBeNull();
    expect(employeePayrollClient.getPeriodPays).not.toHaveBeenCalled();
    expect(facade.initialLoading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('sets the error flag when the employee lookup fails', () => {
    employeeClient.getCurrentEmployee.mockReturnValue(throwError(() => new Error('boom')));

    facade.init();

    expect(facade.hasError()).toBe(true);
    expect(facade.initialLoading()).toBe(false);
    expect(payPeriodClient.getPagedPayPeriods).not.toHaveBeenCalled();
  });

  it('sets the error flag when the periods load fails', () => {
    payPeriodClient.getPagedPayPeriods.mockReturnValue(throwError(() => new Error('boom')));

    facade.init();

    expect(facade.hasError()).toBe(true);
    expect(facade.initialLoading()).toBe(false);
    expect(employeePayrollClient.getPeriodPays).not.toHaveBeenCalled();
  });

  it('sets the error flag and clears loading when the summary load fails', () => {
    employeePayrollClient.getPeriodPays.mockReturnValue(throwError(() => new Error('boom')));

    facade.init();

    expect(facade.hasError()).toBe(true);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.summary()).toBeNull();
  });

  it('retries the whole chain when nothing was loaded yet', () => {
    employeeClient.getCurrentEmployee.mockReturnValueOnce(throwError(() => new Error('boom')));

    facade.init();
    facade.retry();

    expect(facade.hasError()).toBe(false);
    expect(facade.summary()?.grandTotal).toBe(3500);
  });

  it('retries only the summary when the periods are already loaded', () => {
    employeePayrollClient.getPeriodPays.mockReturnValueOnce(throwError(() => new Error('boom')));

    facade.init();
    facade.retry();

    expect(payPeriodClient.getPagedPayPeriods).toHaveBeenCalledTimes(1);
    expect(employeePayrollClient.getPeriodPays).toHaveBeenCalledTimes(2);
    expect(facade.hasError()).toBe(false);
    expect(facade.summary()?.grandTotal).toBe(3500);
  });
});
