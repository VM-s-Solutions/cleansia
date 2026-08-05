import { InjectionToken } from '@angular/core';

/**
 * Cache-owning services that must be warmed when a session starts and blanked
 * when it ends. `CustomerAuthService` calls them; it must not import them.
 *
 * The one implementer today is `SavedAddressStore`, which lives in
 * `customer-stores` — a lib that already reads `customer-services`, so a direct
 * `inject(SavedAddressStore)` was the second arrow that made the two libs
 * circular. The token inverts that arrow: the store depends on the interface,
 * and the app is where the two are joined.
 */
export interface SessionLifecycleListener {
  onSessionStarted(): void;
  onSessionEnded(): void;
}

export const SESSION_LIFECYCLE_LISTENERS = new InjectionToken<
  readonly SessionLifecycleListener[]
>('SESSION_LIFECYCLE_LISTENERS');
