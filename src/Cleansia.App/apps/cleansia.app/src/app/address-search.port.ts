import { inject } from '@angular/core';
import { CustomerClient } from '@cleansia/customer-services';
import { AddressSearchPort, MapboxAddressSuggestion } from '@cleansia/services';
import { catchError, map, of } from 'rxjs';

/**
 * The customer app's address lookup: the generated Customer API client, like
 * every other call this app makes.
 *
 * It exists as a factory rather than living inside `@cleansia/services` because
 * that library is shared with the partner app, and each app has its own
 * generated client. The library declares the port; the app satisfies it.
 *
 * A failed lookup yields no suggestions rather than an error. The address field
 * stays usable either way — the customer types the address by hand — and an
 * unprovisioned or rate-limited provider is not something to interrupt a
 * booking over.
 */
export function customerAddressSearchPort(): AddressSearchPort {
  const client = inject(CustomerClient);

  return {
    search: (query, countries, limit) =>
      client.addressSearchClient.search(query, countries, limit).pipe(
        map((response): MapboxAddressSuggestion[] =>
          (response.suggestions ?? []).map((s) => ({
            placeName: s.placeName ?? '',
            street: s.street ?? '',
            city: s.city ?? '',
            zipCode: s.zipCode ?? '',
            latitude: s.latitude,
            longitude: s.longitude,
          }))
        ),
        catchError(() => of([]))
      ),
  };
}
