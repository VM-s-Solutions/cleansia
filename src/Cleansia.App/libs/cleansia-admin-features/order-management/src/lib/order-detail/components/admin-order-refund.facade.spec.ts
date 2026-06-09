import { TestBed } from '@angular/core/testing';
import {
  AdminRefundClient,
  IssuePartialRefundResponse,
  PaymentStatus,
  RefundReason,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminOrderRefundFacade } from './admin-order-refund.facade';
import { RefundLineOption } from './admin-order-refund.models';

describe('AdminOrderRefundFacade', () => {
  let facade: AdminOrderRefundFacade;
  let refundClient: { partial: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const serviceLine: RefundLineOption = {
    kind: 'service',
    id: 'service-1',
    name: 'Deep clean',
    price: null,
    selected: false,
  };
  const bundledLine: RefundLineOption = {
    kind: 'bundled',
    id: 'service-2',
    name: 'Window cleaning',
    price: null,
    selected: false,
    packageId: 'package-1',
  };

  const successResponse = IssuePartialRefundResponse.fromJS({
    orderId: 'order-1',
    refundAmount: 500,
    refundVat: 105,
    paymentStatus: PaymentStatus.PartiallyRefunded,
    refundInitiated: true,
    windowOverridden: false,
  });

  beforeEach(() => {
    refundClient = { partial: jest.fn() };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminOrderRefundFacade,
        { provide: AdminRefundClient, useValue: refundClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(AdminOrderRefundFacade);
    facade.setLines([{ ...serviceLine }, { ...bundledLine }]);
  });

  it('cannot submit until a line and a reason are selected', () => {
    expect(facade.canSubmit()).toBe(false);

    facade.toggleLine('service-1', true);
    expect(facade.canSubmit()).toBe(false);

    facade.setReason(RefundReason.CustomerCancellation);
    expect(facade.canSubmit()).toBe(true);
  });

  it('submits selected lines, reason and override reason as a typed command', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.toggleLine('service-2', true);
    facade.setReason(RefundReason.ServiceNotRendered);
    facade.setOverrideReason('  forced after window  ');

    const onSuccess = jest.fn();
    facade.submit('order-1', onSuccess);

    expect(refundClient.partial).toHaveBeenCalledTimes(1);
    const command = refundClient.partial.mock.calls[0][0];
    expect(command.orderId).toBe('order-1');
    expect(command.reason).toBe(RefundReason.ServiceNotRendered);
    expect(command.overrideReason).toBe('forced after window');
    expect(command.lines).toEqual([
      expect.objectContaining({ serviceId: 'service-1', packageId: undefined }),
      expect.objectContaining({ serviceId: 'service-2', packageId: 'package-1' }),
    ]);
  });

  it('emits a standalone service line with a serviceId and no packageId', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.CustomerCancellation);

    facade.submit('order-1', jest.fn());

    const command = refundClient.partial.mock.calls[0][0];
    expect(command.lines).toEqual([
      expect.objectContaining({ serviceId: 'service-1', packageId: undefined }),
    ]);
  });

  it('emits a bundled service line with both a serviceId and a packageId', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-2', true);
    facade.setReason(RefundReason.DisputeResolution);

    facade.submit('order-1', jest.fn());

    const command = refundClient.partial.mock.calls[0][0];
    expect(command.lines).toEqual([
      expect.objectContaining({ serviceId: 'service-2', packageId: 'package-1' }),
    ]);
  });

  it('never emits a line with an empty serviceId', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.toggleLine('service-2', true);
    facade.setReason(RefundReason.AdminDiscretion);

    facade.submit('order-1', jest.fn());

    const command = refundClient.partial.mock.calls[0][0];
    for (const line of command.lines) {
      expect(line.serviceId).toBeTruthy();
    }
  });

  it('omits an empty override reason from the command', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.AdminDiscretion);

    facade.submit('order-1', jest.fn());

    const command = refundClient.partial.mock.calls[0][0];
    expect(command.overrideReason).toBeUndefined();
  });

  it('toggles loading while the request is in flight and clears it on success', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.CustomerCancellation);

    expect(facade.submitting()).toBe(false);
    facade.submit('order-1', jest.fn());
    expect(facade.submitting()).toBe(false);
  });

  it('shows a success toast and resets state on success', () => {
    refundClient.partial.mockReturnValue(of(successResponse));
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.CustomerCancellation);
    facade.setOverrideReason('note');

    const onSuccess = jest.fn();
    facade.submit('order-1', onSuccess);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.order_management.refund.success'
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(facade.hasSelection()).toBe(false);
    expect(facade.reason()).toBeNull();
    expect(facade.overrideReason()).toBe('');
    expect(facade.errorKey()).toBeNull();
  });

  it('maps a known backend error code to its translation key', () => {
    refundClient.partial.mockReturnValue(
      throwError(() => ({
        result: { detail: 'refund.override_reason_required' },
      }))
    );
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.CustomerCancellation);

    facade.submit('order-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.refund.override_reason_required');
    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.refund.override_reason_required'
    );
    expect(facade.submitting()).toBe(false);
  });

  it('parses the error code from a JSON response string', () => {
    refundClient.partial.mockReturnValue(
      throwError(() => ({
        response: JSON.stringify({ detail: 'refund.nothing_refundable' }),
      }))
    );
    facade.toggleLine('service-2', true);
    facade.setReason(RefundReason.DisputeResolution);

    facade.submit('order-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.refund.nothing_refundable');
  });

  it('falls back to the generic refund error for unknown codes', () => {
    refundClient.partial.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unexpected' } }))
    );
    facade.toggleLine('service-1', true);
    facade.setReason(RefundReason.CustomerCancellation);

    facade.submit('order-1', jest.fn());

    expect(facade.errorKey()).toBe('errors.refund.failed');
    expect(snackbar.showError).toHaveBeenCalledWith('errors.refund.failed');
  });

  it('does not call the client when nothing is selected', () => {
    facade.setReason(RefundReason.CustomerCancellation);
    facade.submit('order-1', jest.fn());
    expect(refundClient.partial).not.toHaveBeenCalled();
  });
});
