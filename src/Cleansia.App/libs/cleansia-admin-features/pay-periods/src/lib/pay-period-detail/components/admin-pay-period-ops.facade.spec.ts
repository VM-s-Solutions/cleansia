import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ClosePayPeriodCommand,
  ClosePayPeriodResponse,
  MarkPayPeriodPaidCommand,
  MarkPayPeriodPaidResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { AdminPayPeriodOpsFacade } from './admin-pay-period-ops.facade';

// The keys a refusal resolves to; anything else falls back, as an untranslated code does in the app.
const TRANSLATED = new Set([
  'api.pay_period.not_closed',
  'api.pay_period.not_started',
  'api.payroll.pay_period.not_found',
]);

describe('AdminPayPeriodOpsFacade', () => {
  let facade: AdminPayPeriodOpsFacade;
  let payPeriodClient: { markPaid: jest.Mock; close: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const markPaidResponse = MarkPayPeriodPaidResponse.fromJS({
    payPeriodId: 'period-1',
  });
  const closeResponse = ClosePayPeriodResponse.fromJS({
    payPeriodId: 'period-1',
  });

  beforeEach(() => {
    payPeriodClient = { markPaid: jest.fn(), close: jest.fn() };
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AdminPayPeriodOpsFacade,
        {
          provide: AdminClient,
          useValue: { adminPayPeriodClient: payPeriodClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => (TRANSLATED.has(k) ? `${k} (translated)` : k),
          },
        },
      ],
    });

    facade = TestBed.inject(AdminPayPeriodOpsFacade);
  });

  it('toggles the panel and clears a stale refusal when it opens', () => {
    facade.errorKey.set('api.pay_period.not_closed');

    facade.openPanel('markPaid');
    expect(facade.activePanel()).toBe('markPaid');
    expect(facade.errorKey()).toBeNull();

    facade.openPanel('markPaid');
    expect(facade.activePanel()).toBeNull();
  });

  it('clears a stale reason when the close-now panel opens', () => {
    facade.setCloseNowReason('stale');

    facade.openPanel('closeNow');

    expect(facade.activePanel()).toBe('closeNow');
    expect(facade.closeNowReason()).toBe('');
  });

  describe('close now', () => {
    it('sends the period id and the trimmed reason', () => {
      payPeriodClient.close.mockReturnValue(of(closeResponse));
      facade.setCloseNowReason('  The cleaner leaves the company  ');

      facade.closeNow('period-1', jest.fn());

      const command: ClosePayPeriodCommand =
        payPeriodClient.close.mock.calls[0][0];
      expect(command).toBeInstanceOf(ClosePayPeriodCommand);
      expect(command.toJSON()).toEqual({
        payPeriodId: 'period-1',
        reason: 'The cleaner leaves the company',
      });
    });

    it('sends nothing while the reason is blank', () => {
      facade.setCloseNowReason('   ');

      facade.closeNow('period-1', jest.fn());

      expect(payPeriodClient.close).not.toHaveBeenCalled();
      expect(facade.canSubmitCloseNow()).toBe(false);
    });

    it('lands a refusal in errorKey, keeps the panel and does not re-load', () => {
      payPeriodClient.close.mockReturnValue(
        throwError(() => ({ result: { detail: 'pay_period.not_started' } }))
      );
      facade.openPanel('closeNow');
      facade.setCloseNowReason('Agreement ended');
      const onSuccess = jest.fn();

      facade.closeNow('period-1', onSuccess);

      expect(facade.errorKey()).toBe('api.pay_period.not_started');
      expect(facade.activePanel()).toBe('closeNow');
      expect(onSuccess).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    });

    it('toasts, closes the panel and re-loads once on success', () => {
      payPeriodClient.close.mockReturnValue(of(closeResponse));
      facade.openPanel('closeNow');
      facade.setCloseNowReason('Agreement ended');
      const onSuccess = jest.fn();

      facade.closeNow('period-1', onSuccess);

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pay_periods.messages.close_success'
      );
      expect(facade.activePanel()).toBeNull();
      expect(facade.closeNowReason()).toBe('');
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('holds submitting while the close is in flight', () => {
      const response$ = new Subject<ClosePayPeriodResponse>();
      payPeriodClient.close.mockReturnValue(response$);
      facade.setCloseNowReason('Agreement ended');
      expect(facade.canSubmitCloseNow()).toBe(true);

      facade.closeNow('period-1', jest.fn());
      expect(facade.submitting()).toBe(true);
      expect(facade.canSubmitCloseNow()).toBe(false);

      response$.next(closeResponse);
      response$.complete();
      expect(facade.submitting()).toBe(false);
    });
  });

  it('builds a mark-paid command carrying just the pay period id', () => {
    payPeriodClient.markPaid.mockReturnValue(of(markPaidResponse));

    facade.markPaid('period-1', jest.fn());

    const command: MarkPayPeriodPaidCommand =
      payPeriodClient.markPaid.mock.calls[0][0];
    expect(command).toBeInstanceOf(MarkPayPeriodPaidCommand);
    expect(command.toJSON()).toEqual({ payPeriodId: 'period-1' });
  });

  it('shows a success toast, closes the panel and re-loads on success', () => {
    payPeriodClient.markPaid.mockReturnValue(of(markPaidResponse));
    facade.openPanel('markPaid');
    const onSuccess = jest.fn();

    facade.markPaid('period-1', onSuccess);

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pay_periods.detail.ops.mark_paid.success'
    );
    expect(facade.activePanel()).toBeNull();
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.errorKey()).toBeNull();
  });

  it('toggles submitting off once the request settles', () => {
    payPeriodClient.markPaid.mockReturnValue(of(markPaidResponse));
    expect(facade.submitting()).toBe(false);
    facade.markPaid('period-1', jest.fn());
    expect(facade.submitting()).toBe(false);
  });

  it('maps the not-closed backend code to its translation key', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({ result: { detail: 'pay_period.not_closed' } }))
    );

    facade.markPaid('period-1', jest.fn());

    expect(facade.errorKey()).toBe('api.pay_period.not_closed');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('falls back to result.title when detail is absent', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({ result: { title: 'pay_period.not_closed' } }))
    );

    facade.markPaid('period-1', jest.fn());

    expect(facade.errorKey()).toBe('api.pay_period.not_closed');
  });

  it('parses the error code from a JSON response string', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'payroll.pay_period.not_found' }),
      }))
    );

    facade.markPaid('period-1', jest.fn());

    expect(facade.errorKey()).toBe('api.payroll.pay_period.not_found');
  });

  it('falls back to a generic error for unknown codes and does not re-load', () => {
    payPeriodClient.markPaid.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    const onSuccess = jest.fn();

    facade.markPaid('period-1', onSuccess);

    expect(facade.errorKey()).toBe('api.common.error_occurred');
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
