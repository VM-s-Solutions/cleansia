import {
  MarkAllNotificationsReadCommand,
  MarkNotificationReadCommand,
  OrderStatus,
  UserNotificationDto,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';

/**
 * Mirror of the backend `AdminNotificationEventCatalog` — every key the admin feed can carry. The
 * copy spec walks the C# file and fails when the two lists differ, so a key the backend adds arrives
 * here with its five-locale sentence rather than as a row the page cannot read.
 */
export const ADMIN_NOTIFICATION_EVENT_KEYS = [
  'admin.order.new',
  'admin.order.crew_lost',
  'admin.dispute.filed',
  'admin.dispute.chargeback',
  'admin.payment.failed',
  'admin.erasure.failed',
  'admin.company.wind_down_requested',
  'admin.company.wind_down_run',
  'admin.company.archived',
] as const;

export type AdminNotificationEventKey = (typeof ADMIN_NOTIFICATION_EVENT_KEYS)[number];

export const NOTIFICATIONS_PAGE_SIZE = 20;

const EVENTS_ROOT = 'pages.notifications.events';
export const UNKNOWN_EVENT_TITLE_KEY = `${EVENTS_ROOT}.unknown.title`;

const CREW_LOST_KEY: AdminNotificationEventKey = 'admin.order.crew_lost';
const CREW_LOST_UNDER_WAY_STATUSES: ReadonlySet<string> = new Set([
  OrderStatus[OrderStatus.OnTheWay],
  OrderStatus[OrderStatus.InProgress],
]);

const DAY_ARGS: ReadonlySet<string> = new Set(['day', 'windDownFrom', 'archivedOn']);
const INSTANT_ARGS: ReadonlySet<string> = new Set(['cleaningDateTime']);

export type NotificationArgs = Record<string, string>;

export enum NotificationFamily {
  Order = 'order',
  Dispute = 'dispute',
  Payment = 'payment',
  Erasure = 'erasure',
  Company = 'company',
  Unknown = 'unknown',
}

export const NOTIFICATION_FAMILY_ICONS: Record<NotificationFamily, string> = {
  [NotificationFamily.Order]: 'pi pi-shopping-cart',
  [NotificationFamily.Dispute]: 'pi pi-flag',
  [NotificationFamily.Payment]: 'pi pi-credit-card',
  [NotificationFamily.Erasure]: 'pi pi-shield',
  [NotificationFamily.Company]: 'pi pi-building',
  [NotificationFamily.Unknown]: 'pi pi-bell',
};

export interface NotificationRow {
  id: string;
  eventKey: string;
  titleKey: string;
  bodyKey: string | null;
  params: NotificationArgs;
  createdOn: Date;
  createdOnLabel: string;
  isUnread: boolean;
  route: string[] | null;
  interactive: boolean;
  family: NotificationFamily;
  icon: string;
}

export function isAdminNotificationEventKey(key: string | undefined): key is AdminNotificationEventKey {
  return key !== undefined && (ADMIN_NOTIFICATION_EVENT_KEYS as readonly string[]).includes(key);
}

export function getNotificationFamily(eventKey: string | undefined): NotificationFamily {
  if (!isAdminNotificationEventKey(eventKey)) return NotificationFamily.Unknown;
  const segment = eventKey.split('.')[1];
  const families = Object.values(NotificationFamily) as string[];
  return families.includes(segment) ? (segment as NotificationFamily) : NotificationFamily.Unknown;
}

export function getNotificationTitleKey(eventKey: string | undefined): string {
  return isAdminNotificationEventKey(eventKey) ? `${EVENTS_ROOT}.${eventKey}.title` : UNKNOWN_EVENT_TITLE_KEY;
}

export function getNotificationBodyKey(eventKey: string | undefined, args: NotificationArgs): string | null {
  if (!isAdminNotificationEventKey(eventKey)) return null;
  if (eventKey === CREW_LOST_KEY && CREW_LOST_UNDER_WAY_STATUSES.has(args['statusAtLoss'] ?? '')) {
    return `${EVENTS_ROOT}.${eventKey}.body_under_way`;
  }
  return `${EVENTS_ROOT}.${eventKey}.body`;
}

export function buildNotificationParams(
  args: NotificationArgs | undefined,
  lang: string,
  translate: (key: string) => string
): NotificationArgs {
  const params: NotificationArgs = {};
  for (const [name, value] of Object.entries(args ?? {})) {
    params[name] = formatArg(name, value, lang, translate);
  }
  return params;
}

function formatArg(name: string, value: string, lang: string, translate: (key: string) => string): string {
  if (name === 'cause') return translate(`${EVENTS_ROOT}.${CREW_LOST_KEY}.causes.${value}`);
  if (DAY_ARGS.has(name)) return formatDay(value, lang);
  if (INSTANT_ARGS.has(name)) return formatInstant(value, lang);
  return value;
}

function formatDay(value: string, lang: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return value;
  return formatDate(new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])), lang);
}

function formatInstant(value: string, lang: string): string {
  const instant = new Date(value);
  return Number.isNaN(instant.getTime()) ? value : formatStamp(instant, lang);
}

function formatStamp(instant: Date, lang: string): string {
  return formatDate(instant, lang, 'dateTime');
}

export function getNotificationRoute(eventKey: string | undefined, args: NotificationArgs): string[] | null {
  switch (getNotificationFamily(eventKey)) {
    case NotificationFamily.Order:
    case NotificationFamily.Payment:
      return args['orderId'] ? [CleansiaAdminRoute.ORDER_MANAGEMENT, args['orderId']] : null;
    case NotificationFamily.Dispute:
      return args['disputeId'] ? [CleansiaAdminRoute.DISPUTE_MANAGEMENT, args['disputeId']] : null;
    case NotificationFamily.Erasure:
      return [CleansiaAdminRoute.DATA_PROTECTION];
    case NotificationFamily.Company:
      return [CleansiaAdminRoute.COMPANY_LIFECYCLE];
    default:
      return null;
  }
}

export function buildNotificationRows(
  items: UserNotificationDto[],
  lang: string,
  translate: (key: string) => string
): NotificationRow[] {
  const rows: NotificationRow[] = [];
  for (const item of items) {
    if (!item.id) continue;
    const args = item.args ?? {};
    const family = getNotificationFamily(item.eventKey);
    const isUnread = !item.readOn;
    const route = getNotificationRoute(item.eventKey, args);
    rows.push({
      id: item.id,
      eventKey: item.eventKey ?? '',
      titleKey: getNotificationTitleKey(item.eventKey),
      bodyKey: getNotificationBodyKey(item.eventKey, args),
      params: buildNotificationParams(args, lang, translate),
      createdOn: item.createdOn,
      createdOnLabel: formatStamp(item.createdOn, lang),
      isUnread,
      route,
      interactive: isUnread || route !== null,
      family,
      icon: NOTIFICATION_FAMILY_ICONS[family],
    });
  }
  return rows;
}

export function newestCreatedOn(items: UserNotificationDto[]): Date | undefined {
  let newest: Date | undefined;
  for (const item of items) {
    if (item.createdOn && (!newest || item.createdOn > newest)) newest = item.createdOn;
  }
  return newest;
}

export function buildMarkReadCommand(id: string): MarkNotificationReadCommand {
  const command = new MarkNotificationReadCommand();
  command.id = id;
  return command;
}

export function buildMarkAllReadCommand(upToCreatedOn: Date | undefined): MarkAllNotificationsReadCommand {
  const command = new MarkAllNotificationsReadCommand();
  // A JS Date holds milliseconds, the column holds microseconds, and the server keeps rows at or
  // before the watermark — sent as fetched, the newest row's own sub-millisecond tail would leave it
  // unread on every click. The next whole millisecond covers it.
  command.upToCreatedOn = upToCreatedOn && new Date(upToCreatedOn.getTime() + 1);
  return command;
}
