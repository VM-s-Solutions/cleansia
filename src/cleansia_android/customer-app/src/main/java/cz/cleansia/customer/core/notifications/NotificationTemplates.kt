package cz.cleansia.customer.core.notifications

import android.content.Context
import cz.cleansia.customer.R

/**
 * The per-event notification templates — the single source for BOTH surfaces
 * that render an event_key + args into user-visible text: the FCM push display
 * ([CleansiaFirebaseMessagingService]) and the notifications inbox feed. Keys
 * unknown to this catalog are silently dropped on both surfaces (drop-parity),
 * and `promo.new_sitewide` is deliberately absent: its title/body are
 * server-authored per user, handled by the push path alone.
 */
object NotificationTemplates {

    data class Template(
        val titleRes: Int,
        val bodyRes: Int,
        val category: NotificationCategoryDto,
    )

    fun templateFor(eventKey: String): Template? = when (eventKey) {
        "order.confirmed" -> Template(
            R.string.notification_order_confirmed_title,
            R.string.notification_order_confirmed_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.on_the_way" -> Template(
            R.string.notification_order_on_the_way_title,
            R.string.notification_order_on_the_way_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.in_progress" -> Template(
            R.string.notification_order_in_progress_title,
            R.string.notification_order_in_progress_body,
            NotificationCategoryDto.OrderUpdates,
        )
        "order.completed" -> Template(
            R.string.notification_order_completed_title,
            R.string.notification_order_completed_body,
            NotificationCategoryDto.OrderCompleted,
        )
        "order.cancelled" -> Template(
            R.string.notification_order_cancelled_title,
            R.string.notification_order_cancelled_body,
            NotificationCategoryDto.OrderCancelled,
        )
        "order.refunded" -> Template(
            R.string.notification_order_refunded_title,
            R.string.notification_order_refunded_body,
            NotificationCategoryDto.RefundIssued,
        )
        "dispute.reply" -> Template(
            R.string.notification_dispute_reply_title,
            R.string.notification_dispute_reply_body,
            NotificationCategoryDto.DisputeReply,
        )
        "recurring.scheduled" -> Template(
            R.string.notification_recurring_scheduled_title,
            R.string.notification_recurring_scheduled_body,
            NotificationCategoryDto.RecurringScheduled,
        )
        "loyalty.tier_upgrade" -> Template(
            R.string.notification_loyalty_tier_upgrade_title,
            R.string.notification_loyalty_tier_upgrade_body,
            NotificationCategoryDto.TierUpgrade,
        )
        "membership.expiring_soon" -> Template(
            R.string.notification_membership_expiring_title,
            R.string.notification_membership_expiring_body,
            NotificationCategoryDto.MembershipExpiring,
        )
        "membership.cancellation_effective" -> Template(
            R.string.notification_membership_cancelled_title,
            R.string.notification_membership_cancelled_body,
            NotificationCategoryDto.MembershipCancelled,
        )
        "order.assignment_cancelled" -> Template(
            R.string.notification_order_assignment_cancelled_title,
            R.string.notification_order_assignment_cancelled_body,
            NotificationCategoryDto.OrderUpdates,
        )
        else -> null
    }

    /**
     * Format the body string with the args we expect for the given event.
     * String.format with the right positional args per template.
     */
    fun formatBody(context: Context, eventKey: String, bodyRes: Int, args: Map<String, String>): String =
        when (eventKey) {
            "order.confirmed",
            "order.on_the_way",
            "order.in_progress",
            "order.completed",
            "order.cancelled",
            "order.refunded",
            "order.assignment_cancelled",
            "recurring.scheduled" -> {
                val orderNumber = args["orderNumber"].orEmpty()
                context.getString(bodyRes, orderNumber)
            }
            "loyalty.tier_upgrade" -> {
                // Body carries a localized tier name. The wire `tier` arg is the
                // enum's .ToString() value ("SilverMopper", "GoldPolisher", etc.)
                // — map to a localized label so the user sees "Silver Mopper"
                // not the enum identifier.
                val tierLabel = args["tier"]?.let { resolveTierLabel(context, it) } ?: ""
                context.getString(bodyRes, tierLabel)
            }
            else -> context.getString(bodyRes)
        }

    /**
     * Map the backend LoyaltyTier enum identifier (sent as `tier` arg on the
     * loyalty.tier_upgrade push) to a localized human label. Unknown values
     * fall back to the raw enum string so the user still sees something.
     */
    private fun resolveTierLabel(context: Context, enumName: String): String = when (enumName) {
        "BronzeCleaner" -> context.getString(R.string.loyalty_tier_bronze_cleaner)
        "SilverMopper" -> context.getString(R.string.loyalty_tier_silver_mopper)
        "GoldPolisher" -> context.getString(R.string.loyalty_tier_gold_polisher)
        "PlatinumSparkler" -> context.getString(R.string.loyalty_tier_platinum_sparkler)
        else -> enumName
    }
}
