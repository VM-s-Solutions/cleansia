import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ApproveInvoiceCommand,
  AssignInvoiceVariableSymbolCommand,
  CancelInvoiceCommand,
  EmployeeInvoiceStatus,
  MarkInvoicePaidCommand,
  RegenerateInvoicePdfCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { InvoiceDetailFacade } from './invoice-detail.facade';

describe('InvoiceDetailFacade', () => {
  let facade: InvoiceDetailFacade;
  let detailsMock: jest.Mock;
  let approveMock: jest.Mock;
  let markPaidMock: jest.Mock;
  let cancelMock: jest.Mock;
  let regenerateMock: jest.Mock;
  let assignVariableSymbolMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let translateParams: Record<string, unknown> | undefined;

  const loaded = { id: 'invoice-1', status: EmployeeInvoiceStatus.Pending };

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of(loaded));
    approveMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    markPaidMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    cancelMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    regenerateMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    assignVariableSymbolMock = jest.fn().mockReturnValue(
      of({
        invoiceId: 'invoice-1',
        variableSymbol: '2026000001',
        pdfBlobUrl: 'https://blob/invoice-1.pdf',
      })
    );
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    translateParams = undefined;

    TestBed.configureTestingModule({
      providers: [
        InvoiceDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminInvoiceClient: {
              details: detailsMock,
              approve: approveMock,
              markPaid: markPaidMock,
              cancel: cancelMock,
              regeneratePdf: regenerateMock,
            },
            adminPayrollClient: {
              assignInvoiceVariableSymbol: assignVariableSymbolMock,
            },
          },
        },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string, params?: Record<string, unknown>) => {
              translateParams = params;
              return k;
            },
            currentLang: 'cs',
          },
        },
      ],
    });

    facade = TestBed.inject(InvoiceDetailFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.invoice()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(facade.actionLoading()).toBe(false);
  });

  it('settles loading and holds nothing when the read fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadInvoiceDetail('invoice-1');

    expect(facade.invoice()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('does nothing at all before an invoice is loaded', () => {
    facade.approveInvoice();
    facade.markAsPaid();
    facade.cancelInvoice('duplicate');
    facade.regeneratePdf();
    facade.assignVariableSymbol();

    expect(approveMock).not.toHaveBeenCalled();
    expect(markPaidMock).not.toHaveBeenCalled();
    expect(cancelMock).not.toHaveBeenCalled();
    expect(regenerateMock).not.toHaveBeenCalled();
    expect(assignVariableSymbolMock).not.toHaveBeenCalled();
  });

  it('re-reads the invoice after an approve lands, and not when it fails', () => {
    facade.loadInvoiceDetail('invoice-1');
    expect(detailsMock).toHaveBeenCalledTimes(1);

    facade.approveInvoice();
    expect(detailsMock).toHaveBeenCalledTimes(2);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.invoice_detail.messages.approve_success'
    );

    approveMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.approveInvoice();
    expect(detailsMock).toHaveBeenCalledTimes(2);
    expect(facade.actionLoading()).toBe(false);
  });

  it('gates the actions off the loaded status', () => {
    facade.loadInvoiceDetail('invoice-1');
    expect(facade.canApprove()).toBe(true);
    expect(facade.canMarkPaid()).toBe(false);
    expect(facade.canCancel()).toBe(true);
    expect(facade.canDownload()).toBe(false);

    detailsMock.mockReturnValue(
      of({ id: 'invoice-1', status: EmployeeInvoiceStatus.Approved })
    );
    facade.loadInvoiceDetail('invoice-1');
    expect(facade.canApprove()).toBe(false);
    expect(facade.canMarkPaid()).toBe(true);
  });

  describe('the payment-reference gate', () => {
    const assignable = [
      EmployeeInvoiceStatus.Pending,
      EmployeeInvoiceStatus.Approved,
      EmployeeInvoiceStatus.Disputed,
    ];
    const refused = [
      EmployeeInvoiceStatus.Paid,
      EmployeeInvoiceStatus.Rejected,
      EmployeeInvoiceStatus.Cancelled,
    ];

    it.each(assignable)('offers the assignment in status %s', (status) => {
      detailsMock.mockReturnValue(of({ id: 'invoice-1', status }));
      facade.loadInvoiceDetail('invoice-1');

      expect(facade.canAssignVariableSymbol()).toBe(true);
    });

    it.each(refused)('withholds the assignment in status %s', (status) => {
      detailsMock.mockReturnValue(of({ id: 'invoice-1', status }));
      facade.loadInvoiceDetail('invoice-1');

      expect(facade.canAssignVariableSymbol()).toBe(false);
    });

    it('withholds the assignment once a symbol is on the row', () => {
      detailsMock.mockReturnValue(
        of({
          id: 'invoice-1',
          status: EmployeeInvoiceStatus.Approved,
          variableSymbol: '2026000001',
        })
      );
      facade.loadInvoiceDetail('invoice-1');

      expect(facade.canAssignVariableSymbol()).toBe(false);
    });

    it('reads an empty symbol as no symbol', () => {
      detailsMock.mockReturnValue(
        of({
          id: 'invoice-1',
          status: EmployeeInvoiceStatus.Approved,
          variableSymbol: '',
        })
      );
      facade.loadInvoiceDetail('invoice-1');

      expect(facade.canAssignVariableSymbol()).toBe(true);
    });

    it('offers nothing before an invoice is loaded', () => {
      expect(facade.canAssignVariableSymbol()).toBe(false);
    });
  });

  describe('assigning a payment reference', () => {
    beforeEach(() => facade.loadInvoiceDetail('invoice-1'));

    it('serializes the assignment with the invoice id and the active UI language', () => {
      facade.assignVariableSymbol();

      const command: AssignInvoiceVariableSymbolCommand =
        assignVariableSymbolMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(AssignInvoiceVariableSymbolCommand);
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        languageCode: 'cs',
      });
    });

    it('re-reads the invoice so the displayed symbol is the stored one', () => {
      expect(detailsMock).toHaveBeenCalledTimes(1);

      facade.assignVariableSymbol();

      expect(detailsMock).toHaveBeenCalledTimes(2);
      expect(facade.actionLoading()).toBe(false);
    });

    it('names the allocated symbol in the success message', () => {
      facade.assignVariableSymbol();

      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.invoice_detail.messages.assign_variable_symbol_success'
      );
      expect(translateParams).toEqual({ variableSymbol: '2026000001' });
    });

    it('reports a durable reference on a stale document when the PDF did not regenerate', () => {
      assignVariableSymbolMock.mockReturnValue(
        of({
          invoiceId: 'invoice-1',
          variableSymbol: '2026000002',
          pdfBlobUrl: undefined,
        })
      );

      facade.assignVariableSymbol();

      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(snackbar.showError).toHaveBeenCalledWith(
        'pages.invoice_detail.messages.assign_variable_symbol_pdf_stale'
      );
      expect(translateParams).toEqual({ variableSymbol: '2026000002' });
      expect(detailsMock).toHaveBeenCalledTimes(2);
    });

    it('sends nothing for an invoice carrying no id', () => {
      detailsMock.mockReturnValue(
        of({ status: EmployeeInvoiceStatus.Approved })
      );
      facade.loadInvoiceDetail('invoice-1');

      facade.assignVariableSymbol();

      expect(assignVariableSymbolMock).not.toHaveBeenCalled();
    });

    it('settles and re-reads nothing when the assignment is refused', () => {
      assignVariableSymbolMock.mockReturnValue(
        throwError(() => new Error('payroll.invoice.reference_already_assigned'))
      );

      facade.assignVariableSymbol();

      expect(detailsMock).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(facade.actionLoading()).toBe(false);
    });
  });

  it('renders an absent amount and date as a dash', () => {
    expect(facade.formatCurrency(null)).toBe('-');
    expect(facade.formatCurrency(12.5)).toBe('12.50 CZK');
    expect(facade.formatCurrency(12.5, 'EUR')).toBe('12.50 EUR');
    expect(facade.formatDate(null)).toBe('-');
    expect(facade.formatDateTime(undefined)).toBe('-');
  });

  describe('command bodies on the wire', () => {
    beforeEach(() => facade.loadInvoiceDetail('invoice-1'));

    it('serializes an approve with the invoice id and no admin notes', () => {
      facade.approveInvoice();

      const command: ApproveInvoiceCommand = approveMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(ApproveInvoiceCommand);
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        adminNotes: undefined,
      });
    });

    it('serializes a mark-paid carrying the bank transfer note', () => {
      facade.markAsPaid('FIO 2026-08-05');

      const command: MarkInvoicePaidCommand = markPaidMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(MarkInvoicePaidCommand);
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        bankTransferNote: 'FIO 2026-08-05',
        adminNotes: undefined,
      });
    });

    it('leaves the bank transfer note undefined when the caller omits it', () => {
      facade.markAsPaid();

      const command: MarkInvoicePaidCommand = markPaidMock.mock.calls[0][0];
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        bankTransferNote: undefined,
        adminNotes: undefined,
      });
    });

    it('serializes a cancel with the invoice id and the reason', () => {
      facade.cancelInvoice('duplicate invoice');

      const command: CancelInvoiceCommand = cancelMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CancelInvoiceCommand);
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        reason: 'duplicate invoice',
      });
    });

    it('serializes a PDF regeneration in the active UI language', () => {
      facade.regeneratePdf();

      const command: RegenerateInvoicePdfCommand =
        regenerateMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(RegenerateInvoicePdfCommand);
      expect(command.toJSON()).toEqual({
        invoiceId: 'invoice-1',
        languageCode: 'cs',
      });
    });
  });
});
