import { Injectable, signal } from '@angular/core';
import { LookupOrderResponse } from '@cleansia/customer-services';

/**
 * Bridges the guest lookup form and the read-only detail view.
 *
 * The form calls Lookup (or LookupBatch) and stores the resolved
 * LookupOrderResponse here, keyed by orderId. The detail page reads it
 * synchronously to avoid a second network round-trip and so guests don't
 * need to re-enter their email when navigating directly from the form.
 *
 * Memory-only by design: a refresh of the detail URL falls back to the
 * lookup form (no email is persisted to localStorage).
 */
@Injectable({ providedIn: 'root' })
export class GuestOrderLookupCacheService {
  private readonly cache = new Map<string, { order: LookupOrderResponse; email: string }>();
  // Signal to drive change detection if anyone wants to subscribe.
  readonly version = signal(0);

  set(orderId: string, order: LookupOrderResponse, email: string): void {
    this.cache.set(orderId, { order, email });
    this.version.update((v) => v + 1);
  }

  get(orderId: string): { order: LookupOrderResponse; email: string } | undefined {
    return this.cache.get(orderId);
  }

  clear(): void {
    this.cache.clear();
    this.version.update((v) => v + 1);
  }
}
