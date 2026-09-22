import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { PermissionService, Policy } from '@cleansia/services';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { SilentFailureAdminClient } from '../client/admin-base-client';
import { UnreadNotificationCountDto } from '../client/admin-client';
import { AdminAuthService } from './admin-auth.service';
import { ADMIN_NOTIFICATION_POLL_INTERVAL_MS, formatUnreadBadge } from './admin-notification-badge.models';
import { AdminNotificationBadgeService } from './admin-notification-badge.service';

const count = (value: number) => of(UnreadNotificationCountDto.fromJS({ count: value }));

describe('formatUnreadBadge', () => {
  it.each([
    [0, null],
    [-1, null],
    [Number.NaN, null],
    [1, '1'],
    [42, '42'],
    [99, '99'],
    [100, '99+'],
    [1234, '99+'],
  ])('renders %p as %p', (value, expected) => {
    expect(formatUnreadBadge(value)).toBe(expected);
  });
});

describe('AdminNotificationBadgeService', () => {
  let unreadCount: jest.Mock;
  let isLoggedIn$: BehaviorSubject<boolean>;
  let hasPolicy: jest.Mock;
  let service: AdminNotificationBadgeService;

  function setVisibility(state: DocumentVisibilityState): void {
    Object.defineProperty(document, 'visibilityState', { value: state, configurable: true });
  }

  beforeEach(() => {
    jest.useFakeTimers();
    setVisibility('visible');
    unreadCount = jest.fn().mockReturnValue(count(3));
    isLoggedIn$ = new BehaviorSubject<boolean>(true);
    hasPolicy = jest.fn().mockReturnValue(true);

    TestBed.configureTestingModule({
      providers: [
        AdminNotificationBadgeService,
        { provide: SilentFailureAdminClient, useValue: { adminNotificationClient: { unreadCount } } },
        { provide: AdminAuthService, useValue: { isLoggedIn$ } },
        { provide: PermissionService, useValue: { hasPolicy } },
        { provide: DOCUMENT, useValue: document },
      ],
    });

    service = TestBed.inject(AdminNotificationBadgeService);
  });

  afterEach(() => {
    jest.useRealTimers();
    setVisibility('visible');
  });

  it('starts at zero with no badge before anything is read', () => {
    expect(service.unreadCount()).toBe(0);
    expect(service.badgeLabel()).toBeNull();
    expect(unreadCount).not.toHaveBeenCalled();
  });

  it('reads the count as soon as it starts and again on every poll while the tab is visible', () => {
    service.start();
    expect(unreadCount).toHaveBeenCalledTimes(1);
    expect(service.unreadCount()).toBe(3);
    expect(service.badgeLabel()).toBe('3');

    unreadCount.mockReturnValue(count(2));
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS);
    expect(unreadCount).toHaveBeenCalledTimes(2);
    expect(service.unreadCount()).toBe(2);

    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS);
    expect(unreadCount).toHaveBeenCalledTimes(3);
  });

  it('asks the feed for the caller with the admin notification policy, not for anyone signed in', () => {
    service.start();

    expect(hasPolicy).toHaveBeenCalledWith(Policy.CanViewAdminNotifications);
  });

  it('starting twice does not double the polling', () => {
    service.start();
    service.start();
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS);

    expect(unreadCount).toHaveBeenCalledTimes(2);
  });

  it('polls nothing while signed out and nothing without the policy', () => {
    isLoggedIn$.next(false);
    service.start();
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS * 2);
    expect(unreadCount).not.toHaveBeenCalled();

    hasPolicy.mockReturnValue(false);
    isLoggedIn$.next(true);
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS * 2);
    expect(unreadCount).not.toHaveBeenCalled();
    expect(service.unreadCount()).toBe(0);
  });

  it('clears the badge and stops polling on sign-out, and resumes on the next sign-in', () => {
    service.start();
    expect(service.unreadCount()).toBe(3);

    isLoggedIn$.next(false);
    expect(service.unreadCount()).toBe(0);
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS * 2);
    expect(unreadCount).toHaveBeenCalledTimes(1);

    isLoggedIn$.next(true);
    expect(unreadCount).toHaveBeenCalledTimes(2);
    expect(service.unreadCount()).toBe(3);
  });

  it('a session refresh that re-announces the same signed-in state does not restart the poll', () => {
    service.start();
    isLoggedIn$.next(true);
    isLoggedIn$.next(true);

    expect(unreadCount).toHaveBeenCalledTimes(1);
  });

  it('skips the poll while the tab is hidden and reads the moment it becomes visible again', () => {
    service.start();
    expect(unreadCount).toHaveBeenCalledTimes(1);

    setVisibility('hidden');
    document.dispatchEvent(new Event('visibilitychange'));
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS * 3);
    expect(unreadCount).toHaveBeenCalledTimes(1);

    unreadCount.mockReturnValue(count(7));
    setVisibility('visible');
    document.dispatchEvent(new Event('visibilitychange'));
    expect(unreadCount).toHaveBeenCalledTimes(2);
    expect(service.unreadCount()).toBe(7);
  });

  it('keeps the last count when a read fails, and recovers on the next one', () => {
    service.start();
    expect(service.unreadCount()).toBe(3);

    unreadCount.mockReturnValue(throwError(() => new Error('offline')));
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS);
    expect(service.unreadCount()).toBe(3);

    unreadCount.mockReturnValue(count(5));
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS);
    expect(service.unreadCount()).toBe(5);
  });

  it('refresh reads on demand without starting the poll', () => {
    unreadCount.mockReturnValue(count(11));

    service.refresh();

    expect(unreadCount).toHaveBeenCalledTimes(1);
    expect(service.unreadCount()).toBe(11);
    jest.advanceTimersByTime(ADMIN_NOTIFICATION_POLL_INTERVAL_MS * 2);
    expect(unreadCount).toHaveBeenCalledTimes(1);
  });

  it('never reports a negative count', () => {
    unreadCount.mockReturnValue(count(-4));

    service.refresh();

    expect(service.unreadCount()).toBe(0);
    expect(service.badgeLabel()).toBeNull();
  });
});
