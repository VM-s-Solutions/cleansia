import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ApproveInvoiceCommand,
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
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const loaded = { id: 'invoice-1', status: EmployeeInvoiceStatus.Pending };

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of(loaded));
    approveMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    markPaidMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    cancelMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    regenerateMock = jest.fn().mockReturnValue(of({ invoiceId: 'invoice-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

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
          },
        },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs' },
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

    expect(approveMock).not.toHaveBeenCalled();
    expect(markPaidMock).not.toHaveBeenCalled();
    expect(cancelMock).not.toHaveBeenCalled();
    expect(regenerateMock).not.toHaveBeenCalled();
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
