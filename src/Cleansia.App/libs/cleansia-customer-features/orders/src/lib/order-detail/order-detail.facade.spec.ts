import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CancelOrderCommand,
  CancelOrderResponse,
  CancellationFeeTier,
  CustomerAuthService,
  CustomerClient,
  GetCancellationFeePreviewResponse,
  OrderItem,
  OrderStatus,
  SubmitOrderReviewCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { OrderDetailFacade } from './order-detail.facade';

const ORDER_ID = 'ord-1';

const preview = GetCancellationFeePreviewResponse.fromJS({
  orderId: ORDER_ID,
  tier: CancellationFeeTier.Partial,
  feeRate: 0.25,
  feeAmount: 300,
  refundAmount: 900,
  totalPrice: 1200,
  currencyCode: 'CZK',
  expressWaiverForfeitedOnCancel: false,
});

const cancelled = CancelOrderResponse.fromJS({
  orderId: ORDER_ID,
  feeRate: 0.25,
  refundAmount: 900,
  totalPrice: 1200,
  refundInitiated: true,
  actualRefundAmount: 850,
});

function orderIn(status: OrderStatus): OrderItem {
  return OrderItem.fromJS({ id: ORDER_ID, orderStatus: { value: status, name: OrderStatus[status] } });
}

describe('OrderDetailFacade', () => {
  let orderClient: {
    getById: jest.Mock;
    submitReview: jest.Mock;
    cancellationPreview: jest.Mock;
    cancel: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock; showApiError: jest.Mock };
  let facade: OrderDetailFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      getById: jest.fn().mockReturnValue(of(OrderItem.fromJS({ id: ORDER_ID }))),
      submitReview: jest.fn().mockReturnValue(of({ rating: 5 })),
      cancellationPreview: jest.fn().mockReturnValue(of(preview)),
      cancel: jest.fn().mockReturnValue(of(cancelled)),
    };
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        OrderDetailFacade,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: CustomerClient,
          useValue: {
            orderClient,
            membershipClient: { getMine: jest.fn().mockReturnValue(of(null)) },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => true } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(OrderDetailFacade);
    facade.order.set(OrderItem.fromJS({ id: ORDER_ID }));
  });

  it('never submits an unrated review', () => {
    facade.submitReview(0, 'nothing to say');

    expect(orderClient.submitReview).not.toHaveBeenCalled();
  });

  it('reports success and clears the in-flight flag once the review lands', () => {
    facade.submitReview(5, 'Spotless');

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.order_detail.review.success'
    );
    expect(facade.reviewSubmitting()).toBe(false);
  });

  it('surfaces the API error and clears the in-flight flag when the review is refused', () => {
    const error = new Error('order.review_already_submitted');
    orderClient.submitReview.mockReturnValue(throwError(() => error));

    facade.submitReview(5, 'Spotless');

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      error,
      'pages.order_detail.review.error'
    );
    expect(facade.reviewSubmitting()).toBe(false);
  });

  /**
   * The server's CancellationAssessor blocks InProgress, Completed and Cancelled and nothing
   * else, and the guest flow already offers the same set; a signed-in customer gets no narrower
   * a window than a guest does.
   */
  describe('the cancel affordance', () => {
    it.each([OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay])(
      'is offered while the order is in status %s',
      (status) => {
        facade.order.set(orderIn(status));
        expect(facade.canCancel()).toBe(true);
      },
    );

    it.each([OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Cancelled])(
      'is withheld once the order is in status %s',
      (status) => {
        facade.order.set(orderIn(status));
        expect(facade.canCancel()).toBe(false);
      },
    );

    it('is withheld while the order has not loaded', () => {
      facade.order.set(null);
      expect(facade.canCancel()).toBe(false);
    });
  });

  describe('the cancellation dialog', () => {
    beforeEach(() => facade.order.set(orderIn(OrderStatus.OnTheWay)));

    it('opens on the fee preview the server quotes for this order', () => {
      facade.openCancellation();

      expect(orderClient.cancellationPreview).toHaveBeenCalledWith(ORDER_ID);
      expect(facade.cancellationOpen()).toBe(true);
      expect(facade.cancellationPreview()).toBe(preview);
      expect(facade.previewLoading()).toBe(false);
      expect(facade.canConfirmCancellation()).toBe(true);
    });

    it('does not ask for a preview when cancelling is not on offer', () => {
      facade.order.set(orderIn(OrderStatus.Completed));

      facade.openCancellation();

      expect(orderClient.cancellationPreview).not.toHaveBeenCalled();
      expect(facade.cancellationOpen()).toBe(false);
    });

    it('cannot be confirmed on a preview that failed to load', () => {
      orderClient.cancellationPreview.mockReturnValue(throwError(() => new Error('boom')));

      facade.openCancellation();

      expect(facade.cancellationOpen()).toBe(true);
      expect(facade.cancellationPreview()).toBeNull();
      expect(facade.cancellationPreviewFailed()).toBe(true);
      expect(facade.canConfirmCancellation()).toBe(false);
    });

    it('cannot be confirmed on a preview quoted for a different order', () => {
      orderClient.cancellationPreview.mockReturnValue(
        of(GetCancellationFeePreviewResponse.fromJS({ ...preview.toJSON(), orderId: 'ord-2' })),
      );

      facade.openCancellation();

      expect(facade.cancellationPreview()).toBeNull();
      expect(facade.canConfirmCancellation()).toBe(false);
    });

    it('closes and forgets the preview without cancelling', () => {
      facade.openCancellation();

      facade.closeCancellation();

      expect(facade.cancellationOpen()).toBe(false);
      expect(facade.cancellationPreview()).toBeNull();
      expect(orderClient.cancel).not.toHaveBeenCalled();
    });
  });

  describe('cancelling', () => {
    beforeEach(() => {
      facade.order.set(orderIn(OrderStatus.Confirmed));
      facade.openCancellation();
      orderClient.getById.mockReturnValue(of(orderIn(OrderStatus.Cancelled)));
    });

    it('keeps the server response, closes the dialog and re-reads the order', () => {
      facade.cancelOrder('Plans changed');

      expect(facade.cancellationResult()).toBe(cancelled);
      expect(facade.cancellationOpen()).toBe(false);
      expect(facade.cancelling()).toBe(false);
      expect(orderClient.getById).toHaveBeenLastCalledWith(ORDER_ID);
      expect(facade.order()?.orderStatus?.value).toBe(OrderStatus.Cancelled);
      expect(facade.canCancel()).toBe(false);
    });

    it('re-reads the order when the server refuses, so the page agrees with it', () => {
      orderClient.cancel.mockReturnValue(throwError(() => new Error('order.in_progress_cannot_cancel')));

      facade.cancelOrder('');

      expect(facade.cancellationResult()).toBeNull();
      expect(facade.cancellationOpen()).toBe(false);
      expect(facade.cancelling()).toBe(false);
      expect(orderClient.getById).toHaveBeenLastCalledWith(ORDER_ID);
    });

    it('does nothing before the preview has arrived', () => {
      orderClient.cancellationPreview.mockReturnValue(throwError(() => new Error('boom')));
      facade.openCancellation();

      facade.cancelOrder('');

      expect(orderClient.cancel).not.toHaveBeenCalled();
    });
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes the cancellation with the order id and the trimmed reason', () => {
      facade.order.set(orderIn(OrderStatus.New));
      facade.openCancellation();

      facade.cancelOrder('  Plans changed  ');

      const command: CancelOrderCommand = orderClient.cancel.mock.calls[0][0];
      expect(command).toBeInstanceOf(CancelOrderCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, reason: 'Plans changed' });
    });

    it('leaves a blank reason off the cancellation body', () => {
      facade.order.set(orderIn(OrderStatus.New));
      facade.openCancellation();

      facade.cancelOrder('   ');

      const command: CancelOrderCommand = orderClient.cancel.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, reason: undefined });
    });

    it('serializes the review with the order id, the rating and the comment', () => {
      facade.submitReview(5, 'Spotless');

      const command: SubmitOrderReviewCommand =
        orderClient.submitReview.mock.calls[0][0];
      expect(command).toBeInstanceOf(SubmitOrderReviewCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        rating: 5,
        comment: 'Spotless',
      });
    });

    it('leaves an empty comment off the body rather than sending a blank string', () => {
      facade.submitReview(4, '');

      const command: SubmitOrderReviewCommand =
        orderClient.submitReview.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, rating: 4 });
    });
  });
});
