import { computed, inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { errorToastSuppressingHttpClient, extractApiErrorCode } from '@cleansia/services';
import {
  CUSTOMER_API_BASE_URL,
  CustomerOrderClient,
  LookupOrderBatchOrderLookupItem,
  LookupOrderBatchQuery,
  LookupOrderBatchResponse,
  LookupOrderResponse,
  LookupOrderQuery,
  CancelGuestOrderCommand,
  CancelOrderResponse,
  CancellationFeeTier,
  Code,
  GetCancellationFeePreviewResponse,
  GetGuestCancellationFeePreviewQuery,
} from '@cleansia/customer-services';
import { OrderStatus } from '@cleansia/models';
import { catchError, finalize, Observable, of, Subject, takeUntil } from 'rxjs';
import { GuestOrderService } from './guest-order.service';

interface GuestBookingKey {
  displayOrderNumber: string;
  email: string;
  confirmationCode: string;
}

/**
 * Shared facade for guest order lookup. One caller now: the track-order page.
 *
 * **The customer order client has no DI registration in this app** — components used to build one
 * inline from HttpClient and the base-URL token. This centralises that wiring and keeps the selected
 * booking's credentials and cancellation state together.
 *
 * Calls answer INLINE, so they opt out of the shared error snackbar: a failed lookup already
 * says so in amber on the form — nothing failed, the three values simply matched no order — and a
 * red "An error occurred" toast over the top contradicts it. A failed batch is silent by design;
 * the remembered list is a convenience and the form underneath still works.
 * → /flows/booking-and-pricing
 */
@Injectable()
export class TrackOrderFacade extends UnsubscribeControlDirective {
  private readonly http = errorToastSuppressingHttpClient();
  private readonly baseUrl =
    inject(CUSTOMER_API_BASE_URL, { optional: true }) ?? 'http://localhost:5003';
  private readonly orderClient = new CustomerOrderClient(this.http, this.baseUrl);
  private readonly guestOrders = inject(GuestOrderService);
  private readonly cancellationReset$ = new Subject<void>();
  private selectedKey: GuestBookingKey | null = null;
  private selectionVersion = 0;

  readonly selectedOrder = signal<LookupOrderResponse | null>(null);
  readonly cancellationOpen = signal(false);
  readonly previewLoading = signal(false);
  readonly cancellationPreview = signal<GetCancellationFeePreviewResponse | null>(null);
  readonly cancellationError = signal<string | null>(null);
  readonly cancelling = signal(false);
  readonly cancellationResult = signal<CancelOrderResponse | null>(null);
  readonly refundCurrency = signal<string | undefined>(undefined);
  readonly canCancel = computed(() => {
    const status = this.selectedOrder()?.orderStatus?.value;
    return !!this.selectedKey && !this.cancellationResult() && status !== undefined &&
      [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay].includes(status);
  });
  readonly canConfirmCancellation = computed(() =>
    this.canCancel() && this.cancellationOpen() && !!this.cancellationPreview() &&
    !this.previewLoading() && !this.cancelling() && !this.cancellationError());

  selectOrder(order: LookupOrderResponse, email: string, confirmationCode: string): boolean {
    if (!this.clearSelection()) return false;
    this.selectedKey = order.displayOrderNumber && email.trim() && confirmationCode.trim()
      ? { displayOrderNumber: order.displayOrderNumber, email: email.trim(), confirmationCode: confirmationCode.trim() }
      : null;
    this.selectedOrder.set(order);
    return true;
  }

  selectRememberedOrder(order: LookupOrderResponse): boolean {
    const remembered = this.guestOrders.getAll().find(item => item.orderId === order.id);
    return this.selectOrder(order, remembered?.email ?? '', order.confirmationCode ?? '');
  }

  clearSelection(): boolean {
    if (this.cancelling()) return false;
    this.selectionVersion++;
    this.cancellationReset$.next();
    this.selectedKey = null;
    this.selectedOrder.set(null);
    this.cancellationOpen.set(false);
    this.cancellationPreview.set(null);
    this.cancellationError.set(null);
    this.cancellationResult.set(null);
    this.refundCurrency.set(undefined);
    this.previewLoading.set(false);
    return true;
  }

  closeCancellation(): void {
    if (this.cancelling()) return;
    this.cancellationReset$.next();
    this.cancellationOpen.set(false);
    this.cancellationPreview.set(null);
    this.cancellationError.set(null);
    this.previewLoading.set(false);
  }

  openCancellation(): void {
    if (!this.canCancel() || this.cancelling() || !this.selectedKey) return;
    this.cancellationReset$.next();
    const version = this.selectionVersion;
    const query = new GetGuestCancellationFeePreviewQuery();
    query.displayOrderNumber = this.selectedKey.displayOrderNumber;
    query.email = this.selectedKey.email;
    query.confirmationCode = this.selectedKey.confirmationCode;
    this.cancellationOpen.set(true);
    this.cancellationPreview.set(null);
    this.cancellationError.set(null);
    this.previewLoading.set(true);
    this.orderClient.guestCancellationPreview(query)
      .pipe(
        takeUntil(this.destroyed$), takeUntil(this.cancellationReset$),
        catchError(error => {
          if (version === this.selectionVersion) this.cancellationError.set(this.errorKey(error, 'preview_error'));
          return of(null);
        }),
        finalize(() => { if (version === this.selectionVersion) this.previewLoading.set(false); }),
      )
      .subscribe(preview => {
        if (version !== this.selectionVersion || !preview) return;
        const knownTier = [CancellationFeeTier.FreeNotAccepted, CancellationFeeTier.FreeOopsWindow,
          CancellationFeeTier.FreeOutsideWindow, CancellationFeeTier.Partial, CancellationFeeTier.LastMinute].includes(preview.tier);
        if (!knownTier || preview.orderId !== this.selectedOrder()?.id || !preview.currencyCode ||
          ![preview.feeRate, preview.feeAmount, preview.refundAmount, preview.totalPrice].every(value => Number.isFinite(value) && value >= 0)) {
          this.cancellationError.set('pages.track_order.cancellation.preview_error');
          return;
        }
        this.cancellationPreview.set(preview);
      });
  }

  cancelBooking(language: string): void {
    const order = this.selectedOrder();
    const preview = this.cancellationPreview();
    const key = this.selectedKey;
    if (!this.canConfirmCancellation() || !key || !order || !preview) return;
    const version = this.selectionVersion;
    const command = new CancelGuestOrderCommand();
    command.displayOrderNumber = key.displayOrderNumber;
    command.email = key.email;
    command.confirmationCode = key.confirmationCode;
    command.language = language;
    this.cancelling.set(true);
    this.cancellationError.set(null);
    this.orderClient.cancelGuest(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(error => {
          if (version === this.selectionVersion) {
            this.cancellationError.set(this.errorKey(error, 'submit_error'));
            this.cancellationPreview.set(null);
          }
          return of(null);
        }),
        finalize(() => this.cancelling.set(false)),
      )
      .subscribe(result => {
        if (version !== this.selectionVersion || !result) return;
        this.cancellationResult.set(result);
        this.refundCurrency.set(preview.currencyCode);
        this.cancellationOpen.set(false);
        const status = new Code();
        status.value = OrderStatus.Cancelled;
        status.name = 'Cancelled';
        status.type = order.orderStatus?.type;
        const cancelled = new LookupOrderResponse();
        Object.assign(cancelled, order);
        cancelled.orderStatus = status;
        this.selectedOrder.set(cancelled);
        this.lookup(key.displayOrderNumber, key.email, key.confirmationCode)
          .pipe(takeUntil(this.destroyed$), takeUntil(this.cancellationReset$), catchError(() => of(null)))
          .subscribe(refreshed => {
            if (version === this.selectionVersion && refreshed) this.selectedOrder.set(refreshed);
          });
      });
  }

  private errorKey(error: unknown, fallback: 'preview_error' | 'submit_error'): string {
    const code = extractApiErrorCode(error);
    return code && ['order.not_found', 'order.already_cancelled', 'order.in_progress_cannot_cancel',
      'order.already_completed'].includes(code)
      ? `api.${code}` : `pages.track_order.cancellation.${fallback}`;
  }

  /**
   * Guest lookup — order number, e-mail AND the order's confirmation code.
   * The code is the third factor: without it the endpoint answered to a
   * sequential order number and an e-mail, neither of which is a secret.
   */
  lookup(
    orderNumber: string,
    email: string,
    confirmationCode: string,
  ): Observable<LookupOrderResponse> {
    const query = new LookupOrderQuery();
    query.displayOrderNumber = orderNumber;
    query.email = email;
    query.confirmationCode = confirmationCode;
    return this.orderClient.lookupPost(query);
  }

  lookupBatch(
    items: { orderId: string; email: string }[]
  ): Observable<LookupOrderBatchResponse> {
    const query = new LookupOrderBatchQuery();
    query.items = items.map((i) => {
      const item = new LookupOrderBatchOrderLookupItem();
      item.orderId = i.orderId;
      item.email = i.email;
      return item;
    });

    return this.orderClient.lookupBatch(query);
  }
}
