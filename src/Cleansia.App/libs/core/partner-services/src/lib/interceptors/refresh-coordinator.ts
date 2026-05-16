import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, filter, take } from 'rxjs';

/** See `customer-services/.../refresh-coordinator.ts` for design notes. */
@Injectable({ providedIn: 'root' })
export class PartnerRefreshCoordinator {
  private isRefreshing = false;
  private readonly refreshedToken$ = new BehaviorSubject<string | null>(null);

  isInFlight(): boolean {
    return this.isRefreshing;
  }

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

  waitForRefresh(): Observable<string> {
    return this.refreshedToken$.pipe(
      filter((token): token is string => token !== null),
      take(1),
    );
  }
}
