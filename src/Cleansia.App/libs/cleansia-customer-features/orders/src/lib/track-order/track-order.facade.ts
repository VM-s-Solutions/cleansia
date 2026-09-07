import { inject, Injectable } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { errorToastSuppressingHttpClient } from '@cleansia/services';
import {
  CUSTOMER_API_BASE_URL,
  CustomerOrderClient,
  LookupOrderBatchOrderLookupItem,
  LookupOrderBatchQuery,
  LookupOrderBatchResponse,
  LookupOrderResponse,
} from '@cleansia/customer-services';
import { Observable } from 'rxjs';

/**
 * Shared facade for guest order lookup. One caller now: the track-order page.
 *
 * **The customer order client has no DI registration in this app** — components used to build one
 * inline from HttpClient and the base-URL token. This centralises that wiring. Methods return
 * observables so callers compose their own navigation and error handling.
 *
 * Both calls answer INLINE, so both opt out of the shared error snackbar: a failed lookup already
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
    return this.orderClient.lookup(orderNumber, email, confirmationCode);
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
