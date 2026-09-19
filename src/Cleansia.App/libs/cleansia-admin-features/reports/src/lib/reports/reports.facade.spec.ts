import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCurrencyListItem,
  PayrollReportDto,
  RevenueReportDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ReportsFacade } from './reports.facade';

describe('ReportsFacade', () => {
  let facade: ReportsFacade;
  let revenueMock: jest.Mock;
  let payrollMock: jest.Mock;
  let getOverviewMock: jest.Mock;

  beforeEach(() => {
    revenueMock = jest
      .fn()
      .mockReturnValue(of(RevenueReportDto.fromJS({ totalRevenue: 0 })));
    payrollMock = jest
      .fn()
      .mockReturnValue(of(PayrollReportDto.fromJS({ totalPayroll: 0 })));
    getOverviewMock = jest.fn().mockReturnValue(of([]));

    TestBed.configureTestingModule({
      providers: [
        ReportsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminReportClient: { revenue: revenueMock, payroll: payrollMock },
            adminCurrencyClient: { getOverview: getOverviewMock },
          },
        },
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(ReportsFacade);
  });

  it('passes the chosen currency to both report endpoints', () => {
    facade.setDateRange(new Date('2026-01-01'), new Date('2026-01-31'), 'cur-eur');

    expect(revenueMock).toHaveBeenCalledWith(
      expect.any(Date),
      expect.any(Date),
      'cur-eur'
    );

    facade.setActiveTab('payroll');

    expect(payrollMock).toHaveBeenCalledWith(
      expect.any(Date),
      expect.any(Date),
      'cur-eur'
    );

    facade.resetToDefaultDateRange();
    facade.setActiveTab('revenue');

    expect(facade.selectedCurrencyId()).toBeUndefined();
    expect(payrollMock.mock.lastCall?.[2]).toBeUndefined();
    expect(revenueMock.mock.lastCall?.[2]).toBeUndefined();
  });

  it('formats report amounts in the currency the report names', () => {
    facade.revenueReport.set(
      RevenueReportDto.fromJS({ totalRevenue: 45.1, currencyCode: 'EUR' })
    );
    facade.payrollReport.set(
      PayrollReportDto.fromJS({ totalPayroll: 1100, currencyCode: 'CZK' })
    );

    const expectedEur = new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'EUR',
    }).format(45.1);

    const revenue = facade.formatRevenueAmount(45.1);
    expect(revenue).toBe(expectedEur);
    expect(revenue).toContain('€');
    expect(revenue).toContain('45.10');

    const payroll = facade.formatPayrollAmount(1100);
    expect(payroll).toMatch(/CZK|Kč/);
    expect(payroll).not.toContain('€');

    expect(facade.formatRevenueAmount(undefined)).toBe('');
  });

  it('reads the headline off the net figure the server sent, never off the gross one', () => {
    facade.revenueReport.set(
      RevenueReportDto.fromJS({
        totalRevenue: 3000,
        totalRefundedToCard: 300,
        totalReturnedToCredit: 0,
        totalRefunded: 300,
        netRevenue: 2700,
        currencyCode: 'EUR',
      })
    );

    const eur = (value: number) =>
      new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'EUR' }).format(value);

    expect(facade.revenueHeadline()).toBe(eur(2700));
    expect(facade.revenueHeadline()).not.toBe(eur(3000));
  });

  it('breaks the headline into gross, refunded (both legs) and the credit leg, each in the report currency', () => {
    facade.revenueReport.set(
      RevenueReportDto.fromJS({
        totalRevenue: 2000,
        totalRefundedToCard: 1500,
        totalReturnedToCredit: 500,
        totalRefunded: 2000,
        netRevenue: 0,
        currencyCode: 'CZK',
      })
    );

    const czk = (value: number) =>
      new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'CZK' }).format(value);

    expect(facade.revenueBreakdown()).toEqual({
      gross: czk(2000),
      refunded: czk(2000),
      credit: czk(500),
    });
    expect(facade.revenueHeadline()).toBe(czk(0));
  });

  it('renders an empty headline and breakdown before a report has loaded', () => {
    expect(facade.revenueHeadline()).toBe('');
    expect(facade.revenueBreakdown()).toEqual({ gross: '', refunded: '', credit: '' });
  });

  it('keeps only currencies that carry an id and a code', () => {
    getOverviewMock.mockReturnValue(
      of([
        AdminCurrencyListItem.fromJS({ id: 'cur-czk', code: 'CZK', isDefault: true }),
        AdminCurrencyListItem.fromJS({ id: 'cur-eur', code: 'EUR', isDefault: false }),
        AdminCurrencyListItem.fromJS({ id: undefined, code: 'XXX' }),
        AdminCurrencyListItem.fromJS({ id: 'cur-blank', code: undefined }),
      ])
    );

    facade.loadCurrencies();

    expect(facade.currencies()).toEqual([
      { id: 'cur-czk', code: 'CZK', isDefault: true },
      { id: 'cur-eur', code: 'EUR', isDefault: false },
    ]);
  });
});
