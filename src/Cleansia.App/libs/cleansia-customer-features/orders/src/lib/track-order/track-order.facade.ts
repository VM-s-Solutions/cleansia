import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
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
 * Shared facade for guest order lookup, used by the track, lookup and guest-detail screens.
 *
 * **The customer order client has no DI registration in this app** — components used to build one
 * inline from HttpClient and the base-URL token. This centralises that wiring. Methods return
 * observables so callers compose their own navigation and error handling.
 * → /flows/booking-and-pricing
 */
@Injectable()
export class TrackOrderFacade extends UnsubscribeControlDirective {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    inject(CUSTOMER_API_BASE_URL, { optional: true }) ?? 'http://localhost:5003';
  private readonly orderClient = new CustomerOrderClient(this.http, this.baseUrl);

  /**
   * Guest lookup — order number, e-mail AND the order's confirmation code.
   * The code is the third factor: without it the endpoint answered to a
   * sequential order number and an e-mail, neither of which is a secret.
   *
   * Issued here rather than through the generated client, which still has the
   * two-argument signature. `manual_step: nswag-regen` — once the client is
   * regenerated this becomes `this.orderClient.lookup(number, email, code)`
   * and the hand-built request below goes away. Written against the same base
   * URL and the same response type so the swap is a one-line change.
   */
  lookup(
    orderNumber: string,
    email: string,
    confirmationCode: string,
  ): Observable<LookupOrderResponse> {
    const params = new HttpParams()
      .set('orderNumber', orderNumber)
      .set('email', email)
      .set('confirmationCode', confirmationCode);

    return this.http.get<LookupOrderResponse>(`${this.baseUrl}/api/Order/Lookup`, { params });
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
