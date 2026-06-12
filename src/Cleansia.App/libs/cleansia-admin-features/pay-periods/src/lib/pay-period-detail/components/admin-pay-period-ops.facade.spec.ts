import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  MarkPayPeriodPaidResponse,
  ReopenPayPeriodResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminPayPeriodOpsFacade } from './admin-pay-period-ops.facade';

describe('AdminPayPeriodOpsFacade', () => {
  let facade: AdminPayPeriodOpsFacade;
  let payPeriodClient: { markPaid: jest.Mock; reopen: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const markPaidResponse = MarkPayPeriodPaidResponse.fromJS({
    payPeriodId: 'period-1',
  });
  const reopenResponse = ReopenPayPeriodResponse.fromJS({
    payPeriodId: 'period-1',
  });

  beforeEach(() => {
    payPeriodClient = { markPaid: jest.fn(), reopen: jest.fn() };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminPayPeriodOpsFacade,
        {
          provide: AdminClient,
          useValue: { adminPayPeriodClient: payPeriodClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(AdminPayPeriodOpsFacade);
  });

  it('toggles panels and resets inputs when switching', () => {
    facade.setReopenNotes('stale');
    facade.openPanel('reopen');
    expect(facade.activePanel()).toBe('reopen');
    expect(facade.reopenNotes()).toBe('');

    facade.openPanel('markPaid');
    expect(facade.activePanel()).toBe('markPaid');

    facade.openPanel('markPaid');
    expect(facade.activePanel()).toBeNull();
  });

  it('builds a mark-paid command carrying just the pay period id', () => {
    payPeriodClient.markPaid.mockReturnValue(of(markPaidResponse));

    facade.markPaid('period-1', jest.fn());

    const command = payPeriodClient.markPaid.mock.calls[0][0];
    expect(command.payPeriodId).toBe('period-1');
    expect(Object.keys(command.toJSON())).toEqual(['payPeriodId']);
  });

  it('builds a typed reopen command with trimmed optional notes', () => {
    payPeriodClient.reopen.mockReturnValue(of(reopenResponse));
    facade.setReopenNotes('  correction needed  ');

    facade.reopen('period-1', jest.fn());

    const command = payPeriodClient.reopen.mock.calls[0][0];
    expect(command.payPeriodId).toBe('period-1');
    expect(command.notes).toBe('correction needed');
  });

  it('omits empty reopen notes from the command', () => {
    payPeriodClient.reopen.mockReturnValue(of(reopenResponse));

    facade.reopen('period-1', jest.fn());

    const command = payPeriodClient.reopen.mock.calls[0][0];
    expect(command.notes).toBeUndefined();
  });

  it('shows a success toast, closes the panel and re-loads on success', () => {
    payPeriodClient.markPaid.mockReturnValue(of(markPaidResponse));
    facade.openPanel('markPaid');
    const onSuccess = jest.fn();

    facade.markPaid('period-1', onSuccess);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pay_periods.detail.ops.mark_paid.success'
    );
    expect(facade.activePanel()).toBeNull();
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.errorKey()).toBeNull();
  });

  it('toggles submitting off once the request settles', () => {
    payPeriodClient.reopen.mockReturnValue(of(reopenResponse));
    expect(facade.submitting()).toBe(false);
    facade.reopen('period-1', jest.fn());
    expect(facade.submitting()).toBe(false);
  });

  it('maps the not-closed backend code to its translation key', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({ result: { detail: 'pay_period.not_closed' } }))
    );

    facade.markPaid('period-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.pay_period.not_closed');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.pay_period.not_closed'
    );
  });

  it('maps the already-paid backend code on a reopen failure', () => {
    payPeriodClient.reopen.mockReturnValue(
      throwError(() => ({ result: { detail: 'pay_period.already_paid' } }))
    );

    facade.reopen('period-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.pay_period.already_paid');
  });

  it('parses the error code from a JSON response string', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'payroll.pay_period.not_found' }),
      }))
    );

    facade.markPaid('period-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.payroll.pay_period.not_found');
  });

  it('falls back to a generic error for unknown codes and does not re-load', () => {
    payPeriodClient.reopen.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    const onSuccess = jest.fn();

    facade.reopen('period-1', onSuccess);

    expect(facade.errorKey()).toBe('errors.common.error_occurred');
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
