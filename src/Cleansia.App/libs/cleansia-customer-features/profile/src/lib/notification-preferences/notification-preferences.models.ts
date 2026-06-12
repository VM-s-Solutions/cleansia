import { INotificationPreferencesDto } from '@cleansia/customer-services';

export type NotificationPreferenceField = keyof INotificationPreferencesDto;

export type NotificationPreferencesValues = Record<
  NotificationPreferenceField,
  boolean
>;

export interface NotificationPreferenceCategory {
  field: NotificationPreferenceField;
  labelKey: string;
  icon: string;
}

export const NOTIFICATION_PREFERENCE_CATEGORIES: NotificationPreferenceCategory[] =
  [
    {
      field: 'orderUpdates',
      labelKey: 'pages.profile.notifications.order_updates',
      icon: 'pi pi-shopping-bag',
    },
    {
      field: 'cleanerOnTheWay',
      labelKey: 'pages.profile.notifications.cleaner_on_the_way',
      icon: 'pi pi-car',
    },
    {
      field: 'orderCompleted',
      labelKey: 'pages.profile.notifications.order_completed',
      icon: 'pi pi-check-circle',
    },
    {
      field: 'orderCancelled',
      labelKey: 'pages.profile.notifications.order_cancelled',
      icon: 'pi pi-times-circle',
    },
    {
      field: 'refundIssued',
      labelKey: 'pages.profile.notifications.refund_issued',
      icon: 'pi pi-money-bill',
    },
    {
      field: 'membershipExpiring',
      labelKey: 'pages.profile.notifications.membership_expiring',
      icon: 'pi pi-clock',
    },
    {
      field: 'membershipCancelled',
      labelKey: 'pages.profile.notifications.membership_cancelled',
      icon: 'pi pi-bookmark',
    },
    {
      field: 'tierUpgrade',
      labelKey: 'pages.profile.notifications.tier_upgrade',
      icon: 'pi pi-star',
    },
    {
      field: 'promo',
      labelKey: 'pages.profile.notifications.promo',
      icon: 'pi pi-megaphone',
    },
    {
      field: 'disputeReply',
      labelKey: 'pages.profile.notifications.dispute_reply',
      icon: 'pi pi-comments',
    },
    {
      field: 'recurringScheduled',
      labelKey: 'pages.profile.notifications.recurring_scheduled',
      icon: 'pi pi-refresh',
    },
  ];

export function toPreferencesValues(
  dto: INotificationPreferencesDto
): NotificationPreferencesValues {
  return {
    orderUpdates: dto.orderUpdates,
    cleanerOnTheWay: dto.cleanerOnTheWay,
    orderCompleted: dto.orderCompleted,
    orderCancelled: dto.orderCancelled,
    refundIssued: dto.refundIssued,
    membershipExpiring: dto.membershipExpiring,
    membershipCancelled: dto.membershipCancelled,
    tierUpgrade: dto.tierUpgrade,
    promo: dto.promo,
    disputeReply: dto.disputeReply,
    recurringScheduled: dto.recurringScheduled,
  };
}
