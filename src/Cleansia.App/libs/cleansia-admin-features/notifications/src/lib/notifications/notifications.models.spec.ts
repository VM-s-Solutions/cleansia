import { MarkAllNotificationsReadCommand, MarkNotificationReadCommand, UserNotificationDto } from '@cleansia/admin-services';
import {
  ADMIN_NOTIFICATION_EVENT_KEYS,
  buildMarkAllReadCommand,
  buildMarkReadCommand,
  buildNotificationParams,
  buildNotificationRows,
  getNotificationBodyKey,
  getNotificationFamily,
  getNotificationRoute,
  getNotificationTitleKey,
  isAdminNotificationEventKey,
  newestCreatedOn,
  NOTIFICATION_FAMILY_ICONS,
  NotificationFamily,
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
  it('pins the nine keys the backend catalogue declares, in its order', () => {
    expect(ADMIN_NOTIFICATION_EVENT_KEYS).toEqual([
      'admin.order.new',
      'admin.order.crew_lost',
      'admin.dispute.filed',
      'admin.dispute.chargeback',
      'admin.payment.failed',
      'admin.erasure.failed',
      'admin.company.wind_down_requested',
      'admin.company.wind_down_run',
      'admin.company.archived',
    ]);
  });

  it('recognises only those keys', () => {
    expect(isAdminNotificationEventKey('admin.order.new')).toBe(true);
    expect(isAdminNotificationEventKey('order.confirmed')).toBe(false);
    expect(isAdminNotificationEventKey('admin.order.unknown')).toBe(false);
    expect(isAdminNotificationEventKey(undefined)).toBe(false);
  });

  it('sorts every key into a family the page has an icon for', () => {
    const families = ADMIN_NOTIFICATION_EVENT_KEYS.map(getNotificationFamily);
    expect(families).toEqual([
      NotificationFamily.Order,
      NotificationFamily.Order,
      NotificationFamily.Dispute,
      NotificationFamily.Dispute,
      NotificationFamily.Payment,
      NotificationFamily.Erasure,
      NotificationFamily.Company,
      NotificationFamily.Company,
      NotificationFamily.Company,
    ]);
    for (const family of families) {
      expect(NOTIFICATION_FAMILY_ICONS[family]).toMatch(/^pi pi-/);
    }
    expect(getNotificationFamily('something.else')).toBe(NotificationFamily.Unknown);
    expect(NOTIFICATION_FAMILY_ICONS[NotificationFamily.Unknown]).toMatch(/^pi pi-/);
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
      'en-GB',
      translate
    );

    expect(params['day']).toBe(new Date(2026, 8, 19).toLocaleDateString('en-GB'));
    expect(params['windDownFrom']).toBe(new Date(2026, 9, 1).toLocaleDateString('en-GB'));
    expect(params['archivedOn']).toBe('not-a-date');
  });

  it('tolerates a row without args', () => {
    expect(buildNotificationParams(undefined, 'en', translate)).toEqual({});
  });
});

describe('getNotificationRoute', () => {
  it.each(['admin.order.new', 'admin.order.crew_lost', 'admin.payment.failed'])('%s opens the order', (key) => {
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

  it('has nowhere to go when the id the link needs is missing, or the key is unknown', () => {
    expect(getNotificationRoute('admin.order.new', {})).toBeNull();
    expect(getNotificationRoute('admin.dispute.filed', { orderId: 'o-1' })).toBeNull();
    expect(getNotificationRoute('admin.future.event', { orderId: 'o-1' })).toBeNull();
  });
});

describe('buildNotificationRows', () => {
  it('renders a row per item with its copy keys, params, route, read state and family icon', () => {
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
      family: NotificationFamily.Dispute,
      icon: NOTIFICATION_FAMILY_ICONS[NotificationFamily.Dispute],
    });
    expect(rows[1].isUnread).toBe(false);
  });

  it('keeps a row whose key it does not know, under the shared title, with no body and no link', () => {
    const [row] = buildNotificationRows([dto({ eventKey: 'admin.future.event', args: { orderId: 'o-1' } })], 'en', translate);

    expect(row.titleKey).toBe(UNKNOWN_EVENT_TITLE_KEY);
    expect(row.bodyKey).toBeNull();
    expect(row.route).toBeNull();
    expect(row.family).toBe(NotificationFamily.Unknown);
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

  it('mark-all-read carries the watermark the page fetched', () => {
    const upTo = new Date('2026-09-19T08:30:00Z');
    const command = buildMarkAllReadCommand(upTo);

    expect(command).toBeInstanceOf(MarkAllNotificationsReadCommand);
    expect(Object.keys(command.toJSON()).sort()).toEqual(['audience', 'upToCreatedOn']);
    expect(command.toJSON()).toEqual({ upToCreatedOn: '2026-09-19T08:30:00.000Z', audience: undefined });
  });

  it('mark-all-read without a watermark asks for everything', () => {
    expect(buildMarkAllReadCommand(undefined).toJSON()).toEqual({ upToCreatedOn: undefined, audience: undefined });
  });
});
