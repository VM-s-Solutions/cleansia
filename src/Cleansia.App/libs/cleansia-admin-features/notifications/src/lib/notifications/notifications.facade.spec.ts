import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminNotificationBadgeService,
  MarkAllNotificationsReadCommand,
  MarkAllNotificationsReadResponse,
  MarkNotificationReadCommand,
  MarkNotificationReadResponse,
  PagedDataOfUserNotificationDto,
  UserNotificationDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { NotificationsFacade } from './notifications.facade';
import { NOTIFICATIONS_PAGE_SIZE } from './notifications.models';

const NEWEST = new Date('2026-09-19T08:30:00Z');
const OLDER = new Date('2026-09-18T08:30:00Z');
const WATERMARK_AFTER_NEWEST = new Date(NEWEST.getTime() + 1).toISOString();

function item(overrides: Record<string, unknown> = {}): UserNotificationDto {
  return UserNotificationDto.fromJS({
    id: 'n-1',
    eventKey: 'admin.dispute.filed',
    args: { orderNumber: 'ORD-1', reason: 'ServiceQuality', disputeId: 'd-1', orderId: 'o-1' },
    createdOn: NEWEST.toISOString(),
    ...overrides,
  });
}

function page(items: UserNotificationDto[], total = items.length): PagedDataOfUserNotificationDto {
  return PagedDataOfUserNotificationDto.fromJS({ pageNumber: 1, pageSize: NOTIFICATIONS_PAGE_SIZE, total, data: items.map((i) => i.toJSON()) });
}

describe('NotificationsFacade', () => {
  let facade: NotificationsFacade;
  let getPaged: jest.Mock;
  let markRead: jest.Mock;
  let markAllRead: jest.Mock;
  let refreshBadge: jest.Mock;
  let badgeCount: ReturnType<typeof signal<number>>;
  let navigate: jest.Mock;
  let showSuccessTranslated: jest.Mock;
  let onLangChange: Subject<{ lang: string }>;

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page([item(), item({ id: 'n-2', createdOn: OLDER.toISOString(), readOn: OLDER.toISOString() })])));
    markRead = jest.fn().mockReturnValue(of(MarkNotificationReadResponse.fromJS({ id: 'n-1', readOn: NEWEST.toISOString() })));
    markAllRead = jest.fn().mockReturnValue(of(MarkAllNotificationsReadResponse.fromJS({ markedCount: 1 })));
    refreshBadge = jest.fn();
    badgeCount = signal(0);
    navigate = jest.fn().mockResolvedValue(true);
    showSuccessTranslated = jest.fn();
    onLangChange = new Subject<{ lang: string }>();

    TestBed.configureTestingModule({
      providers: [
        NotificationsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminNotificationClient: { getPaged, markRead, markAllRead },
          },
        },
        {
          provide: AdminNotificationBadgeService,
          useValue: {
            refresh: refreshBadge,
            unreadCount: badgeCount,
          },
        },
        { provide: SnackbarService, useValue: { showSuccessTranslated } },
        { provide: Router, useValue: { navigate } },
        {
          provide: TranslateService,
          useValue: {
            instant: (key: string, params?: Record<string, unknown>) => (params ? `${key}:${JSON.stringify(params)}` : key),
            currentLang: 'en',
            onLangChange,
          },
        },
      ],
    });

    facade = TestBed.inject(NotificationsFacade);
  });

  describe('load', () => {
    it('starts empty, loading, with no error', () => {
      expect(facade.rows()).toEqual([]);
      expect(facade.initialLoading()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
      expect(facade.totalRecords()).toBe(0);
    });

    it('reads the first page of twenty, newest first, with the audience left to the server', () => {
      facade.load();

      expect(getPaged).toHaveBeenCalledWith(undefined, undefined, 0, NOTIFICATIONS_PAGE_SIZE);
      expect(facade.rows().map((r) => r.id)).toEqual(['n-1', 'n-2']);
      expect(facade.rows()[0].isUnread).toBe(true);
      expect(facade.rows()[1].isUnread).toBe(false);
      expect(facade.totalRecords()).toBe(2);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('settles the empty state when the feed has nothing', () => {
      getPaged.mockReturnValue(of(page([])));

      facade.load();

      expect(facade.rows()).toEqual([]);
      expect(facade.totalRecords()).toBe(0);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
      expect(facade.hasUnread()).toBe(false);
    });

    it('settles the error state, keeps nothing on screen, and clears it on the next successful read', () => {
      getPaged.mockReturnValueOnce(throwError(() => new Error('boom')));

      facade.load();

      expect(facade.hasError()).toBe(true);
      expect(facade.rows()).toEqual([]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);

      facade.load();

      expect(facade.hasError()).toBe(false);
      expect(facade.rows()).toHaveLength(2);
    });

    it('pages server-side from the offset the paginator reports', () => {
      facade.onPageChange(NOTIFICATIONS_PAGE_SIZE * 2);

      expect(facade.first()).toBe(NOTIFICATIONS_PAGE_SIZE * 2);
      expect(getPaged).toHaveBeenLastCalledWith(undefined, undefined, NOTIFICATIONS_PAGE_SIZE * 2, NOTIFICATIONS_PAGE_SIZE);
    });

    it('re-renders the rows in the new language without another read', () => {
      getPaged.mockReturnValue(
        of(page([item({ eventKey: 'admin.order.crew_lost', args: { orderNumber: 'ORD-1', cause: 'dropped', statusAtLoss: 'Confirmed', cleaningDateTime: '2026-09-20T07:00:00.0000000Z', orderId: 'o-1' } })]))
      );
      facade.load();
      const english = facade.rows()[0].params['cleaningDateTime'];

      onLangChange.next({ lang: 'cs' });

      expect(getPaged).toHaveBeenCalledTimes(1);
      expect(facade.rows()[0].params['cleaningDateTime']).toBe(new Date('2026-09-20T07:00:00Z').toLocaleString('cs', { dateStyle: 'medium', timeStyle: 'short' }));
      expect(facade.rows()[0].params['cleaningDateTime']).not.toBe(english);
    });
  });

  describe('open', () => {
    beforeEach(() => facade.load());

    it('marks an unread row read on the server, refreshes the badge, then lands on its resource', () => {
      facade.open(facade.rows()[0]);

      const command: MarkNotificationReadCommand = markRead.mock.calls[0][0];
      expect(command).toBeInstanceOf(MarkNotificationReadCommand);
      expect(command.toJSON()).toEqual({ id: 'n-1', audience: undefined });
      expect(refreshBadge).toHaveBeenCalledTimes(1);
      expect(navigate).toHaveBeenCalledWith(['dispute-management', 'd-1']);
      expect(facade.openingId()).toBeNull();
    });

    it('lands on the resource without a server call when the row is already read', () => {
      facade.open(facade.rows()[1]);

      expect(markRead).not.toHaveBeenCalled();
      expect(refreshBadge).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['dispute-management', 'd-1']);
    });

    it('still lands on the resource when the mark-read is refused — the toast is the interceptor\'s', () => {
      markRead.mockReturnValue(throwError(() => new Error('refused')));

      facade.open(facade.rows()[0]);

      expect(refreshBadge).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['dispute-management', 'd-1']);
      expect(facade.openingId()).toBeNull();
    });

    it('marks a row with nowhere to go read and re-reads the page so it loses its emphasis', () => {
      getPaged.mockReturnValue(of(page([item({ eventKey: 'admin.future.event', args: {} })])));
      facade.load();
      getPaged.mockClear();

      facade.open(facade.rows()[0]);

      expect(markRead).toHaveBeenCalledTimes(1);
      expect(navigate).not.toHaveBeenCalled();
      expect(getPaged).toHaveBeenCalledTimes(1);
    });

    it('ignores a second click while the first is in flight', () => {
      const pending = new Subject<MarkNotificationReadResponse>();
      markRead.mockReturnValue(pending);

      facade.open(facade.rows()[0]);
      expect(facade.openingId()).toBe('n-1');
      facade.open(facade.rows()[0]);

      expect(markRead).toHaveBeenCalledTimes(1);
      pending.next(MarkNotificationReadResponse.fromJS({ id: 'n-1', readOn: NEWEST.toISOString() }));
      pending.complete();
      expect(facade.openingId()).toBeNull();
      expect(navigate).toHaveBeenCalledTimes(1);
    });
  });

  describe('markAllRead', () => {
    it('sends the millisecond after the newest fetched createdOn as the watermark, toasts the count, re-reads and refreshes the badge', () => {
      facade.load();
      expect(facade.hasUnread()).toBe(true);
      getPaged.mockClear();

      facade.markAllRead();

      const command: MarkAllNotificationsReadCommand = markAllRead.mock.calls[0][0];
      expect(command).toBeInstanceOf(MarkAllNotificationsReadCommand);
      expect(command.toJSON()).toEqual({ upToCreatedOn: WATERMARK_AFTER_NEWEST, audience: undefined });
      expect(showSuccessTranslated).toHaveBeenCalledWith('pages.notifications.messages.marked_all_read', { count: 1 });
      expect(getPaged).toHaveBeenCalledTimes(1);
      expect(refreshBadge).toHaveBeenCalledTimes(1);
      expect(facade.markingAll()).toBe(false);
    });

    it('keeps the newest watermark across pages, so a later page never narrows it', () => {
      facade.load();
      getPaged.mockReturnValue(of(page([item({ id: 'n-9', createdOn: OLDER.toISOString() })], 40)));
      facade.onPageChange(NOTIFICATIONS_PAGE_SIZE);

      facade.markAllRead();

      const command: MarkAllNotificationsReadCommand = markAllRead.mock.calls[0][0];
      expect(command.toJSON()['upToCreatedOn']).toBe(WATERMARK_AFTER_NEWEST);
    });

    it('stays offered while the badge still counts unread rows on another page, even when this page is all read', () => {
      getPaged.mockReturnValue(of(page([item({ readOn: NEWEST.toISOString() })], 40)));
      facade.load();
      expect(facade.hasUnread()).toBe(false);

      badgeCount.set(2);

      expect(facade.hasUnread()).toBe(true);
    });

    it('does nothing before anything was fetched', () => {
      facade.markAllRead();

      expect(markAllRead).not.toHaveBeenCalled();
    });

    it('re-reads on a refusal too, without a toast of its own, and releases the button', () => {
      facade.load();
      getPaged.mockClear();
      markAllRead.mockReturnValue(throwError(() => new Error('refused')));

      facade.markAllRead();

      expect(showSuccessTranslated).not.toHaveBeenCalled();
      expect(refreshBadge).not.toHaveBeenCalled();
      expect(getPaged).toHaveBeenCalledTimes(1);
      expect(facade.markingAll()).toBe(false);
    });

    it('ignores a second click while the first is in flight', () => {
      facade.load();
      const pending = new Subject<MarkAllNotificationsReadResponse>();
      markAllRead.mockReturnValue(pending);

      facade.markAllRead();
      expect(facade.markingAll()).toBe(true);
      facade.markAllRead();

      expect(markAllRead).toHaveBeenCalledTimes(1);
      pending.next(MarkAllNotificationsReadResponse.fromJS({ markedCount: 1 }));
      pending.complete();
      expect(facade.markingAll()).toBe(false);
    });
  });
});
