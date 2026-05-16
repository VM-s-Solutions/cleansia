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
 * Shared facade for guest order lookup operations.
 *
 * Used by TrackOrderComponent, OrderLookupComponent, and GuestOrderDetailComponent.
 * The customer order client has no DI registration in this app — the components
 * historically built one inline from HttpClient + the customer API base URL token.
 * The facade centralizes that wiring so consumers just call lookup/lookupBatch.
 *
 * Methods return Observables so components can compose their own navigation /
 * caching / error-classification logic. The component subscribes with
 * takeUntil(facade.destroyed$) to scope cleanup to its own lifecycle.
 *
 * Provided per-component (NOT providedIn:'root') so destroyed$ fires when the
 * consuming component is destroyed.
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
    const batchItems = items.map(
      (i) =>
        new LookupOrderBatchOrderLookupItem({
          orderId: i.orderId,
          email: i.email,
        })
    );
    return this.orderClient.lookupBatch(new LookupOrderBatchQuery({ items: batchItems }));
  }
}
