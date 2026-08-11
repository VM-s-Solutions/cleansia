import { Provider } from '@angular/core';
import { SESSION_LIFECYCLE_LISTENERS } from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';

/**
 * `CustomerAuthService` warms and blanks the saved-address cache through the
 * token; the app is the only place that can see both sides. Dropping this
 * provider leaves user B looking at user A's addresses after a sign-out on a
 * shared device — `session-listeners.spec.ts` pins it.
 */
export const SESSION_LIFECYCLE_PROVIDERS: Provider[] = [
  {
    provide: SESSION_LIFECYCLE_LISTENERS,
    useExisting: SavedAddressStore,
    multi: true,
  },
];
