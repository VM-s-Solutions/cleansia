import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CustomerAuthService,
  CustomerClient,
  OrderItem,
  SubmitOrderReviewCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { OrderDetailFacade } from './order-detail.facade';

const ORDER_ID = 'ord-1';

describe('OrderDetailFacade', () => {
  let orderClient: { getById: jest.Mock; submitReview: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock; showApiError: jest.Mock };
  let facade: OrderDetailFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      getById: jest.fn().mockReturnValue(of(OrderItem.fromJS({ id: ORDER_ID }))),
      submitReview: jest.fn().mockReturnValue(of({ rating: 5 })),
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

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
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
