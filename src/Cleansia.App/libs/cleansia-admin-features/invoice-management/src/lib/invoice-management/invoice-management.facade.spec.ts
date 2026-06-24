import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  EmployeeInvoiceDto,
  PagedDataOfEmployeeInvoiceDto,
  RegenerateInvoicePdfResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { InvoiceManagementFacade } from './invoice-management.facade';

describe('InvoiceManagementFacade', () => {
  let facade: InvoiceManagementFacade;
  let invoiceClient: {
    getPaged: jest.Mock;
    download: jest.Mock;
    regeneratePdf: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

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
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        InvoiceManagementFacade,
        {
          provide: AdminClient,
          useValue: { adminInvoiceClient: invoiceClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs' },
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
    const command = invoiceClient.regeneratePdf.mock.calls[0][0];
    expect(command.invoiceId).toBe('invoice-1');
    expect(command.languageCode).toBe('cs');
  });

  it('does not call regenerate without an invoice id', () => {
    facade.retryPdf(EmployeeInvoiceDto.fromJS({}));
    expect(invoiceClient.regeneratePdf).not.toHaveBeenCalled();
  });

  it('shows a success toast and re-loads the list after a retry', () => {
    invoiceClient.regeneratePdf.mockReturnValue(of(regenerateResponse));

    facade.retryPdf(invoice);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.invoice_management.messages.retry_pdf_success'
    );
    expect(invoiceClient.getPaged).toHaveBeenCalledTimes(1);
    expect(facade.retryingPdf()).toBe(false);
  });

  it('maps a known backend error code to its translation key on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { detail: 'payroll.invoice.not_found' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.payroll.invoice.not_found'
    );
    expect(invoiceClient.getPaged).not.toHaveBeenCalled();
    expect(facade.retryingPdf()).toBe(false);
  });

  it('falls back to result.title when detail is absent on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { title: 'payroll.invoice.not_found' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.payroll.invoice.not_found'
    );
  });

  it('parses the error code from a JSON response string on retry failure', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'company.not_found' }),
      }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showError).toHaveBeenCalledWith('errors.company.not_found');
  });

  it('falls back to a generic error for unknown retry failures', () => {
    invoiceClient.regeneratePdf.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );

    facade.retryPdf(invoice);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
  });
});
