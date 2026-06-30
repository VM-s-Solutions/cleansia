import Foundation

extension L10n {
    enum Devices {
        static var title: String {
            localized("devices_title")
        }

        static var intro: String {
            localized("devices_intro")
        }

        static var thisDevice: String {
            localized("devices_this_device")
        }

        static func lastActive(_ value: String) -> String {
            format("devices_last_active", value)
        }

        static var revokeButton: String {
            localized("devices_revoke_action")
        }

        static var revokeDialogTitle: String {
            localized("devices_revoke_dialog_title")
        }

        static func revokeDialogMessage(_ platform: String) -> String {
            format("devices_revoke_dialog_message", platform)
        }

        static var revokeDialogConfirm: String {
            localized("devices_revoke_dialog_confirm")
        }

        static var revokeSuccess: String {
            localized("devices_revoke_success")
        }

        static var revokeRetryHint: String {
            localized("devices_revoke_retry_hint")
        }

        static var platformAndroid: String {
            localized("devices_platform_android")
        }

        static var platformIos: String {
            localized("devices_platform_ios")
        }

        static var platformWeb: String {
            localized("devices_platform_web")
        }

        static var platformUnknown: String {
            localized("devices_platform_unknown")
        }

        static var empty: String {
            localized("devices_empty")
        }

        static var errorMessage: String {
            localized("devices_error_message")
        }
    }

    enum Notifications {
        static var title: String {
            localized("notifications_title")
        }

        static var errorMessage: String {
            localized("notifications_error_message")
        }

        static var sectionOrders: String {
            localized("notifications_section_orders")
        }

        static var sectionMembership: String {
            localized("notifications_section_membership")
        }

        static var sectionAccount: String {
            localized("notifications_section_account")
        }

        static var orderUpdates: String {
            localized("notifications_order_updates")
        }

        static var orderUpdatesDesc: String {
            localized("notifications_order_updates_desc")
        }

        static var cleanerOnTheWay: String {
            localized("notifications_cleaner_on_the_way")
        }

        static var cleanerOnTheWayDesc: String {
            localized("notifications_cleaner_on_the_way_desc")
        }

        static var orderCompleted: String {
            localized("notifications_order_completed")
        }

        static var orderCompletedDesc: String {
            localized("notifications_order_completed_desc")
        }

        static var orderCancelled: String {
            localized("notifications_order_cancelled")
        }

        static var orderCancelledDesc: String {
            localized("notifications_order_cancelled_desc")
        }

        static var recurringScheduled: String {
            localized("notifications_recurring_scheduled")
        }

        static var recurringScheduledDesc: String {
            localized("notifications_recurring_scheduled_desc")
        }

        static var membershipExpiring: String {
            localized("notifications_membership_expiring")
        }

        static var membershipExpiringDesc: String {
            localized("notifications_membership_expiring_desc")
        }

        static var membershipCancelled: String {
            localized("notifications_membership_cancelled")
        }

        static var membershipCancelledDesc: String {
            localized("notifications_membership_cancelled_desc")
        }

        static var tierUpgrade: String {
            localized("notifications_tier_upgrade")
        }

        static var tierUpgradeDesc: String {
            localized("notifications_tier_upgrade_desc")
        }

        static var promo: String {
            localized("notifications_promo")
        }

        static var promoDesc: String {
            localized("notifications_promo_desc")
        }

        static var refundIssued: String {
            localized("notifications_refund_issued")
        }

        static var refundIssuedDesc: String {
            localized("notifications_refund_issued_desc")
        }

        static var disputeReply: String {
            localized("notifications_dispute_reply")
        }

        static var disputeReplyDesc: String {
            localized("notifications_dispute_reply_desc")
        }
    }
}
