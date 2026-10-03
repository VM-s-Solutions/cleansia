import { PLATFORM_ID, signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CancelOrderCommand,
  CancelOrderResponse,
  CancellationFeeTier,
  ConfirmRecurringOrderCommand,
  ConfirmRecurringOrderResponse,
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  GetCancellationFeePreviewResponse,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  PaymentType,
  SubmitOrderReviewCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
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
    confirmRecurring: jest.Mock;
  };
  let gdprClient: { consentsGet: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock; showApiError: jest.Mock };
  let signedIn: WritableSignal<boolean>;
  let facade: OrderDetailFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    signedIn = signal(true);
    orderClient = {
      getById: jest.fn().mockReturnValue(of(OrderItem.fromJS({ id: ORDER_ID }))),
      submitReview: jest.fn().mockReturnValue(of({ rating: 5 })),
      cancellationPreview: jest.fn().mockReturnValue(of(preview)),
      cancel: jest.fn().mockReturnValue(of(cancelled)),
      confirmRecurring: jest.fn().mockReturnValue(
        of(ConfirmRecurringOrderResponse.fromJS({ orderId: ORDER_ID })),
      ),
    };
    gdprClient = { consentsGet: jest.fn().mockReturnValue(of([])) };
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
            gdprClient,
            membershipClient: { getMine: jest.fn().mockReturnValue(of(null)) },
          },
        },
        { provide: CustomerAuthService, useValue: { isLoggedIn: () => signedIn() } },
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

  // Owner ruling 2026-09-28: past the booked start, with a cleaner on the job who has not started, the
  // server refuses a self-cancel and the customer reports that the cleaner did not arrive instead.
  describe('after the booked start', () => {
    const HOUR_MS = 60 * 60 * 1000;
    const startPassed = () => ({ errors: { OrderId: 'order.start_passed_cannot_cancel' } });
    const booking = (status: OrderStatus, startsInMs: number, staffed: boolean) =>
      OrderItem.fromJS({
        id: ORDER_ID,
        orderStatus: { value: status, name: OrderStatus[status] },
        cleaningDateTime: new Date(Date.now() + startsInMs).toISOString(),
        assignedEmployees: staffed ? [{ employeeId: 'emp-1', fullName: 'Petra S.' }] : [],
      });

    it.each([OrderStatus.Confirmed, OrderStatus.OnTheWay])(
      'offers the no-show report in place of Cancel on a staffed order in status %s',
      (status) => {
        facade.order.set(booking(status, -HOUR_MS, true));
        expect(facade.canCancel()).toBe(false);
        expect(facade.canReportCleanerNoShow()).toBe(true);
      },
    );

    it('keeps Cancel before the start, with the cleaner already on the way', () => {
      facade.order.set(booking(OrderStatus.OnTheWay, HOUR_MS, true));
      expect(facade.canCancel()).toBe(true);
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });

    it('keeps Cancel past the start while nobody is assigned', () => {
      facade.order.set(booking(OrderStatus.New, -HOUR_MS, false));
      expect(facade.canCancel()).toBe(true);
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });

    it.each([OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Cancelled])(
      'offers neither once the order is in status %s',
      (status) => {
        facade.order.set(booking(status, -HOUR_MS, true));
        expect(facade.canCancel()).toBe(false);
        expect(facade.canReportCleanerNoShow()).toBe(false);
      },
    );

    it('turns to the no-show report when the server refuses the preview because the start has passed', () => {
      facade.order.set(orderIn(OrderStatus.Confirmed));
      orderClient.cancellationPreview.mockReturnValue(throwError(startPassed));

      facade.openCancellation();

      expect(facade.cancellationOpen()).toBe(false);
      expect(facade.cancellationPreviewFailed()).toBe(false);
      expect(facade.canCancel()).toBe(false);
      expect(facade.canReportCleanerNoShow()).toBe(true);
    });

    it('turns to the no-show report when the server refuses the cancel because the start has passed', () => {
      facade.order.set(orderIn(OrderStatus.Confirmed));
      facade.openCancellation();
      orderClient.cancel.mockReturnValue(throwError(startPassed));
      orderClient.getById.mockReturnValue(of(orderIn(OrderStatus.Confirmed)));

      facade.cancelOrder('');

      expect(facade.cancellationResult()).toBeNull();
      expect(facade.canCancel()).toBe(false);
      expect(facade.canReportCleanerNoShow()).toBe(true);
    });

    it('keeps the preview-failed state for any other refusal', () => {
      facade.order.set(orderIn(OrderStatus.Confirmed));
      orderClient.cancellationPreview.mockReturnValue(
        throwError(() => ({ errors: { OrderId: 'order.in_progress_cannot_cancel' } })),
      );

      facade.openCancellation();

      expect(facade.cancellationPreviewFailed()).toBe(true);
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });
  });

  // With no card charge behind the booking the cancel sheet has no card refund to estimate.
  describe('whether the booking took a card payment', () => {
    const booking = (type: PaymentType, status: PaymentStatus) =>
      OrderItem.fromJS({
        id: ORDER_ID,
        paymentType: { value: type, name: PaymentType[type] },
        paymentStatus: { value: status, name: PaymentStatus[status] },
      });

    it.each([
      ['a cash booking not yet collected', PaymentType.Cash, PaymentStatus.Pending],
      ['a cash booking already marked paid', PaymentType.Cash, PaymentStatus.Paid],
      ['a card booking never charged', PaymentType.Card, PaymentStatus.Pending],
      ['a card booking whose charge failed', PaymentType.Card, PaymentStatus.Failed],
    ])('took none on %s', (_, type, status) => {
      facade.order.set(booking(type, status));
      expect(facade.tookNoCardPayment()).toBe(true);
    });

    it('took one on a charged card booking, and says nothing before the order has loaded', () => {
      facade.order.set(booking(PaymentType.Card, PaymentStatus.Paid));
      expect(facade.tookNoCardPayment()).toBe(false);

      facade.order.set(null);
      expect(facade.tookNoCardPayment()).toBe(false);
    });
  });

  describe('the free-cancellation window the page states', () => {
    it('is the window the server resolved for this order', () => {
      facade.order.set(OrderItem.fromJS({ id: ORDER_ID, freeCancellationHours: 12 }));
      expect(facade.freeCancellationHours()).toBe(12);
    });

    it('is stated even when the server resolved no window at all', () => {
      facade.order.set(OrderItem.fromJS({ id: ORDER_ID, freeCancellationHours: 0 }));
      expect(facade.freeCancellationHours()).toBe(0);
    });

    it('is not stated when the server sent none, or before the order has loaded', () => {
      facade.order.set(OrderItem.fromJS({ id: ORDER_ID }));
      expect(facade.freeCancellationHours()).toBeNull();

      facade.order.set(null);
      expect(facade.freeCancellationHours()).toBeNull();
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

  // Cash is the plain confirm; card pays through the Checkout Session the server opens for the web.
  describe('confirming a recurring occurrence', () => {
    const occurrence = (type: PaymentType, needsConfirmation = true) =>
      OrderItem.fromJS({
        id: ORDER_ID,
        orderStatus: { value: OrderStatus.New, name: OrderStatus[OrderStatus.New] },
        paymentType: { value: type, name: PaymentType[type] },
        paymentStatus: { value: PaymentStatus.Pending, name: PaymentStatus[PaymentStatus.Pending] },
        needsConfirmation,
      });
    const refusal = (field: string, key: string) => () => ({ errors: { [field]: key } });

    beforeEach(() => facade.alreadyConsented.set(true));

    afterEach(() => {
      window.location.hash = '';
    });

    it('is offered only while the server says the occurrence awaits confirmation', () => {
      facade.order.set(occurrence(PaymentType.Cash));
      expect(facade.canConfirmRecurring()).toBe(true);

      facade.order.set(occurrence(PaymentType.Cash, false));
      expect(facade.canConfirmRecurring()).toBe(false);

      facade.order.set(null);
      expect(facade.canConfirmRecurring()).toBe(false);
    });

    it('does not call the server when nothing awaits confirmation', () => {
      facade.order.set(occurrence(PaymentType.Cash, false));

      facade.confirmRecurring();

      expect(orderClient.confirmRecurring).not.toHaveBeenCalled();
    });

    it('confirms a cash occurrence, says so and re-reads the order', () => {
      facade.order.set(occurrence(PaymentType.Cash));
      orderClient.getById.mockReturnValue(of(occurrence(PaymentType.Cash, false)));

      facade.confirmRecurring();

      expect(snackbar.showSuccess).toHaveBeenCalledWith('pages.order_detail.recurring_confirm.success');
      expect(facade.confirmingRecurring()).toBe(false);
      expect(orderClient.getById).toHaveBeenLastCalledWith(ORDER_ID);
      expect(facade.canConfirmRecurring()).toBe(false);
    });

    it('hands the browser to the Checkout Session the server opened for a card occurrence', () => {
      facade.order.set(occurrence(PaymentType.Card));
      const { origin, pathname } = window.location;
      orderClient.confirmRecurring.mockReturnValue(
        of(ConfirmRecurringOrderResponse.fromJS({
          orderId: ORDER_ID,
          checkoutUrl: `${origin}${pathname}#checkout-session`,
        })),
      );

      facade.confirmRecurring();

      expect(window.location.hash).toBe('#checkout-session');
      expect(facade.confirmingRecurring()).toBe(true);
      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(orderClient.getById).not.toHaveBeenCalled();
    });

    it('sends one confirm while the first is still in flight', () => {
      facade.order.set(occurrence(PaymentType.Cash));
      orderClient.confirmRecurring.mockReturnValue(new Subject<ConfirmRecurringOrderResponse>());

      facade.confirmRecurring();
      facade.confirmRecurring();

      expect(orderClient.confirmRecurring).toHaveBeenCalledTimes(1);
      expect(facade.confirmingRecurring()).toBe(true);
    });

    it('re-reads an occurrence the server says is already confirmed', () => {
      facade.order.set(occurrence(PaymentType.Cash));
      orderClient.confirmRecurring.mockReturnValue(
        throwError(refusal('OrderId', 'order.recurring_already_confirmed')),
      );
      orderClient.getById.mockReturnValue(of(occurrence(PaymentType.Cash, false)));

      facade.confirmRecurring();

      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(facade.confirmingRecurring()).toBe(false);
      expect(orderClient.getById).toHaveBeenLastCalledWith(ORDER_ID);
      expect(facade.canConfirmRecurring()).toBe(false);
      expect(facade.recurringPaymentBegunInApp()).toBe(false);
    });

    it('points a card occurrence begun in the mobile app back there instead of offering Confirm again', () => {
      facade.order.set(occurrence(PaymentType.Card));
      orderClient.confirmRecurring.mockReturnValue(
        throwError(refusal('Id', 'order.invalid_status_transition')),
      );
      orderClient.getById.mockReturnValue(of(occurrence(PaymentType.Card)));

      facade.confirmRecurring();

      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(facade.confirmingRecurring()).toBe(false);
      expect(orderClient.getById).toHaveBeenLastCalledWith(ORDER_ID);
      expect(facade.canConfirmRecurring()).toBe(false);
      expect(facade.recurringPaymentBegunInApp()).toBe(true);
    });

    it('keeps Confirm on offer after any other refusal', () => {
      facade.order.set(occurrence(PaymentType.Card));
      orderClient.confirmRecurring.mockReturnValue(
        throwError(refusal('Id', 'order.payment_gateway_unavailable')),
      );
      orderClient.getById.mockReturnValue(of(occurrence(PaymentType.Card)));

      facade.confirmRecurring();

      expect(facade.confirmingRecurring()).toBe(false);
      expect(facade.canConfirmRecurring()).toBe(true);
      expect(facade.recurringPaymentBegunInApp()).toBe(false);
    });

    describe('keeping the card for the next bookings', () => {
      const sentSaveCard = () =>
        (orderClient.confirmRecurring.mock.calls[0][0] as ConfirmRecurringOrderCommand).saveCard;

      it('is offered only to a signed-in customer on a card occurrence awaiting confirmation', () => {
        facade.order.set(occurrence(PaymentType.Card));
        expect(facade.saveCardOffered()).toBe(true);

        facade.order.set(occurrence(PaymentType.Cash));
        expect(facade.saveCardOffered()).toBe(false);

        facade.order.set(occurrence(PaymentType.Card, false));
        expect(facade.saveCardOffered()).toBe(false);

        facade.order.set(occurrence(PaymentType.Card));
        signedIn.set(false);
        expect(facade.saveCardOffered()).toBe(false);
      });

      it('is withdrawn once the occurrence turns out to be paid for in the mobile app', () => {
        facade.order.set(occurrence(PaymentType.Card));
        orderClient.confirmRecurring.mockReturnValue(
          throwError(refusal('Id', 'order.invalid_status_transition')),
        );
        orderClient.getById.mockReturnValue(of(occurrence(PaymentType.Card)));

        facade.confirmRecurring();

        expect(facade.saveCardOffered()).toBe(false);
      });

      it('keeps nothing until the customer ticks it', () => {
        facade.order.set(occurrence(PaymentType.Card));

        facade.confirmRecurring();

        expect(facade.saveCard()).toBe(false);
        expect(sentSaveCard()).toBe(false);
      });

      it('asks to keep the card the customer ticked', () => {
        facade.order.set(occurrence(PaymentType.Card));
        facade.setSaveCard(true);

        facade.confirmRecurring();

        expect(sentSaveCard()).toBe(true);
      });

      it('asks to keep nothing once the tick is taken off again', () => {
        facade.order.set(occurrence(PaymentType.Card));
        facade.setSaveCard(true);
        facade.setSaveCard(false);

        facade.confirmRecurring();

        expect(sentSaveCard()).toBe(false);
      });

      it('asks to keep nothing on a cash occurrence, whatever the tick says', () => {
        facade.order.set(occurrence(PaymentType.Cash));
        facade.setSaveCard(true);

        facade.confirmRecurring();

        expect(sentSaveCard()).toBe(false);
      });

      it('asks to keep nothing for a guest, whatever the tick says', () => {
        signedIn.set(false);
        facade.order.set(occurrence(PaymentType.Card));
        facade.setSaveCard(true);

        facade.confirmRecurring();

        expect(sentSaveCard()).toBe(false);
      });
    });
  });

  // Owner ruling 2026-10-03: confirming an occurrence asks for the terms exactly as a booking does.
  describe('the terms tick on a recurring confirm', () => {
    const awaiting = (needsConfirmation = true) =>
      OrderItem.fromJS({
        id: ORDER_ID,
        orderStatus: { value: OrderStatus.New, name: OrderStatus[OrderStatus.New] },
        paymentType: { value: PaymentType.Cash, name: PaymentType[PaymentType.Cash] },
        needsConfirmation,
      });
    const consent = (
      type: ConsentType,
      { isGranted = true, withdrawnAt = undefined as string | undefined, coversCurrentVersion = true } = {},
    ) => ({
      id: `c${type}`,
      consentType: type,
      isGranted,
      grantedAt: '2026-09-14T10:00:00Z',
      withdrawnAt,
      createdOn: '2026-09-14T10:00:00Z',
      documentVersion: '2026-09-14',
      coversCurrentVersion,
    });
    const sentTerms = () =>
      (orderClient.confirmRecurring.mock.calls[0][0] as ConfirmRecurringOrderCommand).termsAccepted;

    function load(order: OrderItem): void {
      orderClient.getById.mockReturnValue(of(order));
      facade.loadOrder(ORDER_ID);
    }

    it('is not asked of an account whose two consents cover the texts in force, and asserts nothing', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([consent(ConsentType.TermsOfService), consent(ConsentType.PrivacyPolicy)]),
      );
      load(awaiting());

      expect(facade.termsAsked()).toBe(false);
      facade.confirmRecurring();
      expect(sentTerms()).toBeUndefined();
    });

    it.each([
      { what: 'only the terms are on record', consents: [consent(ConsentType.TermsOfService)] },
      {
        what: 'the terms accepted are older than the text in force',
        consents: [
          consent(ConsentType.TermsOfService, { coversCurrentVersion: false }),
          consent(ConsentType.PrivacyPolicy),
        ],
      },
      {
        what: 'the privacy policy was withdrawn',
        consents: [
          consent(ConsentType.TermsOfService),
          consent(ConsentType.PrivacyPolicy, { withdrawnAt: '2026-09-20T10:00:00Z' }),
        ],
      },
      {
        what: 'the privacy policy was refused',
        consents: [
          consent(ConsentType.TermsOfService),
          consent(ConsentType.PrivacyPolicy, { isGranted: false }),
        ],
      },
    ])('is asked when $what', ({ consents }) => {
      gdprClient.consentsGet.mockReturnValue(of(consents));
      load(awaiting());

      expect(facade.termsAsked()).toBe(true);
    });

    it('is asked when the consents could not be read', () => {
      gdprClient.consentsGet.mockReturnValue(throwError(() => new Error('offline')));
      load(awaiting());

      expect(facade.termsAsked()).toBe(true);
    });

    it('is asked when the consent read hands back null rather than a list', () => {
      gdprClient.consentsGet.mockReturnValue(of(null));
      load(awaiting());

      expect(facade.termsAsked()).toBe(true);
    });

    it('is asked of a guest, without reading consents nobody holds', () => {
      signedIn.set(false);
      load(awaiting());

      expect(gdprClient.consentsGet).not.toHaveBeenCalled();
      expect(facade.termsAsked()).toBe(true);
    });

    it('reads no consents for an order that awaits no confirmation, and asks nothing', () => {
      load(awaiting(false));

      expect(gdprClient.consentsGet).not.toHaveBeenCalled();
      expect(facade.termsAsked()).toBe(false);
    });

    it('holds the confirm until the customer ticks it', () => {
      load(awaiting());

      expect(facade.confirmAwaitsTerms()).toBe(true);
      facade.confirmRecurring();

      expect(orderClient.confirmRecurring).not.toHaveBeenCalled();
    });

    it('confirms with the tick asserted once the customer ticks it', () => {
      load(awaiting());

      facade.setTermsAccepted(true);
      expect(facade.confirmAwaitsTerms()).toBe(false);
      facade.confirmRecurring();

      expect(sentTerms()).toBe(true);
    });

    it('holds the confirm again once the tick is taken off', () => {
      load(awaiting());
      facade.setTermsAccepted(true);
      facade.setTermsAccepted(false);

      facade.confirmRecurring();

      expect(orderClient.confirmRecurring).not.toHaveBeenCalled();
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

    it('serializes the recurring confirm with the order id and an unticked save-card', () => {
      facade.alreadyConsented.set(true);
      facade.order.set(OrderItem.fromJS({ id: ORDER_ID, needsConfirmation: true }));

      facade.confirmRecurring();

      const command: ConfirmRecurringOrderCommand = orderClient.confirmRecurring.mock.calls[0][0];
      expect(command).toBeInstanceOf(ConfirmRecurringOrderCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, saveCard: false });
    });

    it('serializes a ticked save-card on a card occurrence', () => {
      facade.alreadyConsented.set(true);
      facade.order.set(OrderItem.fromJS({
        id: ORDER_ID,
        needsConfirmation: true,
        paymentType: { value: PaymentType.Card, name: PaymentType[PaymentType.Card] },
      }));
      facade.setSaveCard(true);

      facade.confirmRecurring();

      const command: ConfirmRecurringOrderCommand = orderClient.confirmRecurring.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, saveCard: true });
    });

    it('serializes the terms tick on a recurring confirm that asked for it', () => {
      facade.order.set(OrderItem.fromJS({ id: ORDER_ID, needsConfirmation: true }));
      facade.setTermsAccepted(true);

      facade.confirmRecurring();

      const command: ConfirmRecurringOrderCommand = orderClient.confirmRecurring.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID, saveCard: false, termsAccepted: true });
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
