import { MarkAllNotificationsReadCommand, MarkNotificationReadCommand, UserNotificationDto } from '@cleansia/admin-services';
import {
  ADMIN_CRITICAL_EVENT_KEYS,
  ADMIN_NOTIFICATION_EVENT_KEYS,
  buildMarkAllReadCommand,
  buildMarkReadCommand,
  buildNotificationParams,
  buildNotificationRows,
  getNotificationBodyKey,
  getNotificationRoute,
  getNotificationTitleKey,
  getNotificationType,
  isAdminNotificationEventKey,
  isCriticalNotification,
  newestCreatedOn,
  NOTIFICATION_TYPE_ICONS,
  NotificationType,
  UNKNOWN_EVENT_TITLE_KEY,
} from './notifications.models';

const translate = (key: string) => `t(${key})`;

function dto(overrides: Partial<UserNotificationDto> = {}): UserNotificationDto {
  return UserNotificationDto.fromJS({
    id: 'n-1',
    eventKey: 'admin.dispute.filed',
    args: { orderNumber: 'ORD-1', reason: 'ServiceQuality', disputeId: 'd-1', orderId: 'o-1' },
    createdOn: '2026-09-19T08:30:00Z',
    readOn: undefined,
    ...overrides,
  });
}

describe('the admin event catalogue mirror', () => {
  it('pins the twenty keys the backend catalogue declares, in its order', () => {
    expect(ADMIN_NOTIFICATION_EVENT_KEYS).toEqual([
      'admin.order.new',
      'admin.order.crew_lost',
      'admin.order.crew_short',
      'admin.order.cleaner_not_started',
      'admin.order.no_cleaner_yet',
      'admin.order.no_cleaner_soon',
      'admin.order.lockout_reported',
      'admin.order.cash_not_paid',
      'admin.dispute.filed',
      'admin.dispute.chargeback',
      'admin.dispute.chargeback_unmatched',
      'admin.payment.failed',
      'admin.payment.refund_stuck',
      'admin.payment.refund_needs_retry',
      'admin.payment.refund_without_cancel',
      'admin.erasure.failed',
      'admin.company.wind_down_requested',
      'admin.company.wind_down_run',
      'admin.company.archived',
      'admin.referral.held',
    ]);
  });

  it('recognises only those keys', () => {
    expect(isAdminNotificationEventKey('admin.order.new')).toBe(true);
    expect(isAdminNotificationEventKey('order.completed')).toBe(false);
    expect(isAdminNotificationEventKey('admin.order.unknown')).toBe(false);
    expect(isAdminNotificationEventKey(undefined)).toBe(false);
  });

  it('routes an unmatched chargeback nowhere, since there is no dispute to open', () => {
    expect(getNotificationRoute('admin.dispute.chargeback_unmatched', { amount: '100 CZK', stripeDisputeId: 'dp_1' })).toBeNull();
  });

  it('gives every key its type, each type its own icon, and an unknown key reads Other', () => {
    expect(Object.fromEntries(ADMIN_NOTIFICATION_EVENT_KEYS.map((key) => [key, getNotificationType(key)]))).toEqual({
      'admin.order.new': NotificationType.Order,
      'admin.order.crew_lost': NotificationType.Cleaner,
      'admin.order.crew_short': NotificationType.Cleaner,
      'admin.order.cleaner_not_started': NotificationType.Cleaner,
      'admin.order.no_cleaner_yet': NotificationType.Cleaner,
      'admin.order.no_cleaner_soon': NotificationType.Cleaner,
      'admin.order.lockout_reported': NotificationType.Cleaner,
      'admin.order.cash_not_paid': NotificationType.Payment,
      'admin.dispute.filed': NotificationType.Dispute,
      'admin.dispute.chargeback': NotificationType.Dispute,
      'admin.dispute.chargeback_unmatched': NotificationType.Dispute,
      'admin.payment.failed': NotificationType.Payment,
      'admin.payment.refund_stuck': NotificationType.Refund,
      'admin.payment.refund_needs_retry': NotificationType.Refund,
      'admin.payment.refund_without_cancel': NotificationType.Refund,
      'admin.erasure.failed': NotificationType.DataProtection,
      'admin.company.wind_down_requested': NotificationType.Company,
      'admin.company.wind_down_run': NotificationType.Company,
      'admin.company.archived': NotificationType.Company,
      'admin.referral.held': NotificationType.Referral,
    });
    expect(NOTIFICATION_TYPE_ICONS).toEqual({
      [NotificationType.Order]: 'pi pi-shopping-cart',
      [NotificationType.Cleaner]: 'pi pi-user',
      [NotificationType.Dispute]: 'pi pi-flag',
      [NotificationType.Payment]: 'pi pi-credit-card',
      [NotificationType.Refund]: 'pi pi-replay',
      [NotificationType.Referral]: 'pi pi-users',
      [NotificationType.DataProtection]: 'pi pi-shield',
      [NotificationType.Company]: 'pi pi-building',
      [NotificationType.Other]: 'pi pi-bell',
    });
    expect(new Set(Object.values(NOTIFICATION_TYPE_ICONS)).size).toBe(Object.values(NotificationType).length);
    expect(getNotificationType('something.else')).toBe(NotificationType.Other);
    expect(getNotificationType(undefined)).toBe(NotificationType.Other);
  });

  it('marks only the catalogue critical keys critical, and an unknown key is info', () => {
    expect(ADMIN_NOTIFICATION_EVENT_KEYS.filter(isCriticalNotification)).toEqual([...ADMIN_CRITICAL_EVENT_KEYS]);
    expect(ADMIN_NOTIFICATION_EVENT_KEYS.filter((key) => !isCriticalNotification(key))).toEqual([
      'admin.order.new',
      'admin.payment.failed',
      'admin.company.wind_down_requested',
      'admin.company.wind_down_run',
      'admin.company.archived',
    ]);
    expect(isCriticalNotification('admin.future.event')).toBe(false);
    expect(isCriticalNotification(undefined)).toBe(false);
  });
});

describe('the copy keys', () => {
  it('nests the event key under the page block, so the locale carries it as a path', () => {
    expect(getNotificationTitleKey('admin.order.new')).toBe('pages.notifications.events.admin.order.new.title');
    expect(getNotificationBodyKey('admin.order.new', {})).toBe('pages.notifications.events.admin.order.new.body');
  });

  it('falls back to one shared title and no body for a key the console does not know', () => {
    expect(getNotificationTitleKey('admin.future.event')).toBe(UNKNOWN_EVENT_TITLE_KEY);
    expect(getNotificationBodyKey('admin.future.event', {})).toBeNull();
  });

  it.each(['OnTheWay', 'InProgress'])(
    'says the clean was under way when the crew was lost at %s',
    (statusAtLoss) => {
      expect(getNotificationBodyKey('admin.order.crew_lost', { statusAtLoss })).toBe(
        'pages.notifications.events.admin.order.crew_lost.body_under_way'
      );
    }
  );

  it.each(['Confirmed', 'New', ''])('says the order is back on the board when the crew was lost at %p', (statusAtLoss) => {
    expect(getNotificationBodyKey('admin.order.crew_lost', { statusAtLoss })).toBe(
      'pages.notifications.events.admin.order.crew_lost.body'
    );
  });

  it('words an order that lost one of its cleaners in its one sentence, naming the cause in crew-lost\'s words', () => {
    const [row] = buildNotificationRows(
      [
        dto({
          eventKey: 'admin.order.crew_short',
          args: {
            orderNumber: 'ORD-8',
            cause: 'dropped',
            statusAtLoss: 'Confirmed',
            cleaningDateTime: '2026-10-26T08:00:00.0000000Z',
            orderId: 'o-8',
          },
        }),
      ],
      'en',
      translate
    );

    expect(row.type).toBe(NotificationType.Cleaner);
    expect(row.titleKey).toBe('pages.notifications.events.admin.order.crew_short.title');
    expect(row.bodyKey).toBe('pages.notifications.events.admin.order.crew_short.body');
    expect(row.params['cause']).toBe('t(pages.notifications.events.admin.order.crew_lost.causes.dropped)');
    expect(row.route).toEqual(['order-management', 'o-8']);
  });

  it('words an order still without a cleaner ahead of its slot in one sentence, with the slot as a date and time', () => {
    const [row] = buildNotificationRows(
      [
        dto({
          eventKey: 'admin.order.no_cleaner_yet',
          args: { orderNumber: 'ORD-7', cleaningDateTime: '2026-10-25T08:00:00.0000000Z', orderId: 'o-7' },
        }),
      ],
      'en',
      translate
    );

    expect(row.type).toBe(NotificationType.Cleaner);
    expect(row.titleKey).toBe('pages.notifications.events.admin.order.no_cleaner_yet.title');
    expect(row.bodyKey).toBe('pages.notifications.events.admin.order.no_cleaner_yet.body');
    expect(row.params).toEqual({
      orderNumber: 'ORD-7',
      cleaningDateTime: new Date('2026-10-25T08:00:00Z').toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' }),
      orderId: 'o-7',
    });
    expect(row.route).toEqual(['order-management', 'o-7']);
  });
});

describe('buildNotificationParams', () => {
  it('passes the server-formatted args through untouched', () => {
    expect(buildNotificationParams({ orderNumber: 'ORD-9', amount: '1 200 Kč' }, 'en', translate)).toEqual({
      orderNumber: 'ORD-9',
      amount: '1 200 Kč',
    });
  });

  it('formats the cleaning instant in the reader language and names the cause in words', () => {
    const params = buildNotificationParams(
      { orderNumber: 'ORD-9', cause: 'dropped', statusAtLoss: 'Confirmed', cleaningDateTime: '2026-09-20T07:00:00.0000000Z' },
      'en',
      translate
    );

    expect(params['cause']).toBe('t(pages.notifications.events.admin.order.crew_lost.causes.dropped)');
    expect(params['cleaningDateTime']).toBe(
      new Date('2026-09-20T07:00:00Z').toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' })
    );
    expect(params['orderNumber']).toBe('ORD-9');
  });

  it('formats the day arguments as dates and leaves an unparseable one as sent', () => {
    const params = buildNotificationParams(
      { day: '2026-09-19', windDownFrom: '2026-10-01', archivedOn: 'not-a-date' },
      'cs',
      translate
    );

    expect(params['day']).toBe('19. 9. 2026');
    expect(params['windDownFrom']).toBe('1. 10. 2026');
    expect(params['archivedOn']).toBe('not-a-date');
  });

  it('tolerates a row without args', () => {
    expect(buildNotificationParams(undefined, 'en', translate)).toEqual({});
  });
});

describe('getNotificationRoute', () => {
  it.each([
    'admin.order.new',
    'admin.order.crew_lost',
    'admin.order.crew_short',
    'admin.order.cleaner_not_started',
    'admin.order.no_cleaner_yet',
    'admin.order.no_cleaner_soon',
    'admin.order.lockout_reported',
    'admin.order.cash_not_paid',
    'admin.payment.failed',
    'admin.payment.refund_stuck',
    'admin.payment.refund_needs_retry',
    'admin.payment.refund_without_cancel',
  ])('%s opens the order', (key) => {
    expect(getNotificationRoute(key, { orderId: 'o-1', disputeId: 'd-1' })).toEqual(['order-management', 'o-1']);
  });

  it.each(['admin.dispute.filed', 'admin.dispute.chargeback'])('%s opens the dispute', (key) => {
    expect(getNotificationRoute(key, { orderId: 'o-1', disputeId: 'd-1' })).toEqual(['dispute-management', 'd-1']);
  });

  it('a failed erasure opens the data-protection requests page', () => {
    expect(getNotificationRoute('admin.erasure.failed', { requestId: 'r-1' })).toEqual(['data-protection']);
  });

  it.each(['admin.company.wind_down_requested', 'admin.company.wind_down_run', 'admin.company.archived'])(
    '%s opens the company lifecycle page',
    (key) => {
      expect(getNotificationRoute(key, {})).toEqual(['company-lifecycle']);
    }
  );

  it('a held referral opens the referrals page', () => {
    expect(getNotificationRoute('admin.referral.held', { referralId: 'r-1' })).toEqual(['loyalty/referrals']);
  });

  it('has nowhere to go when the id the link needs is missing, or the key is unknown', () => {
    expect(getNotificationRoute('admin.order.new', {})).toBeNull();
    expect(getNotificationRoute('admin.dispute.filed', { orderId: 'o-1' })).toBeNull();
    expect(getNotificationRoute('admin.future.event', { orderId: 'o-1' })).toBeNull();
  });
});

describe('buildNotificationRows', () => {
  it('renders a row per item with its copy keys, params, route, read state, type, severity and type icon', () => {
    const rows = buildNotificationRows([dto(), dto({ id: 'n-2', readOn: new Date('2026-09-19T09:00:00Z') })], 'en', translate);

    expect(rows).toHaveLength(2);
    expect(rows[0]).toEqual({
      id: 'n-1',
      eventKey: 'admin.dispute.filed',
      titleKey: 'pages.notifications.events.admin.dispute.filed.title',
      bodyKey: 'pages.notifications.events.admin.dispute.filed.body',
      params: { orderNumber: 'ORD-1', reason: 'ServiceQuality', disputeId: 'd-1', orderId: 'o-1' },
      createdOn: new Date('2026-09-19T08:30:00Z'),
      createdOnLabel: new Date('2026-09-19T08:30:00Z').toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' }),
      isUnread: true,
      route: ['dispute-management', 'd-1'],
      interactive: true,
      type: NotificationType.Dispute,
      typeLabelKey: 'pages.notifications.types.dispute',
      critical: true,
      icon: NOTIFICATION_TYPE_ICONS[NotificationType.Dispute],
    });
    expect(rows[1].isUnread).toBe(false);
    expect(rows[1].interactive).toBe(true);
  });

  it('keeps a row whose key it does not know, under the shared title, with no body and no link', () => {
    const [row] = buildNotificationRows([dto({ eventKey: 'admin.future.event', args: { orderId: 'o-1' } })], 'en', translate);

    expect(row.titleKey).toBe(UNKNOWN_EVENT_TITLE_KEY);
    expect(row.bodyKey).toBeNull();
    expect(row.route).toBeNull();
    expect(row.type).toBe(NotificationType.Other);
    expect(row.critical).toBe(false);
    expect(row.interactive).toBe(true);
  });

  it('is inert once a row with nowhere to go has been read — a click could neither mark nor land anywhere', () => {
    const rows = buildNotificationRows(
      [
        dto({ eventKey: 'admin.future.event', args: { orderId: 'o-1' }, readOn: new Date('2026-09-19T09:00:00Z') }),
        dto({ id: 'n-2', eventKey: 'admin.order.new', args: { orderNumber: 'ORD-1' }, readOn: new Date('2026-09-19T09:00:00Z') }),
      ],
      'en',
      translate
    );

    expect(rows.map((row) => row.interactive)).toEqual([false, false]);
  });

  it('drops a row without an id — nothing could be marked read', () => {
    expect(buildNotificationRows([dto({ id: undefined })], 'en', translate)).toEqual([]);
  });
});

describe('newestCreatedOn', () => {
  it('is the latest createdOn of the fetched rows, whatever their order', () => {
    const items = [
      dto({ createdOn: new Date('2026-09-18T08:00:00Z') }),
      dto({ createdOn: new Date('2026-09-19T08:00:00Z') }),
      dto({ createdOn: new Date('2026-09-17T08:00:00Z') }),
    ];
    expect(newestCreatedOn(items)).toEqual(new Date('2026-09-19T08:00:00Z'));
  });

  it('is undefined for no rows', () => {
    expect(newestCreatedOn([])).toBeUndefined();
  });
});

describe('the wire bodies', () => {
  it('mark-read carries the row id; the audience is the server\'s to set', () => {
    const command = buildMarkReadCommand('n-1');

    expect(command).toBeInstanceOf(MarkNotificationReadCommand);
    expect(Object.keys(command.toJSON()).sort()).toEqual(['audience', 'id']);
    expect(command.toJSON()).toEqual({ id: 'n-1', audience: undefined });
  });

  it('mark-all-read carries the watermark the page fetched, widened to the next whole millisecond', () => {
    const upTo = new Date('2026-09-19T08:30:00Z');
    const command = buildMarkAllReadCommand(upTo);

    expect(command).toBeInstanceOf(MarkAllNotificationsReadCommand);
    expect(Object.keys(command.toJSON()).sort()).toEqual(['audience', 'upToCreatedOn']);
    expect(command.toJSON()).toEqual({ upToCreatedOn: '2026-09-19T08:30:00.001Z', audience: undefined });
  });

  it('mark-all-read covers the sub-millisecond tail the wire drops from the newest row, so that row is inside the watermark', () => {
    const row = UserNotificationDto.fromJS({ ...dto().toJSON(), createdOn: '2026-09-19T08:30:00.1234567Z' });
    expect(row.createdOn.toISOString()).toBe('2026-09-19T08:30:00.123Z');

    const command = buildMarkAllReadCommand(row.createdOn);

    expect(command.upToCreatedOn?.getTime()).toBeGreaterThan(row.createdOn.getTime());
    expect(command.toJSON()['upToCreatedOn']).toBe('2026-09-19T08:30:00.124Z');
  });

  it('mark-all-read without a watermark asks for everything', () => {
    expect(buildMarkAllReadCommand(undefined).toJSON()).toEqual({ upToCreatedOn: undefined, audience: undefined });
  });
});
