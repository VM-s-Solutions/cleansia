import { HttpClient } from '@angular/common/http';
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

  lookup(orderNumber: string, email: string): Observable<LookupOrderResponse> {
    return this.orderClient.lookup(orderNumber, email);
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
