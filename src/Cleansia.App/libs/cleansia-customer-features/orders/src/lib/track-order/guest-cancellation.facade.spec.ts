import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  CUSTOMER_API_BASE_URL, CancelOrderResponse, CancellationFeeTier, CustomerAuthService,
  CustomerOrderClient, GetCancellationFeePreviewResponse, LookupOrderResponse,
  ReportGuestCleanerNoShowResponse,
} from '@cleansia/customer-services';
import { OrderStatus } from '@cleansia/models';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { GuestOrderService } from './guest-order.service';
import { TrackOrderComponent } from './track-order.component';
import { TrackOrderFacade } from './track-order.facade';

function order(id = 'first', status = OrderStatus.New): LookupOrderResponse {
  return LookupOrderResponse.fromJS({
    id, displayOrderNumber: `CLS-${id}`,
    orderStatus: { value: status }, currency: { code: 'EUR' },
  });
}

function preview(id = 'first'): GetCancellationFeePreviewResponse {
  return GetCancellationFeePreviewResponse.fromJS({
    orderId: id, tier: CancellationFeeTier.Partial, feeRate: 0.25, feeAmount: 25,
    refundAmount: 75, totalPrice: 100, currencyCode: 'EUR', expressWaiverForfeitedOnCancel: false,
  });
}

describe('guest cancellation selection and confirmation', () => {
  let facade: TrackOrderFacade;
  let previewCall: jest.SpyInstance;
  let cancelCall: jest.SpyInstance;
  let lookupCall: jest.SpyInstance;
  const remembered = { getAll: jest.fn(), save: jest.fn() };

  beforeEach(() => {
    TestBed.resetTestingModule();
    remembered.getAll.mockReturnValue([
      { orderId: 'first', accessToken: 'tok-first' },
      { orderId: 'second', accessToken: 'tok-second' },
    ]);
    remembered.save.mockReset();
    TestBed.configureTestingModule({ providers: [
      TrackOrderFacade, FormBuilder, provideHttpClient(),
      { provide: CUSTOMER_API_BASE_URL, useValue: 'https://api.test' },
      { provide: GuestOrderService, useValue: remembered },
      { provide: CustomerAuthService, useValue: { isLoggedIn: () => false } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParams: {} } } },
      { provide: TranslateService, useValue: {
        currentLang: 'en', instant: (key: string, args?: { amount?: string }) => args?.amount ?? key,
      } },
    ] });
    previewCall = jest.spyOn(CustomerOrderClient.prototype, 'guestCancellationPreview').mockReturnValue(of(preview()));
    cancelCall = jest.spyOn(CustomerOrderClient.prototype, 'cancelGuest').mockReturnValue(of(
      CancelOrderResponse.fromJS({ orderId: 'first', refundAmount: 75, actualRefundAmount: 40 }),
    ));
    lookupCall = jest.spyOn(CustomerOrderClient.prototype, 'lookupPost').mockReturnValue(of(order('first', OrderStatus.Cancelled)));
    facade = TestBed.inject(TrackOrderFacade);
  });

  afterEach(() => {
    facade.ngOnDestroy();
    jest.restoreAllMocks();
  });

  it('uses the selected access token for preview and cancellation, including the requested language', () => {
    facade.selectOrder(order(), ' tok-typed ');
    facade.openCancellation();
    expect(previewCall.mock.calls[0][0].toJSON()).toEqual({ accessToken: 'tok-typed' });
    expect(facade.canConfirmCancellation()).toBe(true);
    facade.cancelBooking('sk');
    expect(cancelCall.mock.calls[0][0].toJSON()).toEqual({
      accessToken: 'tok-typed', language: 'sk', reason: undefined,
    });
    expect(facade.selectedOrder()?.orderStatus?.value).toBe(OrderStatus.Cancelled);
    expect(facade.cancellationResult()?.actualRefundAmount).toBe(40);
    expect(facade.refundCurrency()).toBe('EUR');
    expect(facade.canCancel()).toBe(false);
  });

  it('remembers the token an order was opened with, so the browser can find it again', () => {
    facade.selectOrder(order(), 'tok-typed');
    expect(remembered.save).toHaveBeenCalledWith('first', 'tok-typed');
  });

  it('joins a remembered order to its own stored token instead of the last lookup', () => {
    facade.selectOrder(order(), 'tok-typed');
    facade.selectRememberedOrder(order('second'));
    previewCall.mockReturnValue(of(preview('second')));
    facade.openCancellation();
    expect(previewCall.mock.calls[0][0].toJSON()).toEqual({ accessToken: 'tok-second' });
  });

  it('does not offer cancellation when a remembered order has no stored token', () => {
    facade.selectRememberedOrder(order('unknown'));
    facade.openCancellation();
    expect(facade.canCancel()).toBe(false);
    expect(previewCall).not.toHaveBeenCalled();
  });

  it.each([OrderStatus.InProgress, OrderStatus.Completed, OrderStatus.Cancelled])('hides cancellation for status %s', status => {
    facade.selectOrder(order('first', status), 'tok-first');
    facade.openCancellation();
    expect(facade.canCancel()).toBe(false);
    expect(previewCall).not.toHaveBeenCalled();
  });

  it.each([
    { tier: 99 }, { currencyCode: undefined }, { orderId: 'other' }, { feeAmount: NaN },
  ])('refuses confirmation of an unknown or mismatched preview %j', change => {
    const invalid = preview();
    Object.assign(invalid, change);
    previewCall.mockReturnValue(of(invalid));
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    facade.cancelBooking('en');
    expect(facade.canConfirmCancellation()).toBe(false);
    expect(facade.cancellationError()).toBe('pages.track_order.cancellation.preview_error');
    expect(cancelCall).not.toHaveBeenCalled();
  });

  it('shows a uniform lookup refusal inline and never submits after a failed preview', () => {
    previewCall.mockReturnValue(throwError(() => ({ errors: { key: 'order.not_found' } })));
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    facade.cancelBooking('en');
    expect(facade.cancellationError()).toBe('api.order.not_found');
    expect(facade.previewLoading()).toBe(false);
    expect(cancelCall).not.toHaveBeenCalled();
  });

  it('discards a preview when the selection changes or the dialog closes', () => {
    const pending = new Subject<GetCancellationFeePreviewResponse>();
    previewCall.mockReturnValue(pending);
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    expect(facade.previewLoading()).toBe(true);
    facade.selectRememberedOrder(order('second'));
    pending.next(preview());
    expect(facade.cancellationPreview()).toBeNull();
    expect(facade.cancellationOpen()).toBe(false);
    facade.openCancellation();
    facade.closeCancellation();
    pending.next(preview('second'));
    expect(facade.canConfirmCancellation()).toBe(false);
  });

  it('blocks duplicate submits and selection changes until the existing cancellation finishes', () => {
    const pending = new Subject<CancelOrderResponse>();
    cancelCall.mockReturnValue(pending);
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    facade.cancelBooking('en');
    facade.cancelBooking('en');
    facade.closeCancellation();
    expect(facade.clearSelection()).toBe(false);
    expect(facade.selectRememberedOrder(order('second'))).toBe(false);
    expect(facade.cancellationOpen()).toBe(true);
    expect(cancelCall).toHaveBeenCalledTimes(1);
    pending.next(CancelOrderResponse.fromJS({ orderId: 'first' }));
    pending.complete();
    expect(facade.cancelling()).toBe(false);
    expect(facade.clearSelection()).toBe(true);
    expect(facade.cancellationResult()).toBeNull();
  });

  it('requires a fresh fee preview after a failed cancellation', () => {
    cancelCall.mockReturnValue(throwError(() => ({ errors: { order: 'order.in_progress_cannot_cancel' } })));
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    facade.cancelBooking('en');
    expect(facade.cancelling()).toBe(false);
    expect(facade.cancellationPreview()).toBeNull();
    expect(facade.cancellationError()).toBe('api.order.in_progress_cannot_cancel');
    facade.cancelBooking('en');
    expect(cancelCall).toHaveBeenCalledTimes(1);
  });

  it('shows the cancelled booking without re-reading it, because cancelling revokes the token', () => {
    facade.selectRememberedOrder(order());
    facade.openCancellation();
    facade.cancelBooking('en');
    expect(facade.selectedOrder()?.orderStatus?.value).toBe(OrderStatus.Cancelled);
    expect(facade.cancellationResult()).not.toBeNull();
    expect(lookupCall).not.toHaveBeenCalled();
  });

  it.each([undefined, 0, 40])('renders only actual issued refund money (%s)', actualRefundAmount => {
    const component = TestBed.runInInjectionContext(() => new TrackOrderComponent());
    facade.cancellationResult.set(CancelOrderResponse.fromJS({ refundAmount: 75, actualRefundAmount }));
    facade.refundCurrency.set('EUR');
    if (actualRefundAmount) {
      expect(component.refundMessage()).toContain('40');
      expect(component.refundMessage()).not.toContain('75');
    } else {
      expect(component.refundMessage()).toBeNull();
    }
  });

  it('does not restore a late lookup after the page is reset', () => {
    const pending = new Subject<LookupOrderResponse>();
    lookupCall.mockReturnValue(pending);
    const component = TestBed.runInInjectionContext(() => new TrackOrderComponent());
    component.lookup('tok-first');
    component.lookup('tok-first');
    expect(lookupCall).toHaveBeenCalledTimes(1);
    component.reset();
    pending.next(order());
    expect(component.manualResult()).toBeNull();
    expect(component.loading()).toBe(false);
  });

  // Owner ruling 2026-09-28: past the booked start, with a cleaner on the job who has not started, the
  // server refuses a self-cancel and the guest reports that the cleaner did not arrive instead.
  describe('after the booked start', () => {
    const HOUR_MS = 60 * 60 * 1000;
    let reportCall: jest.SpyInstance;
    const booking = (status: OrderStatus, startsInMs: number) => {
      const found = order('first', status);
      found.cleaningDateTime = new Date(Date.now() + startsInMs);
      return found;
    };

    beforeEach(() => {
      reportCall = jest.spyOn(CustomerOrderClient.prototype, 'reportGuestNoShow').mockReturnValue(
        of(ReportGuestCleanerNoShowResponse.fromJS({ orderId: 'first' })),
      );
    });

    it.each([OrderStatus.Confirmed, OrderStatus.OnTheWay])(
      'offers the no-show report in place of Cancel once a cleaner has taken it, in status %s',
      status => {
        facade.selectOrder(booking(status, -HOUR_MS), 'tok-first');
        expect(facade.canCancel()).toBe(false);
        expect(facade.canReportCleanerNoShow()).toBe(true);
      },
    );

    it('keeps Cancel before the start, with the cleaner already on the way', () => {
      facade.selectOrder(booking(OrderStatus.OnTheWay, HOUR_MS), 'tok-first');
      expect(facade.canCancel()).toBe(true);
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });

    it('keeps Cancel past the start while no cleaner has taken it', () => {
      facade.selectOrder(booking(OrderStatus.New, -HOUR_MS), 'tok-first');
      expect(facade.canCancel()).toBe(true);
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });

    it('offers nothing without the access token', () => {
      remembered.getAll.mockReturnValue([]);
      facade.selectRememberedOrder(booking(OrderStatus.Confirmed, -HOUR_MS));
      expect(facade.canReportCleanerNoShow()).toBe(false);
      facade.reportCleanerNoShow();
      expect(reportCall).not.toHaveBeenCalled();
    });

    it('reports with the selected access token and says it was received', () => {
      facade.selectOrder(booking(OrderStatus.Confirmed, -HOUR_MS), ' tok-typed ');

      facade.reportCleanerNoShow();

      expect(reportCall.mock.calls[0][0].toJSON()).toEqual({ accessToken: 'tok-typed' });
      expect(facade.noShowReported()).toBe(true);
      expect(facade.reportingNoShow()).toBe(false);
      expect(facade.noShowError()).toBeNull();
      expect(facade.canReportCleanerNoShow()).toBe(false);
    });

    it.each(['order.cleaner_already_started', 'order.start_time_not_reached', 'order.already_cancelled'])(
      'shows the refusal %s inline and keeps the report on offer',
      code => {
        reportCall.mockReturnValue(throwError(() => ({ errors: { AccessToken: code } })));
        facade.selectOrder(booking(OrderStatus.Confirmed, -HOUR_MS), 'tok-first');

        facade.reportCleanerNoShow();

        expect(facade.noShowError()).toBe(`api.${code}`);
        expect(facade.noShowReported()).toBe(false);
        expect(facade.reportingNoShow()).toBe(false);
      },
    );

    it('falls back to its own message for a failure the server did not name', () => {
      reportCall.mockReturnValue(throwError(() => new Error('offline')));
      facade.selectOrder(booking(OrderStatus.Confirmed, -HOUR_MS), 'tok-first');

      facade.reportCleanerNoShow();

      expect(facade.noShowError()).toBe('pages.track_order.cleaner_no_show.error');
    });

    it('sends one report at a time', () => {
      const pending = new Subject<ReportGuestCleanerNoShowResponse>();
      reportCall.mockReturnValue(pending);
      facade.selectOrder(booking(OrderStatus.Confirmed, -HOUR_MS), 'tok-first');

      facade.reportCleanerNoShow();
      facade.reportCleanerNoShow();

      expect(reportCall).toHaveBeenCalledTimes(1);
      expect(facade.reportingNoShow()).toBe(true);
    });

    it.each(['preview', 'cancel'])(
      'turns to the no-show report when the server refuses the %s because the start has passed',
      step => {
        const refusal = throwError(() => ({ errors: { AccessToken: 'order.start_passed_cannot_cancel' } }));
        if (step === 'preview') previewCall.mockReturnValue(refusal);
        else cancelCall.mockReturnValue(refusal);
        facade.selectOrder(order('first', OrderStatus.Confirmed), 'tok-first');

        facade.openCancellation();
        facade.cancelBooking('en');

        expect(facade.cancellationOpen()).toBe(false);
        expect(facade.cancellationError()).toBeNull();
        expect(facade.canCancel()).toBe(false);
        expect(facade.canReportCleanerNoShow()).toBe(true);
      },
    );

    it('forgets the report when another booking is selected', () => {
      facade.selectOrder(booking(OrderStatus.Confirmed, -HOUR_MS), 'tok-first');
      facade.reportCleanerNoShow();

      facade.selectRememberedOrder(booking(OrderStatus.Confirmed, -HOUR_MS));

      expect(facade.noShowReported()).toBe(false);
      expect(facade.canReportCleanerNoShow()).toBe(true);
    });
  });

  it('keeps a remembered selection when an earlier lookup returns late', () => {
    const pending = new Subject<LookupOrderResponse>();
    lookupCall.mockReturnValue(pending);
    const component = TestBed.runInInjectionContext(() => new TrackOrderComponent());
    component.lookup('tok-first');
    component.showOrder(order('second'));
    pending.next(order());
    expect(component.manualResult()?.id).toBe('second');
    previewCall.mockReturnValue(of(preview('second')));
    facade.openCancellation();
    expect(previewCall.mock.calls[0][0].accessToken).toBe('tok-second');
  });
});
