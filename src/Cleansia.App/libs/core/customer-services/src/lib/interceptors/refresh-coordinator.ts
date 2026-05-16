import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, filter, take } from 'rxjs';

/**
 * Single-flight coordinator for refresh-token rotation across concurrent 401s.
 *
 * Previously this state lived at module scope inside the error interceptor —
 * which worked in production but stuck through hot-reload in dev (a stuck
 * `isRefreshing=true` after HMR would block every 401 retry until the tab
 * was reloaded). Lifting it to a Hilt-style singleton service ties the
 * state to the Angular injector, which IS reset on full reload but NOT on
 * trivial component swaps that don't touch the root injector.
 */
@Injectable({ providedIn: 'root' })
export class CustomerRefreshCoordinator {
  private isRefreshing = false;
  private readonly refreshedToken$ = new BehaviorSubject<string | null>(null);

  isInFlight(): boolean {
    return this.isRefreshing;
  }

  /** Begin a refresh — caller is expected to publish via `complete` / `fail`. */
  begin(): void {
    this.isRefreshing = true;
    this.refreshedToken$.next(null);
  }

  complete(newToken: string): void {
    this.isRefreshing = false;
    this.refreshedToken$.next(newToken);
  }

  fail(): void {
    this.isRefreshing = false;
    this.refreshedToken$.next(null);
  }

  /** Observable that emits the next successfully-refreshed token once. */
  waitForRefresh(): Observable<string> {
    return this.refreshedToken$.pipe(
      filter((token): token is string => token !== null),
      take(1),
    );
  }
}
