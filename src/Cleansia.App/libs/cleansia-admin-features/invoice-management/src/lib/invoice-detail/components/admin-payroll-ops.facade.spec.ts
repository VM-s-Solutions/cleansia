import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  DisputeInvoiceResponse,
  RejectInvoiceResponse,
  UpdateInvoiceAmountsResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminPayrollOpsFacade } from './admin-payroll-ops.facade';

describe('AdminPayrollOpsFacade', () => {
  let facade: AdminPayrollOpsFacade;
  let payrollClient: {
    updateInvoiceAmounts: jest.Mock;
    disputeInvoice: jest.Mock;
    rejectInvoice: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const adjustResponse = UpdateInvoiceAmountsResponse.fromJS({
    invoiceId: 'invoice-1',
  });
  const disputeResponse = DisputeInvoiceResponse.fromJS({
    invoiceId: 'invoice-1',
  });
  const rejectResponse = RejectInvoiceResponse.fromJS({
    invoiceId: 'invoice-1',
  });

  beforeEach(() => {
    payrollClient = {
      updateInvoiceAmounts: jest.fn(),
      disputeInvoice: jest.fn(),
      rejectInvoice: jest.fn(),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminPayrollOpsFacade,
        {
          provide: AdminClient,
          useValue: { adminPayrollClient: payrollClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(AdminPayrollOpsFacade);
  });

  it('toggles panels and resets inputs when switching', () => {
    facade.setDisputeNotes('stale note');
    facade.openPanel('dispute');
    expect(facade.activePanel()).toBe('dispute');
    expect(facade.disputeNotes()).toBe('');

    facade.openPanel('reject');
    expect(facade.activePanel()).toBe('reject');

    facade.openPanel('reject');
    expect(facade.activePanel()).toBeNull();
  });

  it('seeds the adjust panel from the current invoice amounts', () => {
    facade.openAdjustPanel(150, 25.5);
    expect(facade.activePanel()).toBe('adjust');
    expect(facade.bonusAmount()).toBe('150');
    expect(facade.deductionAmount()).toBe('25.5');
  });

  it('closes the adjust panel when toggled a second time', () => {
    facade.openAdjustPanel(0, 0);
    facade.openAdjustPanel(0, 0);
    expect(facade.activePanel()).toBeNull();
  });

  it('gates the adjust submit on non-negative numeric amounts', () => {
    facade.openAdjustPanel(0, 0);
    expect(facade.canSubmitAdjust()).toBe(true);

    facade.setBonusAmount('-1');
    expect(facade.canSubmitAdjust()).toBe(false);

    facade.setBonusAmount('100');
    facade.setDeductionAmount('abc');
    expect(facade.canSubmitAdjust()).toBe(false);

    facade.setDeductionAmount('');
    expect(facade.canSubmitAdjust()).toBe(false);

    facade.setDeductionAmount('12.34');
    expect(facade.canSubmitAdjust()).toBe(true);
  });

  it('builds a typed update-amounts command with parsed amounts and trimmed optional notes', () => {
    payrollClient.updateInvoiceAmounts.mockReturnValue(of(adjustResponse));
    facade.openAdjustPanel(0, 0);
    facade.setBonusAmount('150');
    facade.setDeductionAmount('25.5');
    facade.setAdjustNotes('  weekend bonus  ');

    facade.adjustAmounts('invoice-1', jest.fn());

    const command = payrollClient.updateInvoiceAmounts.mock.calls[0][0];
    expect(command.invoiceId).toBe('invoice-1');
    expect(command.bonusAmount).toBe(150);
    expect(command.deductionAmount).toBe(25.5);
    expect(command.adminNotes).toBe('weekend bonus');
  });

  it('omits empty adjust notes from the command', () => {
    payrollClient.updateInvoiceAmounts.mockReturnValue(of(adjustResponse));
    facade.openAdjustPanel(10, 0);

    facade.adjustAmounts('invoice-1', jest.fn());

    const command = payrollClient.updateInvoiceAmounts.mock.calls[0][0];
    expect(command.adminNotes).toBeUndefined();
  });

  it('does not call update-amounts when an amount is invalid', () => {
    facade.openAdjustPanel(0, 0);
    facade.setBonusAmount('not-a-number');

    facade.adjustAmounts('invoice-1', jest.fn());

    expect(payrollClient.updateInvoiceAmounts).not.toHaveBeenCalled();
  });

  it('requires dispute notes before submitting', () => {
    expect(facade.canSubmitDispute()).toBe(false);

    facade.disputeInvoice('invoice-1', jest.fn());
    expect(payrollClient.disputeInvoice).not.toHaveBeenCalled();

    facade.setDisputeNotes('  amounts contested  ');
    expect(facade.canSubmitDispute()).toBe(true);
  });

  it('builds a typed dispute command with trimmed notes', () => {
    payrollClient.disputeInvoice.mockReturnValue(of(disputeResponse));
    facade.setDisputeNotes('  amounts contested  ');

    facade.disputeInvoice('invoice-1', jest.fn());

    const command = payrollClient.disputeInvoice.mock.calls[0][0];
    expect(command.invoiceId).toBe('invoice-1');
    expect(command.adminNotes).toBe('amounts contested');
  });

  it('requires reject notes and builds a typed reject command', () => {
    payrollClient.rejectInvoice.mockReturnValue(of(rejectResponse));
    expect(facade.canSubmitReject()).toBe(false);

    facade.rejectInvoice('invoice-1', jest.fn());
    expect(payrollClient.rejectInvoice).not.toHaveBeenCalled();

    facade.setRejectNotes('  duplicate invoice  ');
    facade.rejectInvoice('invoice-1', jest.fn());

    const command = payrollClient.rejectInvoice.mock.calls[0][0];
    expect(command.invoiceId).toBe('invoice-1');
    expect(command.adminNotes).toBe('duplicate invoice');
  });

  it('shows a success toast, closes the panel and re-loads on success', () => {
    payrollClient.disputeInvoice.mockReturnValue(of(disputeResponse));
    facade.openPanel('dispute');
    facade.setDisputeNotes('contested');
    const onSuccess = jest.fn();

    facade.disputeInvoice('invoice-1', onSuccess);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.invoice_detail.ops.dispute.success'
    );
    expect(facade.activePanel()).toBeNull();
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.errorKey()).toBeNull();
  });

  it('toggles submitting off once the request settles', () => {
    payrollClient.rejectInvoice.mockReturnValue(of(rejectResponse));
    facade.setRejectNotes('duplicate');
    expect(facade.submitting()).toBe(false);
    facade.rejectInvoice('invoice-1', jest.fn());
    expect(facade.submitting()).toBe(false);
  });

  it('maps the already-paid backend code to its translation key', () => {
    payrollClient.updateInvoiceAmounts.mockReturnValue(
      throwError(() => ({
        result: { detail: 'payroll.invoice.already_paid' },
      }))
    );
    facade.openAdjustPanel(10, 0);

    facade.adjustAmounts('invoice-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.payroll.invoice.already_paid');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.payroll.invoice.already_paid'
    );
    expect(facade.submitting()).toBe(false);
  });

  it('falls back to result.title when detail is absent', () => {
    payrollClient.updateInvoiceAmounts.mockReturnValue(
      throwError(() => ({ result: { title: 'payroll.invoice.already_paid' } }))
    );
    facade.openAdjustPanel(10, 0);

    facade.adjustAmounts('invoice-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.payroll.invoice.already_paid');
  });

  it('parses the error code from a JSON response string', () => {
    payrollClient.disputeInvoice.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'payroll.invoice.not_found' }),
      }))
    );
    facade.setDisputeNotes('contested');

    facade.disputeInvoice('invoice-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.payroll.invoice.not_found');
  });

  it('falls back to a generic error for unknown codes and does not re-load', () => {
    payrollClient.rejectInvoice.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    facade.setRejectNotes('duplicate');
    const onSuccess = jest.fn();

    facade.rejectInvoice('invoice-1', onSuccess);

    expect(facade.errorKey()).toBe('errors.common.error_occurred');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
