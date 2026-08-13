import { TestBed } from '@angular/core/testing';
import {
  AdminCancelOrderCommand,
  AdminCancelOrderResponse,
  AdminClient,
  AdminOverrideOrderStatusCommand,
  AdminOverrideOrderStatusResponse,
  AdminReassignOrderCommand,
  AdminReassignOrderResponse,
  AdminRefundOrderCommand,
  AdminRefundOrderResponse,
  OrderStatus,
  PaymentStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminOrderOpsFacade } from './admin-order-ops.facade';

describe('AdminOrderOpsFacade', () => {
  let facade: AdminOrderOpsFacade;
  let orderClient: {
    cancel: jest.Mock;
    overrideStatus: jest.Mock;
    reassign: jest.Mock;
    refund: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

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

  beforeEach(() => {
    orderClient = {
      cancel: jest.fn(),
      overrideStatus: jest.fn(),
      reassign: jest.fn(),
      refund: jest.fn(),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminOrderOpsFacade,
        {
          provide: AdminClient,
          useValue: { adminOrderClient: orderClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
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

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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
    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.order.invalid_status_transition'
    );
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
    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.common.error_occurred'
    );
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
