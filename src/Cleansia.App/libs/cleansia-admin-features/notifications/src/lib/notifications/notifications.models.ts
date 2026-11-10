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
  'admin.order.crew_short',
  'admin.order.cleaner_not_started',
  'admin.order.no_cleaner_yet',
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
] as const;

export type AdminNotificationEventKey = (typeof ADMIN_NOTIFICATION_EVENT_KEYS)[number];

/** Mirror of `AdminNotificationEventCatalog.Critical`, pinned against the C# list by the copy spec. */
export const ADMIN_CRITICAL_EVENT_KEYS: readonly AdminNotificationEventKey[] = [
  'admin.order.crew_lost',
  'admin.order.crew_short',
  'admin.order.cleaner_not_started',
  'admin.order.no_cleaner_yet',
  'admin.order.lockout_reported',
  'admin.order.cash_not_paid',
  'admin.dispute.filed',
  'admin.dispute.chargeback',
  'admin.dispute.chargeback_unmatched',
  'admin.payment.refund_stuck',
  'admin.payment.refund_needs_retry',
  'admin.payment.refund_without_cancel',
  'admin.erasure.failed',
  'admin.referral.held',
];

export type NotificationSeverity = 'all' | 'critical' | 'info';

export const NOTIFICATION_CRITICAL_FLAG: Record<NotificationSeverity, boolean | undefined> = {
  all: undefined,
  critical: true,
  info: false,
};

export const NOTIFICATIONS_PAGE_SIZE = 20;

const EVENTS_ROOT = 'pages.notifications.events';
export const UNKNOWN_EVENT_TITLE_KEY = `${EVENTS_ROOT}.unknown.title`;
const TYPES_ROOT = 'pages.notifications.types';

const CREW_LOST_KEY: AdminNotificationEventKey = 'admin.order.crew_lost';
const CREW_LOST_UNDER_WAY_STATUSES: ReadonlySet<string> = new Set([
  OrderStatus[OrderStatus.OnTheWay],
  OrderStatus[OrderStatus.InProgress],
]);

const DAY_ARGS: ReadonlySet<string> = new Set(['day', 'windDownFrom', 'archivedOn']);
const INSTANT_ARGS: ReadonlySet<string> = new Set(['cleaningDateTime']);

export type NotificationArgs = Record<string, string>;

export enum NotificationType {
  Order = 'order',
  Cleaner = 'cleaner',
  Dispute = 'dispute',
  Payment = 'payment',
  Refund = 'refund',
  Referral = 'referral',
  DataProtection = 'data_protection',
  Company = 'company',
  Other = 'other',
}

const NOTIFICATION_TYPES: Record<AdminNotificationEventKey, NotificationType> = {
  'admin.order.new': NotificationType.Order,
  'admin.order.crew_lost': NotificationType.Cleaner,
  'admin.order.crew_short': NotificationType.Cleaner,
  'admin.order.cleaner_not_started': NotificationType.Cleaner,
  'admin.order.no_cleaner_yet': NotificationType.Cleaner,
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
};

export const NOTIFICATION_TYPE_ICONS: Record<NotificationType, string> = {
  [NotificationType.Order]: 'pi pi-shopping-cart',
  [NotificationType.Cleaner]: 'pi pi-user',
  [NotificationType.Dispute]: 'pi pi-flag',
  [NotificationType.Payment]: 'pi pi-credit-card',
  [NotificationType.Refund]: 'pi pi-replay',
  [NotificationType.Referral]: 'pi pi-users',
  [NotificationType.DataProtection]: 'pi pi-shield',
  [NotificationType.Company]: 'pi pi-building',
  [NotificationType.Other]: 'pi pi-bell',
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
  type: NotificationType;
  typeLabelKey: string;
  critical: boolean;
  icon: string;
}

export function isAdminNotificationEventKey(key: string | undefined): key is AdminNotificationEventKey {
  return key !== undefined && (ADMIN_NOTIFICATION_EVENT_KEYS as readonly string[]).includes(key);
}

export function getNotificationType(eventKey: string | undefined): NotificationType {
  return isAdminNotificationEventKey(eventKey) ? NOTIFICATION_TYPES[eventKey] : NotificationType.Other;
}

export function isCriticalNotification(eventKey: string | undefined): boolean {
  return isAdminNotificationEventKey(eventKey) && ADMIN_CRITICAL_EVENT_KEYS.includes(eventKey);
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
  switch (getNotificationType(eventKey)) {
    case NotificationType.Order:
    case NotificationType.Cleaner:
    case NotificationType.Payment:
    case NotificationType.Refund:
      return args['orderId'] ? [CleansiaAdminRoute.ORDER_MANAGEMENT, args['orderId']] : null;
    case NotificationType.Dispute:
      return args['disputeId'] ? [CleansiaAdminRoute.DISPUTE_MANAGEMENT, args['disputeId']] : null;
    case NotificationType.DataProtection:
      return [CleansiaAdminRoute.DATA_PROTECTION];
    case NotificationType.Company:
      return [CleansiaAdminRoute.COMPANY_LIFECYCLE];
    case NotificationType.Referral:
      return [CleansiaAdminRoute.LOYALTY_REFERRALS];
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
    const type = getNotificationType(item.eventKey);
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
      type,
      typeLabelKey: `${TYPES_ROOT}.${type}`,
      critical: isCriticalNotification(item.eventKey),
      icon: NOTIFICATION_TYPE_ICONS[type],
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
