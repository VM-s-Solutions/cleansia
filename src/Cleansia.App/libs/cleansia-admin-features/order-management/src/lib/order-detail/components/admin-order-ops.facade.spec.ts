import { TestBed } from '@angular/core/testing';
import {
  AdminCancelOrderAsNoShowCommand,
  AdminCancelOrderAsNoShowResponse,
  AdminCancelOrderCommand,
  AdminCancelOrderResponse,
  AdminClient,
  AdminOverrideOrderStatusCommand,
  AdminOverrideOrderStatusResponse,
  AdminReassignOrderCommand,
  AdminReassignOrderResponse,
  AdminRecordCashReceivedCommand,
  AdminRecordCashReceivedResponse,
  AdminRefundOrderCommand,
  AdminRefundOrderResponse,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { formatMoney } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminOrderOpsFacade } from './admin-order-ops.facade';
import { NO_SHOW_OUTCOME_TOAST_MS } from './admin-order-ops.models';

// The keys a refusal resolves to; anything else falls back, as an untranslated code does in the app.
const TRANSLATED = new Set([
  'api.order.invalid_status_transition',
  'api.order.no_available_spots',
  'api.refund.order_not_refundable',
  'api.order.cleaner_already_started',
  'api.order.cash_received_at_before_clean',
]);

function translated(key: string, params?: Record<string, unknown>): string {
  if (TRANSLATED.has(key)) return `${key} (translated)`;
  return params ? `${key} ${JSON.stringify(params)}` : key;
}

function czk(amount: number): string {
  return formatMoney(amount, 'CZK', 'en-US', { fractionDigits: 2 });
}

describe('AdminOrderOpsFacade', () => {
  let facade: AdminOrderOpsFacade;
  let orderClient: {
    cancel: jest.Mock;
    overrideStatus: jest.Mock;
    reassign: jest.Mock;
    refund: jest.Mock;
    cancelNoShow: jest.Mock;
    recordCash: jest.Mock;
  };
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const cancelResponse = AdminCancelOrderResponse.fromJS({
    orderId: 'order-1',
    refundAmount: 0,
    totalPrice: 1000,
    refundInitiated: false,
  });
  const overrideResponse = AdminOverrideOrderStatusResponse.fromJS({
    orderId: 'order-1',
    status: OrderStatus.OnTheWay,
  });
  const reassignResponse = AdminReassignOrderResponse.fromJS({
    orderId: 'order-1',
    toEmployeeId: 'employee-2',
  });
  const refundResponse = AdminRefundOrderResponse.fromJS({
    orderId: 'order-1',
    refundAmount: 1000,
    paymentStatus: PaymentStatus.Refunded,
    refundInitiated: true,
  });
  const noShowResponse = (
    outcome: Partial<{ refundedAmount: number; refundPending: boolean; apologyCredit: number }>
  ) =>
    AdminCancelOrderAsNoShowResponse.fromJS({
      orderId: 'order-1',
      refundedAmount: null,
      refundPending: false,
      apologyCredit: null,
      ...outcome,
    });
  const recordCashResponse = AdminRecordCashReceivedResponse.fromJS({
    orderId: 'order-1',
    paymentStatus: PaymentStatus.Paid,
  });

  beforeEach(() => {
    orderClient = {
      cancel: jest.fn(),
      overrideStatus: jest.fn(),
      reassign: jest.fn(),
      refund: jest.fn(),
      cancelNoShow: jest.fn(),
      recordCash: jest.fn(),
    };
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AdminOrderOpsFacade,
        {
          provide: AdminClient,
          useValue: { adminOrderClient: orderClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: translated },
        },
      ],
    });

    facade = TestBed.inject(AdminOrderOpsFacade);
  });

  it('toggles panels and resets inputs when switching', () => {
    facade.setCancelReason('typo');
    facade.openPanel('cancel');
    expect(facade.activePanel()).toBe('cancel');

    facade.openPanel('refund');
    expect(facade.activePanel()).toBe('refund');

    facade.openPanel('refund');
    expect(facade.activePanel()).toBeNull();
  });

  it('builds a typed cancel command with a trimmed optional reason', () => {
    orderClient.cancel.mockReturnValue(of(cancelResponse));
    facade.setCancelReason('  duplicate booking  ');

    facade.cancelOrder('order-1', jest.fn());

    expect(orderClient.cancel).toHaveBeenCalledTimes(1);
    const command: AdminCancelOrderCommand = orderClient.cancel.mock.calls[0][0];
    expect(command).toBeInstanceOf(AdminCancelOrderCommand);
    expect(command.toJSON()).toEqual({
      orderId: 'order-1',
      reason: 'duplicate booking',
    });
  });

  it('omits an empty cancel reason from the command', () => {
    orderClient.cancel.mockReturnValue(of(cancelResponse));

    facade.cancelOrder('order-1', jest.fn());

    const command: AdminCancelOrderCommand = orderClient.cancel.mock.calls[0][0];
    expect(command.toJSON()).toEqual({
      orderId: 'order-1',
      reason: undefined,
    });
  });

  it('builds a typed override-status command and gates submit on a chosen status', () => {
    orderClient.overrideStatus.mockReturnValue(of(overrideResponse));
    expect(facade.canSubmitOverrideStatus()).toBe(false);

    facade.setTargetStatus(OrderStatus.OnTheWay);
    expect(facade.canSubmitOverrideStatus()).toBe(true);

    facade.overrideStatus('order-1', jest.fn());

    const command: AdminOverrideOrderStatusCommand =
      orderClient.overrideStatus.mock.calls[0][0];
    expect(command).toBeInstanceOf(AdminOverrideOrderStatusCommand);
    expect(command.toJSON()).toEqual({
      orderId: 'order-1',
      targetStatus: OrderStatus.OnTheWay,
    });
  });

  it('does not call override-status when no status is chosen', () => {
    facade.overrideStatus('order-1', jest.fn());
    expect(orderClient.overrideStatus).not.toHaveBeenCalled();
  });

  it('builds a typed reassign command with from/to employee ids', () => {
    orderClient.reassign.mockReturnValue(of(reassignResponse));
    expect(facade.canSubmitReassign()).toBe(false);

    facade.setFromEmployeeId('employee-1');
    facade.setToEmployeeId('  employee-2  ');
    expect(facade.canSubmitReassign()).toBe(true);

    facade.reassignOrder('order-1', jest.fn());

    const command: AdminReassignOrderCommand =
      orderClient.reassign.mock.calls[0][0];
    expect(command).toBeInstanceOf(AdminReassignOrderCommand);
    expect(command.toJSON()).toEqual({
      orderId: 'order-1',
      fromEmployeeId: 'employee-1',
      toEmployeeId: 'employee-2',
    });
  });

  it('does not call reassign when an employee id is missing', () => {
    facade.setFromEmployeeId('employee-1');
    facade.reassignOrder('order-1', jest.fn());
    expect(orderClient.reassign).not.toHaveBeenCalled();
  });

  it('builds a typed refund-only command carrying just the order id', () => {
    orderClient.refund.mockReturnValue(of(refundResponse));

    facade.refundOrder('order-1', jest.fn());

    const command: AdminRefundOrderCommand = orderClient.refund.mock.calls[0][0];
    expect(command).toBeInstanceOf(AdminRefundOrderCommand);
    expect(command.toJSON()).toEqual({ orderId: 'order-1' });
  });

  it('shows a success toast, closes the panel and re-loads on success', () => {
    orderClient.cancel.mockReturnValue(of(cancelResponse));
    facade.openPanel('cancel');
    const onSuccess = jest.fn();

    facade.cancelOrder('order-1', onSuccess);

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.order_management.ops.cancel.success'
    );
    expect(facade.activePanel()).toBeNull();
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.errorKey()).toBeNull();
  });

  it('toggles loading off once the request settles', () => {
    orderClient.refund.mockReturnValue(of(refundResponse));
    expect(facade.submitting()).toBe(false);
    facade.refundOrder('order-1', jest.fn());
    expect(facade.submitting()).toBe(false);
  });

  it('maps a known backend error code to its translation key', () => {
    orderClient.overrideStatus.mockReturnValue(
      throwError(() => ({
        result: { detail: 'order.invalid_status_transition' },
      }))
    );
    facade.setTargetStatus(OrderStatus.Completed);

    facade.overrideStatus('order-1', jest.fn());

    expect(facade.errorKey()).toBe('api.order.invalid_status_transition');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.submitting()).toBe(false);
  });

  it('falls back to result.title when detail is absent', () => {
    orderClient.overrideStatus.mockReturnValue(
      throwError(() => ({
        result: { title: 'order.invalid_status_transition' },
      }))
    );
    facade.setTargetStatus(OrderStatus.Completed);

    facade.overrideStatus('order-1', jest.fn());

    expect(facade.errorKey()).toBe('api.order.invalid_status_transition');
  });

  it('parses the error code from a JSON response string', () => {
    orderClient.reassign.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'order.no_available_spots' }),
      }))
    );
    facade.setFromEmployeeId('employee-1');
    facade.setToEmployeeId('employee-2');

    facade.reassignOrder('order-1', jest.fn());

    expect(facade.errorKey()).toBe('api.order.no_available_spots');
  });

  it('maps the refund-not-refundable code on a refund failure', () => {
    orderClient.refund.mockReturnValue(
      throwError(() => ({ result: { detail: 'refund.order_not_refundable' } }))
    );

    facade.refundOrder('order-1', jest.fn());

    expect(facade.errorKey()).toBe('api.refund.order_not_refundable');
  });

  it('falls back to a generic error for unknown codes and does not re-load', () => {
    orderClient.cancel.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    const onSuccess = jest.fn();

    facade.cancelOrder('order-1', onSuccess);

    expect(facade.errorKey()).toBe('api.common.error_occurred');
    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(onSuccess).not.toHaveBeenCalled();
  });
  describe('cancel as cleaner no-show', () => {
    it('builds a typed no-show command carrying just the order id', () => {
      orderClient.cancelNoShow.mockReturnValue(of(noShowResponse({})));

      facade.cancelAsNoShow('order-1', 'CZK', jest.fn());

      const command: AdminCancelOrderAsNoShowCommand = orderClient.cancelNoShow.mock.calls[0][0];
      expect(command).toBeInstanceOf(AdminCancelOrderAsNoShowCommand);
      expect(command.toJSON()).toEqual({ orderId: 'order-1' });
    });

    it('does not call the endpoint without an order id', () => {
      facade.cancelAsNoShow('', 'CZK', jest.fn());
      expect(orderClient.cancelNoShow).not.toHaveBeenCalled();
    });

    it('announces the card refund and the apology credit, closes the panel and re-loads', () => {
      orderClient.cancelNoShow.mockReturnValue(
        of(noShowResponse({ refundedAmount: 1200, apologyCredit: 250 }))
      );
      facade.openPanel('noShow');
      const onSuccess = jest.fn();

      facade.cancelAsNoShow('order-1', 'CZK', onSuccess);

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.order_management.ops.no_show.success',
        {
          refund: translated('pages.order_management.ops.no_show.refunded', { amount: czk(1200) }),
          credit: translated('pages.order_management.ops.no_show.credit_granted', { amount: czk(250) }),
        },
        NO_SHOW_OUTCOME_TOAST_MS
      );
      expect(facade.activePanel()).toBeNull();
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('says the card refund is still pending when it did not go through', () => {
      orderClient.cancelNoShow.mockReturnValue(
        of(noShowResponse({ refundPending: true, apologyCredit: 250 }))
      );

      facade.cancelAsNoShow('order-1', 'CZK', jest.fn());

      const params = snackbar.showSuccessTranslated.mock.calls[0][1];
      expect(params.refund).toBe('pages.order_management.ops.no_show.refund_pending');
      expect(params.credit).toBe(
        translated('pages.order_management.ops.no_show.credit_granted', { amount: czk(250) })
      );
    });

    it('says nothing was refunded and no credit issued when neither happened', () => {
      orderClient.cancelNoShow.mockReturnValue(of(noShowResponse({})));

      facade.cancelAsNoShow('order-1', 'CZK', jest.fn());

      const params = snackbar.showSuccessTranslated.mock.calls[0][1];
      expect(params).toEqual({
        refund: 'pages.order_management.ops.no_show.nothing_refunded',
        credit: 'pages.order_management.ops.no_show.no_credit',
      });
    });

    it('shows the refusal inline and does not re-load when the cleaner already started', () => {
      orderClient.cancelNoShow.mockReturnValue(
        throwError(() => ({ result: { detail: 'order.cleaner_already_started' } }))
      );
      facade.openPanel('noShow');
      const onSuccess = jest.fn();

      facade.cancelAsNoShow('order-1', 'CZK', onSuccess);

      expect(facade.errorKey()).toBe('api.order.cleaner_already_started');
      expect(facade.activePanel()).toBe('noShow');
      expect(facade.submitting()).toBe(false);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(onSuccess).not.toHaveBeenCalled();
    });
  });

  describe('record cash received', () => {
    const receivedAt = new Date('2026-09-28T09:30:00Z');

    function fillRecordCash(amount = ' 1200.50 '): void {
      facade.setCashEmployeeId('employee-1');
      facade.setCashReceivedAt(receivedAt);
      facade.setCashAmount(amount);
    }

    it('enables submit only once the cleaner, the time and the amount are all given', () => {
      expect(facade.canSubmitRecordCash()).toBe(false);

      facade.setCashEmployeeId('employee-1');
      expect(facade.canSubmitRecordCash()).toBe(false);

      facade.setCashReceivedAt(receivedAt);
      expect(facade.canSubmitRecordCash()).toBe(false);

      facade.setCashAmount('1200.50');
      expect(facade.canSubmitRecordCash()).toBe(true);
    });

    it('does not enable submit on an amount that is not a number', () => {
      fillRecordCash('twelve');
      expect(facade.canSubmitRecordCash()).toBe(false);
    });

    it('builds a typed command with who took the cash, when and how much', () => {
      orderClient.recordCash.mockReturnValue(of(recordCashResponse));
      fillRecordCash();

      facade.recordCashReceived('order-1', jest.fn());

      const command: AdminRecordCashReceivedCommand = orderClient.recordCash.mock.calls[0][0];
      expect(command).toBeInstanceOf(AdminRecordCashReceivedCommand);
      expect(command.toJSON()).toEqual({
        orderId: 'order-1',
        employeeId: 'employee-1',
        receivedAt: '2026-09-28T09:30:00.000Z',
        amount: 1200.5,
      });
    });

    it('does not call the endpoint while any field is missing', () => {
      facade.setCashEmployeeId('employee-1');
      facade.setCashAmount('1200');
      facade.recordCashReceived('order-1', jest.fn());

      facade.setCashEmployeeId(null);
      facade.setCashReceivedAt(receivedAt);
      facade.recordCashReceived('order-1', jest.fn());

      fillRecordCash();
      facade.recordCashReceived('', jest.fn());

      expect(orderClient.recordCash).not.toHaveBeenCalled();
    });

    it('confirms, closes the panel, clears the form and re-loads on success', () => {
      orderClient.recordCash.mockReturnValue(of(recordCashResponse));
      facade.openPanel('recordCash');
      fillRecordCash();
      const onSuccess = jest.fn();

      facade.recordCashReceived('order-1', onSuccess);

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.order_management.ops.record_cash.success'
      );
      expect(facade.activePanel()).toBeNull();
      expect(facade.cashEmployeeId()).toBeNull();
      expect(facade.cashReceivedAt()).toBeNull();
      expect(facade.cashAmount()).toBe('');
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('shows the refusal inline, keeps what was typed and does not re-load', () => {
      orderClient.recordCash.mockReturnValue(
        throwError(() => ({ result: { detail: 'order.cash_received_at_before_clean' } }))
      );
      facade.openPanel('recordCash');
      fillRecordCash();
      const onSuccess = jest.fn();

      facade.recordCashReceived('order-1', onSuccess);

      expect(facade.errorKey()).toBe('api.order.cash_received_at_before_clean');
      expect(facade.activePanel()).toBe('recordCash');
      expect(facade.cashAmount()).toBe(' 1200.50 ');
      expect(facade.submitting()).toBe(false);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(onSuccess).not.toHaveBeenCalled();
    });

    it('clears the record-cash form when another panel is opened', () => {
      facade.openPanel('recordCash');
      fillRecordCash();

      facade.openPanel('refund');

      expect(facade.cashEmployeeId()).toBeNull();
      expect(facade.cashReceivedAt()).toBeNull();
      expect(facade.cashAmount()).toBe('');
    });
  });
});
