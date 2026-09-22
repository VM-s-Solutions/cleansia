import { DOCUMENT } from '@angular/common';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionService, Policy } from '@cleansia/services';
import {
  catchError,
  defer,
  distinctUntilChanged,
  EMPTY,
  filter,
  finalize,
  fromEvent,
  interval,
  merge,
  of,
  startWith,
  Subscription,
  switchMap,
} from 'rxjs';
import { SilentFailureAdminClient } from '../client/admin-base-client';
import { AdminAuthService } from './admin-auth.service';
import { ADMIN_NOTIFICATION_POLL_INTERVAL_MS, formatUnreadBadge } from './admin-notification-badge.models';

/**
 * The unread count behind the administrator's bell. Root-lived because the shell draws the badge
 * and the notifications page changes it, and neither may import the other. There is no push
 * channel to the console, so the count is polled — once a minute, only while the tab is visible,
 * only for a signed-in administrator — and re-read on demand after a mark-read. A failed read keeps
 * the last value: the badge is a hint, and it travels on the silent client so an outage does not
 * toast every minute.
 */
@Injectable({
  providedIn: 'root',
})
export class AdminNotificationBadgeService {
  private readonly client = inject(SilentFailureAdminClient);
  private readonly auth = inject(AdminAuthService);
  private readonly permissions = inject(PermissionService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  private readonly count = signal(0);
  readonly unreadCount = this.count.asReadonly();
  readonly badgeLabel = computed(() => formatUnreadBadge(this.count()));

  private polling?: Subscription;
  private inFlight = false;

  start(): void {
    this.polling ??= this.auth.isLoggedIn$
      .pipe(
        distinctUntilChanged(),
        switchMap((loggedIn) => (loggedIn ? this.pollWhileAllowed() : this.cleared())),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.refresh());
  }

  refresh(): void {
    if (this.inFlight) return;
    this.inFlight = true;
    this.client.adminNotificationClient
      .unreadCount()
      .pipe(
        catchError(() => of(null)),
        finalize(() => (this.inFlight = false))
      )
      .subscribe((response) => {
        if (response) this.count.set(Math.max(0, response.count ?? 0));
      });
  }

  private pollWhileAllowed() {
    if (!this.permissions.hasPolicy(Policy.CanViewAdminNotifications)) return this.cleared();

    return merge(
      interval(ADMIN_NOTIFICATION_POLL_INTERVAL_MS).pipe(startWith(0)),
      fromEvent(this.document, 'visibilitychange')
    ).pipe(filter(() => this.document.visibilityState === 'visible'));
  }

  private cleared() {
    return defer(() => {
      this.count.set(0);
      return EMPTY;
    });
  }
}
