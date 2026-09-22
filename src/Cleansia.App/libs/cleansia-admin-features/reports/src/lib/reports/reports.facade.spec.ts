import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCurrencyListItem,
  PayrollReportDto,
  RevenueReportDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of } from 'rxjs';
import { ReportsFacade } from './reports.facade';

describe('ReportsFacade', () => {
  let facade: ReportsFacade;
  let revenueMock: jest.Mock;
  let payrollMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let onLangChange: Subject<{ lang: string }>;

  beforeEach(() => {
    onLangChange = new Subject<{ lang: string }>();
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
          useValue: {
            showSuccess: jest.fn(),
            showSuccessTranslated: jest.fn(),
            showError: jest.fn(),
            showErrorTranslated: jest.fn(),
          },
        },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'en-GB', onLangChange },
        },
      ],
    });

    facade = TestBed.inject(ReportsFacade);
  });

  it('passes the chosen currency to both report endpoints', () => {
    jest.useFakeTimers();
    facade.filterForm.patchValue({
      startDate: new Date('2026-01-01'),
      endDate: new Date('2026-01-31'),
      currencyId: 'cur-eur',
    });
    jest.advanceTimersByTime(500);

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

    facade.filters.reset();
    facade.setActiveTab('revenue');

    expect(facade.selectedCurrencyId()).toBeUndefined();
    expect(payrollMock.mock.lastCall?.[2]).toBeUndefined();
    expect(revenueMock.mock.lastCall?.[2]).toBeUndefined();
    jest.useRealTimers();
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

  it('reads the headline off the net figure the server sent, never re-summing it from the parts', () => {
    // The parts deliberately do not add up: a client that subtracted the refund legs from the gross
    // figure would print 4 700 or 4 600 here, and only a read of netRevenue prints 2 700.
    facade.revenueReport.set(
      RevenueReportDto.fromJS({
        totalRevenue: 5000,
        totalRefundedToCard: 300,
        totalReturnedToCredit: 100,
        totalRefunded: 300,
        netRevenue: 2700,
        currencyCode: 'EUR',
      })
    );

    const eur = (value: number) =>
      new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'EUR' }).format(value);

    expect(facade.revenueHeadline()).toBe(eur(2700));
    expect(facade.revenueBreakdown()).toEqual({
      gross: eur(5000),
      refunded: eur(300),
      credit: eur(100),
    });
  });

  it('re-renders the headline in the new locale when the language changes', () => {
    facade.revenueReport.set(
      RevenueReportDto.fromJS({ netRevenue: 2700, currencyCode: 'EUR' })
    );
    const inLocale = (locale: string) =>
      new Intl.NumberFormat(locale, { style: 'currency', currency: 'EUR' }).format(2700);

    expect(facade.revenueHeadline()).toBe(inLocale('en-GB'));

    onLangChange.next({ lang: 'cs' });

    expect(facade.revenueHeadline()).toBe(inLocale('cs'));
    expect(facade.revenueHeadline()).not.toBe(inLocale('en-GB'));
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
