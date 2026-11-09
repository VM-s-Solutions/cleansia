import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCurrencyListItem,
  EmployeeInvoiceDto,
  PagedDataOfEmployeeInvoiceDto,
  PagedDataOfPayPeriodDto,
  RegenerateInvoicePdfCommand,
  RegenerateInvoicePdfResponse,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { InvoiceManagementFacade } from './invoice-management.facade';

describe('InvoiceManagementFacade', () => {
  let facade: InvoiceManagementFacade;
  let invoiceClient: {
    getPaged: jest.Mock;
    download: jest.Mock;
    regeneratePdf: jest.Mock;
  };
  let getOverviewMock: jest.Mock;
  let getPagedPeriodsMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const PAY_PERIOD_SLOT = 1;
  const CURRENCY_SLOT = 8;

  const emptyPage = PagedDataOfEmployeeInvoiceDto.fromJS({
    data: [],
    total: 0,
  });
  const regenerateResponse = RegenerateInvoicePdfResponse.fromJS({
    pdfBlobUrl: 'https://blob/invoice-1.pdf',
  });
  const invoice = EmployeeInvoiceDto.fromJS({
    id: 'invoice-1',
    invoiceNumber: 'INV-001',
  });

  beforeEach(() => {
    invoiceClient = {
      getPaged: jest.fn().mockReturnValue(of(emptyPage)),
      download: jest.fn(),
      regeneratePdf: jest.fn(),
    };
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    getPagedPeriodsMock = jest.fn().mockReturnValue(
      of(
        PagedDataOfPayPeriodDto.fromJS({
          data: [
            { id: 'period-2', startDate: '2026-11-15', endDate: '2026-11-28' },
            { id: 'period-1', startDate: '2026-11-01', endDate: '2026-11-14' },
            { startDate: '2026-10-18', endDate: '2026-10-31' },
          ],
          total: 3,
        })
      )
    );
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        InvoiceManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminInvoiceClient: invoiceClient,
            adminCurrencyClient: { getOverview: getOverviewMock },
            adminPayPeriodClient: { getPaged: getPagedPeriodsMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
        },
      ],
    });

    facade = TestBed.inject(InvoiceManagementFacade);
  });

  it('loads invoices and settles the loading states', () => {
    const page = PagedDataOfEmployeeInvoiceDto.fromJS({
      data: [{ id: 'invoice-1' }],
      total: 1,
    });
    invoiceClient.getPaged.mockReturnValue(of(page));

    facade.loadInvoices();

    expect(facade.invoices()).toHaveLength(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('settles loading on a load error and keeps the previous data', () => {
    invoiceClient.getPaged.mockReturnValue(throwError(() => new Error('x')));

    facade.loadInvoices();

    expect(facade.invoices()).toEqual([]);
    expect(facade.loading()).toBe(false);
  });

  it('builds a typed regenerate command with the invoice id and current language', () => {
    invoiceClient.regeneratePdf.mockReturnValue(of(regenerateResponse));

    facade.retryPdf(invoice);

    expect(invoiceClient.regeneratePdf).toHaveBeenCalledTimes(1);
    const command: RegenerateInvoicePdfCommand =
      invoiceClient.regeneratePdf.mock.calls[0][0];
    expect(command).toBeInstanceOf(RegenerateInvoicePdfCommand);
    expect(command.toJSON()).toEqual({
      invoiceId: 'invoice-1',
      languageCode: 'cs',
    });
  });

  it('does not call regenerate without an invoice id', () => {
    facade.retryPdf(EmployeeInvoiceDto.fromJS({}));
    expect(invoiceClient.regeneratePdf).not.toHaveBeenCalled();
  });

  it('shows a success toast and re-loads the list after a retry', () => {
    invoiceClient.regeneratePdf.mockReturnValue(of(regenerateResponse));

    facade.retryPdf(invoice);

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.invoice_management.messages.retry_pdf_success'
    );
    expect(invoiceClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.retryingPdf()).toBe(false);
  });

  it('leaves the a known backend error code refusal to the interceptor toast on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { detail: 'payroll.invoice.not_found' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(invoiceClient.getPaged).not.toHaveBeenCalled();
    expect(facade.retryingPdf()).toBe(false);
  });

  it('leaves a title-only refusal to the interceptor toast on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { title: 'payroll.invoice.not_found' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves a JSON-string refusal to the interceptor toast on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'company.not_found' }),
      }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('falls back to a generic error for unknown retry failures', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('passes the chosen currency to the list in its own slot, and clears it on reset', () => {
    facade.applyFilter({ currencyId: 'cur-eur' });

    expect(invoiceClient.getPaged.mock.lastCall?.[CURRENCY_SLOT]).toBe('cur-eur');

    facade.applyFilter({});

    expect(invoiceClient.getPaged).toHaveBeenCalledTimes(2);
    expect(invoiceClient.getPaged.mock.lastCall?.[CURRENCY_SLOT]).toBeUndefined();
  });

  it('sends no currency when the filter names none', () => {
    facade.loadInvoices();

    expect(invoiceClient.getPaged.mock.lastCall?.[CURRENCY_SLOT]).toBeUndefined();
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

  describe('the pay period filter', () => {
    afterEach(() => jest.useRealTimers());

    it('lists the periods newest first, each named by its dates in the session language', () => {
      facade.loadPayPeriods();

      expect(getPagedPeriodsMock).toHaveBeenCalledWith(
        undefined,
        undefined,
        [new SortDefinition({ field: 'startDate', direction: SortDirection.Descending })],
        0,
        1000
      );
      expect(facade.payPeriodOptions()).toEqual([
        { label: '15. 11. 2026 – 28. 11. 2026', value: 'period-2' },
        { label: '1. 11. 2026 – 14. 11. 2026', value: 'period-1' },
      ]);
    });

    it('sends the chosen period in its own slot, names it in a chip, and clears it with the chip', () => {
      jest.useFakeTimers();
      facade.loadPayPeriods();

      facade.filterForm.patchValue({ payPeriodId: 'period-1' });
      jest.advanceTimersByTime(500);

      expect(invoiceClient.getPaged.mock.lastCall?.[PAY_PERIOD_SLOT]).toBe('period-1');
      expect(facade.filters.chips()).toEqual([
        {
          key: 'payPeriodId',
          label: 'pages.invoice_management.filters.pay_period',
          value: '1. 11. 2026 – 14. 11. 2026',
        },
      ]);

      facade.filters.removeChip('payPeriodId');

      expect(invoiceClient.getPaged.mock.lastCall?.[PAY_PERIOD_SLOT]).toBeUndefined();
      expect(facade.filters.chips()).toEqual([]);
    });

    // The period page's View invoices link opens the list here; an unfiltered first load would show
    // every period's invoices until the filter landed.
    it('opens on one period with its first and only load already filtered to it', () => {
      jest.useFakeTimers();

      facade.showPayPeriod('period-1');
      expect(invoiceClient.getPaged).not.toHaveBeenCalled();
      jest.advanceTimersByTime(500);

      expect(invoiceClient.getPaged).toHaveBeenCalledTimes(1);
      expect(invoiceClient.getPaged.mock.lastCall?.[PAY_PERIOD_SLOT]).toBe('period-1');
      expect(facade.filterForm.value.payPeriodId).toBe('period-1');
    });

    it('offers no period when the read fails', () => {
      getPagedPeriodsMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadPayPeriods();

      expect(facade.payPeriodOptions()).toEqual([]);
    });
  });
});
