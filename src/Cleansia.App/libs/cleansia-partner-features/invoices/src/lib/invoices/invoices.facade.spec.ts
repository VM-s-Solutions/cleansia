import { TestBed } from '@angular/core/testing';
import {
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  EmployeeItem,
  PagedDataOfEmployeeInvoiceDto,
  PartnerClient,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { InvoicesFacade } from './invoices.facade';

describe('InvoicesFacade', () => {
  let employeeClient: { getCurrentEmployee: jest.Mock };
  let employeePayrollClient: { getPagedInvoices: jest.Mock; downloadInvoice: jest.Mock };
  let snackbar: { showErrorTranslated: jest.Mock; showSuccessTranslated: jest.Mock };

  const page = PagedDataOfEmployeeInvoiceDto.fromJS({
    data: [
      { id: 'inv-1', invoiceNumber: 'INV-1', status: EmployeeInvoiceStatus.Approved, totalAmount: 1200, currencyCode: 'EUR', generatedAt: '2026-09-01T00:00:00Z' },
      { id: 'inv-2', invoiceNumber: 'INV-2', status: EmployeeInvoiceStatus.Paid, totalAmount: 800, currencyCode: 'CZK', generatedAt: '2026-09-02T00:00:00Z', pdfBlobName: 'inv-2.pdf' },
    ],
    total: 7,
  });

  function create(): InvoicesFacade {
    TestBed.configureTestingModule({
      imports: [TranslateModule.forRoot()],
      providers: [
        InvoicesFacade,
        { provide: PartnerClient, useValue: { employeeClient, employeePayrollClient } },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });
    return TestBed.inject(InvoicesFacade);
  }

  beforeEach(() => {
    employeeClient = { getCurrentEmployee: jest.fn(() => of(EmployeeItem.fromJS({ id: 'emp-1' }))) };
    employeePayrollClient = {
      getPagedInvoices: jest.fn(() => of(page)),
      downloadInvoice: jest.fn(() => of(null)),
    };
    snackbar = { showErrorTranslated: jest.fn(), showSuccessTranslated: jest.fn() };
  });

  it('holds the generated rows as the server sent them, status enum included', () => {
    const facade = create();

    expect(employeePayrollClient.getPagedInvoices).toHaveBeenCalledTimes(1);
    expect(employeePayrollClient.getPagedInvoices.mock.calls[0][0]).toBe('emp-1');
    expect(facade.invoices()).toBe(page.data);
    expect(facade.invoices().map((invoice) => invoice.status)).toEqual([
      EmployeeInvoiceStatus.Approved,
      EmployeeInvoiceStatus.Paid,
    ]);
    expect(facade.totalRecords()).toBe(7);
    expect(facade.loading()).toBe(false);
  });

  it('empties the list when the read fails', () => {
    employeePayrollClient.getPagedInvoices.mockReturnValue(throwError(() => new Error('boom')));
    const facade = create();

    expect(facade.invoices()).toEqual([]);
    expect(facade.totalRecords()).toBe(0);
    expect(facade.loading()).toBe(false);
  });

  it('refuses to download an invoice that has no PDF yet, without calling the server', () => {
    const facade = create();

    facade.downloadInvoice(facade.invoices()[0]);

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith('pages.invoices.pdf_not_available');
    expect(employeePayrollClient.downloadInvoice).not.toHaveBeenCalled();
  });

  it('refuses to download a row that carries a PDF but no id, without calling the server', () => {
    const facade = create();

    facade.downloadInvoice(EmployeeInvoiceDto.fromJS({ invoiceNumber: 'INV-3', pdfBlobName: 'inv-3.pdf' }));

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith('pages.invoices.pdf_not_available');
    expect(employeePayrollClient.downloadInvoice).not.toHaveBeenCalled();
  });

  it('asks the server for the PDF by the invoice id', () => {
    const facade = create();

    facade.downloadInvoice(facade.invoices()[1]);

    expect(employeePayrollClient.downloadInvoice).toHaveBeenCalledWith('inv-2');
  });
});
