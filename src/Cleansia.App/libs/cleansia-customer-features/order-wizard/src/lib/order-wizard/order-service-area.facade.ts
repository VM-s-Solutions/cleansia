import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { AddressDto, CustomerClient } from '@cleansia/customer-services';
import { catchError, of, takeUntil } from 'rxjs';

/** Dependency the service-area check reads from the orchestrating wizard facade. */
interface ServiceAreaConnection {
  currentAddress: () => AddressDto;
}

/**
 * Client-side service-area (city-serviced) check for the booking wizard.
 *
 * Backend rejects orders in non-served cities with `city.not_serviced`, but that
 * only fires on submit — the wizard surfaces an inline warning earlier so the
 * user doesn't waste time filling out the rest of the form. Backend stays the
 * source of truth; this is purely UX defense-in-depth.
 *
 *  - 'idle'    → no city yet, nothing to check
 *  - 'pending' → query in flight
 *  - 'ok'      → city matches a ServiceCity row
 *  - 'rejected'→ city not served (show banner + disable Next)
 *  - 'error'   → network failed; treat as pass-through, backend re-checks
 */
@Injectable()
export class OrderServiceAreaFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private deps: ServiceAreaConnection | null = null;

  readonly cityServiced = signal<'idle' | 'pending' | 'ok' | 'rejected' | 'error'>('idle');
  /** Internal cache: avoids re-querying when city/country haven't changed. */
  private lastCityCheckKey = '';

  connect(deps: ServiceAreaConnection): void {
    this.deps = deps;
  }

  /**
   * Fire-and-forget service-area lookup. Skips when nothing's changed
   * (avoids hammering /api/ServiceCity on every keystroke), skips during
   * SSR, and degrades to 'error' (pass-through) on network failure so a
   * flaky connection can't strand the user — backend re-validates on
   * submit anyway.
   */
  refreshCheck(): void {
    if (!this.isBrowser || !this.deps) return;
    const addr = this.deps.currentAddress();
    const countryId = addr.countryId ?? '';
    const city = (addr.city ?? '').trim();
    if (!countryId || !city) {
      this.cityServiced.set('idle');
      this.lastCityCheckKey = '';
      return;
    }
    const key = `${countryId}|${city.toLowerCase()}`;
    if (key === this.lastCityCheckKey) return;
    this.lastCityCheckKey = key;
    this.cityServiced.set('pending');
    this.customerClient.apiClient
      .serviceCity(countryId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
      )
      .subscribe((cities) => {
        // Stale-response guard: if the user already typed past this
        // city between the request and the response, drop the result.
        if (key !== this.lastCityCheckKey) return;
        if (cities === null) {
          this.cityServiced.set('error');
          return;
        }
        const normalized = city.toLowerCase();
        const match = cities.some(
          (c) => (c.name ?? '').trim().toLowerCase() === normalized,
        );
        this.cityServiced.set(match ? 'ok' : 'rejected');
      });
  }
}
